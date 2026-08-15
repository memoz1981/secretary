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

        if (request.IsHeadline)
        {
            await ClearOtherHeadlinesAsync(existing, exceptQuestionId: null, now);
        }

        var question = SurveyQuestion.Create(
            surveyId, existing.Count, request.Text, request.QuestionType, request.IsHeadline, now);

        await _uow.SurveyQuestions.AddAsync(question, cancellationToken);

        // Saved before the options so the identity exists — they key on it, the same reason
        // customer registration saves in two steps.
        await _uow.SaveChangesAsync(cancellationToken);

        foreach (var option in request.Options)
        {
            question.AddOption(option.Text, option.Value, now);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(question);
    }

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
        var optionsChanged = OptionsDiffer(question, request);

        if (answered && optionsChanged)
        {
            throw new InvalidStateTransitionException(
                nameof(SurveyQuestion), questionId, "already answered by callers",
                "changed — reword it, or add a new question and remove this one");
        }

        if (request.IsHeadline)
        {
            var siblings = await _uow.SurveyQuestions.GetForSurveyAsync(question.SurveyId, cancellationToken);
            await ClearOtherHeadlinesAsync(siblings, questionId, now);
        }

        question.Update(request.Text, question.Position, request.IsHeadline, now);

        if (optionsChanged)
        {
            question.ClearOptions(now);
            foreach (var option in request.Options)
            {
                question.AddOption(option.Text, option.Value, now);
            }
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

            question.Update(question.Text, position++, question.IsHeadline, now);
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
            question.Update(question.Text, position++, question.IsHeadline, now);
            _uow.SurveyQuestions.Update(question);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearOtherHeadlinesAsync(
        IReadOnlyList<SurveyQuestion> questions, int? exceptQuestionId, Instant now)
    {
        foreach (var other in questions.Where(q => q.IsHeadline && q.Id != exceptQuestionId))
        {
            other.Update(other.Text, other.Position, isHeadline: false, now);
            _uow.SurveyQuestions.Update(other);
        }

        await Task.CompletedTask;
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

    private static void Validate(SaveQuestionRequest request)
    {
        if (request.QuestionType == FeedbackQuestionType.Choice && request.Options.Count < 2)
        {
            throw new ArgumentException("A multiple-choice question needs at least two options to choose between.");
        }

        if (request.QuestionType == FeedbackQuestionType.Open && request.Options.Count > 0)
        {
            throw new ArgumentException("An open question is answered in the caller's own words and has no options.");
        }

        // All numbered or none. A half-scored question would average some answers and silently
        // drop the rest, which is worse than not averaging at all.
        var valued = request.Options.Count(o => o.Value is not null);
        if (valued is not 0 && valued != request.Options.Count)
        {
            throw new ArgumentException(
                "Give every option a number or none of them — a partly numbered question cannot be averaged honestly.");
        }
    }

    private static bool OptionsDiffer(SurveyQuestion question, SaveQuestionRequest request)
    {
        if (question.Options.Count != request.Options.Count)
        {
            return true;
        }

        return question.Options
            .OrderBy(o => o.Position)
            .Zip(request.Options, (existing, wanted) => existing.Text != wanted.Text.Trim() || existing.Value != wanted.Value)
            .Any(different => different);
    }

    private static SurveyQuestionResponse ToResponse(SurveyQuestion question)
        => new(
            question.Id,
            question.Position,
            question.Text,
            question.QuestionType,
            question.IsHeadline,
            question.Options
                .OrderBy(o => o.Position)
                .Select(o => new SurveyOptionResponse(o.Id, o.Position, o.Text, o.Value))
                .ToList());

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");
}
