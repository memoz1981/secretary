namespace Secretary.Domain.Enums;

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    Cancelled,
}

/// <summary>Who created the appointment — drives no behavior difference today, but the
/// call log and dashboard both need to distinguish agent-originated bookings from manual ones.</summary>
public enum AppointmentCreatedBy
{
    Agent,
    Staff,
}
