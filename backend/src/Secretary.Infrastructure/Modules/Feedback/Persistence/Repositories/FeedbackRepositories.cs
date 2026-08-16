using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Secretary.Infrastructure.Persistence.Repositories;

internal sealed class SurveyRepository : ISurveyRepository
{
    private readonly AppDbContext _db;

    public SurveyRepository(AppDbContext db) => _db = db;

    public async Task<Survey?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.Surveys.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Survey>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.Surveys.OrderBy(s => s.Name).ToListAsync(cancellationToken);

    public async Task AddAsync(Survey entity, CancellationToken cancellationToken)
        => await _db.Surveys.AddAsync(entity, cancellationToken);

    public void Update(Survey entity) => _db.Surveys.Update(entity);

    public void Remove(Survey entity) => _db.Surveys.Remove(entity);

    public async Task<IReadOnlyList<Survey>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.Surveys
            .Where(s => s.Status == EntityStatus.Active)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public async Task<int> CountForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.Surveys.CountAsync(s => s.Status == EntityStatus.Active, cancellationToken);
}

internal sealed class SurveyQuestionRepository : ISurveyQuestionRepository
{
    private readonly AppDbContext _db;

    public SurveyQuestionRepository(AppDbContext db) => _db = db;

    public async Task<SurveyQuestion?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.SurveyQuestions.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SurveyQuestion>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.SurveyQuestions.OrderBy(q => q.Position).ToListAsync(cancellationToken);

    public async Task AddAsync(SurveyQuestion entity, CancellationToken cancellationToken)
        => await _db.SurveyQuestions.AddAsync(entity, cancellationToken);

    public void Update(SurveyQuestion entity) => _db.SurveyQuestions.Update(entity);

    public void Remove(SurveyQuestion entity) => _db.SurveyQuestions.Remove(entity);

    public async Task<IReadOnlyList<SurveyQuestion>> GetForSurveyAsync(
        int surveyId, CancellationToken cancellationToken)
        => await _db.SurveyQuestions
            .Include(q => q.Options)
            .Where(q => q.SurveyId == surveyId && q.Status == EntityStatus.Active)
            .OrderBy(q => q.Position)
            .ToListAsync(cancellationToken);

    public async Task<SurveyQuestion?> GetWithOptionsAsync(int questionId, CancellationToken cancellationToken)
        => await _db.SurveyQuestions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);

    public async Task<bool> HasAnswersAsync(int questionId, CancellationToken cancellationToken)
        => await _db.FeedbackAnswers.AnyAsync(a => a.SurveyQuestionId == questionId, cancellationToken);
}

internal sealed class SurveyRequestRepository : ISurveyRequestRepository
{
    private readonly AppDbContext _db;

    public SurveyRequestRepository(AppDbContext db) => _db = db;

    public async Task<SurveyRequest?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.SurveyRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SurveyRequest>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.SurveyRequests.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task AddAsync(SurveyRequest entity, CancellationToken cancellationToken)
        => await _db.SurveyRequests.AddAsync(entity, cancellationToken);

    public void Update(SurveyRequest entity) => _db.SurveyRequests.Update(entity);

    public void Remove(SurveyRequest entity) => _db.SurveyRequests.Remove(entity);

    public async Task<IReadOnlyList<SurveyRequest>> SearchAsync(
        Instant? from, Instant? to, int? surveyId, CancellationToken cancellationToken)
    {
        var query = _db.SurveyRequests.AsQueryable();

        if (from is { } start)
        {
            query = query.Where(r => r.CreatedAtUtc >= start);
        }

        if (to is { } end)
        {
            query = query.Where(r => r.CreatedAtUtc <= end);
        }

        if (surveyId is { } id)
        {
            query = query.Where(r => r.SurveyId == id);
        }

        return await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SurveyRequest>> GetDueAsync(Instant asOf, CancellationToken cancellationToken)
        => await _db.SurveyRequests
            .Where(r => r.NextAttemptDueAt != null && r.NextAttemptDueAt <= asOf)
            .OrderBy(r => r.NextAttemptDueAt)
            .ToListAsync(cancellationToken);
}

internal sealed class FeedbackCallRepository : IFeedbackCallRepository
{
    private readonly AppDbContext _db;

    public FeedbackCallRepository(AppDbContext db) => _db = db;

