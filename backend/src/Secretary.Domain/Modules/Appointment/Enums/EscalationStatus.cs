namespace Secretary.Domain.Enums;

/// <summary>Live state of an in-progress escalation (Flow D). Once it resolves one way or
/// the other, that outcome is what gets written onto the finalized Call record.</summary>
public enum EscalationStatus
{
    Ringing,
    Connected,
    Abandoned,
}
