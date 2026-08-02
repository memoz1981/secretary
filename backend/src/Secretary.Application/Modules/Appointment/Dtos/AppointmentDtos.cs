using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Dtos;

public sealed record AppointmentResponse(
    int Id,
    int ClientId,
    string ClientPhoneNumber,
    string? ClientName,
    int ProviderId,
    int ServiceOfferingId,
    Instant Start,
    Instant End,
    AppointmentStatus Status,
    bool ReminderNoAnswerFlag,
    string? Notes,
    AppointmentCreatedBy CreatedBy);

/// <summary>ClientPhoneNumber/ClientName identify or create the Client (Stage 1 — the agent
/// looks up an existing client record by phone number, or creates a new one). Start/End are
/// Instant, not OffsetDateTime — every tenant currently runs on one fixed offset (Asia/Baku,
/// Stage 1), and NodaTime's OffsetDateTime deliberately has no ordering operators (comparing
/// across differing offsets is ambiguous by design), which range/overlap queries need.
/// IdempotencyKey is optional — a caller that might retry the same logical booking request
/// (a network blip, a phone integration replaying a request) can supply a stable key and get
/// back the same appointment on a retry instead of a duplicate. Callers that don't need this
/// (the web app's manual booking) just leave it null.</summary>
public sealed record CreateAppointmentRequest(
    string ClientPhoneNumber,
    string? ClientName,
    int ProviderId,
    int ServiceOfferingId,
    Instant Start,
    Instant End,
    string? Notes,
    string? IdempotencyKey = null);

public sealed record RescheduleAppointmentRequest(
    int ProviderId, int ServiceOfferingId, Instant Start, Instant End);

public sealed record AvailabilityRequest(
    int ProviderId, int ServiceOfferingId, Instant From, Instant To);

public sealed record AvailableSlot(Instant Start, Instant End);

public sealed record AvailabilityResponse(IReadOnlyList<AvailableSlot> Slots);
