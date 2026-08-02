using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>A login for the web app. Accounts exist for auditing only — every tenant account
/// sees the same tenant-wide data. Accounts are deliberately not linked to Providers:
/// service providers don't need logins.</summary>
public sealed class Account : BaseEntity
{
    public int? TenantId { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public AccountRole Role { get; private set; }

    private Account(int? tenantId, string name, string email, string passwordHash, AccountRole role, Instant now)
    {
        TenantId = tenantId;
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        InitBase(now);
    }

    private Account()
    {
        Name = string.Empty;
        Email = string.Empty;
        PasswordHash = string.Empty;
    }

    /// <summary>Platform-admin accounts have no tenant. Owner/Staff always do.</summary>
    public static Account CreatePlatformAdmin(string name, string email, string passwordHash, Instant now)
        => new(null, name, email, passwordHash, AccountRole.PlatformAdmin, now);

    public static Account CreateOwner(int tenantId, string name, string email, string passwordHash, Instant now)
        => new(tenantId, name, email, passwordHash, AccountRole.Owner, now);

    /// <summary>The tenant's Owner adds a staff account directly (sets its password
    /// themselves) — there is no separate invite-link/activation step.</summary>
    public static Account AddStaff(int tenantId, string name, string email, string passwordHash, Instant now)
        => new(tenantId, name, email, passwordHash, AccountRole.Staff, now);

    /// <summary>One service account per tenant for the AI voice agent to authenticate as —
    /// provisioned alongside the tenant's Owner account (see TenantService.CreateAsync) so
    /// the agent gets a tenant-scoped JWT the same way a human would, rather than a single
    /// cross-tenant credential that would need special-casing everywhere else.</summary>
    public static Account CreateAgent(int tenantId, string passwordHash, Instant now)
        => new(tenantId, "AI Voice Agent", $"agent+{tenantId}@ai-appointment.internal", passwordHash, AccountRole.Agent, now);

    public void UpdateDetails(string name, Instant now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Account name is required.", nameof(name));
        }

        Name = name;
        Touch(now);
    }

    public void ResetPassword(string passwordHash, Instant now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
        Touch(now);
    }
}
