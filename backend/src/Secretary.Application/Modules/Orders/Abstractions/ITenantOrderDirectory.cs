using Secretary.Application.Dtos;
using NodaTime;

namespace Secretary.Application.Abstractions;

/// <summary>What the order line needs to know about a tenant that does not change during a call:
/// what they sell, and which day they can deliver.
///
/// Held in memory between calls rather than read live, for latency rather than load. These reads
/// happen inside a tool call, which happens in the middle of a spoken sentence — the caller
/// hears the wait. The queries are small, but a database round trip on the far side of an Azure
/// region is dead air, and there is one on the way into every order.
///
/// Agents provides the caching implementation; nothing else registers one, because nothing else
/// answers a phone.</summary>
public interface ITenantOrderDirectory
{
    /// <summary>The catalogue with its ids — the ids are what the model orders by, so this is
    /// read at least once on every call that gets as far as an order.</summary>
    Task<IReadOnlyList<ProductResponse>> GetProductsAsync(CancellationToken cancellationToken);

    /// <summary>The soonest day this business will promise, or null when it is shut for the next
    /// fortnight — a configuration problem to escalate rather than invent an answer for.
    ///
    /// The policy behind it is cached; the day itself is worked out per call, because the answer
    /// depends on today's date and a cached day would be wrong by morning.</summary>
    Task<LocalDate?> GetDeliveryDayAsync(CancellationToken cancellationToken);
}
