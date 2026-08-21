using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>The questionnaire quota — read by the tenant, written by the platform.
///
/// Two ways in, because the two callers are different people. An Owner asks about their own
/// account and the ambient tenant filter answers. A platform admin has no tenant of their own,
/// so they pass the id and the filter has to be stepped around; see the repository.</summary>
public sealed class FeedbackSettingsService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public FeedbackSettingsService(IUnitOfWork uow, IClock clock)
    {
        _uow = uow;
        _clock = clock;
    }

    /// <summary>What this tenant may have, and what they have used. Both numbers, because a
    /// limit on its own does not tell an Owner whether they can add another.</summary>
    public async Task<FeedbackSettingsResponse> GetForCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var settings = await _uow.FeedbackSettings.GetForCurrentTenantAsync(cancellationToken);
        var used = await _uow.Surveys.CountForCurrentTenantAsync(cancellationToken);
        return new FeedbackSettingsResponse(settings?.MaxSurveys ?? FeedbackSettings.DefaultMaxSurveys, used);
    }

    public async Task<FeedbackSettingsResponse> GetForTenantAsync(int tenantId, CancellationToken cancellationToken)
    {
        var settings = await _uow.FeedbackSettings.GetForTenantAsync(tenantId, cancellationToken);
        return new FeedbackSettingsResponse(settings?.MaxSurveys ?? FeedbackSettings.DefaultMaxSurveys, UsedSurveys: 0);
    }

    /// <summary>Creates the row on first write rather than seeding one for every tenant — a
    /// tenant without the module has no opinion about questionnaires. Same shape as delivery
    /// settings in the order module.</summary>
    public async Task<FeedbackSettingsResponse> UpdateAsync(
        int tenantId, UpdateFeedbackSettingsRequest request, CancellationToken cancellationToken)
    {
        var now = _clock.GetCurrentInstant();
        var settings = await _uow.FeedbackSettings.GetForTenantAsync(tenantId, cancellationToken);

        if (settings is null)
        {
            settings = FeedbackSettings.Create(tenantId, request.MaxSurveys, now);
            await _uow.FeedbackSettings.AddAsync(settings, cancellationToken);
        }
        else
        {
            settings.Update(request.MaxSurveys, now);
            _uow.FeedbackSettings.Update(settings);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return new FeedbackSettingsResponse(settings.MaxSurveys, UsedSurveys: 0);
    }
}
