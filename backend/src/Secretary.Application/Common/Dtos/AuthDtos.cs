using Secretary.Domain.Enums;

namespace Secretary.Application.Dtos;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, int AccountId, AccountRole Role, int? TenantId);

/// <summary>EnabledModules is what the front end routes on: one module goes straight through to
/// it, several show the picker, none is an error worth saying out loud. Empty for a platform
/// admin, who administers grants rather than holding any.</summary>
public sealed record MeResponse(
    int Id,
    string Name,
    string Email,
    AccountRole Role,
    int? TenantId,
    string? TenantName,
    IReadOnlyList<Module> EnabledModules);
