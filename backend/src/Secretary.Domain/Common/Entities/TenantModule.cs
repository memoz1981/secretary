using Secretary.Domain.Abstractions;
using Secretary.Domain.Enums;
using NodaTime;

namespace Secretary.Domain.Entities;

/// <summary>One tenant's access to one module, granted and revoked by the platform admin.
///
/// A row exists once a module has ever been granted; revoking sets it Inactive rather than
/// deleting, so the history of what a tenant had — and when — survives, which billing will want.
/// Absence of a row and an inactive row mean the same thing to a caller: no access.
///
/// Deliberately NOT tenant-query-filtered. The platform admin who administers these has no
/// TenantId at all, so the usual filter would hide the table from the only account allowed to
/// change it. Scoping is explicit in the repository instead.</summary>
public sealed class TenantModule : BaseEntity
{
    public int TenantId { get; private set; }
    public Module Module { get; private set; }

    private TenantModule(int tenantId, Module module, Instant now)
    {
        TenantId = tenantId;
        Module = module;
        InitBase(now);
    }

    private TenantModule()
    {
    }

    public static TenantModule Grant(int tenantId, Module module, Instant now) => new(tenantId, module, now);

    /// <summary>True when the tenant may use it right now.</summary>
    public bool IsEnabled => Status == EntityStatus.Active;
}
