using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Services page (page-inventory.md) — Owner read/write, Staff read-only (enforced
/// by an authorization policy at the controller, not by this service). ListAsync is the same
/// call the AI agent's cold-start cache load and cache-refresh (Flow G) both use.</summary>
public sealed class ServiceOfferingService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly IAgentDirectoryChangeNotifier _changeNotifier;

    public ServiceOfferingService(
        IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant, IAgentDirectoryChangeNotifier changeNotifier)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
        _changeNotifier = changeNotifier;
    }

    public async Task<IReadOnlyList<ServiceOfferingResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var services = await _uow.ServiceOfferings.GetAllForCurrentTenantAsync(cancellationToken);
        return services.Select(ToResponse).ToList();
    }

    public async Task<ServiceOfferingResponse> CreateAsync(CreateServiceOfferingRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var now = _clock.GetCurrentInstant();
        var service = ServiceOffering.Create(tenantId, request.Name, request.Price, request.DurationMinutes, now);
        await _uow.ServiceOfferings.AddAsync(service, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        // A new offering is assigned to every provider; the tenant unchecks who doesn't
        // do it on the Providers page matrix.
        var providers = await _uow.Providers.GetAllForCurrentTenantAsync(cancellationToken);
        foreach (var provider in providers)
        {
            await _uow.ProviderServiceOfferings.AddAsync(
                ProviderServiceOffering.Create(tenantId, provider.Id, service.Id, now), cancellationToken);
        }

        if (providers.Count > 0)
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }

        _changeNotifier.NotifyChanged(tenantId);
        return ToResponse(service);
    }

    public async Task<ServiceOfferingResponse> UpdateAsync(int id, UpdateServiceOfferingRequest request, CancellationToken cancellationToken)
    {
        var service = await _uow.ServiceOfferings.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceOffering), id);

        service.UpdateDetails(request.Name, request.Price, request.DurationMinutes, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        _changeNotifier.NotifyChanged(service.TenantId);
        return ToResponse(service);
    }

    /// <summary>Soft-deactivates — past appointments keep pointing at the row.</summary>
    public async Task RemoveAsync(int id, CancellationToken cancellationToken)
    {
        var service = await _uow.ServiceOfferings.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceOffering), id);

        service.Deactivate(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        _changeNotifier.NotifyChanged(service.TenantId);
    }

    private static ServiceOfferingResponse ToResponse(ServiceOffering service)
        => new(service.Id, service.Name, service.Price, service.DurationMinutes, service.UpdatedAt);
}
