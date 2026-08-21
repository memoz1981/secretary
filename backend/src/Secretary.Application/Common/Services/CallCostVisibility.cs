using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;

namespace Secretary.Application.Services;

/// <summary>Whether the caller of this request may be shown what their AI calls cost.
///
/// ⚠ Asked where the response is built, not where it is drawn. A cost left out of a page but
/// present in the JSON is hidden from nobody: it is two clicks away in any browser's network tab,
/// and this is a multi-tenant product where the number in question is our own margin.
///
/// The platform admin has no tenant of their own, which is exactly the signal — a caller with no
/// TenantId is us, and sees everything. A tenant-scoped caller sees costs only if the platform
/// admin ticked the box on their record, which is expected to be demonstration tenants and
/// nobody else.
///
/// Answered once per request. Every call row on a page would otherwise ask the same question of
/// the database, and the answer cannot change while one page is being built.</summary>
public sealed class CallCostVisibility
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantProvider _currentTenant;
    private bool? _allowed;

    public CallCostVisibility(IUnitOfWork uow, ICurrentTenantProvider currentTenant)
    {
        _uow = uow;
        _currentTenant = currentTenant;
    }

    public async Task<bool> IsAllowedAsync(CancellationToken cancellationToken)
    {
        if (_allowed is { } known)
        {
            return known;
        }

        if (_currentTenant.TenantId is not { } tenantId)
        {
            return (_allowed = true).Value;
        }

        var tenant = await _uow.Tenants.GetByIdAsync(tenantId, cancellationToken);

        // A tenant we cannot read is not a tenant we show money to. The failure of a lookup
        // should not be the thing that reveals a figure.
        return (_allowed = tenant?.ShowCallCosts ?? false).Value;
    }

    /// <summary>The figure, or nothing. Null rather than zero: a zero is a claim that the call
    /// was free, and it would be summed, averaged and charted as one.</summary>
    public async Task<decimal?> ShowAsync(decimal cost, CancellationToken cancellationToken)
        => await IsAllowedAsync(cancellationToken) ? cost : null;
}
