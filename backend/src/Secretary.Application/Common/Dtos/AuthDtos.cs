using Secretary.Domain.Enums;

namespace Secretary.Application.Dtos;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, int AccountId, AccountRole Role, int? TenantId);

public sealed record MeResponse(int Id, string Name, string Email, AccountRole Role, int? TenantId, string? TenantName);
