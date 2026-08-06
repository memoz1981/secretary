using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Granting and revoking module access. Platform-admin work — the tenant is always
/// passed in, never inferred from the caller, because the caller has no tenant of their own.</summary>
public sealed class TenantModuleService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public TenantModuleService(IUnitOfWork uow, IClock clock)
    {
        _uow = uow;
        _clock = clock;
    }

    /// <summary>Every module with its current state, so the admin screen can render a full set
    /// of switches rather than only what has been granted before.</summary>
    public async Task<IReadOnlyList<TenantModuleResponse>> ListAsync(int tenantId, CancellationToken cancellationToken)
    {
        await EnsureTenantExistsAsync(tenantId, cancellationToken);

        var granted = await _uow.TenantModules.GetForTenantAsync(tenantId, cancellationToken);

        return Enum.GetValues<Module>()
            .Select(module => new TenantModuleResponse(
                module,
                granted.FirstOrDefault(g => g.Module == module)?.IsEnabled ?? false))
            .ToList();
    }

    /// <summary>Grants or revokes. Idempotent: setting a module to what it already is succeeds
    /// and changes nothing, which is what a checkbox that got double-clicked should do.</summary>
    public async Task<TenantModuleResponse> SetAsync(
        int tenantId, SetTenantModuleRequest request, CancellationToken cancellationToken)
    {
        await EnsureTenantExistsAsync(tenantId, cancellationToken);

        var now = _clock.GetCurrentInstant();
        var existing = await _uow.TenantModules.GetAsync(tenantId, request.Module, cancellationToken);

        if (existing is null)
        {
            if (!request.Enabled)
            {
                // Nothing granted and nothing asked for — no row is the same as a revoked one.
                return new TenantModuleResponse(request.Module, false);
            }

            var granted = TenantModule.Grant(tenantId, request.Module, now);
            await _uow.TenantModules.AddAsync(granted, cancellationToken);
        }
        else if (request.Enabled)
        {
            // Reactivate rather than insert: the unique index is on (TenantId, Module), and
            // reusing the row keeps the record of when this tenant first had the module.
            existing.Reactivate(now);
            _uow.TenantModules.Update(existing);
        }
        else
        {
            existing.Deactivate(now);
            _uow.TenantModules.Update(existing);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return new TenantModuleResponse(request.Module, request.Enabled);
    }

    private async Task EnsureTenantExistsAsync(int tenantId, CancellationToken cancellationToken)
    {
        if (await _uow.Tenants.GetByIdAsync(tenantId, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(Tenant), tenantId);
        }
    }
}
