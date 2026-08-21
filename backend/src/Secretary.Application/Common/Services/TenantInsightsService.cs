using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using Secretary.Domain.ValueObjects;

namespace Secretary.Application.Services;

/// <summary>What one tenant has used, across every module they hold. Platform-admin work.
///
/// ⚠ Reads all three call tables and adds them up here rather than asking each module's dashboard
/// service. Those services answer a tenant's question — "how is my appointment line doing" — in
/// that module's own words, and they are scoped to the caller's own tenant, which the platform
/// admin does not have. This asks a different question in a vocabulary all three share.</summary>
public sealed class TenantInsightsService
{
    private readonly IUnitOfWork _uow;

    public TenantInsightsService(IUnitOfWork uow) => _uow = uow;

    public async Task<TenantInsightsResponse> GetAsync(int tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        var appointmentCalls = await _uow.Calls.GetForTenantAsync(tenantId, cancellationToken);
        var orderCalls = await _uow.OrderCalls.GetForTenantAsync(tenantId, cancellationToken);
        var feedbackCalls = await _uow.FeedbackCalls.GetForTenantAsync(tenantId, cancellationToken);

        var byModule = new List<TenantModuleUsage>
        {
            new(Module.Appointment,
                appointmentCalls.Count,
                appointmentCalls.Sum(c => c.DurationSeconds),
                appointmentCalls.Sum(c => (long)c.TokenUsage.TotalTokens),
                appointmentCalls.Sum(c => c.CostUsd)),

            new(Module.Order,
                orderCalls.Count,
                orderCalls.Sum(c => c.DurationSeconds),
                orderCalls.Sum(c => (long)c.TokenUsage.TotalTokens),
                orderCalls.Sum(c => c.CostUsd)),

            new(Module.Feedback,
                feedbackCalls.Count,
                feedbackCalls.Sum(c => c.DurationSeconds),
                feedbackCalls.Sum(c => (long)c.TokenUsage.TotalTokens),
                feedbackCalls.Sum(c => c.CostUsd)),
        };

        var categories = appointmentCalls.Select(c => CallCategories.For(c.Outcome))
            .Concat(orderCalls.Select(c => CallCategories.For(c.Outcome)))
            .Concat(feedbackCalls.Select(c => CallCategories.For(c.CallStatus, c.CallerTurnCount)))
            .ToList();

        // Every category listed, including the empty ones. A missing row reads as a category that
        // does not exist rather than one nothing landed in, and "no calls were forwarded" is one
        // of the more useful things this page can say.
        var byCategory = CallCategories.All
            .Select(category => new TenantCategoryUsage(category, categories.Count(c => c == category)))
            .ToList();

        return new TenantInsightsResponse(
            tenant.Id,
            tenant.Name,
            byModule.Sum(m => m.Calls),
            byModule.Sum(m => m.DurationSeconds),
            byModule.Sum(m => m.Tokens),
            byModule.Sum(m => m.CostUsd),
            byModule,
            byCategory);
    }
}
