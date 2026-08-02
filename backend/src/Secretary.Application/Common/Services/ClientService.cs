using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Client lookup/creation for the AI agent (Stage 1) plus the tenant-facing
/// Clients page: list/add/edit/remove and blacklisting. A blacklisted client is refused
/// bookings (see AppointmentService) until the tenant lifts the blacklist.</summary>
public sealed class ClientService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;

    public ClientService(IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var clients = await _uow.Clients.GetAllForCurrentTenantAsync(cancellationToken);
        return clients.Select(ToResponse).ToList();
    }

    /// <summary>Stage 1: the agent looks up a caller by phone number, or creates a new
    /// client record if none exists.</summary>
    public async Task<Client> FindOrCreateByPhoneNumberAsync(string phoneNumber, string? name, CancellationToken cancellationToken)
    {
        var existing = await _uow.Clients.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (existing is not null)
        {
            if (name is not null && existing.Name is null)
            {
                existing.UpdateName(name, _clock.GetCurrentInstant());
                await _uow.SaveChangesAsync(cancellationToken);
            }

            return existing;
        }

        var tenantId = RequireTenant();

        var client = Client.Create(tenantId, phoneNumber, _clock.GetCurrentInstant(), name);
        await _uow.Clients.AddAsync(client, cancellationToken);

        // This is also called from inside AppointmentService.CreateAsync's own flow (which has
        // its own SaveChanges), but on its own — the agent's LookupCaller tool — nothing else
        // saves. Without this, a caller's record/name looked saved but silently never persisted.
        await _uow.SaveChangesAsync(cancellationToken);
        return client;
    }

    /// <summary>Pure lookup, no creation — for staff/agent callers who just need to know
    /// whether a caller is already a known client.</summary>
    public async Task<ClientResponse?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var client = await _uow.Clients.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        return client is null ? null : ToResponse(client);
    }

    public async Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        if (await _uow.Clients.GetByPhoneNumberAsync(request.PhoneNumber, cancellationToken) is not null)
        {
            throw new DuplicateClientPhoneNumberException(request.PhoneNumber);
        }

        var client = Client.Create(tenantId, request.PhoneNumber, _clock.GetCurrentInstant(), request.Name);
        await _uow.Clients.AddAsync(client, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(client);
    }

    public async Task<ClientResponse> UpdateAsync(int id, UpdateClientRequest request, CancellationToken cancellationToken)
    {
        var client = await GetOrThrowAsync(id, cancellationToken);

        var byPhone = await _uow.Clients.GetByPhoneNumberAsync(request.PhoneNumber, cancellationToken);
        if (byPhone is not null && byPhone.Id != id)
        {
            throw new DuplicateClientPhoneNumberException(request.PhoneNumber);
        }

        client.UpdateDetails(request.PhoneNumber, request.Name, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(client);
    }

    /// <summary>Soft-deactivates — call history and appointments keep pointing at the row.</summary>
    public async Task RemoveAsync(int id, CancellationToken cancellationToken)
    {
        var client = await GetOrThrowAsync(id, cancellationToken);
        client.Deactivate(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClientResponse> BlackListAsync(int id, BlackListClientRequest request, CancellationToken cancellationToken)
    {
        var client = await GetOrThrowAsync(id, cancellationToken);
        client.BlackList(request.Reason, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(client);
    }

    public async Task<ClientResponse> UndoBlackListAsync(int id, CancellationToken cancellationToken)
    {
        var client = await GetOrThrowAsync(id, cancellationToken);
        client.UndoBlackList(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(client);
    }

    public async Task<IReadOnlyList<AppointmentResponse>> GetUpcomingAppointmentsAsync(
        int clientId, CancellationToken cancellationToken)
    {
        var now = _clock.GetCurrentInstant();
        var appointments = await _uow.Appointments.GetUpcomingForClientAsync(clientId, now, cancellationToken);
        var client = await _uow.Clients.GetByIdAsync(clientId, cancellationToken);

        return appointments.Select(a => new AppointmentResponse(
            a.Id, a.ClientId, client?.PhoneNumber ?? string.Empty, client?.Name,
            a.ProviderId, a.ServiceOfferingId, a.Start, a.End, a.AppointmentStatus, a.ReminderNoAnswerFlag, a.Notes, a.CreatedBy))
            .ToList();
    }

    private async Task<Client> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _uow.Clients.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Client), id);

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

    private static ClientResponse ToResponse(Client client)
        => new(client.Id, client.PhoneNumber, client.Name, client.BlackListed, client.BlackListReason, client.CreatedAt);
}
