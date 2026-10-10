namespace Secretary.Domain.Enums;

/// <summary>What became of an appointment call, in the words the appointment dashboard reports.
///
/// ⚠ A second enum rather than an edit to CallOutcome. That one lives in this module's folder but
/// Orders records it too — OrderCall.Outcome, OrderCallService, the order repositories — so
/// changing its members to suit the appointment dashboard would have silently rewritten what an
/// order call means. It stays exactly as it was and Orders keeps using it.
///
///   Resolved       something was booked, moved or cancelled — the call did its job
///   Forwarded      handed to a person; what the person then did is not ours to record
///   Unfinished     reached somebody and did not finish: a hang-up, or the agent hitting a wall
///   NoAvailability the caller wanted a time and the calendar had none. Its own member because
///                  it is the one failure that is about the business rather than the agent — a
///                  week of these is a staffing decision, not a bug
///
/// ⚠ There is no NoAnswer. The old shared enum had one and it never applied here: an inbound line
/// answers by definition, and it existed for outbound reminders that were never built. Four
/// members means the four tiles on the dashboard account for every call, which is the property
/// that makes the percentages worth printing — a fifth outcome with no tile would leave a
/// remainder nobody could see.
///
/// Numbered explicitly and from 1. The values are persisted on every call row, so a member's
/// number is not free to change and a new one takes the next number rather than a tidy
/// alphabetical position. Starting at 1 also means a row that somehow never got written reads as
/// 0 — not as the first member, which would be a silent claim that the call went well.</summary>
public enum AppointmentCallOutcome
{
    Resolved = 1,
    Forwarded = 2,
    Unfinished = 3,
    NoAvailability = 4,
}
