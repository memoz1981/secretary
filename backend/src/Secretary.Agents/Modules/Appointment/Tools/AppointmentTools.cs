using System.ComponentModel;
using System.Text.RegularExpressions;
using Secretary.Agents.ServiceCatalog;
using Secretary.Application.Dtos;
using Secretary.Application.Services;
using Secretary.Domain.Entities;
using Secretary.Domain.Enums;
using Secretary.Domain.Exceptions;
using NodaTime;

namespace Secretary.Agents.Tools;

/// <summary>Tool results here are DATA, never instructions. Anything phrased as "tell the
/// caller…" ends up spoken aloud by the model — that is where "indi mən nəzakətlə…" style
/// narration came from. How to behave belongs in Instructions/PhoneAgent.md.</summary>
public sealed class AppointmentTools
{
    /// <summary>A caller can't absorb more than a couple of days of options in speech, and every
    /// extra line is latency before the model starts talking.</summary>
    private static readonly Duration MaxAvailabilityWindow = Duration.FromDays(7);

    private readonly AppointmentService _appointmentService;
    private readonly ClientService _clientService;
    private readonly ITenantProviderDirectory _providers;
    private readonly ITenantServiceCatalogCache _catalog;
    private readonly BusinessHoursService _businessHours;
    private readonly IClock _clock;

    public AppointmentTools(
        AppointmentService appointmentService, ClientService clientService, ITenantProviderDirectory providers,
        ITenantServiceCatalogCache catalog, BusinessHoursService businessHours, IClock clock)
    {
        _appointmentService = appointmentService;
        _clientService = clientService;
        _providers = providers;
        _catalog = catalog;
        _businessHours = businessHours;
        _clock = clock;
    }

    [Description("Names the providers who perform a given service. Call this once the service is known and " +
                 "before proposing any provider, slot or booking.")]
    public async Task<string> ListProvidersForService(
        [Description("Exact service name, matching GetServiceCatalog")] string serviceName)
    {
        var catalog = await _catalog.GetCatalogAsync(default);
        var service = NameMatching.MatchByName(catalog, s => s.Name, serviceName);
        if (service is null)
        {
            return $"No such service. Services: {string.Join(", ", catalog.Select(s => s.Name))}.";
        }

        var providers = await _providers.GetProvidersForServiceAsync(service.Id, default);
        return providers.Count == 0
            ? $"No provider currently performs {service.Name}."
            : $"{service.Name}: {string.Join(", ", providers.Select(p => p.Name))}.";
    }

    [Description("Open appointment slots for a named service, optionally with one named provider, within a " +
                 "time window. All times are Azerbaijan local time. Only providers who perform the service are " +
                 "checked, and only opening hours are returned.")]
    public async Task<string> CheckAvailability(
        [Description("Exact service name, matching GetServiceCatalog")] string serviceName,
        [Description("Provider name, or leave empty to check every provider")] string? providerName,
        [Description("Start of the window, Azerbaijan local time, e.g. 2026-08-01 09:00")] string fromLocal,
        [Description("End of the window, Azerbaijan local time, e.g. 2026-08-01 18:00")] string toLocal)
    {
        var catalog = await _catalog.GetCatalogAsync(default);
        var service = NameMatching.MatchByName(catalog, s => s.Name, serviceName);
        if (service is null)
        {
            return $"No such service. Services: {string.Join(", ", catalog.Select(s => s.Name))}.";
        }

        var providers = await _providers.GetProvidersForServiceAsync(service.Id, default);
        if (providers.Count == 0)
        {
            return $"No provider currently performs {service.Name}.";
        }

        var candidates = providers.ToList();
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            var match = NameMatching.MatchByName(providers, p => p.Name, providerName);
            if (match is null)
            {
                return $"{providerName} does not perform {service.Name}. " +
                       $"{service.Name}: {string.Join(", ", providers.Select(p => p.Name))}.";
            }

            candidates = [match];
        }

        if (!AzerbaijanTime.TryParse(fromLocal, out var from) || !AzerbaijanTime.TryParse(toLocal, out var to))
        {
            return "Unreadable time range. Use Azerbaijan local time like 2026-08-01 09:00.";
        }

        if (to > from + MaxAvailabilityWindow)
        {
            to = from + MaxAvailabilityWindow;
        }

        // Fetched once for the whole answer rather than per provider or per day: the week does
        // not change between two providers checked a millisecond apart, and a live call pays for
        // every round trip.
        var week = await _businessHours.GetWeekByDayAsync(default);

        var lines = new List<string>();
        foreach (var provider in candidates)
        {
            var availability = await _appointmentService.FindAvailabilityAsync(
                new AvailabilityRequest(provider.Id, service.Id, from, to), default);

            if (DescribeOpenSlots(availability.Slots, week) is { } openRanges)
            {
                lines.Add($"{provider.Name}: {openRanges}");
            }
        }