    public async Task<FeedbackCall?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.FeedbackCalls.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FeedbackCall>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.FeedbackCalls.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task AddAsync(FeedbackCall entity, CancellationToken cancellationToken)
        => await _db.FeedbackCalls.AddAsync(entity, cancellationToken);

    public void Update(FeedbackCall entity) => _db.FeedbackCalls.Update(entity);

    public void Remove(FeedbackCall entity) => _db.FeedbackCalls.Remove(entity);

    public async Task<IReadOnlyList<FeedbackCall>> SearchAsync(
        Instant? from, Instant? to, int? surveyId, CancellationToken cancellationToken)
    {
        var query = _db.FeedbackCalls.AsQueryable();

        if (from is { } start)
        {
            query = query.Where(c => c.CreatedAtUtc >= start);
        }

        if (to is { } end)
        {
            query = query.Where(c => c.CreatedAtUtc <= end);
        }

        // Through the request, because that is where the questionnaire is recorded. Copying the
        // survey id onto the attempt would make this a column comparison and a second answer to
        // the same question, free to disagree with the first.
        if (surveyId is { } id)
        {
            query = query.Where(c => _db.SurveyRequests.Any(r => r.Id == c.SurveyRequestId && r.SurveyId == id));
        }

        return await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FeedbackCall>> GetForRequestsAsync(
        IReadOnlyCollection<int> requestIds, CancellationToken cancellationToken)
        => requestIds.Count == 0
            ? []
            : await _db.FeedbackCalls
                .Where(c => requestIds.Contains(c.SurveyRequestId))
                .OrderBy(c => c.CreatedAtUtc)
                .ToListAsync(cancellationToken);
}

internal sealed class FeedbackAnswerRepository : IFeedbackAnswerRepository
{
    private readonly AppDbContext _db;

    public FeedbackAnswerRepository(AppDbContext db) => _db = db;

    public async Task<FeedbackAnswer?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.FeedbackAnswers.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FeedbackAnswer>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.FeedbackAnswers.ToListAsync(cancellationToken);

    public async Task AddAsync(FeedbackAnswer entity, CancellationToken cancellationToken)
        => await _db.FeedbackAnswers.AddAsync(entity, cancellationToken);

    public void Update(FeedbackAnswer entity) => _db.FeedbackAnswers.Update(entity);

    public void Remove(FeedbackAnswer entity) => _db.FeedbackAnswers.Remove(entity);

    public async Task<IReadOnlyList<FeedbackAnswer>> GetForCallAsync(int callId, CancellationToken cancellationToken)
        => await _db.FeedbackAnswers
            .Where(a => a.FeedbackCallId == callId)
            .OrderBy(a => a.AnsweredAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeedbackAnswer>> GetForCallsAsync(
        IReadOnlyCollection<int> callIds, CancellationToken cancellationToken)
        => callIds.Count == 0
            ? []
            : await _db.FeedbackAnswers
                .Where(a => callIds.Contains(a.FeedbackCallId))
                .ToListAsync(cancellationToken);
}

internal sealed class FeedbackSettingsRepository : IFeedbackSettingsRepository
{
    private readonly AppDbContext _db;

    public FeedbackSettingsRepository(AppDbContext db) => _db = db;

    public async Task<FeedbackSettings?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => await _db.FeedbackSettings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FeedbackSettings>> GetAllAsync(CancellationToken cancellationToken)
        => await _db.FeedbackSettings.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public async Task AddAsync(FeedbackSettings entity, CancellationToken cancellationToken)
        => await _db.FeedbackSettings.AddAsync(entity, cancellationToken);

    public void Update(FeedbackSettings entity) => _db.FeedbackSettings.Update(entity);

    public void Remove(FeedbackSettings entity) => _db.FeedbackSettings.Remove(entity);

    public async Task<FeedbackSettings?> GetForCurrentTenantAsync(CancellationToken cancellationToken)
        => await _db.FeedbackSettings.FirstOrDefaultAsync(cancellationToken);

    /// <summary>⚠ Filters ignored on purpose. The platform admin who sets a quota has no tenant
    /// of their own, so the ambient filter would find nothing and every quota would read as
    /// unset.</summary>
    public async Task<FeedbackSettings?> GetForTenantAsync(int tenantId, CancellationToken cancellationToken)
        => await _db.FeedbackSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
}
