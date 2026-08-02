using Secretary.Agents;
using Secretary.Agents.Tools;
using Secretary.Application.Dtos;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Agents.Tests;

/// <summary>"When are you free this week?" used to return 78 hourly slot lines, 3am among them,
/// which the model had to read through before it could say anything. These are the two rules
/// that turned that into a sentence a person would say.</summary>
public sealed class AvailabilityDescriptionTests
{
    private static AvailableSlot Slot(int hour, int durationMinutes = 60)
    {
        AzerbaijanTime.TryParse($"2026-07-26 {hour:00}:00", out var start).ShouldBeTrue();
        return new AvailableSlot(start, start + Duration.FromMinutes(durationMinutes));
    }

    [Fact]
    public void Back_to_back_slots_collapse_into_one_range()
    {
        var description = AppointmentTools.DescribeOpenSlots([Slot(9), Slot(10), Slot(11)]);

        description.ShouldBe("2026-07-26 09:00–12:00");
    }

    [Fact]
    public void A_gap_between_slots_is_kept_because_it_is_a_booked_appointment()
    {
        var description = AppointmentTools.DescribeOpenSlots([Slot(9), Slot(10), Slot(14), Slot(15)]);

        description.ShouldBe("2026-07-26 09:00–11:00, 2026-07-26 14:00–16:00");
    }

    [Fact]
    public void Slots_before_opening_are_never_offered()
    {
        var description = AppointmentTools.DescribeOpenSlots([Slot(3), Slot(4), Slot(9)]);

        description.ShouldBe("2026-07-26 09:00–10:00");
    }

    [Fact]
    public void A_slot_that_would_run_past_closing_is_never_offered()
    {
        // Closing is 21:00: an hour-long service starting at 20:00 finishes exactly on time and
        // is offered, the same service starting at 20:30 would overrun and is not.
        AzerbaijanTime.TryParse("2026-07-26 20:30", out var halfPastEight).ShouldBeTrue();
        var overrunning = new AvailableSlot(halfPastEight, halfPastEight + Duration.FromMinutes(60));

        var description = AppointmentTools.DescribeOpenSlots([Slot(20), overrunning]);

        description.ShouldBe("2026-07-26 20:00–21:00");
    }

    [Fact]
    public void Nothing_open_says_so_rather_than_returning_an_empty_line()
    {
        AppointmentTools.DescribeOpenSlots([Slot(3)]).ShouldBeNull();
        AppointmentTools.DescribeOpenSlots([]).ShouldBeNull();
    }

    [Fact]
    public void Slots_arriving_out_of_order_still_merge()
    {
        var description = AppointmentTools.DescribeOpenSlots([Slot(11), Slot(9), Slot(10)]);

        description.ShouldBe("2026-07-26 09:00–12:00");
    }
}
