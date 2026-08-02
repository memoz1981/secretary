namespace Secretary.Domain.Exceptions;

/// <summary>TC-TENANT-03: deactivating a tenant must actually block its Owner/Staff/Agent
/// accounts from logging in, not just hide the tenant from the platform-admin list.</summary>
public sealed class TenantInactiveException : DomainException
{
    public TenantInactiveException() : base("This business's account is currently inactive.")
    {
    }
}
