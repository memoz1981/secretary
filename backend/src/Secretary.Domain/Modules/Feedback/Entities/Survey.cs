using Secretary.Domain.Abstractions;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One questionnaire — "Servis rəyi", "Satış rəyi" — belonging to a tenant.
///
/// A tenant may have several, because a garage asking about a service visit and asking about a
/// sale are not the same set of questions and their results must not be averaged together. How
/// many they may have is a quota the platform sets; see FeedbackSettings.</summary>
public sealed class Survey : BaseEntity
{
    /// <summary>Two dials in a minute is a nuisance call; a week later nobody remembers the
    /// visit. An hour is somewhere sensible to start and the owner moves it.</summary>
    public const int DefaultRetryDelayMinutes = 60;

    public int TenantId { get; private set; }
    public string Name { get; private set; }

    /// <summary>How many more times to ring somebody who never answered. Zero means one attempt
    /// and no more, which is the default: ringing again is a decision, not a fallback.</summary>
    public int RetryCount { get; private set; }

    /// <summary>How long to leave it before ringing again.</summary>
    public int RetryDelayMinutes { get; private set; } = DefaultRetryDelayMinutes;

    private Survey() => Name = string.Empty;

    public static Survey Create(int tenantId, string name, Instant now)
    {
        var survey = new Survey { TenantId = tenantId, Name = Clean(name) };
        survey.InitBase(now);
        return survey;
    }

    public void Rename(string name, Instant now)
    {
        Name = Clean(name);
        Touch(now);
    }

    /// <summary>⚠ Applies to nobody who answered. A caller who picked up and then could not be
    /// understood is a NeedsHuman, and dialling them again with the same agent would fail the
    /// same way — a second identical call is how a survey turns into a nuisance.</summary>
    public void SetRetryPolicy(int retryCount, int retryDelayMinutes, Instant now)
    {
        if (retryCount is < 0 or > MaxRetryCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryCount), retryCount, $"Between 0 and {MaxRetryCount} retries.");
        }

        if (retryDelayMinutes is < MinRetryDelayMinutes or > MaxRetryDelayMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryDelayMinutes), retryDelayMinutes,
                $"Between {MinRetryDelayMinutes} minutes and {MaxRetryDelayMinutes / 60 / 24} days.");
        }

        RetryCount = retryCount;
        RetryDelayMinutes = retryDelayMinutes;
        Touch(now);
    }

    /// <summary>Bounded rather than free. An unbounded retry count is a number somebody types
    /// wrong once and a customer gets rung fifty times; a five-minute floor is the same mistake
    /// in the other dimension.</summary>
    public const int MaxRetryCount = 5;

    public const int MinRetryDelayMinutes = 15;
    public const int MaxRetryDelayMinutes = 7 * 24 * 60;

    private static string Clean(string name)
        => string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A questionnaire needs a name.", nameof(name))
            : name.Trim();
}
