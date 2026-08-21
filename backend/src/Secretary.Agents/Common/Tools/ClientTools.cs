using System.ComponentModel;
using Secretary.Agents.Appointment;
using Secretary.Application.Services;

namespace Secretary.Agents.Tools;

public sealed class ClientTools
{
    private readonly ClientService _clientService;
    private readonly AppointmentCallSession _session;

    public ClientTools(ClientService clientService, AppointmentCallSession session)
    {
        _clientService = clientService;
        _session = session;
    }

    [Description("Looks up the caller by phone number, creating a client record if none exists yet. " +
                 "Returns whether they're known and any upcoming appointments. If the caller later gives their " +
                 "name, call this again with callerName filled in to save it.")]
    public async Task<string> LookupCaller(
        [Description("The caller's phone number, in the form they gave it")] string phoneNumber,
        [Description("The caller's name, if they've given it — leave empty if not yet known")] string? callerName)
    {
        var client = await _clientService.FindOrCreateByPhoneNumberAsync(phoneNumber, string.IsNullOrWhiteSpace(callerName) ? null : callerName, default);

        // Remembered so the call log can name them. Looking them up again afterwards by the
        // number they called from does not work when they called from a browser — see
        // AppointmentCallSession.
        _session.Identified(client.Id);

        // A marker, not a paragraph of instructions: the previous version explained at length
        // what to do about a blocked caller, and the model read that explanation out loud.
        // PhoneAgent.md says how to handle it.
        var blocked = client.BlackListed ? "BLOCKED_CALLER. " : string.Empty;

        var upcoming = await _clientService.GetUpcomingAppointmentsAsync(client.Id, default);
        var who = client.Name is null ? "New caller, no name on file" : $"Known caller: {client.Name}";

        if (upcoming.Count == 0)
        {
            return $"{blocked}{who}. No upcoming appointments.";
        }

        var list = string.Join("; ", upcoming.Select(a => $"appointment id {a.Id} — {AzerbaijanTime.Format(a.Start)} ({a.Status})"));
        return $"{blocked}{who}. Upcoming: {list}";
    }
}
