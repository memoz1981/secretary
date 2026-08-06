using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>Live state of a mid-call escalation to a human (Flow D) — the "ringing" alert
/// any available Owner/Staff account can accept. Once it resolves (Connected → ended, or
/// abandoned on timeout), CallService writes the outcome onto the finalized Call record;
/// this entity is the transient/real-time half of that story, not the permanent log.
/// Every escalation is linked to a Client (created from the caller's number if unknown),
/// so there is no separate phone-number column here.</summary>
public sealed class Escalation : BaseEntity
{
    public int TenantId { get; private set; }
    public int ClientId { get; private set; }
    public string Reason { get; private set; }
    public EscalationStatus EscalationStatus { get; private set; }
    public Instant RaisedAt { get; private set; }
    public int? AcceptedByAccountId { get; private set; }
    public Instant? AcceptedAt { get; private set; }
    public Instant? EndedAt { get; private set; }

    private Escalation(int tenantId, int clientId, string reason, Instant now)
    {
        TenantId = tenantId;
        ClientId = clientId;
        Reason = reason;
        EscalationStatus = EscalationStatus.Ringing;
        RaisedAt = now;
        InitBase(now);
    }

    private Escalation()
    {
        Reason = string.Empty;
    }

    public static Escalation Raise(int tenantId, int clientId, string reason, Instant now)
        => new(tenantId, clientId, reason, now);

    public void Accept(int acceptedBy, Instant now)
    {
        if (EscalationStatus != EscalationStatus.Ringing)
        {
            throw new InvalidStateTransitionException(nameof(Escalation), Id, EscalationStatus.ToString().ToLowerInvariant(), "accepted");
        }

        EscalationStatus = EscalationStatus.Connected;
        AcceptedByAccountId = acceptedBy;
        AcceptedAt = now;
        Touch(now);
    }

    /// <summary>Staff ends the now-direct call between them and the client.</summary>
    public void EndConnectedCall(Instant now)
    {
        if (EscalationStatus != EscalationStatus.Connected)
        {
            throw new InvalidStateTransitionException(nameof(Escalation), Id, EscalationStatus.ToString().ToLowerInvariant(), "ended");
        }

        EndedAt = now;
        Touch(now);
    }

    /// <summary>No staff accepted within the ringing timeout.</summary>
    public void Abandon(Instant now)
    {
        if (EscalationStatus != EscalationStatus.Ringing)
        {
            throw new InvalidStateTransitionException(nameof(Escalation), Id, EscalationStatus.ToString().ToLowerInvariant(), "abandoned");
        }

        EscalationStatus = EscalationStatus.Abandoned;
        EndedAt = now;
        Touch(now);
    }

    public Duration WaitTime() => (AcceptedAt ?? EndedAt ?? RaisedAt) - RaisedAt;
}
