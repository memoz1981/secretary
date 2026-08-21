using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;

namespace Secretary.Application.Dtos;

/// <summary>One module's share of a tenant's traffic.</summary>
public sealed record TenantModuleUsage(
    Module Module, int Calls, int DurationSeconds, long Tokens, decimal CostUsd);

/// <summary>How the tenant's calls ended, in the one vocabulary all three modules share — see
/// CallCategories.</summary>
public sealed record TenantCategoryUsage(CallCategory Category, int Calls)
{
    public decimal ShareOf(int total) => total == 0 ? 0 : (decimal)Calls / total;
}

/// <summary>What one tenant has actually used, for the platform admin looking at them.
///
/// ⚠ Money is always present here, unlike everywhere else. This response is only ever built for
/// a caller with no tenant of their own — see CallCostVisibility — and the whole point of the
/// page is what they are costing us.</summary>
public sealed record TenantInsightsResponse(
    int TenantId,
    string TenantName,
    int TotalCalls,
    int TotalDurationSeconds,
    long TotalTokens,
    decimal TotalCostUsd,
    IReadOnlyList<TenantModuleUsage> ByModule,
    IReadOnlyList<TenantCategoryUsage> ByCategory)
{
    /// <summary>Null when nothing has been dialled — an average over no calls is not zero, and a
    /// tenant who has never used the product should not read as one whose calls are free.</summary>
    public decimal? AverageCostPerCallUsd => TotalCalls == 0 ? null : TotalCostUsd / TotalCalls;

    public int? AverageDurationSeconds => TotalCalls == 0 ? null : TotalDurationSeconds / TotalCalls;
}
