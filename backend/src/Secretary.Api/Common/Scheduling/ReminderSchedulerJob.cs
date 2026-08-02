using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using NodaTime;

namespace Secretary.Api.Scheduling;

/// <summary>Flow E: "At 9:00 AM (business's local timezone), the system triggers the
/// reminder job for each tenant." Invoked by Hangfire on a daily recurring schedule (see
/// Program.cs — registered with Asia/Baku as the job's timezone) rather than a hand-rolled
/// BackgroundService, since a calendar-scheduled daily job is exactly what a job scheduler
/// is for. Computes "today" once, in Asia/Baku, for every tenant — every tenant currently
/// runs on this one fixed timezone (Stage 1, confirmed); revisit if multi-timezone tenants
/// are ever supported.</summary>
public sealed class ReminderSchedulerJob
{
    private readonly IUnitOfWork _uow;
    private readonly IOutboundReminderNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger<ReminderSchedulerJob> _logger;

    public ReminderSchedulerJob(IUnitOfWork uow, IOutboundReminderNotifier notifier, IClock clock, ILogger<ReminderSchedulerJob> logger)
    {
        _uow = uow;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var bakuZone = DateTimeZoneProviders.Tzdb["Asia/Baku"];
        var todayInBaku = _clock.GetCurrentInstant().InZone(bakuZone).Date;
        var startOfDay = todayInBaku.AtStartOfDayInZone(bakuZone).ToInstant();
        var endOfDay = todayInBaku.PlusDays(1).AtStartOfDayInZone(bakuZone).ToInstant();

        var appointments = await _uow.Appointments.GetNeedingReminderAcrossAllTenantsAsync(startOfDay, endOfDay, cancellationToken);
        _logger.LogInformation("Reminder scheduler: {Count} appointment(s) need a reminder today ({Date}).", appointments.Count, todayInBaku);

        foreach (var appointment in appointments)
        {
            await _notifier.NotifyReminderDueAsync(appointment, cancellationToken);
        }
    }
}
