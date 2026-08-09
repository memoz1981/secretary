using Secretary.Application.Abstractions.Persistence;
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

    public BusinessHoursService(IBusinessHoursRepository repository) => _repository = repository;

    public Task<IReadOnlyList<BusinessHours>> GetWeekAsync(CancellationToken cancellationToken)
        => _repository.GetWeekAsync(cancellationToken);

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
    {
        var day = from;
        var remaining = leadWorkingDays;

        for (var attempts = 0; attempts <= 14; attempts++)
        {
            var isWorking = week.TryGetValue(day.DayOfWeek, out var hours) && !hours.IsClosed;
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
