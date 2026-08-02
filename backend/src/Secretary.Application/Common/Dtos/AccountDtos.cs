using Secretary.Domain.Enums;

namespace Secretary.Application.Dtos;

public sealed record AccountResponse(
    int Id, string Name, string Email, AccountRole Role, EntityStatus Status);

public sealed record AddStaffRequest(string Name, string Email, string Password);

public sealed record UpdateAccountRequest(string Name);

/// <summary>Owner self-service (Admin page): set a new password for a tenant account.</summary>
public sealed record ResetAccountPasswordRequest(string Password);
