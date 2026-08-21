namespace Secretary.Domain.Exceptions;

/// <summary>The tenant asked for one more of something than the platform sells them.
///
/// Distinct from a validation failure: nothing about the request is malformed, and repeating it
/// correctly will not help. What has to change is the plan, which is why the message names both
/// the limit and what they already have — an owner who reads "limited to 3 and already has 3"
/// knows exactly who to ask and for what.</summary>
public sealed class QuotaExceededException : DomainException
{
    public QuotaExceededException(string what, int limit, int used)
        : base($"This account is limited to {limit} {what} and already has {used}. "
               + "Ask the platform to raise the limit.")
    {
        Limit = limit;
        Used = used;
    }

    public int Limit { get; }

    public int Used { get; }
}