        return lines.Count == 0
            ? "No open slots in that range."
            : string.Join("; ", lines) + ". Any start on the hour or half hour inside these ranges is bookable.";
    }

    [Description("Books a new appointment. Only after the caller has accepted a specific provider and time.")]
    public async Task<string> BookAppointment(
        [Description("The caller's phone number")] string callerPhoneNumber,
        [Description("The caller's name, if known — leave empty otherwise")] string? callerName,
        [Description("Exact service name, matching GetServiceCatalog")] string serviceName,
        [Description("Exact provider name, or leave empty if the caller has no preference")] string? providerName,
        [Description("Appointment start, Azerbaijan local time, e.g. 2026-08-01 14:00")] string startLocal,
        [Description("Any notes the caller mentioned — leave empty if none")] string? notes)
    {
        var catalog = await _catalog.GetCatalogAsync(default);
        var service = NameMatching.MatchByName(catalog, s => s.Name, serviceName);
        if (service is null)
        {
            return $"No such service. Services: {string.Join(", ", catalog.Select(s => s.Name))}.";
        }

        var providers = await _providers.GetProvidersForServiceAsync(service.Id, default);
        if (providers.Count == 0)
        {
            return $"No provider currently performs {service.Name}.";
        }

        // No preference is a real answer, and the common one — most callers do not mind who cuts
        // their hair. With nowhere to put it the model invented a provider called "Any", which
        // then failed as "Any does not perform Saç kəsimi" AFTER the caller had been offered a
        // time. CheckAvailability has always accepted an empty name; booking now matches it, and
        // the literal words a model reaches for are treated as the same answer.
        var provider = IsNoPreference(providerName)
            ? providers[0]
            : NameMatching.MatchByName(providers, p => p.Name, providerName!);

        if (provider is null)
        {
            return $"{providerName} does not perform {service.Name}. " +
                   $"{service.Name}: {string.Join(", ", providers.Select(p => p.Name))}.";
        }

        if (!AzerbaijanTime.TryParse(startLocal, out var start))
        {
            return "Unreadable start time. Use Azerbaijan local time like 2026-08-01 14:00.";
        }

        if (start < _clock.GetCurrentInstant())
        {
            return "That start time is in the past.";
        }

        var end = start + Duration.FromMinutes(service.DurationMinutes);

        try
        {
            var appointment = await _appointmentService.CreateAsync(
                new CreateAppointmentRequest(callerPhoneNumber, string.IsNullOrWhiteSpace(callerName) ? null : callerName,
                    provider.Id, service.Id, start, end, string.IsNullOrWhiteSpace(notes) ? null : notes),
                AppointmentCreatedBy.Agent, default);

            return $"Booked. {service.Name}, {provider.Name}, {AzerbaijanTime.Format(appointment.Start)}, {service.Price} AZN.";
        }
        catch (SchedulingConflictException)
        {
            return "That time is already taken.";
        }
        catch (ProviderDoesNotOfferServiceException)
        {
            return $"{provider.Name} no longer performs {service.Name}.";
        }
        catch (ClientBlackListedException)
        {
            return "BLOCKED_CALLER: this caller cannot be booked.";
        }
    }

    /// <summary>"No preference" as the model is likely to express it. Empty is what the tool
    /// description asks for, but a model told the caller does not mind reaches for a word — and
    /// "Any" is the one it actually sent on a live call. Matching those words costs nothing and
    /// turns a dead end into a booking; a real provider called "Any" would be matched by name
    /// before this is consulted anyway.</summary>
    private static bool IsNoPreference(string? providerName)
        => string.IsNullOrWhiteSpace(providerName)
        || providerName.Trim() is "any" or "Any" or "ANY" or "any provider" or "no preference"
        || providerName.Trim().Equals("hər hansı", StringComparison.OrdinalIgnoreCase)
        || providerName.Trim().Equals("farq etmez", StringComparison.OrdinalIgnoreCase)
        || providerName.Trim().Equals("fərq etməz", StringComparison.OrdinalIgnoreCase);

    [Description("The caller's upcoming appointments, with the ids needed to reschedule or cancel one.")]
    public async Task<string> GetUpcomingAppointments([Description("The caller's phone number")] string callerPhoneNumber)
    {
        var client = await _clientService.GetByPhoneNumberAsync(callerPhoneNumber, default);
        if (client is null)
        {
            return "No client record under that number.";
        }

        var upcoming = await _clientService.GetUpcomingAppointmentsAsync(client.Id, default);
        return upcoming.Count == 0
            ? "No upcoming appointments under that number."
            : string.Join("; ", upcoming.Select(a => $"appointment id {a.Id} — {AzerbaijanTime.Format(a.Start)} ({a.Status})"));
    }

    [Description("Reschedules an existing appointment (id from GetUpcomingAppointments) to a new time, and " +
                 "optionally a new service or provider.")]
    public async Task<string> RescheduleAppointment(
        [Description("The numeric appointment id from GetUpcomingAppointments, e.g. 2")] string appointmentId,
        [Description("Exact provider name to keep or change to")] string providerName,
        [Description("Exact service name to keep or change to")] string serviceName,
        [Description("New start time, Azerbaijan local time, e.g. 2026-08-01 14:00")] string startLocal)
    {
        if (!TryParseAppointmentId(appointmentId, out var id))
        {
            return "Not a valid appointment id.";
        }

        var catalog = await _catalog.GetCatalogAsync(default);
        var service = NameMatching.MatchByName(catalog, s => s.Name, serviceName);
        if (service is null)
        {
            return $"No such service. Services: {string.Join(", ", catalog.Select(s => s.Name))}.";
        }

        var providers = await _providers.GetProvidersForServiceAsync(service.Id, default);
        var provider = NameMatching.MatchByName(providers, p => p.Name, providerName);
        if (provider is null)
        {
            return $"{providerName} does not perform {service.Name}. " +
                   $"{service.Name}: {string.Join(", ", providers.Select(p => p.Name))}.";
        }

        if (!AzerbaijanTime.TryParse(startLocal, out var start))
        {
            return "Unreadable start time. Use Azerbaijan local time like 2026-08-01 14:00.";
        }

        if (start < _clock.GetCurrentInstant())
        {
            return "That start time is in the past.";
        }

        var end = start + Duration.FromMinutes(service.DurationMinutes);

        try
        {
            var appointment = await _appointmentService.RescheduleAsync(
                id, new RescheduleAppointmentRequest(provider.Id, service.Id, start, end), default);
            return $"Rescheduled. {AzerbaijanTime.Format(appointment.Start)}, {provider.Name}.";
        }
        catch (SchedulingConflictException)
        {
            return "That time is already taken.";
        }
        catch (ProviderDoesNotOfferServiceException)
        {
            return $"{provider.Name} no longer performs {service.Name}.";
        }
        catch (NotFoundException)
        {
            return "No such appointment.";
        }
    }

    [Description("Cancels an existing appointment, identified by the id from GetUpcomingAppointments.")]
    public async Task<string> CancelAppointment([Description("The numeric appointment id from GetUpcomingAppointments, e.g. 2")] string appointmentId)
    {
        if (!TryParseAppointmentId(appointmentId, out var id))
        {
            return "Not a valid appointment id.";
        }

        try
        {
            await _appointmentService.CancelAsync(id, default);
            return "Cancelled.";
        }
        catch (NotFoundException)
        {
            return "No such appointment.";
        }
    }

    /// <summary>One provider's free time, as the caller should hear it: inside the tenant's own
    /// opening hours, contiguous slots collapsed into ranges. Null when there is nothing to
    /// offer. This is the whole shape of a "when are you free?" answer, so it is tested
    /// directly — which is why the week is a parameter rather than fetched in here.</summary>
    internal static string? DescribeOpenSlots(
        IEnumerable<AvailableSlot> slots, IReadOnlyDictionary<IsoDayOfWeek, BusinessHours> week)
    {
        var ranges = MergeToRanges(slots.Where(s => IsWithinOpeningHours(s, week)));
        return ranges.Count == 0 ? null : string.Join(", ", ranges.Select(FormatRange));
    }

    /// <summary>The tenant's hours for that weekday, and the slot must finish before closing —
    /// a haircut cannot start five minutes before the shutters come down. A day the tenant has
    /// not configured is closed, not open all hours.</summary>
    private static bool IsWithinOpeningHours(
        AvailableSlot slot, IReadOnlyDictionary<IsoDayOfWeek, BusinessHours> week)
        => BusinessHoursService.IsOpen(week, AzerbaijanTime.ToLocal(slot.Start), AzerbaijanTime.ToLocal(slot.End));

    /// <summary>Back-to-back slots collapse into one range: 26 hourly entries per provider
    /// become "2026-07-26 09:00–21:00". The model reads far less before it can speak, and the
    /// caller hears an opening rather than a list.</summary>
    private static List<(Instant Start, Instant End)> MergeToRanges(IEnumerable<AvailableSlot> slots)
    {
        var ranges = new List<(Instant Start, Instant End)>();
        foreach (var slot in slots.OrderBy(s => s.Start))
        {
            if (ranges.Count > 0 && ranges[^1].End == slot.Start)
            {
                ranges[^1] = (ranges[^1].Start, slot.End);
            }
            else
            {
                ranges.Add((slot.Start, slot.End));
            }
        }

        return ranges;
    }

    private static string FormatRange((Instant Start, Instant End) range)
        => $"{AzerbaijanTime.Format(range.Start)}–{AzerbaijanTime.FormatTimeOnly(range.End)}";

    /// <summary>The model copies ids back with words around them — it sent "appointment 2"
    /// verbatim from GetUpcomingAppointments' own output, and strict int.TryParse refused a
    /// cancellation the caller had every right to. Accept anything containing exactly one
    /// number.</summary>
    private static bool TryParseAppointmentId(string input, out int id)
    {
        id = 0;
        var matches = Regex.Matches(input ?? string.Empty, @"\d+");
        return matches.Count == 1 && int.TryParse(matches[0].Value, out id);
    }
}
