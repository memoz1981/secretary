using Secretary.Application.Abstractions;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Agents.Orders;

internal sealed class TenantOrderDirectory : ITenantOrderDirectory
{
    private readonly OrderService _orders;
    private readonly BusinessHoursService _businessHours;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;

    public TenantOrderDirectory(
        OrderService orders, BusinessHoursService businessHours, IClock clock, ICurrentTenantProvider currentTenant)
    {
        _orders = orders;
        _businessHours = businessHours;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(CancellationToken cancellationToken)
    {
        var tenantId = TenantId();
        if (OrderDirectoryStore.TryGetProducts(tenantId, out var cached))
        {
            return cached;
        }

        var fresh = await _orders.GetCatalogAsync(cancellationToken);
        OrderDirectoryStore.SetProducts(tenantId, fresh);
        return fresh;
    }

    public async Task<LocalDate?> GetDeliveryDayAsync(CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        var today = _clock.GetCurrentInstant().InZone(AzerbaijanTime.Zone).Date;

        // Worked out per call rather than cached. The policy is what holds still; the day it
        // produces changes at midnight, and a cached one would have the agent promising
        // yesterday.
        return BusinessHoursService.NextWorkingDay(policy.OpenDays, today, policy.LeadWorkingDays);
    }

    private async Task<DeliveryPolicy> GetPolicyAsync(CancellationToken cancellationToken)
    {
        var tenantId = TenantId();
        if (OrderDirectoryStore.TryGetPolicy(tenantId, out var cached))
        {
            return cached;
        }

        var settings = await _orders.GetSettingsAsync(cancellationToken);
        var week = await _businessHours.GetWeekByDayAsync(cancellationToken);

        // A day with no row is closed — a tenant who has never set their hours should be
        // offering nothing, not everything.
        var openDays = week.Where(kv => !kv.Value.IsClosed).Select(kv => kv.Key).ToHashSet();

        var policy = new DeliveryPolicy(settings.LeadWorkingDays, settings.MaxDeliveryDaysAhead, openDays);
        OrderDirectoryStore.SetPolicy(tenantId, policy);
        return policy;
    }

    private int TenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");
}
