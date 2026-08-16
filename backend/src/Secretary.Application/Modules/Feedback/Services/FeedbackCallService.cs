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

    /// <summary>Records who is about to be rung, before anybody is. Today the form calls this;
    /// when telephony lands the scheduler calls exactly the same method.</summary>
    public async Task<FeedbackCallResponse> QueueAsync(
        QueueFeedbackCallRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var survey = await _uow.Surveys.GetByIdAsync(request.SurveyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Survey), request.SurveyId);

        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(survey.Id, cancellationToken);
        if (questions.Count == 0)
        {
            throw new ArgumentException("This questionnaire has no questions yet, so there is nothing to ask.");
        }

        var call = FeedbackCall.Queue(
            tenantId, survey.Id, request.PersonName,
            PhoneNumberNormalizer.Normalize(request.PhoneNumber), _clock.GetCurrentInstant());

        await _uow.FeedbackCalls.AddAsync(call, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return ToResponse(call, survey.Name, answered: 0, questions.Count);
    }

    // ---- What the agent can do ----

    /// <summary>Who this call is about, so the agent can greet them by name and say which visit
    /// it is calling about — the thing that makes it sound like a real follow-up.</summary>
    public async Task<FeedbackCallSubject> GetSubjectAsync(int callId, CancellationToken cancellationToken)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        var survey = await _uow.Surveys.GetByIdAsync(call.SurveyId, cancellationToken);
        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(call.SurveyId, cancellationToken);

        if (call.CallStatus == FeedbackCallStatus.Created)
        {
            call.Begin(_clock.GetCurrentInstant());
            _uow.FeedbackCalls.Update(call);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return new FeedbackCallSubject(call.Id, call.PersonName, survey?.Name ?? string.Empty, questions.Count);
    }

    /// <summary>The next question nobody has answered yet, in running order. Null when the last
    /// one is done — which is also when the call is marked complete, because reaching the end is
    /// the only definition of completion that does not depend on the agent claiming it.</summary>
    public async Task<FeedbackQuestionForAgent?> GetNextQuestionAsync(int callId, CancellationToken cancellationToken)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(call.SurveyId, cancellationToken);
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
                await _uow.SaveChangesAsync(cancellationToken);
            }

            return null;
        }

        return new FeedbackQuestionForAgent(
            next.Id, next.Position, next.Text, next.QuestionType,
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

        return question.Options.FirstOrDefault(o =>
        {
            var option = AddressText.Normalize(o.Text);
            return option.Length > 0
                   && (said.Contains(option, StringComparison.Ordinal)
                       || option.Contains(said, StringComparison.Ordinal));
        });
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
        await _uow.SaveChangesAsync(cancellationToken);
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
        var answers = (await _uow.FeedbackAnswers.GetForCallsAsync(calls.Select(c => c.Id).ToList(), cancellationToken))
            .GroupBy(a => a.FeedbackCallId)
            .ToDictionary(g => g.Key, g => g.Count());

        var questionCounts = new Dictionary<int, int>();
        foreach (var surveyId2 in calls.Select(c => c.SurveyId).Distinct())
        {
            questionCounts[surveyId2] = (await _uow.SurveyQuestions.GetForSurveyAsync(surveyId2, cancellationToken)).Count;
        }

        return calls
            .Select(c => ToResponse(
                c,
                surveys.GetValueOrDefault(c.SurveyId)?.Name ?? string.Empty,
                answers.GetValueOrDefault(c.Id),
                questionCounts.GetValueOrDefault(c.SurveyId)))
            .ToList();
    }

    public async Task<FeedbackCallDetailResponse> GetDetailAsync(int callId, CancellationToken cancellationToken)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        var survey = await _uow.Surveys.GetByIdAsync(call.SurveyId, cancellationToken);
        var questions = (await _uow.SurveyQuestions.GetForSurveyAsync(call.SurveyId, cancellationToken))
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
            ToResponse(call, survey?.Name ?? string.Empty, answers.Count, questions.Count),
            detailed,
            call.Transcript);
    }

    /// <summary>The dashboard, generated from the questionnaire rather than designed for it.
    ///
    /// Two halves that answer different people. The agent statistics are the same shape whatever
    /// was asked, so they say whether the thing works. The per-question results say what the
    /// customers think.
    ///
    /// ⚠ Open questions are absent from the results on purpose. There is nothing honest to chart
    /// from free text, and inventing a summary of it is exactly the failure this codebase keeps
    /// designing against — they are read verbatim on the call detail page instead.</summary>
    public async Task<FeedbackDashboardResponse> GetDashboardAsync(
        int surveyId, Instant? from, Instant? to, CancellationToken cancellationToken)
    {
        var survey = await _uow.Surveys.GetByIdAsync(surveyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Survey), surveyId);

        var calls = await _uow.FeedbackCalls.SearchAsync(from, to, surveyId, cancellationToken);
        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(surveyId, cancellationToken);
        var answers = await _uow.FeedbackAnswers.GetForCallsAsync(calls.Select(c => c.Id).ToList(), cancellationToken);

        var started = calls.Count(c => c.CallStatus is FeedbackCallStatus.InProgress
                                       or FeedbackCallStatus.Completed or FeedbackCallStatus.Abandoned);

        var withDuration = calls.Where(c => c.DurationSeconds > 0).ToList();

        var agent = new FeedbackAgentStats(
            CallsQueued: calls.Count,
            CallsStarted: started,
            CallsCompleted: calls.Count(c => c.CallStatus == FeedbackCallStatus.Completed),
            CallsAbandoned: calls.Count(c => c.CallStatus == FeedbackCallStatus.Abandoned),
            AverageDurationSeconds: withDuration.Count == 0 ? 0 : (int)withDuration.Average(c => c.DurationSeconds),
            TotalCostUsd: calls.Sum(c => c.CostUsd));

        var byQuestion = answers.GroupBy(a => a.SurveyQuestionId).ToDictionary(g => g.Key, g => g.ToList());

        var results = questions
            .Where(q => q.QuestionType == FeedbackQuestionType.Choice)
            .Select(q => BuildResult(q, byQuestion.GetValueOrDefault(q.Id) ?? []))
            .ToList();

        return new FeedbackDashboardResponse(
            survey.Id, survey.Name, agent, results, BuildDropOff(questions, answers));
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

    /// <summary>Where callers stop, and the most useful thing on the page — it needs no knowledge
    /// of what was asked.
    ///
    /// A caller reached a question if they answered it, or answered any question after it: the
    /// agent works strictly in order, so getting to question four means questions one to three
    /// were put. Reached minus answered is the number of people who hung up on that question.</summary>
    private static IReadOnlyList<QuestionDropOff> BuildDropOff(
        IReadOnlyList<SurveyQuestion> questions, IReadOnlyList<FeedbackAnswer> answers)
    {
        var positions = questions.ToDictionary(q => q.Id, q => q.Position);

        var furthest = answers
            .GroupBy(a => a.FeedbackCallId)
            .ToDictionary(
                g => g.Key,
                g => g.Max(a => positions.TryGetValue(a.SurveyQuestionId, out var p) ? p : -1));

        return questions
            .Select(q => new QuestionDropOff(
                q.Id,
                q.Position,
                q.Text,
                // They got as far as the previous question, so this one was asked of them.
                Reached: furthest.Count(f => f.Value >= q.Position - 1),
                Answered: answers.Count(a => a.SurveyQuestionId == q.Id)))
            .ToList();
    }

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

    private static FeedbackCallResponse ToResponse(
        FeedbackCall call, string surveyName, int answered, int questionCount)
        => new(
            call.Id, call.SurveyId, surveyName, call.PersonName, call.PhoneNumber, call.CallStatus,
            call.CreatedAtUtc, call.CompletedAt, call.DurationSeconds, call.TurnCount, call.CallerTurnCount,
            call.AgentModel, call.Pipeline, call.TokenUsage, call.CostUsd, answered, questionCount);
}
