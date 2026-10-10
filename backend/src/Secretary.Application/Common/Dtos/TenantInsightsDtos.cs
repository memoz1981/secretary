using Secretary.Domain.Enums;
using Secretary.Domain.ValueObjects;

namespace Secretary.Application.Dtos;

/// <summary>One module's share of a tenant's traffic.</summary>
public sealed record TenantModuleUsage(
    Module Module, int Calls, int DurationSeconds, long Tokens, decimal CostUsd)
{
    /// <summary>What a minute on this line costs. The rate, rather than the bill.
    ///
    /// ⚠ Null when nothing has been dialled, like the averages below, and for the same reason: a
    /// module nobody has used has no rate, and printing 0.00 would read as a line that runs free.
    ///
    /// Worth having per module rather than only in total, because the modules do not cost the
    /// same. A survey call is short and mostly the agent talking; an appointment call is longer
    /// and carries a tool call per turn, each one re-sending the whole prompt.</summary>
    public decimal? CostPerMinuteUsd
        => DurationSeconds == 0 ? null : CostUsd / ((decimal)DurationSeconds / 60m);
}

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
    IReadOnlyList<TenantCategoryUsage> ByCategory,
    /// <summary>What this tenant is allowed to use — not what they have used. A module they hold
    /// and have never dialled says something (it is sold and idle); so does a module in the usage
    /// table below that no longer appears here.</summary>
    IReadOnlyList<Module> EnabledModules)
{
    /// <summary>Null when nothing has been dialled — an average over no calls is not zero, and a
    /// tenant who has never used the product should not read as one whose calls are free.</summary>
    public decimal? AverageCostPerCallUsd => TotalCalls == 0 ? null : TotalCostUsd / TotalCalls;

    public int? AverageDurationSeconds => TotalCalls == 0 ? null : TotalDurationSeconds / TotalCalls;

    /// <summary>The blended rate across everything this tenant runs — what a minute of their
    /// traffic costs us on average. Null on no calls, as above.</summary>
    public decimal? AverageCostPerMinuteUsd
        => TotalDurationSeconds == 0 ? null : TotalCostUsd / ((decimal)TotalDurationSeconds / 60m);
}
