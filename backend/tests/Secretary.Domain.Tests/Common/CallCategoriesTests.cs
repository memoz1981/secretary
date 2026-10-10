using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Secretary.Domain.Tests;

/// <summary>One vocabulary across three modules that had none in common.
///
/// ⚠ Appointments and orders record a CallOutcome about booking; feedback records how far through
/// a questionnaire somebody got. A platform-wide "calls by category" had nothing to count until
/// these two mappings existed, and the mappings are the whole of what "category" means — so they
/// are pinned rather than trusted.</summary>
public sealed class CallCategoriesTests
{
    [Theory]
    [InlineData(CallOutcome.ResolvedByAgent, CallCategory.Answered)]
    [InlineData(CallOutcome.NoAnswer, CallCategory.NotAnswered)]
    [InlineData(CallOutcome.EscalatedResolvedByStaff, CallCategory.Forwarded)]
    [InlineData(CallOutcome.EscalatedAbandoned, CallCategory.Forwarded)]
    [InlineData(CallOutcome.FailedNoAvailability, CallCategory.Unfinished)]
    [InlineData(CallOutcome.FailedAgentLimitation, CallCategory.Unfinished)]
    public void An_order_outcome_maps_to_one_of_the_four(CallOutcome outcome, CallCategory expected)
        => CallCategories.For(outcome).ShouldBe(expected);

    /// <summary>⚠ Bucket for bucket the same as the mapping above, and that is the point being
    /// pinned. Appointments moved to their own outcome enum; the platform page counts calls
    /// across three modules, and a tenant whose totals shifted because we renamed something
    /// inside one module would be right to distrust the page.</summary>
    [Theory]
    [InlineData(AppointmentCallOutcome.Resolved, CallCategory.Answered)]
    [InlineData(AppointmentCallOutcome.Forwarded, CallCategory.Forwarded)]
    [InlineData(AppointmentCallOutcome.Unfinished, CallCategory.Unfinished)]
    [InlineData(AppointmentCallOutcome.NoAvailability, CallCategory.Unfinished)]
    public void An_appointment_outcome_maps_to_one_of_the_four(
        AppointmentCallOutcome outcome, CallCategory expected)
        => CallCategories.For(outcome).ShouldBe(expected);

    /// <summary>An appointment call never reads as NotAnswered, because there is no outcome for
    /// it: an inbound line answers by definition.</summary>
    [Fact]
    public void No_appointment_outcome_reads_as_unanswered()
        => Enum.GetValues<AppointmentCallOutcome>()
            .ShouldAllBe(outcome => CallCategories.For(outcome) != CallCategory.NotAnswered);

    /// <summary>Both escalations are the same thing from the platform's seat: a person was
    /// needed. Whether that person then picked up is the appointment dashboard's business.</summary>
    [Fact]
    public void Both_kinds_of_escalation_read_as_forwarded()
        => CallCategories.For(CallOutcome.EscalatedResolvedByStaff)
            .ShouldBe(CallCategories.For(CallOutcome.EscalatedAbandoned));

    [Theory]
    [InlineData(FeedbackCallStatus.Completed, 4, CallCategory.Answered)]
    [InlineData(FeedbackCallStatus.NeedsHuman, 3, CallCategory.Forwarded)]
    [InlineData(FeedbackCallStatus.Created, 0, CallCategory.NotAnswered)]
    [InlineData(FeedbackCallStatus.InProgress, 0, CallCategory.NotAnswered)]
    public void A_survey_status_maps_to_one_of_the_four(
        FeedbackCallStatus status, int callerTurns, CallCategory expected)
        => CallCategories.For(status, callerTurns).ShouldBe(expected);

    /// <summary>⚠ Abandoned covers two different events and the status alone cannot separate
    /// them. A dial that opened and died in two seconds reached nobody; one abandoned after three
    /// answers reached somebody and did not finish. Four rows on 15 August were the first kind and
    /// counting them as the second would have said we spoke to four people we never reached.</summary>
    [Theory]
    [InlineData(0, CallCategory.NotAnswered)]
    [InlineData(3, CallCategory.Unfinished)]
    public void An_abandoned_survey_is_told_apart_by_whether_anybody_spoke(
        int callerTurns, CallCategory expected)
        => CallCategories.For(FeedbackCallStatus.Abandoned, callerTurns).ShouldBe(expected);
}
