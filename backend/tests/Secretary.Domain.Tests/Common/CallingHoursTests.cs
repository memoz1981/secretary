using Secretary.Domain.Entities;
using Secretary.Domain.ValueObjects;
using NodaTime;
using NodaTime.Text;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>When it is acceptable to ring somebody.
///
/// ⚠ Opening hours existed for months and nothing consulted them about *placing* a call —
/// appointments used them to avoid offering a slot, orders to avoid promising a delivery, both
/// about the business's own day. A survey retry is the first thing that puts a phone in a
/// stranger's hand at a time of our choosing: "in an hour" from a six o'clock failure is seven
/// o'clock, and a day's delay lands at whatever time the first attempt happened to be made.</summary>
public sealed class CallingHoursTests
{
    private static readonly DateTimeZone Baku = DateTimeZoneProviders.Tzdb["Asia/Baku"];

    private static Instant At(string local)
        => LocalDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm")
            .Parse(local).Value.InZoneStrictly(Baku).ToInstant();

    private static string Local(Instant? instant)
        => instant is null
            ? "none"
            : LocalDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm")
                .Format(instant.Value.InZone(Baku).LocalDateTime);

    /// <summary>Mon–Fri 09:00–18:00, Saturday 10:00–14:00, Sunday shut.</summary>
    private static IReadOnlyList<BusinessHours> Week()
    {
        var now = Instant.FromUnixTimeSeconds(0);
        var days = new List<BusinessHours>();
        for (var day = IsoDayOfWeek.Monday; day <= IsoDayOfWeek.Friday; day++)
        {
            days.Add(BusinessHours.Open(1, day, new LocalTime(9, 0), new LocalTime(18, 0), now));
        }

        days.Add(BusinessHours.Open(1, IsoDayOfWeek.Saturday, new LocalTime(10, 0), new LocalTime(14, 0), now));
        days.Add(BusinessHours.Closed(1, IsoDayOfWeek.Sunday, now));
        return days;
    }

    // 2026-08-17 is a Monday.

    [Fact]
    public void A_time_inside_the_working_day_is_left_alone()
        => Local(CallingHours.NextOpening(At("2026-08-17 14:30"), Week(), Baku))
            .ShouldBe("2026-08-17 14:30");

    /// <summary>The one that matters. A retry computed at half past two in the morning waits for
    /// nine, rather than ringing somebody in the middle of the night.</summary>
    [Fact]
    public void The_small_hours_wait_for_opening_time()
        => Local(CallingHours.NextOpening(At("2026-08-17 02:30"), Week(), Baku))
            .ShouldBe("2026-08-17 09:00");

    [Fact]
    public void After_closing_waits_for_the_next_morning()
        => Local(CallingHours.NextOpening(At("2026-08-17 19:15"), Week(), Baku))
            .ShouldBe("2026-08-18 09:00");

    /// <summary>Friday evening skips the weekend's shape correctly: Saturday opens at ten, not
    /// at nine, and a Saturday evening skips Sunday altogether.</summary>
    [Fact]
    public void A_short_saturday_opens_when_it_opens()
        => Local(CallingHours.NextOpening(At("2026-08-21 19:00"), Week(), Baku))
            .ShouldBe("2026-08-22 10:00");

    [Fact]
    public void A_closed_day_is_skipped_entirely()
        => Local(CallingHours.NextOpening(At("2026-08-23 11:00"), Week(), Baku))
            .ShouldBe("2026-08-24 09:00");

    /// <summary>⚠ No hours set is missing information, not permission. Returning the desired time
    /// would make an unconfigured tenant the one whose customers get rung at four in the morning
    /// — the exact case where the system knows least.</summary>
    [Fact]
    public void A_tenant_with_no_hours_at_all_schedules_nothing()
        => CallingHours.NextOpening(At("2026-08-17 14:30"), [], Baku).ShouldBeNull();

    [Fact]
    public void A_business_shut_every_day_schedules_nothing()
    {
        var shut = Enum.GetValues<IsoDayOfWeek>()
            .Where(d => d != IsoDayOfWeek.None)
            .Select(d => BusinessHours.Closed(1, d, Instant.FromUnixTimeSeconds(0)))
            .ToList();

        CallingHours.NextOpening(At("2026-08-17 14:30"), shut, Baku).ShouldBeNull();
    }
}
