using Secretary.Domain.Enums;

namespace Secretary.Domain.ValueObjects;

/// <summary>What became of a call, in words that mean the same thing in every module.
///
/// ⚠ There was no such vocabulary. Appointments and orders record a CallOutcome with six members
/// about booking; feedback records how far through a questionnaire somebody got. Both are right
/// for their own screens and neither can be added to the other, so a platform-wide "calls by
/// category" had nothing to count until this existed.
///
/// Four categories, chosen for what somebody looking at a tenant would do about each: nobody
/// picked up, we handled it, we handed it to a person, or it fell over. Anything finer belongs on
/// the module's own dashboard, where the words can be about booking or about surveys.</summary>
public enum CallCategory
{
    /// <summary>Nobody was reached, or the line opened and produced nothing.</summary>
    NotAnswered = 0,

    /// <summary>The agent handled it end to end.</summary>
    Answered = 1,

    /// <summary>Handed to a person — asked for, or the agent gave up and escalated.</summary>
    Forwarded = 2,

    /// <summary>Reached somebody and did not finish: a hang-up, or the agent hitting a wall.
    /// Distinct from NotAnswered because there is nothing to dial again about.</summary>
    Unfinished = 3,
}

public static class CallCategories
{
    /// <summary>Every category, in the order a reader wants them: what worked, what needed us,
    /// what went wrong, and who we never got hold of.</summary>
    public static readonly IReadOnlyList<CallCategory> All =
        [CallCategory.Answered, CallCategory.Forwarded, CallCategory.Unfinished, CallCategory.NotAnswered];

    /// <summary>Appointment and order calls, which share CallOutcome.</summary>
    public static CallCategory For(CallOutcome outcome) => outcome switch
    {
        CallOutcome.ResolvedByAgent => CallCategory.Answered,
        CallOutcome.NoAnswer => CallCategory.NotAnswered,

        // Both escalations are the same thing from here: a person was needed. Whether that person
        // then picked up is the appointment dashboard's business, not the platform's.
        CallOutcome.EscalatedResolvedByStaff or CallOutcome.EscalatedAbandoned => CallCategory.Forwarded,

        _ => CallCategory.Unfinished,
    };

    /// <summary>Survey calls, which record how far through the questions somebody got.</summary>
    /// <param name="callerTurnCount">⚠ Needed because Abandoned covers two different events. A
    /// dial that opened and died without the caller ever speaking reached nobody; one abandoned
    /// after they answered three questions reached them and did not finish. The status alone
    /// cannot tell those apart, and they are the two categories a reader would act on
    /// differently.</param>
    public static CallCategory For(FeedbackCallStatus status, int callerTurnCount) => status switch
    {
        FeedbackCallStatus.Completed => CallCategory.Answered,
        FeedbackCallStatus.NeedsHuman => CallCategory.Forwarded,
        FeedbackCallStatus.Created or FeedbackCallStatus.InProgress => CallCategory.NotAnswered,
        _ => callerTurnCount > 0 ? CallCategory.Unfinished : CallCategory.NotAnswered,
    };
}
