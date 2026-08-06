using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>Backs the escalation ringing overlay (Flow D) — Agent raises, any available
/// Owner/Staff account in the tenant can accept (page-inventory.md's global-overlay note),
/// and a timeout job (Api-layer background service) abandons unaccepted ones. The Api layer
/// pushes state changes over the SignalR escalation hub; this service only owns persistence
/// and the state machine itself. Every escalation is linked to a Client — the caller's
/// number resolves to an existing client or creates one on the spot.</summary>
public sealed class EscalationService
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly ClientService _clientService;

    public EscalationService(IUnitOfWork uow, IClock clock, ICurrentTenantProvider currentTenant, ClientService clientService)
    {
        _uow = uow;
        _clock = clock;
        _currentTenant = currentTenant;
        _clientService = clientService;
    }

    public async Task<EscalationResponse> RaiseAsync(RaiseEscalationRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var client = await _clientService.FindOrCreateByPhoneNumberAsync(request.CallerPhoneNumber, null, cancellationToken);

        var escalation = Escalation.Raise(tenantId, client.Id, request.Reason, _clock.GetCurrentInstant());
        await _uow.Escalations.AddAsync(escalation, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToResponse(escalation, client);
    }

    public async Task<EscalationResponse> AcceptAsync(int id, int acceptedBy, CancellationToken cancellationToken)
    {
        var escalation = await GetOrThrowAsync(id, cancellationToken);
        escalation.Accept(acceptedBy, _clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(escalation, cancellationToken);
    }

    public async Task<EscalationResponse> AbandonAsync(int id, CancellationToken cancellationToken)
    {
        var escalation = await GetOrThrowAsync(id, cancellationToken);
        escalation.Abandon(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(escalation, cancellationToken);
    }

    public async Task<EscalationResponse> EndConnectedCallAsync(int id, CancellationToken cancellationToken)
    {
        var escalation = await GetOrThrowAsync(id, cancellationToken);
        escalation.EndConnectedCall(_clock.GetCurrentInstant());
        await _uow.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(escalation, cancellationToken);
    }

    public async Task<IReadOnlyList<EscalationResponse>> GetRingingAsync(CancellationToken cancellationToken)
    {
        var escalations = await _uow.Escalations.GetRingingForCurrentTenantAsync(cancellationToken);
        var responses = new List<EscalationResponse>(escalations.Count);
        foreach (var escalation in escalations)
        {
            responses.Add(await ToResponseAsync(escalation, cancellationToken));
        }

        return responses;
    }

    private async Task<Escalation> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _uow.Escalations.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Escalation), id);

    private async Task<EscalationResponse> ToResponseAsync(Escalation escalation, CancellationToken cancellationToken)
    {
        var client = await _uow.Clients.GetByIdAsync(escalation.ClientId, cancellationToken);
        return ToResponse(escalation, client);
    }

    private static EscalationResponse ToResponse(Escalation escalation, Client? client)
        => new(
            escalation.Id, escalation.ClientId, client?.PhoneNumber ?? string.Empty, client?.Name,
            escalation.Reason, escalation.EscalationStatus, escalation.RaisedAt,
            escalation.AcceptedByAccountId, escalation.AcceptedAt);
}
