using NodaTime;

namespace Secretary.Domain.Exceptions;

/// <summary>Thrown when a requested appointment slot overlaps one that already exists for
/// the same provider. See component-specs.md / handoff.md's concurrency call-out — this is
/// checked inside a transaction rather than enforced by a DB constraint, since interval
/// overlap can't be expressed as a simple uniqueness rule.</summary>
public sealed class SchedulingConflictException : DomainException
{
    public SchedulingConflictException(int providerId, Instant start, Instant end)
        : base($"Provider '{providerId}' already has an appointment overlapping {start}–{end}.")
    {
    }
}
