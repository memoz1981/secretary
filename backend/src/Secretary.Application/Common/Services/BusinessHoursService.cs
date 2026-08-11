using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Application.Services;

/// <summary>When the tenant is open, and the two questions both modules ask of that.
///
/// Appointments ask "may this slot be offered"; Orders ask "which day can we promise". Neither
/// should reason about opening hours itself — the agent doing that is how a caller was offered
/// 19:30 on a day the tool had said nothing about.
///
/// Shared rather than per-module: one business, one set of hours.</summary>
public sealed class BusinessHoursService
{
    private readonly IBusinessHoursRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentTenantProvider _currentTenant;
    private readonly IAgentDirectoryChangeNotifier _changeNotifier;

    public BusinessHoursService(
        IBusinessHoursRepository repository, IUnitOfWork unitOfWork, IClock clock,
        ICurrentTenantProvider currentTenant, IAgentDirectoryChangeNotifier changeNotifier)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentTenant = currentTenant;
        _changeNotifier = changeNotifier;
    }

    public Task<IReadOnlyList<BusinessHours>> GetWeekAsync(CancellationToken cancellationToken)
        => _repository.GetWeekAsync(cancellationToken);

    /// <summary>The Administration page's view: always seven days, in week order, with the days
    /// the tenant has never configured shown as closed rather than missing. A form with gaps in
    /// it is a form nobody can fill in correctly.</summary>
    public async Task<IReadOnlyList<BusinessHoursDay>> GetWeekForEditingAsync(CancellationToken cancellationToken)
    {
        var existing = (await _repository.GetWeekAsync(cancellationToken)).ToDictionary(h => h.DayOfWeek);

        return WeekDays
            .Select(day => existing.TryGetValue(day, out var hours)
                ? new BusinessHoursDay(day, hours.OpensAt, hours.ClosesAt)
                : new BusinessHoursDay(day, null, null))
            .ToList();
    }

    /// <summary>Saves the whole week at once. Partial updates would let a tenant close Tuesday
    /// and never find out that Wednesday was never set in the first place.</summary>
    public async Task<IReadOnlyList<BusinessHoursDay>> UpdateWeekAsync(
        UpdateBusinessHoursRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("This operation requires a tenant-scoped caller.");

        var now = _clock.GetCurrentInstant();
        var existing = (await _repository.GetWeekAsync(cancellationToken)).ToDictionary(h => h.DayOfWeek);

        foreach (var day in request.Days)
        {
            var isOpen = day.OpensAt is not null && day.ClosesAt is not null;
            if (existing.TryGetValue(day.DayOfWeek, out var row))
            {
                if (isOpen)
                {
                    row.SetOpen(day.OpensAt!.Value, day.ClosesAt!.Value, now);
                }
                else
                {
                    row.SetClosed(now);
                }

                continue;
            }

            await _repository.AddAsync(
                isOpen
                    ? BusinessHours.Open(tenantId, day.DayOfWeek, day.OpensAt!.Value, day.ClosesAt!.Value, now)
                    : BusinessHours.Closed(tenantId, day.DayOfWeek, now),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The order line holds these days between calls to keep a database round trip out of the
        // middle of a sentence. A tenant who closes Sunday and is still offered it by the agent
        // has a delivery nobody is there for.
        _changeNotifier.NotifyChanged(tenantId);

        return await GetWeekForEditingAsync(cancellationToken);
    }

    /// <summary>Monday first, the way a week is read here.</summary>
    private static readonly IsoDayOfWeek[] WeekDays =
    [
        IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday,
        IsoDayOfWeek.Friday, IsoDayOfWeek.Saturday, IsoDayOfWeek.Sunday,
    ];

    /// <summary>A lookup of the week, for callers that ask about many days at once and should not
    /// hit the database per day.</summary>
    public async Task<IReadOnlyDictionary<IsoDayOfWeek, BusinessHours>> GetWeekByDayAsync(
        CancellationToken cancellationToken)
    {
        var week = await _repository.GetWeekAsync(cancellationToken);
        return week.ToDictionary(h => h.DayOfWeek);
    }

    /// <summary>Whether something running between two local times on one day fits inside opening
    /// hours. A day with no row is closed: a tenant who has never set their hours should be
    /// offering nothing, not everything.</summary>
    public static bool IsOpen(
        IReadOnlyDictionary<IsoDayOfWeek, BusinessHours> week, LocalDateTime start, LocalDateTime end)
    {
        // A slot crossing midnight cannot be inside one day's hours, and treating it as if the
        // second half belonged to the first day would offer time on a closed day.
        if (start.Date != end.Date)
        {
            return false;
        }

        return week.TryGetValue(start.Date.DayOfWeek, out var hours)
            && hours.Contains(start.TimeOfDay, end.TimeOfDay);
    }

    /// <summary>The delivery day a promise of N working days lands on, counting only days the
    /// business is open. Zero means today, if today is a working day.
    ///
    /// Gives up after a fortnight of closed days rather than looping: a tenant whose whole week
    /// is closed has a configuration problem, and an agent must not hang on a live call while we
    /// discover that.</summary>
    public static LocalDate? NextWorkingDay(
        IReadOnlyDictionary<IsoDayOfWeek, BusinessHours> week, LocalDate from, int leadWorkingDays)
        => NextWorkingDay(
            week.Where(kv => !kv.Value.IsClosed).Select(kv => kv.Key).ToHashSet(), from, leadWorkingDays);

    /// <summary>The same walk over the days the business opens, taking only the weekdays rather
    /// than the rows. The order line caches its delivery policy between calls, and caching plain
    /// weekdays keeps entities loaded by one request's DbContext from outliving it.</summary>
    public static LocalDate? NextWorkingDay(
        IReadOnlySet<IsoDayOfWeek> openDays, LocalDate from, int leadWorkingDays)
    {
        var day = from;
        var remaining = leadWorkingDays;

        for (var attempts = 0; attempts <= 14; attempts++)
        {
            var isWorking = openDays.Contains(day.DayOfWeek);
            if (isWorking)
            {
                if (remaining == 0)
                {
                    return day;
                }

                remaining--;
            }

            day = day.PlusDays(1);
        }

        return null;
    }
}
