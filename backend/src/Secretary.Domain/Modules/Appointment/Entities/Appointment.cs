using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>The base Status is always Active (appointments are never soft-deleted);
/// the appointment lifecycle lives in the separate AppointmentStatus.</summary>
public sealed class Appointment : BaseEntity
{
    public int TenantId { get; private set; }
    public int ClientId { get; private set; }
    public int ProviderId { get; private set; }
    public int ServiceOfferingId { get; private set; }
    public Instant Start { get; private set; }
    public Instant End { get; private set; }
    public AppointmentStatus AppointmentStatus { get; private set; }
    public bool ReminderNoAnswerFlag { get; private set; }
    public string? Notes { get; private set; }
    public AppointmentCreatedBy CreatedBy { get; private set; }

    /// <summary>Optional, caller-supplied key so a retried booking request (a network blip,
    /// a phone agent replaying a request) resolves to the same appointment instead of
    /// creating a duplicate. Null for callers that don't supply one — idempotency is opt-in,
    /// not enforced on every booking.</summary>
    public string? IdempotencyKey { get; private set; }

    private Appointment(
        int tenantId, int clientId, int providerId, int serviceOfferingId,
        Instant start, Instant end, string? notes, AppointmentCreatedBy createdBy, string? idempotencyKey, Instant now)
    {
        TenantId = tenantId;
        ClientId = clientId;
        ProviderId = providerId;
        ServiceOfferingId = serviceOfferingId;
        Start = start;
        End = end;
        AppointmentStatus = AppointmentStatus.Confirmed;
        ReminderNoAnswerFlag = false;
        Notes = notes;
        CreatedBy = createdBy;
        IdempotencyKey = idempotencyKey;
        InitBase(now);
    }

    private Appointment()
    {
    }

    public static Appointment Create(
        int tenantId, int clientId, int providerId, int serviceOfferingId,
        Instant start, Instant end, string? notes, AppointmentCreatedBy createdBy, Instant now, string? idempotencyKey = null)
    {
        ValidateRange(start, end);
        return new Appointment(tenantId, clientId, providerId, serviceOfferingId, start, end, notes, createdBy, idempotencyKey, now);
    }

    public void Reschedule(int providerId, int serviceOfferingId, Instant start, Instant end, Instant now)
    {
        EnsureNotCancelled("rescheduled");
        ValidateRange(start, end);
        ProviderId = providerId;
        ServiceOfferingId = serviceOfferingId;
        Start = start;
        End = end;
        Touch(now);
    }

    public void UpdateNotes(string? notes, Instant now)
    {
        Notes = notes;
        Touch(now);
    }

    public void Cancel(Instant now)
    {
        EnsureNotCancelled("cancelled");
        AppointmentStatus = AppointmentStatus.Cancelled;
        Touch(now);
    }

    public void MarkReminderNoAnswer(Instant now)
    {
        EnsureNotCancelled("flagged");
        ReminderNoAnswerFlag = true;
        Touch(now);
    }

    public void ConfirmReminder(Instant now)
    {
        EnsureNotCancelled("confirmed");
        ReminderNoAnswerFlag = false;
        AppointmentStatus = AppointmentStatus.Confirmed;
        Touch(now);
    }

    private void EnsureNotCancelled(string attemptedAction)
    {
        if (AppointmentStatus == AppointmentStatus.Cancelled)
        {
            throw new InvalidStateTransitionException(nameof(Appointment), Id, "already cancelled", attemptedAction);
        }
    }

    private static void ValidateRange(Instant start, Instant end)
    {
        if (end <= start)
        {
            throw new ArgumentException("Appointment end must be after its start.");
        }
    }
}
