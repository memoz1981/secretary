using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Providers page — Owner only. Reads rely on Infrastructure's automatic tenant
/// query filter; only Create needs the current tenant explicitly, to stamp it onto the new
/// entity. Also owns the provider×service-offering correspondence: a new provider is
/// assigned every offering, a toggle flips one cell of the matrix, and
/// GetActiveProviderIdsForOfferingAsync backs the agent's "who can do this service".</summary>
public sealed class ProviderService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly IAgentDirectoryChangeNotifier _changeNotifier;

    public ProviderService(
        IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant, IAgentDirectoryChangeNotifier changeNotifier)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
        _changeNotifier = changeNotifier;
    }

    public async Task<IReadOnlyList<ProviderResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var providers = await _uow.Providers.GetAllForCurrentTenantAsync(cancellationToken);
        return providers.Select(ToResponse).ToList();
    }

    /// <summary>Active providers that actively offer the given service — the agent must only
    /// propose these for availability and booking.</summary>
    public async Task<IReadOnlyList<ProviderResponse>> ListForOfferingAsync(int serviceOfferingId, CancellationToken cancellationToken)
    {
        var providerIds = await _uow.ProviderServiceOfferings.GetActiveProviderIdsForOfferingAsync(serviceOfferingId, cancellationToken);
        var providers = await _uow.Providers.GetAllForCurrentTenantAsync(cancellationToken);
        return providers.Where(p => providerIds.Contains(p.Id)).Select(ToResponse).ToList();
    }

    public async Task<ProviderResponse> CreateAsync(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var now = _clock.GetCurrentInstant();

        var provider = Provider.Create(tenantId, request.Name, now);
        await _uow.Providers.AddAsync(provider, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        // A new provider starts assigned to every service offering; the tenant unchecks
        // what they don't do on the Providers page matrix.
        var offerings = await _uow.ServiceOfferings.GetAllForCurrentTenantAsync(cancellationToken);
        foreach (var offering in offerings)
        {
            await _uow.ProviderServiceOfferings.AddAsync(
                ProviderServiceOffering.Create(tenantId, provider.Id, offering.Id, now), cancellationToken);
        }

        if (offerings.Count > 0)
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }

        _changeNotifier.NotifyChanged(tenantId);
        return ToResponse(provider);
    }

    public async Task<ProviderResponse> UpdateAsync(int id, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        var provider = await _uow.Providers.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Provider), id);

        provider.UpdateDetails(request.Name, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);

        // A rename matters to the agent as much as a reassignment does: it answers "who can do
        // this?" out loud, and the caller then asks for that provider by name.
        _changeNotifier.NotifyChanged(provider.TenantId);
        return ToResponse(provider);
    }

    /// <summary>Soft-deactivates — past appointments keep pointing at the row.</summary>
    public async Task RemoveAsync(int id, CancellationToken cancellationToken)
    {
        var provider = await _uow.Providers.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Provider), id);

        await EnsureNoUpcomingAppointmentsAsync(id, cancellationToken);
        provider.Deactivate(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        _changeNotifier.NotifyChanged(provider.TenantId);
    }

    /// <summary>The Providers page checkbox matrix: all active providers × all active
    /// offerings, plus which pairs are currently active.</summary>
    public async Task<ProviderServiceMatrixResponse> GetServiceMatrixAsync(CancellationToken cancellationToken)
    {
        var providers = await _uow.Providers.GetAllForCurrentTenantAsync(cancellationToken);
        var offerings = await _uow.ServiceOfferings.GetAllForCurrentTenantAsync(cancellationToken);
        var assignments = await _uow.ProviderServiceOfferings.GetAllForCurrentTenantAsync(cancellationToken);

        var active = assignments
            .Where(a => a.Status == EntityStatus.Active)
            .Select(a => (a.ProviderId, a.ServiceOfferingId))
            .ToHashSet();

        var cells = new List<ProviderServiceAssignmentResponse>();
        foreach (var provider in providers)
        {
            foreach (var offering in offerings)
            {
                cells.Add(new ProviderServiceAssignmentResponse(
                    provider.Id, offering.Id, active.Contains((provider.Id, offering.Id))));
            }
        }

        return new ProviderServiceMatrixResponse(
            providers.Select(ToResponse).ToList(),
            offerings.Select(o => new ServiceOfferingResponse(o.Id, o.Name, o.Price, o.DurationMinutes, o.UpdatedAt)).ToList(),
            cells);
    }

    /// <summary>Checks/unchecks one cell of the matrix. The row always exists (created with
    /// the provider or the offering); unchecking deactivates it rather than deleting it, so
    /// the history stays auditable. A missing row (data predating the matrix) is created.</summary>
    public async Task SetServiceAssignmentAsync(
        int providerId, SetProviderServiceAssignmentRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var now = _clock.GetCurrentInstant();

        if (await _uow.Providers.GetByIdAsync(providerId, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(Provider), providerId);
        }

        if (await _uow.ServiceOfferings.GetByIdAsync(request.ServiceOfferingId, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(ServiceOffering), request.ServiceOfferingId);
        }

        var assignment = await _uow.ProviderServiceOfferings.GetByProviderAndOfferingAsync(
            providerId, request.ServiceOfferingId, cancellationToken);

        if (assignment is null)
        {
            assignment = ProviderServiceOffering.Create(tenantId, providerId, request.ServiceOfferingId, now);
            await _uow.ProviderServiceOfferings.AddAsync(assignment, cancellationToken);
        }

        if (request.Active)
        {
            assignment.Reactivate(now);
        }
        else
        {
            assignment.Deactivate(now);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        // The one that matters most: unchecking a cell is how an Owner stops the agent from
        // offering a provider for a service they no longer do.
        _changeNotifier.NotifyChanged(tenantId);
    }

    /// <summary>Upcoming means upcoming.
    ///
    /// ⚠ This used to scan Instant.MinValue to Instant.MaxValue — every appointment that had ever
    /// existed — so one completed haircut from months ago made a provider permanently
    /// undeletable. The name said "upcoming" and the range said "ever", and the two disagreed
    /// silently: a tenant who has been running for a season cannot remove anybody who has ever
    /// worked for them.
    ///
    /// Past appointments are history and must keep pointing at the provider who did them, which
    /// is why this deactivates rather than deletes. Only work still to come is a reason to
    /// refuse.</summary>
    private async Task EnsureNoUpcomingAppointmentsAsync(int id, CancellationToken cancellationToken)
    {
        var upcoming = await _uow.Appointments.GetForDateRangeAsync(
            _clock.GetCurrentInstant(), Instant.MaxValue, id, cancellationToken);

        if (upcoming.Any(a => a.AppointmentStatus != AppointmentStatus.Cancelled))
        {
            throw new InvalidStateTransitionException(nameof(Provider), id, "linked to upcoming appointments", "removed");
        }
    }

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

    private static ProviderResponse ToResponse(Provider provider)
        => new(provider.Id, provider.Name);
}
