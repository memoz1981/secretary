namespace Secretary.Agents.Appointment;

/// <summary>Who the appointment line worked out it was talking to, for the length of one call.
///
/// ⚠ Exists because the call log linked a client by phone number and a browser call has no phone
/// number. Every local call is logged against the literal string "local-device-call", which
/// matches no client, so ClientId was null on every appointment call ever recorded — including the
/// ones that booked an appointment for somebody the agent had just identified by name. The log
/// then had nothing to show but the placeholder.
///
/// The order line has had this since it was built: identify the customer, remember the id, put it
/// on the row. Same shape here.
///
/// Scoped to the call, like every other per-call state here.</summary>
public sealed class AppointmentCallSession
{
    public int? ClientId { get; private set; }

    public void Identified(int clientId) => ClientId = clientId;
}
