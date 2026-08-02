namespace Secretary.Domain.Enums;

/// <summary>
/// PlatformAdmin manages tenants and has no TenantId. Owner/Staff are tenant-scoped humans.
/// Agent is the AI voice agent's own service-account role (a distinct, narrowly-scoped JWT —
/// never a human's token), per the platform's auth convention.
/// </summary>
public enum AccountRole
{
    PlatformAdmin,
    Owner,
    Staff,
    Agent,
}
