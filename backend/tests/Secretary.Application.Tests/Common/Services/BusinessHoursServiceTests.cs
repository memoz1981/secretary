using Secretary.Application.Services;
using Secretary.Domain.Entities;
using NodaTime;
using Shouldly;
using Xunit;

namespace Secretary.Application.Tests;

/// <summary>Two questions, one week: may this appointment slot be offered, and which day can a
/// delivery be promised for. Both used to be answered by a constant that knew nothing about
/// closed days.</summary>
public sealed class BusinessHoursServiceTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(0);

    /// <summary>Monday to Friday 09:00–18:00, Saturday short, Sunday shut.</summary>
    private static Dictionary<IsoDayOfWeek, BusinessHours> TypicalWeek()
    {
        var week = new Dictionary<IsoDayOfWeek, BusinessHours>();
        foreach (var day in new[]
                 {
                     IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday,
                     IsoDayOfWeek.Thursday, IsoDayOfWeek.Friday,
                 })
        {
            week[day] = BusinessHours.Open(1, day, new LocalTime(9, 0), new LocalTime(18, 0), Now);
        }

        week[IsoDayOfWeek.Saturday] = BusinessHours.Open(1, IsoDayOfWeek.Saturday, new LocalTime(10, 0), new LocalTime(14, 0), Now);
        week[IsoDayOfWeek.Sunday] = BusinessHours.Closed(1, IsoDayOfWeek.Sunday, Now);
        return week;
    }

    // 2026-08-10 is a Monday.
    private static LocalDateTime At(int day, int hour, int minute = 0) => new(2026, 8, day, hour, minute);

    [Fact]
    public void A_slot_inside_the_day_is_open()
        => BusinessHoursService.IsOpen(TypicalWeek(), At(10, 9), At(10, 10)).ShouldBeTrue();

    [Fact]
    public void A_slot_that_would_run_past_closing_is_not()
        => BusinessHoursService.IsOpen(TypicalWeek(), At(10, 17, 30), At(10, 18, 30)).ShouldBeFalse();

    [Fact]
    public void Finishing_exactly_at_closing_is_open()
        => BusinessHoursService.IsOpen(TypicalWeek(), At(10, 17), At(10, 18)).ShouldBeTrue();

    [Fact]
    public void A_closed_day_is_closed_however_free_the_diary_is()
        => BusinessHoursService.IsOpen(TypicalWeek(), At(16, 10), At(16, 11)).ShouldBeFalse();

    [Fact]
    public void Saturdays_shorter_hours_are_its_own()
    {
        BusinessHoursService.IsOpen(TypicalWeek(), At(15, 10), At(15, 11)).ShouldBeTrue();
        BusinessHoursService.IsOpen(TypicalWeek(), At(15, 9), At(15, 10)).ShouldBeFalse();
    }

    /// <summary>Nothing crossing midnight can be inside one day's hours, and treating the second
    /// half as belonging to the first day would offer time on a closed day.</summary>
    [Fact]
    public void A_slot_crossing_midnight_is_never_open()
        => BusinessHoursService.IsOpen(TypicalWeek(), At(10, 23, 30), At(11, 0, 30)).ShouldBeFalse();

    [Fact]
    public void A_day_with_no_row_is_closed_rather_than_open_all_hours()
        => BusinessHoursService.IsOpen(new Dictionary<IsoDayOfWeek, BusinessHours>(), At(10, 12), At(10, 13))
            .ShouldBeFalse();

    [Fact]
    public void Tomorrow_from_a_Monday_is_Tuesday()
        => BusinessHoursService.NextWorkingDay(TypicalWeek(), new LocalDate(2026, 8, 10), 1)
            .ShouldBe(new LocalDate(2026, 8, 11));

    /// <summary>The reason lead time counts working days rather than calendar days: promising a
    /// Saturday caller "tomorrow" would promise a Sunday the business is shut.</summary>
    [Fact]
    public void Tomorrow_from_a_Saturday_skips_the_closed_Sunday()
        => BusinessHoursService.NextWorkingDay(TypicalWeek(), new LocalDate(2026, 8, 15), 1)
            .ShouldBe(new LocalDate(2026, 8, 17));

    [Fact]
    public void Zero_lead_is_today_when_today_is_a_working_day()
        => BusinessHoursService.NextWorkingDay(TypicalWeek(), new LocalDate(2026, 8, 10), 0)
            .ShouldBe(new LocalDate(2026, 8, 10));

    [Fact]
    public void Zero_lead_on_a_closed_day_is_the_next_open_one()
        => BusinessHoursService.NextWorkingDay(TypicalWeek(), new LocalDate(2026, 8, 16), 0)
            .ShouldBe(new LocalDate(2026, 8, 17));

    /// <summary>A tenant shut all week is a configuration problem, and an agent must not hang on
    /// a live call while we discover it.</summary>
    [Fact]
    public void A_week_with_no_open_day_gives_up_rather_than_looping()
        => BusinessHoursService.NextWorkingDay(new Dictionary<IsoDayOfWeek, BusinessHours>(), new LocalDate(2026, 8, 10), 1)
            .ShouldBeNull();
}
