using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Application.Pricing;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using Secretary.Domain.ValueObjects;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Survey calls: queueing one, running it, and reading the results.
///
/// The agent-facing half is deliberately narrow. It can find out who the call is about, ask for
/// the next question, and record an answer — and that is all. It cannot skip ahead, cannot
/// revisit, and cannot decide which option the caller picked: the matching happens here, against
/// the stored options, for the same reason product matching does. A model that chooses the option
/// will confidently record an answer nobody gave.</summary>
public sealed class FeedbackCallService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly TokenPricebook _pricebook;

    public FeedbackCallService(
        IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant, TokenPricebook pricebook)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
        _pricebook = pricebook;
    }

    // ---- Queueing ----

    /// <summary>Records who is about to be rung, before anybody is, and opens the first attempt.
    /// Today the form calls this; when telephony lands the scheduler calls exactly the same
    /// method.</summary>
    public async Task<FeedbackCallResponse> QueueAsync(
        QueueFeedbackCallRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var survey = await RequireAskableSurveyAsync(request.SurveyId, cancellationToken);
        var now = _clock.GetCurrentInstant();

        var surveyRequest = SurveyRequest.Queue(
            tenantId, survey.Id, request.PersonName,
            PhoneNumberNormalizer.Normalize(request.PhoneNumber), now);

        await _uow.SurveyRequests.AddAsync(surveyRequest, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return await DialAsync(surveyRequest, cancellationToken);
    }

    /// <summary>Rings somebody we already have a request for, again.
    ///
    /// The same method the scheduler will call for a due retry and the same one the follow-up
    /// list calls when a person presses the button. One path, so an automatic retry and a manual
    /// one cannot count differently.</summary>
    public async Task<FeedbackCallResponse> RetryAsync(int requestId, CancellationToken cancellationToken)
    {
        var surveyRequest = await _uow.SurveyRequests.GetByIdAsync(requestId, cancellationToken)
            ?? throw new NotFoundException(nameof(SurveyRequest), requestId);

        if (surveyRequest.Outcome is SurveyRequestOutcome.Complete or SurveyRequestOutcome.Refused)
        {
            throw new InvalidStateTransitionException(
                nameof(SurveyRequest), requestId,
                surveyRequest.Outcome == SurveyRequestOutcome.Complete ? "already surveyed" : "declined",
                "rung again");
        }

        await RequireAskableSurveyAsync(surveyRequest.SurveyId, cancellationToken);
        return await DialAsync(surveyRequest, cancellationToken);
    }

    /// <summary>One attempt: counted on the request, and its own row for what it costs.
    ///
    /// ⚠ The attempt is counted before the dial rather than after it. Counting afterwards fails
    /// open — a dial that never reports back leaves the count untouched and the request eligible
    /// forever, which is the failure mode where somebody gets rung all night.</summary>
    private async Task<FeedbackCallResponse> DialAsync(
        SurveyRequest surveyRequest, CancellationToken cancellationToken)
    {
        var now = _clock.GetCurrentInstant();

        surveyRequest.BeginAttempt(now);
        _uow.SurveyRequests.Update(surveyRequest);

        var call = FeedbackCall.Attempt(surveyRequest.TenantId, surveyRequest.Id, now);
        await _uow.FeedbackCalls.AddAsync(call, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var survey = await _uow.Surveys.GetByIdAsync(surveyRequest.SurveyId, cancellationToken);
        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(surveyRequest.SurveyId, cancellationToken);

        return ToResponse(call, surveyRequest, survey?.Name ?? string.Empty, answered: 0, questions.Count);
    }

    private async Task<Survey> RequireAskableSurveyAsync(int surveyId, CancellationToken cancellationToken)
    {
        var survey = await _uow.Surveys.GetByIdAsync(surveyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Survey), surveyId);

        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(survey.Id, cancellationToken);
        return questions.Count == 0
            ? throw new ArgumentException("This questionnaire has no questions yet, so there is nothing to ask.")
            : survey;
    }

    // ---- What the agent can do ----

    /// <summary>Who this call is about, so the agent can greet them by name and say which visit
    /// it is calling about — the thing that makes it sound like a real follow-up.</summary>
    public async Task<FeedbackCallSubject> GetSubjectAsync(int callId, CancellationToken cancellationToken)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        var surveyRequest = await RequireRequestAsync(call.SurveyRequestId, cancellationToken);
        var survey = await _uow.Surveys.GetByIdAsync(surveyRequest.SurveyId, cancellationToken);
        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(surveyRequest.SurveyId, cancellationToken);

        if (call.CallStatus == FeedbackCallStatus.Created)
        {
            call.Begin(_clock.GetCurrentInstant());
            _uow.FeedbackCalls.Update(call);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return new FeedbackCallSubject(
            call.Id, surveyRequest.PersonName, survey?.Name ?? string.Empty, questions.Count);
    }

    /// <summary>The next question nobody has answered yet, in running order. Null when the last
    /// one is done — which is also when the call is marked complete, because reaching the end is
    /// the only definition of completion that does not depend on the agent claiming it.</summary>
    public async Task<FeedbackQuestionForAgent?> GetNextQuestionAsync(int callId, CancellationToken cancellationToken)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        var surveyRequest = await RequireRequestAsync(call.SurveyRequestId, cancellationToken);
        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(surveyRequest.SurveyId, cancellationToken);
        var answered = (await _uow.FeedbackAnswers.GetForCallAsync(callId, cancellationToken))
            .Select(a => a.SurveyQuestionId)
            .ToHashSet();

        var next = questions.FirstOrDefault(q => !answered.Contains(q.Id));
        if (next is null)
        {
            if (call.CallStatus != FeedbackCallStatus.Completed)
            {
                call.Complete(_clock.GetCurrentInstant());
                _uow.FeedbackCalls.Update(call);

                // Settled here rather than when the call ends, because reaching the last question
                // is what completion means and the line may drop during the thank-you.
                await SettleAsync(surveyRequest, call, cancellationToken);
            }

            return null;
        }

        return new FeedbackQuestionForAgent(
            next.Id, next.Position, next.Text, next.QuestionType, next.ScaleMax, next.AllowOther,
            next.Options.OrderBy(o => o.Position)
                .Select(o => new SurveyOptionResponse(o.Id, o.Position, o.Text, o.ScorePercent, o.IsOther))
                .ToList());
    }

    /// <summary>Which option the caller picked, decided here rather than by the model.
    ///
    /// Returns null when nothing matched, and the agent's job is then to ask again rather than
    /// guess. Matching folds Azerbaijani the same way product names do, and understands a spoken
    /// number for a scored option: "dörd" and "4" are the same answer to "1-5".</summary>
    public async Task<SurveyQuestionOption?> MatchOptionAsync(
        int questionId, string spokenAnswer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(spokenAnswer))
        {
            return null;
        }

        var question = await _uow.SurveyQuestions.GetWithOptionsAsync(questionId, cancellationToken);
        if (question is null || question.Options.Count == 0)
        {
            return null;
        }

        var said = AddressText.Normalize(spokenAnswer);
        if (said.Length == 0)
        {
            return null;
        }

        var exact = question.Options.FirstOrDefault(o => AddressText.Normalize(o.Text) == said);
        if (exact is not null)
        {
            return exact;
        }

        // ⚠ Yes and no are said in a dozen ways and almost never as the stored label. A real
        // caller answered "hə", was refused twice, and the call ended — on the question type that
        // should be the hardest to get wrong. Matching the label alone is matching the written
        // word against the spoken one.
        if (question.QuestionType == FeedbackQuestionType.YesNo && YesNoWords.Read(spokenAnswer) is { } saidYes)
        {
            var wanted = saidYes ? SurveyQuestion.Yes : SurveyQuestion.No;
            return question.Options.FirstOrDefault(o => o.Text == wanted);
        }

        // A number, said either way round: "dörd" and "4" are the same answer to a 1-5.
        //
        // ⚠ Matched against the option's TEXT, never against its score. It used to fall back to
        // the score, and on a question set up 20/40/60/80/100 that meant "dörd" matched nothing
        // and every spoken number was refused — on the one question people always answer with a
        // number. The score is now derived and is a percentage, so it is not something anyone says.
        //
        // Only where the option is itself a number, or "iki" would pick the second item of a list
        // that has nothing to do with counting.
        if (SpokenNumber(said) is { } number)
        {
            var byText = question.Options.FirstOrDefault(
                o => SpokenNumber(AddressText.Normalize(o.Text)) == number);

            if (byText is not null)
            {
                return byText;
            }
        }

        // ⚠ All of them, not the first. "Servis bölməsi" and "Servis mərkəzi" both contain
        // "servis", and taking whichever came first filed a real answer on a coin toss. Two
        // options that both fit is not a match; it is a question worth asking again.
        var contained = question.Options.Where(o =>
        {
            var option = AddressText.Normalize(o.Text);
            return option.Length > 0
                   && (said.Contains(option, StringComparison.Ordinal)
                       || option.Contains(said, StringComparison.Ordinal));
        }).ToList();

        if (contained.Count == 1)
        {
            return contained[0];
        }

        return contained.Count > 1 ? null : BySpelling(question, spokenAnswer);
    }

    /// <summary>Last resort: the option as somebody typed it and the word as somebody says it are
    /// often the same word spelled differently.
    ///
    /// ⚠ A caller said "resepshn", exactly as the option was typed, and was filed under "Digər".
    /// The transcriber had written it down as "resepsiyon" — it spells what it hears in full,
    /// while the owner had typed the short form. Neither of them was wrong and the two strings
    /// still did not match, which is the whole problem: the word survives the round trip, the
    /// spelling does not.
    ///
    /// A shared opening of five letters is long enough not to be coincidence in Azerbaijani, and
    /// this runs only after an exact match and a containment match have both failed. It also
    /// refuses a tie: two options that both look close is not a match, it is a question worth
    /// asking again.</summary>
    private static SurveyQuestionOption? BySpelling(SurveyQuestion question, string spokenAnswer)
    {
        const int MinSharedPrefix = 5;

        var words = SpokenWords.Fold(spokenAnswer);
        if (words.Count == 0)
        {
            return null;
        }

        var ranked = question.Options
            .Select(option => (
                Option: option,
                Shared: words.Max(word => SharedPrefix(word, string.Concat(SpokenWords.Fold(option.Text))))))
            .Where(match => match.Shared >= MinSharedPrefix)
            .OrderByDescending(match => match.Shared)
            .ToList();

        return ranked.Count == 1 || (ranked.Count > 1 && ranked[0].Shared > ranked[1].Shared)
            ? ranked[0].Option
            : null;
    }

    private static int SharedPrefix(string left, string right)
    {
        var shared = 0;
        while (shared < left.Length && shared < right.Length && left[shared] == right[shared])
        {
            shared++;
        }

        return shared;
    }

    public async Task RecordChoiceAsync(
        int callId, int questionId, int optionId, CancellationToken cancellationToken)
        => await RecordAsync(
            FeedbackAnswer.Chose(callId, questionId, optionId, _clock.GetCurrentInstant()), cancellationToken);

    public async Task RecordOpenAsync(
        int callId, int questionId, string text, CancellationToken cancellationToken)
        => await RecordAsync(
            FeedbackAnswer.Said(callId, questionId, text, _clock.GetCurrentInstant()), cancellationToken);

    public async Task RecordDeclineAsync(int callId, int questionId, CancellationToken cancellationToken)
        => await RecordAsync(
            FeedbackAnswer.Refused(callId, questionId, _clock.GetCurrentInstant()), cancellationToken);

    /// <summary>More of an open answer that was recorded before the caller had finished saying it.
    ///
    /// ⚠ A pause is not the end of a sentence. The provider's voice detection ends the turn at
    /// one, the agent records what it has, and the rest arrives afterwards — on a real call
    /// "…servis çox yaxşıdır" was stored and "…başqa" was thrown away as an answer to nothing.
    ///
    /// Replaces rather than appends when the new text already contains the old, which is the
    /// common shape: the transcriber re-sends the whole utterance with more on the end, and
    /// appending would store the first half twice. Returns false when there is nothing to extend,
    /// so the caller can fall back to refusing.</summary>
    public async Task<bool> ExtendOpenAnswerAsync(
        int callId, int questionId, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var answer = (await _uow.FeedbackAnswers.GetForCallAsync(callId, cancellationToken))
            .FirstOrDefault(a => a.SurveyQuestionId == questionId && a.Text is not null && !a.Declined);

        if (answer is null)
        {
            return false;
        }

        var addition = text.Trim();
        var existing = answer.Text!;
        if (existing.Contains(addition, StringComparison.OrdinalIgnoreCase))
        {
            // Nothing new — the transcriber repeated itself. Not a failure; there is simply
            // nothing to store, and saying so would have the agent apologise for a non-event.
            return true;
        }

        answer.Extend(
            addition.Contains(existing, StringComparison.OrdinalIgnoreCase) ? addition : $"{existing} {addition}",
            _clock.GetCurrentInstant());

        _uow.FeedbackAnswers.Update(answer);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Their own answer, on a question that invited one. Both halves are kept — the
    /// option so the count is right, the words so the count means something.</summary>
    public async Task RecordOtherAsync(
        int callId, int questionId, int optionId, string text, CancellationToken cancellationToken)
        => await RecordAsync(
            FeedbackAnswer.ChoseOther(callId, questionId, optionId, text, _clock.GetCurrentInstant()),
            cancellationToken);

    /// <summary>The survey broke down. Marked here rather than left to the agent to describe,
    /// because "a person will call you" has to be true of a row somebody can find.</summary>
    public async Task HandOverToHumanAsync(int callId, CancellationToken cancellationToken)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        call.HandOverToHuman(_clock.GetCurrentInstant());
        _uow.FeedbackCalls.Update(call);

        await SettleAsync(await RequireRequestAsync(call.SurveyRequestId, cancellationToken), call, cancellationToken);
    }

    /// <summary>Carries an attempt's outcome up to the person it was for, and decides whether
    /// there will be another attempt.
    ///
    /// The retry policy is read from the questionnaire at the moment it is applied, not copied
    /// onto the request when it was queued — an owner who turns retries off means it for the
    /// people already waiting, not only for the next ones.</summary>
    private async Task SettleAsync(
        SurveyRequest surveyRequest, FeedbackCall call, CancellationToken cancellationToken)
    {
        var survey = await _uow.Surveys.GetByIdAsync(surveyRequest.SurveyId, cancellationToken);

        // Fetched even when there is nothing to schedule — one query on a call that has just
        // ended, against a retry that would otherwise be placed at whatever hour the failure
        // happened to occur.
        var week = await _uow.BusinessHours.GetWeekAsync(cancellationToken);

        surveyRequest.Settle(
            call.Outcome,
            survey?.RetryCount ?? 0,
            survey?.RetryDelayMinutes ?? Survey.DefaultRetryDelayMinutes,
            due => CallingHours.NextOpening(due, week, DateTimeZoneProviders.Tzdb["Asia/Baku"]),
            _clock.GetCurrentInstant());

        _uow.SurveyRequests.Update(surveyRequest);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordAsync(FeedbackAnswer answer, CancellationToken cancellationToken)
    {
        await _uow.FeedbackAnswers.AddAsync(answer, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Written when the call ends, however it ended. Priced once, here, at the rates in
    /// force now — the same rule every other call log follows.</summary>
    public async Task LogAsync(LogFeedbackCallRequest request, CancellationToken cancellationToken)
    {
        var call = await RequireCallAsync(request.CallId, cancellationToken);

        var usages = request.ModelUsages ?? [];
        var total = usages.Aggregate(TokenUsage.Zero, (running, entry) => running + entry.Usage);
        var models = string.Join(" + ", usages.Select(u => u.Model).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct());

        call.Finish(
            request.DurationSeconds, request.TurnCount, request.CallerTurnCount, request.Transcript,
            models.Length <= 64 ? models : models[..64], request.Pipeline, total,
            _pricebook.CostUsd(usages), _clock.GetCurrentInstant());

        _uow.FeedbackCalls.Update(call);

        // ⚠ Settled again here, after the counts are on the row. The outcome of a dial that never
        // got anywhere is only knowable once the turn counts are written — before Finish, a line
        // that opened and died looks identical to one still in progress.
        await SettleAsync(
            await RequireRequestAsync(call.SurveyRequestId, cancellationToken), call, cancellationToken);
    }

    // ---- Reading ----

    public async Task<IReadOnlyList<FeedbackCallResponse>> SearchAsync(
        Instant? from, Instant? to, int? surveyId, CancellationToken cancellationToken)
    {
        var calls = await _uow.FeedbackCalls.SearchAsync(from, to, surveyId, cancellationToken);
        if (calls.Count == 0)
        {
            return [];
        }

        var surveys = (await _uow.Surveys.GetAllForCurrentTenantAsync(cancellationToken)).ToDictionary(s => s.Id);
        var requests = (await _uow.SurveyRequests.SearchAsync(null, null, surveyId, cancellationToken))
            .ToDictionary(r => r.Id);

        var answers = (await _uow.FeedbackAnswers.GetForCallsAsync(calls.Select(c => c.Id).ToList(), cancellationToken))
            .GroupBy(a => a.FeedbackCallId)
            .ToDictionary(g => g.Key, g => g.Count());

        var questionCounts = new Dictionary<int, int>();
        foreach (var id in requests.Values.Select(r => r.SurveyId).Distinct())
        {
            questionCounts[id] = (await _uow.SurveyQuestions.GetForSurveyAsync(id, cancellationToken)).Count;
        }

        return calls
            .Where(c => requests.ContainsKey(c.SurveyRequestId))
            .Select(c =>
            {
                var surveyRequest = requests[c.SurveyRequestId];
                return ToResponse(
                    c,
                    surveyRequest,
                    surveys.GetValueOrDefault(surveyRequest.SurveyId)?.Name ?? string.Empty,
                    answers.GetValueOrDefault(c.Id),
                    questionCounts.GetValueOrDefault(surveyRequest.SurveyId));
            })
            .ToList();
    }

    /// <summary>The people, not the dials. What the follow-up list reads.</summary>
    public async Task<IReadOnlyList<SurveyRequestResponse>> SearchRequestsAsync(
        Instant? from, Instant? to, int? surveyId, CancellationToken cancellationToken)
    {
        var requests = await _uow.SurveyRequests.SearchAsync(from, to, surveyId, cancellationToken);
        if (requests.Count == 0)
        {
            return [];
        }

        var surveys = (await _uow.Surveys.GetAllForCurrentTenantAsync(cancellationToken)).ToDictionary(s => s.Id);
        var calls = await _uow.FeedbackCalls.GetForRequestsAsync(requests.Select(r => r.Id).ToList(), cancellationToken);
        var costByRequest = calls
            .GroupBy(c => c.SurveyRequestId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.CostUsd));

        return requests
            .Select(r => new SurveyRequestResponse(
                r.Id, r.SurveyId, surveys.GetValueOrDefault(r.SurveyId)?.Name ?? string.Empty,
                r.PersonName, r.PhoneNumber, r.Outcome, r.AttemptCount, r.CreatedAtUtc,
                r.LastAttemptAt, r.NextAttemptDueAt, r.NeedsFollowUp,
                costByRequest.GetValueOrDefault(r.Id)))
            .ToList();
    }

    /// <summary>Stops chasing somebody without pretending the survey happened.</summary>
    public async Task CloseRequestAsync(int requestId, CancellationToken cancellationToken)
    {
        var surveyRequest = await RequireRequestAsync(requestId, cancellationToken);
        surveyRequest.CloseWithoutAnswer(_clock.GetCurrentInstant());
        _uow.SurveyRequests.Update(surveyRequest);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeedbackCallDetailResponse> GetDetailAsync(int callId, CancellationToken cancellationToken)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        var surveyRequest = await RequireRequestAsync(call.SurveyRequestId, cancellationToken);
        var survey = await _uow.Surveys.GetByIdAsync(surveyRequest.SurveyId, cancellationToken);
        var questions = (await _uow.SurveyQuestions.GetForSurveyAsync(surveyRequest.SurveyId, cancellationToken))
            .ToDictionary(q => q.Id);

        var answers = await _uow.FeedbackAnswers.GetForCallAsync(callId, cancellationToken);

        var detailed = answers
            .Select(a =>
            {
                var question = questions.GetValueOrDefault(a.SurveyQuestionId);
                var option = question?.Options.FirstOrDefault(o => o.Id == a.SurveyQuestionOptionId);

                return new FeedbackAnswerResponse(
                    a.SurveyQuestionId,
                    question?.Position ?? 0,
                    question?.Text ?? string.Empty,
                    question?.QuestionType ?? FeedbackQuestionType.Open,
                    option?.Text,
                    option?.ScorePercent,
                    a.Text,
                    a.Declined,
                    a.AnsweredAt);
            })
            .OrderBy(a => a.Position)
            .ToList();

        return new FeedbackCallDetailResponse(
            ToResponse(call, surveyRequest, survey?.Name ?? string.Empty, answers.Count, questions.Count),
            detailed,
            call.Transcript);
    }

    /// <summary>The dashboard: what the customers said, generated from the questionnaire rather
    /// than designed for it.
    ///
    /// ⚠ Only completed surveys contribute answers. A survey that stopped half way is not half a
    /// result — its answers are the ones somebody gave before deciding not to continue, and
    /// including them would mean the numbers are made partly of people who did not want to be
    /// there. That is also why there is no "partial" outcome to include.
    ///
    /// ⚠ Open questions are absent from the results on purpose. There is nothing honest to chart
    /// from free text, and inventing a summary of it is exactly the failure this codebase keeps
    /// designing against — they are read verbatim on the call detail page instead.</summary>
    public async Task<FeedbackDashboardResponse> GetDashboardAsync(
        int surveyId, Instant? from, Instant? to, CancellationToken cancellationToken)
    {
        var survey = await _uow.Surveys.GetByIdAsync(surveyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Survey), surveyId);

        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(surveyId, cancellationToken);
        var now = await AggregateAsync(surveyId, from, to, questions, cancellationToken);

        // The same window again, immediately before this one. A score with no trend beside it is
        // a number nobody can act on: 78% is good or bad depending entirely on last month.
        decimal? previous = null;
        if (from is { } start)
        {
            var window = (to ?? _clock.GetCurrentInstant()) - start;
            previous = (await AggregateAsync(surveyId, start - window, start, questions, cancellationToken))
                .ScorePercent;
        }

        return new FeedbackDashboardResponse(
            survey.Id, survey.Name, now.ScorePercent, previous, now.ScoreAnswerCount,
            now.Coverage, now.Results);
    }

    private sealed record Aggregate(
        FeedbackCoverage Coverage,
        IReadOnlyList<QuestionResult> Results,
        decimal? ScorePercent,
        int ScoreAnswerCount);

    private async Task<Aggregate> AggregateAsync(
        int surveyId, Instant? from, Instant? to,
        IReadOnlyList<SurveyQuestion> questions, CancellationToken cancellationToken)
    {
        var requests = await _uow.SurveyRequests.SearchAsync(from, to, surveyId, cancellationToken);
        var calls = await _uow.FeedbackCalls.GetForRequestsAsync(requests.Select(r => r.Id).ToList(), cancellationToken);

        var completed = requests.Where(r => r.Outcome == SurveyRequestOutcome.Complete).Select(r => r.Id).ToHashSet();
        var countedCalls = calls.Where(c => completed.Contains(c.SurveyRequestId)).Select(c => c.Id).ToList();
        var answers = await _uow.FeedbackAnswers.GetForCallsAsync(countedCalls, cancellationToken);

        var spoken = calls.Where(c => c.DurationSeconds > 0).ToList();

        var coverage = new FeedbackCoverage(
            Requested: requests.Count,
            Completed: completed.Count,
            NotReached: requests.Count(r => r.Outcome == SurveyRequestOutcome.NotReached),
            Refused: requests.Count(r => r.Outcome == SurveyRequestOutcome.Refused),
            NeedsHuman: requests.Count(r => r.Outcome == SurveyRequestOutcome.NeedsHuman),
            Attempts: calls.Count,
            AverageDurationSeconds: spoken.Count == 0 ? 0 : (int)spoken.Average(c => c.DurationSeconds),
            TotalCostUsd: calls.Sum(c => c.CostUsd));

        var byQuestion = answers.GroupBy(a => a.SurveyQuestionId).ToDictionary(g => g.Key, g => g.ToList());

        var results = questions
            .Where(q => q.QuestionType != FeedbackQuestionType.Open)
            .Select(q => BuildResult(q, byQuestion.GetValueOrDefault(q.Id) ?? []))
            .ToList();

        // ⚠ The mean of the ANSWERS, not the mean of the question averages. Averaging averages
        // gives a five-answer question the same weight as a fifty-answer one, which is how a
        // question nobody reaches ends up steering the headline.
        var counted = results.Where(r => r.CountsTowardScore && r.AveragePercent is not null).ToList();
        var answerCount = counted.Sum(r => r.AnsweredCount);

        var scorePercent = answerCount == 0
            ? (decimal?)null
            : Math.Round(counted.Sum(r => r.AveragePercent!.Value * r.AnsweredCount) / answerCount, 2);

        return new Aggregate(coverage, results, scorePercent, answerCount);
    }

    private static QuestionResult BuildResult(SurveyQuestion question, IReadOnlyList<FeedbackAnswer> answers)
    {
        var chosen = answers.Where(a => a.SurveyQuestionOptionId is not null).ToList();

        var breakdown = question.Options
            .OrderBy(o => o.Position)
            .Select(o => new OptionBreakdown(
                o.Id, o.Text, o.ScorePercent, chosen.Count(a => a.SurveyQuestionOptionId == o.Id)))
            .ToList();

        // Averaged only for the two types that have an ordering. A mean over "Təmir" and "Satış"
        // would be a number with no meaning, which is worse than no number.
        decimal? averagePercent = null;
        if (question.IsScored && chosen.Count > 0)
        {
            var scores = question.Options.ToDictionary(o => o.Id, o => o.ScorePercent);
            var scored = chosen
                .Select(a => scores.GetValueOrDefault(a.SurveyQuestionOptionId!.Value))
                .Where(v => v is not null)
                .Select(v => v!.Value)
                .ToList();

            if (scored.Count > 0)
            {
                averagePercent = Math.Round(scored.Average(), 2);
            }
        }

        return new QuestionResult(
            question.Id, question.Position, question.Text, question.QuestionType,
            question.CountsTowardScore, question.IsScored,
            AnsweredCount: chosen.Count,
            DeclinedCount: answers.Count(a => a.Declined),
            averagePercent,
            breakdown);
    }

    // Per-question drop-off used to be computed here and was the biggest panel on the page. It is
    // gone: it mixed people who were never reached into a rate about question wording, and now
    // that only completed surveys contribute answers there is by definition nowhere to drop off.
    // Who still needs dealing with is a list of people, not a chart — see SearchRequestsAsync.

    /// <summary>A number the caller said, as digits or as an Azerbaijani word. Only 1–10 — a
    /// rating scale never runs past it, and neither does a list of options anybody could hold in
    /// their head while listening.</summary>
    private static int? SpokenNumber(string normalized)
    {
        if (int.TryParse(normalized, out var digits))
        {
            return digits;
        }

        return normalized switch
        {
            "bir" => 1,
            "iki" => 2,
            "uc" => 3,
            "dord" => 4,
            "bes" => 5,
            "alti" => 6,
            "yeddi" => 7,
            "sekkiz" => 8,
            "doqquz" => 9,
            "on" => 10,
            _ => null,
        };
    }

    private async Task<FeedbackCall> RequireCallAsync(int callId, CancellationToken cancellationToken)
        => await _uow.FeedbackCalls.GetByIdAsync(callId, cancellationToken)
           ?? throw new NotFoundException(nameof(FeedbackCall), callId);

    private async Task<SurveyRequest> RequireRequestAsync(int requestId, CancellationToken cancellationToken)
        => await _uow.SurveyRequests.GetByIdAsync(requestId, cancellationToken)
           ?? throw new NotFoundException(nameof(SurveyRequest), requestId);

    private static FeedbackCallResponse ToResponse(
        FeedbackCall call, SurveyRequest surveyRequest, string surveyName, int answered, int questionCount)
        => new(
            call.Id, surveyRequest.Id, surveyRequest.SurveyId, surveyName,
            surveyRequest.PersonName, surveyRequest.PhoneNumber, surveyRequest.AttemptCount,
            call.CallStatus, call.CreatedAtUtc, call.CompletedAt, call.DurationSeconds,
            call.TurnCount, call.CallerTurnCount, call.AgentModel, call.Pipeline, call.TokenUsage,
            call.CostUsd, answered, questionCount);
}
