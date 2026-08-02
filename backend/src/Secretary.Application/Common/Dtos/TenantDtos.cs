using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Application.Dtos;

public sealed record TenantResponse(
    int Id, string Name, string Timezone, string? PhoneLine, EntityStatus Status, Instant CreatedAt);

public sealed record CreateTenantRequest(
    string Name, string Timezone, string? PhoneLine, string OwnerName, string OwnerEmail, string OwnerPassword);

/// <summary>AgentApiKey is a one-time reveal — configure the AI voice agent integration
/// (agent-developer's side) with it immediately; it's not retrievable again afterwards,
/// same as a typical API client secret.</summary>
public sealed record CreateTenantResult(TenantResponse Tenant, int OwnerAccountId, int AgentAccountId, string AgentApiKey);

public sealed record UpdateTenantRequest(string Name, string Timezone, string? PhoneLine);
