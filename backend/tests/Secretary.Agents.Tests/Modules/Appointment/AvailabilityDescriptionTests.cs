using Secretary.Agents;
using Secretary.Agents.Tools;
using Secretary.Application.Dtos;
using Secretary.Domain.Entities;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>"When are you free this week?" used to return 78 hourly slot lines, 3am among them,
/// which the model had to read through before it could say anything. These are the two rules
/// that turned that into a sentence a person would say.
///
/// The hours are the tenant's own now rather than a constant, so every case here passes a week
/// open 09:00–21:00 — the window these were originally written against, kept so the tests still
/// say what they always said.</summary>
public sealed class AvailabilityDescriptionTests
{
    private static AvailableSlot Slot(int hour, int durationMinutes = 60)
    {
        AzerbaijanTime.TryParse($"2026-07-26 {hour:00}:00", out var start).ShouldBeTrue();
        return new AvailableSlot(start, start + Duration.FromMinutes(durationMinutes));
    }

    private static string? Describe(IEnumerable<AvailableSlot> slots) => AppointmentTools.DescribeOpenSlots(slots, OpenAllWeek());

    private static IReadOnlyDictionary<IsoDayOfWeek, BusinessHours> OpenAllWeek(
        int opensAtHour = 9, int closesAtHour = 21)
        => Enum.GetValues<IsoDayOfWeek>()
            .Where(d => d != IsoDayOfWeek.None)
            .ToDictionary(
                d => d,
                d => BusinessHours.Open(
                    1, d, new LocalTime(opensAtHour, 0), new LocalTime(closesAtHour, 0), Instant.FromUnixTimeSeconds(0)));

    /// <summary>The rule that did not exist before hours were per-tenant: a day the business is
    /// shut offers nothing, however free the diary looks.</summary>
    [Fact]
    public void A_day_the_business_is_closed_offers_nothing()
    {
        // 2026-07-26 is a Sunday.
        var week = OpenAllWeek().ToDictionary(kv => kv.Key, kv => kv.Value);
        week[IsoDayOfWeek.Sunday] = BusinessHours.Closed(1, IsoDayOfWeek.Sunday, Instant.FromUnixTimeSeconds(0));

        AppointmentTools.DescribeOpenSlots([Slot(9), Slot(10)], week).ShouldBeNull();
    }

    /// <summary>A tenant who has never set their hours is closed, not open around the clock.
    /// The opposite default would have the agent offering 03:00 again.</summary>
    [Fact]
    public void A_week_with_no_hours_configured_offers_nothing()
        => AppointmentTools.DescribeOpenSlots([Slot(9), Slot(10)], new Dictionary<IsoDayOfWeek, BusinessHours>())
            .ShouldBeNull();

    [Fact]
    public void Hours_come_from_the_tenant_rather_than_a_constant()
    {
        var shortDay = OpenAllWeek(opensAtHour: 10, closesAtHour: 12);

        AppointmentTools.DescribeOpenSlots([Slot(9), Slot(10), Slot(11), Slot(12)], shortDay)
            .ShouldBe("2026-07-26 10:00–12:00");
    }

    [Fact]
    public void Back_to_back_slots_collapse_into_one_range()
    {
        var description = Describe([Slot(9), Slot(10), Slot(11)]);

        description.ShouldBe("2026-07-26 09:00–12:00");
    }

    [Fact]
    public void A_gap_between_slots_is_kept_because_it_is_a_booked_appointment()
    {
        var description = Describe([Slot(9), Slot(10), Slot(14), Slot(15)]);

        description.ShouldBe("2026-07-26 09:00–11:00, 2026-07-26 14:00–16:00");
    }

    [Fact]
    public void Slots_before_opening_are_never_offered()
    {
        var description = Describe([Slot(3), Slot(4), Slot(9)]);

        description.ShouldBe("2026-07-26 09:00–10:00");
    }

    [Fact]
    public void A_slot_that_would_run_past_closing_is_never_offered()
    {
        // Closing is 21:00: an hour-long service starting at 20:00 finishes exactly on time and
        // is offered, the same service starting at 20:30 would overrun and is not.
        AzerbaijanTime.TryParse("2026-07-26 20:30", out var halfPastEight).ShouldBeTrue();
        var overrunning = new AvailableSlot(halfPastEight, halfPastEight + Duration.FromMinutes(60));

        var description = Describe([Slot(20), overrunning]);

        description.ShouldBe("2026-07-26 20:00–21:00");
    }

    [Fact]
    public void Nothing_open_says_so_rather_than_returning_an_empty_line()
    {
        Describe([Slot(3)]).ShouldBeNull();
        Describe([]).ShouldBeNull();
    }

    [Fact]
    public void Slots_arriving_out_of_order_still_merge()
    {
        var description = Describe([Slot(11), Slot(9), Slot(10)]);

        description.ShouldBe("2026-07-26 09:00–12:00");
    }
}
