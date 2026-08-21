using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Dtos;

public sealed record TenantResponse(
    int Id, string Name, string Timezone, string? PhoneLine, bool ShowCallCosts, EntityStatus Status, Instant CreatedAt,
    IReadOnlyList<Module> EnabledModules);

public sealed record CreateTenantRequest(
    string Name, string Timezone, string? PhoneLine, bool ShowCallCosts,
    string OwnerName, string OwnerEmail, string OwnerPassword);

/// <summary>AgentApiKey is a one-time reveal — configure the AI voice agent integration
/// (agent-developer's side) with it immediately; it's not retrievable again afterwards,
/// same as a typical API client secret.</summary>
public sealed record CreateTenantResult(TenantResponse Tenant, int OwnerAccountId, int AgentAccountId, string AgentApiKey);

public sealed record UpdateTenantRequest(string Name, string Timezone, string? PhoneLine, bool ShowCallCosts);

/// <summary>What a tenant may change about themselves — the same details, minus the one that is
/// not theirs.
///
/// ⚠ A separate type rather than the same one with the field ignored. Both edit screens used
/// UpdateTenantRequest, so the moment ShowCallCosts joined it an Owner could have posted it to
/// their own settings endpoint and switched on the figures the platform had decided to withhold.
/// Nothing in the UI offered it; nothing in the UI has to. A request type is the list of things a
/// caller is allowed to say, and leaving a field on it is permission.</summary>
public sealed record UpdateOwnTenantRequest(string Name, string Timezone, string? PhoneLine);
