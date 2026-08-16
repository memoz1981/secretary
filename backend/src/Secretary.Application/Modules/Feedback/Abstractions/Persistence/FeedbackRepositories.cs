using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Application.Abstractions.Persistence;

/// <summary>The Feedback module's repositories. One file, one module's worth of small contracts
/// — splitting five of these into five files would add headers and no clarity.</summary>
public interface ISurveyRepository : IRepository<Survey>
{
    Task<IReadOnlyList<Survey>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken);

    /// <summary>How many this tenant already has, for the quota check. Counted rather than
    /// listed: the caller only wants the number.</summary>
    Task<int> CountForCurrentTenantAsync(CancellationToken cancellationToken);
}

public interface ISurveyQuestionRepository : IRepository<SurveyQuestion>
{
    /// <summary>In running order, options included — the agent reads them straight out and the
    /// editor renders them as they will be asked.</summary>
    Task<IReadOnlyList<SurveyQuestion>> GetForSurveyAsync(int surveyId, CancellationToken cancellationToken);

    /// <summary>With its options, for the one question the agent is putting right now.</summary>
    Task<SurveyQuestion?> GetWithOptionsAsync(int questionId, CancellationToken cancellationToken);

    /// <summary>Whether anyone has already answered this question. A question with history
    /// cannot be deleted without taking reported numbers with it.</summary>
    Task<bool> HasAnswersAsync(int questionId, CancellationToken cancellationToken);
}

public interface ISurveyRequestRepository : IRepository<SurveyRequest>
{
    /// <summary>Newest first, optionally bounded and optionally one questionnaire — the filters
    /// the follow-up list and the dashboard both need.</summary>
    Task<IReadOnlyList<SurveyRequest>> SearchAsync(
        Instant? from, Instant? to, int? surveyId, CancellationToken cancellationToken);

    /// <summary>Whose next dial is due. What the scheduler will ask for when telephony lands, and
    /// what makes the retry policy real rather than a number in a settings page.</summary>
    Task<IReadOnlyList<SurveyRequest>> GetDueAsync(Instant asOf, CancellationToken cancellationToken);
}

public interface IFeedbackCallRepository : IRepository<FeedbackCall>
{
    /// <summary>Newest first, optionally bounded and optionally one questionnaire — the two
    /// filters the calls page and the dashboard both need. The questionnaire is reached through
    /// the request, which is the only place it is recorded.</summary>
    Task<IReadOnlyList<FeedbackCall>> SearchAsync(
        Instant? from, Instant? to, int? surveyId, CancellationToken cancellationToken);

    /// <summary>Every attempt against the given requests, in one query.</summary>
    Task<IReadOnlyList<FeedbackCall>> GetForRequestsAsync(
        IReadOnlyCollection<int> requestIds, CancellationToken cancellationToken);
}

public interface IFeedbackAnswerRepository : IRepository<FeedbackAnswer>
{
    Task<IReadOnlyList<FeedbackAnswer>> GetForCallAsync(int callId, CancellationToken cancellationToken);

    /// <summary>Every answer belonging to the given calls, in one query. The dashboard totals a
    /// month of calls, and one query per call is how that page becomes unusable.</summary>
    Task<IReadOnlyList<FeedbackAnswer>> GetForCallsAsync(
        IReadOnlyCollection<int> callIds, CancellationToken cancellationToken);
}

public interface IFeedbackSettingsRepository : IRepository<FeedbackSettings>
{
    /// <summary>Null when the platform has never set a quota — the caller applies the default
    /// rather than a row existing for every tenant whether or not they hold the module.</summary>
    Task<FeedbackSettings?> GetForCurrentTenantAsync(CancellationToken cancellationToken);

    /// <summary>By tenant id rather than the ambient one: the platform admin setting a quota has
    /// no tenant of their own.</summary>
    Task<FeedbackSettings?> GetForTenantAsync(int tenantId, CancellationToken cancellationToken);
}
