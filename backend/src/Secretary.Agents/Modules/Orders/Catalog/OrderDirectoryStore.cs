using System.Collections.Concurrent;
using Secretary.Application.Dtos;
using NodaTime;

namespace Secretary.Agents.Orders;

/// <summary>How long a business takes to deliver and which weekdays it works — everything
/// GetDeliveryDayAsync needs except today's date.
///
/// Weekdays rather than the BusinessHours rows on purpose. The rows are entities loaded by one
/// request's DbContext, and a static dictionary would keep them alive long after that context
/// was disposed. A set of days is a value, and it is all the working-day walk reads.</summary>
internal sealed record DeliveryPolicy(
    int LeadWorkingDays, int MaxDeliveryDaysAhead, IReadOnlySet<IsoDayOfWeek> OpenDays);

/// <summary>Process-wide backing store shared between TenantOrderDirectory (scoped — reads and
/// populates for the tenant on the call) and AgentDirectoryChangeNotifier (singleton —
/// invalidates from whichever request made the change). A plain static class for the same reason
/// the appointment stores are: two very different DI lifetimes should not have to argue over who
/// owns the dictionary.</summary>
internal static class OrderDirectoryStore
{
    private static readonly ConcurrentDictionary<int, IReadOnlyList<ProductResponse>> Products = new();
    private static readonly ConcurrentDictionary<int, DeliveryPolicy> Policies = new();

    public static bool TryGetProducts(int tenantId, out IReadOnlyList<ProductResponse> value)
        => Products.TryGetValue(tenantId, out value!);

    public static void SetProducts(int tenantId, IReadOnlyList<ProductResponse> value)
        => Products[tenantId] = value;

    public static bool TryGetPolicy(int tenantId, out DeliveryPolicy value)
        => Policies.TryGetValue(tenantId, out value!);

    public static void SetPolicy(int tenantId, DeliveryPolicy value) => Policies[tenantId] = value;

    public static void Invalidate(int tenantId)
    {
        Products.TryRemove(tenantId, out _);
        Policies.TryRemove(tenantId, out _);
    }
}
