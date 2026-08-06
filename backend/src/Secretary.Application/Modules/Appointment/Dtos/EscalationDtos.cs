using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Dtos;

/// <summary>CallerPhoneNumber/ClientName are denormalized from the linked Client record —
/// every escalation is linked to a client (created from the caller's number if unknown).</summary>
public sealed record EscalationResponse(
    int Id,
    int ClientId,
    string CallerPhoneNumber,
    string? ClientName,
    string Reason,
    EscalationStatus Status,
    Instant RaisedAt,
    int? AcceptedByAccountId,
    Instant? AcceptedAt);

public sealed record RaiseEscalationRequest(string CallerPhoneNumber, string Reason);
