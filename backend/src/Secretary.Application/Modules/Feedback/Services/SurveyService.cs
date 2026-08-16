using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>The questionnaires and the questions in them.
///
/// Everything here is the tenant's own work, with one thing that is not: how many questionnaires
/// they may have is the platform's decision, and it is enforced here rather than hidden in the
/// UI. A quota that only greys out a button is not a quota.</summary>
public sealed class SurveyService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;

    public SurveyService(IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<SurveyResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var surveys = await _uow.Surveys.GetAllForCurrentTenantAsync(cancellationToken);
        var responses = new List<SurveyResponse>(surveys.Count);
        foreach (var survey in surveys)
        {
            var questions = await _uow.SurveyQuestions.GetForSurveyAsync(survey.Id, cancellationToken);
            responses.Add(new SurveyResponse(survey.Id, survey.Name, questions.Count));
        }

        return responses;
    }

    public async Task<SurveyDetailResponse> GetAsync(int surveyId, CancellationToken cancellationToken)
    {
        var survey = await _uow.Surveys.GetByIdAsync(surveyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Survey), surveyId);

        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(surveyId, cancellationToken);
        return new SurveyDetailResponse(survey.Id, survey.Name, questions.Select(ToResponse).ToList());
    }

    public async Task<SurveyResponse> CreateAsync(SaveSurveyRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        await EnsureQuotaAllowsAnotherAsync(cancellationToken);

        var survey = Survey.Create(tenantId, request.Name, _clock.GetCurrentInstant());
        await _uow.Surveys.AddAsync(survey, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new SurveyResponse(survey.Id, survey.Name, 0);
    }

    public async Task<SurveyResponse> RenameAsync(
        int surveyId, SaveSurveyRequest request, CancellationToken cancellationToken)
    {
        var survey = await _uow.Surveys.GetByIdAsync(surveyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Survey), surveyId);

        survey.Rename(request.Name, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);

        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(surveyId, cancellationToken);
        return new SurveyResponse(survey.Id, survey.Name, questions.Count);
    }

    /// <summary>Soft-deactivates, like every other removal here. Calls already made against this
    /// questionnaire keep pointing at it, and their answers stay readable.</summary>
    public async Task RemoveAsync(int surveyId, CancellationToken cancellationToken)
    {
        var survey = await _uow.Surveys.GetByIdAsync(surveyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Survey), surveyId);

        survey.Deactivate(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<SurveyQuestionResponse> AddQuestionAsync(
        int surveyId, SaveQuestionRequest request, CancellationToken cancellationToken)
    {
        _ = await _uow.Surveys.GetByIdAsync(surveyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Survey), surveyId);

        var now = _clock.GetCurrentInstant();
        var existing = await _uow.SurveyQuestions.GetForSurveyAsync(surveyId, cancellationToken);
        Validate(request);

        var question = Build(surveyId, existing.Count, request, now);
        await _uow.SurveyQuestions.AddAsync(question, cancellationToken);

        // One save now, where it used to be two. The options are built by the question itself and
        // reach the database through the navigation, so there is no identity to wait for.
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(question);
    }

    /// <summary>Type in, question out. The one place the wire format meets the four shapes, and
    /// the reason nothing downstream has to ask "is this really a scale".</summary>
    private static SurveyQuestion Build(int surveyId, int position, SaveQuestionRequest request, Instant now)
        => request.QuestionType switch
        {
            FeedbackQuestionType.YesNo => SurveyQuestion.YesNoQuestion(
                surveyId, position, request.Text, request.YesIsPositive, request.CountsTowardScore, now),

            FeedbackQuestionType.Scale => SurveyQuestion.ScaleQuestion(
                surveyId, position, request.Text, request.ScaleMax ?? 0, request.CountsTowardScore, now),

            FeedbackQuestionType.Choice => SurveyQuestion.ChoiceQuestion(
                surveyId, position, request.Text, request.Labels ?? [], request.AllowOther, now),

            FeedbackQuestionType.Open => SurveyQuestion.OpenQuestion(surveyId, position, request.Text, now),

            _ => throw new ArgumentException($"Unknown question type '{request.QuestionType}'."),
        };

    /// <summary>Replaces the question's text and its whole option list.
    ///
    /// ⚠ Options are replaced rather than merged, which means editing a question that has
    /// already been answered would orphan the option those answers point at. The answer rows
    /// restrict that delete at the database, so this refuses first with a sentence the owner can
    /// act on rather than letting a foreign key say it.</summary>
    public async Task<SurveyQuestionResponse> UpdateQuestionAsync(
        int questionId, SaveQuestionRequest request, CancellationToken cancellationToken)
    {
        var question = await _uow.SurveyQuestions.GetWithOptionsAsync(questionId, cancellationToken)
            ?? throw new NotFoundException(nameof(SurveyQuestion), questionId);

        Validate(request);

        var now = _clock.GetCurrentInstant();
        var answered = await _uow.SurveyQuestions.HasAnswersAsync(questionId, cancellationToken);
        var shapeChanged = ShapeDiffers(question, request);

        if (answered && shapeChanged)
        {
            throw new InvalidStateTransitionException(
                nameof(SurveyQuestion), questionId, "already answered by callers",
                "changed — reword it, or add a new question and remove this one");
        }

        // ⚠ Only reshape when the shape actually moved. Reshape rebuilds the option rows, so
        // calling it for a typo fix would hand every option a new id and orphan the answers
        // pointing at the old ones — a silent version of the deletion this method refuses.
        if (shapeChanged)
        {
            question.Reshape(
                request.QuestionType, request.Text, request.CountsTowardScore,
                request.ScaleMax, request.YesIsPositive, request.AllowOther, request.Labels ?? [], now);
        }
        else
        {
            question.Retitle(request.Text, request.CountsTowardScore, now);
        }

        _uow.SurveyQuestions.Update(question);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(question);
    }

    /// <summary>Removes a question outright when nobody has answered it, and refuses when they
    /// have — deleting it would take reported numbers with it.</summary>
    public async Task RemoveQuestionAsync(int questionId, CancellationToken cancellationToken)
    {
        var question = await _uow.SurveyQuestions.GetByIdAsync(questionId, cancellationToken)
            ?? throw new NotFoundException(nameof(SurveyQuestion), questionId);

        if (await _uow.SurveyQuestions.HasAnswersAsync(questionId, cancellationToken))
        {
            throw new InvalidStateTransitionException(
                nameof(SurveyQuestion), questionId, "already answered by callers", "deleted");
        }

        _uow.SurveyQuestions.Remove(question);
        await _uow.SaveChangesAsync(cancellationToken);
        await RenumberAsync(question.SurveyId, cancellationToken);
    }

    public async Task<SurveyDetailResponse> ReorderAsync(
        int surveyId, ReorderQuestionsRequest request, CancellationToken cancellationToken)
    {
        var questions = await _uow.SurveyQuestions.GetForSurveyAsync(surveyId, cancellationToken);
        var byId = questions.ToDictionary(q => q.Id);
        var now = _clock.GetCurrentInstant();

        var position = 0;
        foreach (var id in request.QuestionIdsInOrder)
        {
            if (!byId.TryGetValue(id, out var question))
            {
                throw new NotFoundException(nameof(SurveyQuestion), id);
            }

            question.MoveTo(position++, now);
            _uow.SurveyQuestions.Update(question);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return await GetAsync(surveyId, cancellationToken);
    }

    private async Task RenumberAsync(int surveyId, CancellationToken cancellationToken)
    {
        var remaining = await _uow.SurveyQuestions.GetForSurveyAsync(surveyId, cancellationToken);
        var now = _clock.GetCurrentInstant();

        var position = 0;
        foreach (var question in remaining)
        {
            question.MoveTo(position++, now);
            _uow.SurveyQuestions.Update(question);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureQuotaAllowsAnotherAsync(CancellationToken cancellationToken)
    {
        var settings = await _uow.FeedbackSettings.GetForCurrentTenantAsync(cancellationToken);
        var limit = settings?.MaxSurveys ?? FeedbackSettings.DefaultMaxSurveys;
        var used = await _uow.Surveys.CountForCurrentTenantAsync(cancellationToken);

        if (used >= limit)
        {
            throw new QuotaExceededException("questionnaire(s)", limit, used);
        }
    }

    /// <summary>What the entity cannot check for itself, said in words the owner can act on.
    ///
    /// The entity refuses a bad scale and a one-option choice already; these are the two mistakes
    /// the form can make that would otherwise be accepted and quietly mean nothing.</summary>
    private static void Validate(SaveQuestionRequest request)
    {
        if (request.CountsTowardScore
            && request.QuestionType is not (FeedbackQuestionType.YesNo or FeedbackQuestionType.Scale))
        {
            throw new ArgumentException(
                "Only a Yes/No or a scale question has a score. A list of names has counts, and an open "
                + "answer has words.");
        }

        if (request.QuestionType == FeedbackQuestionType.Scale
            && !SurveyQuestion.AllowedScaleMaximums.Contains(request.ScaleMax ?? 0))
        {
            throw new ArgumentException(
                $"A scale runs to {string.Join(", ", SurveyQuestion.AllowedScaleMaximums)} — nothing else.");
        }
    }

    /// <summary>Whether saving this would rebuild the options.
    ///
    /// Compares the shape rather than the option rows, because the options are now a consequence
    /// of the shape: a Scale(5) always has the same five, so the only way to change them is to
    /// change the 5. Text and the score tick are deliberately absent — neither touches an option,
    /// so neither should block an edit to a question people have already answered.</summary>
    private static bool ShapeDiffers(SurveyQuestion question, SaveQuestionRequest request)
    {
        if (question.QuestionType != request.QuestionType)
        {
            return true;
        }

        return request.QuestionType switch
        {
            FeedbackQuestionType.YesNo => question.YesIsPositive != request.YesIsPositive,
            FeedbackQuestionType.Scale => question.ScaleMax != request.ScaleMax,
            FeedbackQuestionType.Choice => question.AllowOther != request.AllowOther
                                           || !question.Options.Where(o => !o.IsOther)
                                               .OrderBy(o => o.Position)
                                               .Select(o => o.Text)
                                               .SequenceEqual(
                                                   (request.Labels ?? [])
                                                   .Where(l => !string.IsNullOrWhiteSpace(l))
                                                   .Select(l => l.Trim())),
            _ => false,
        };
    }

    private static SurveyQuestionResponse ToResponse(SurveyQuestion question)
        => new(
            question.Id,
            question.Position,
            question.Text,
            question.QuestionType,
            question.CountsTowardScore,
            question.ScaleMax,
            question.YesIsPositive,
            question.AllowOther,
            question.Options
                .OrderBy(o => o.Position)
                .Select(o => new SurveyOptionResponse(o.Id, o.Position, o.Text, o.ScorePercent, o.IsOther))
                .ToList());

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");
}
