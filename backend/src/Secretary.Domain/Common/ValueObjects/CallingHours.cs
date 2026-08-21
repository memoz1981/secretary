using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Domain.ValueObjects;

/// <summary>When it is acceptable to ring somebody, given a tenant's opening hours.
///
/// ⚠ Nothing outbound existed when BusinessHours was written, so nothing consulted it about
/// *placing* a call — appointments used it to avoid offering a slot, orders to avoid promising a
/// delivery. Both are about the business's own day. This is the first thing that would put a
/// phone in a stranger's hand at a time of our choosing, and the consequence of getting it wrong
/// is not a bad number on a dashboard.
///
/// A pure function over the week so it can be tested without a database, and so the rule lives in
/// one place rather than inside whichever service happens to schedule something next.</summary>
public static class CallingHours
{
    /// <summary>How far to look for an open slot before giving up. Two weeks: a business closed
    /// for longer than that is on holiday, and the honest answer is then "not automatically".</summary>
    private const int MaxDaysAhead = 14;

    /// <summary>The desired instant, or the next moment the business is open after it.
    ///
    /// Returns null when no day in the fortnight ahead is open — a tenant with no hours set at
    /// all, or shut indefinitely. Null means "do not schedule", never "ring immediately": an
    /// empty opening-hours table is missing information, and guessing from missing information
    /// is how somebody's phone rings at four in the morning.</summary>
    public static Instant? NextOpening(
        Instant desired, IReadOnlyList<BusinessHours> week, DateTimeZone zone)
    {
        var byDay = week.Where(d => !d.IsClosed).ToDictionary(d => d.DayOfWeek);
        if (byDay.Count == 0)
        {
            return null;
        }

        var local = desired.InZone(zone).LocalDateTime;

        for (var offset = 0; offset <= MaxDaysAhead; offset++)
        {
            var date = local.Date.PlusDays(offset);
            if (!byDay.TryGetValue(date.DayOfWeek, out var hours))
            {
                continue;
            }

            // On the first day the desired time may already be inside the window, or before it;
            // on any later day the whole day is ahead of us and opening time is the answer.
            var earliest = offset == 0 ? local.TimeOfDay : LocalTime.Midnight;
            if (earliest > hours.ClosesAt!.Value)
            {
                continue;
            }

            var at = earliest < hours.OpensAt!.Value ? hours.OpensAt.Value : earliest;
            return (date + at).InZoneLeniently(zone).ToInstant();
        }

        return null;
    }
}
