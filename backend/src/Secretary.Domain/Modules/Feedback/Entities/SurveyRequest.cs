using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One person to be surveyed. Not one call — one person.
///
/// ⚠ The distinction is the whole of this file, and it was missing. Every press of the call
/// button wrote a row into fd.Calls, so four failed connections in nine seconds appeared as four
/// calls beside one real one, and the owner counted five surveys where there had been one. A dial
/// that produced nothing is not a survey; it is the same person, still waiting.
///
/// So the request holds who, and the attempts hold what happened. A request is created before
/// anybody is rung — by the form today, by a scheduler later — which is also what makes this
/// module outbound-shaped: the defining feature of outbound is not the dialling, it is knowing
/// the subject before the call starts.
///
/// The person is named here rather than joined to an ord.Customer or an app.Client. They may well
/// be one, but the survey does not care and a link would make a demo depend on a customer record
/// existing.</summary>
public sealed class SurveyRequest : BaseEntity
{
    public int TenantId { get; private set; }
    public int SurveyId { get; private set; }

    public string PersonName { get; private set; }
    public string PhoneNumber { get; private set; }

    public SurveyRequestOutcome Outcome { get; private set; }

    /// <summary>How many times we have dialled. One row in fd.Calls per count.</summary>
    public int AttemptCount { get; private set; }

    public Instant? LastAttemptAt { get; private set; }

    /// <summary>When the next dial is due, or null when none is — because they answered, or
    /// because the retries are used up. The scheduler reads this column and nothing else, so
    /// "should we ring them again" is decided once, here, and not re-derived by every caller.</summary>
    public Instant? NextAttemptDueAt { get; private set; }

    public Instant CreatedAtUtc { get; private set; }

    private SurveyRequest()
    {
        PersonName = string.Empty;
        PhoneNumber = string.Empty;
    }

    public static SurveyRequest Queue(
        int tenantId, int surveyId, string personName, string phoneNumber, Instant now)
    {
        if (string.IsNullOrWhiteSpace(personName))
        {
            throw new ArgumentException("A survey needs somebody to be about.", nameof(personName));
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("A survey needs a number.", nameof(phoneNumber));
        }

        var request = new SurveyRequest
        {
            TenantId = tenantId,
            SurveyId = surveyId,
            PersonName = personName.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            Outcome = SurveyRequestOutcome.Pending,
            CreatedAtUtc = now,
            NextAttemptDueAt = now,
        };

        request.InitBase(now);
        return request;
    }

    /// <summary>Another dial is about to happen. Counted before it does, so a dial that crashes
    /// the process still counted — the alternative fails open, and failing open here means
    /// ringing somebody forever.</summary>
    public void BeginAttempt(Instant now)
    {
        AttemptCount++;
        LastAttemptAt = now;
        NextAttemptDueAt = null;
        Touch(now);
    }

    /// <summary>How that dial ended, and therefore whether there will be another.
    ///
    /// Only NotReached is retried. Refused is an answer — the worst one, but an answer, and
    /// ringing back somebody who said no is the behaviour that gets a number blocked. NeedsHuman
    /// would fail identically on a second attempt with the same agent. Complete is done.</summary>
    /// <param name="withinCallingHours">Moves a due time to the next moment the business is open,
    /// and returns null when there is no such moment. Supplied by the caller because opening
    /// hours are a tenant record and this is an entity — but it is not optional: "in an hour"
    /// from a six o'clock failure is seven o'clock, and a day's delay lands at whatever time the
    /// first attempt happened to be made. The first survey call somebody gets at three in the
    /// morning is the last call from us they will ever answer.</param>
    public void Settle(
        SurveyRequestOutcome outcome, int retryCount, int retryDelayMinutes,
        Func<Instant, Instant?> withinCallingHours, Instant now)
    {
        Outcome = outcome;

        NextAttemptDueAt = outcome == SurveyRequestOutcome.NotReached && AttemptCount <= retryCount
            ? withinCallingHours(now.Plus(Duration.FromMinutes(retryDelayMinutes)))
            : null;

        Touch(now);
    }

    /// <summary>Somebody is dealing with it by hand, or it is not worth chasing. Stops the
    /// retries without pretending the survey happened.</summary>
    public void CloseWithoutAnswer(Instant now)
    {
        NextAttemptDueAt = null;
        Touch(now);
    }

    /// <summary>Whether this is somebody a person still has to deal with. Drives the follow-up
    /// list, and is deliberately not "did the call fail" — a Refused needs nobody.</summary>
    public bool NeedsFollowUp => Outcome is SurveyRequestOutcome.NeedsHuman
                                 || (Outcome == SurveyRequestOutcome.NotReached && NextAttemptDueAt is null);
}
