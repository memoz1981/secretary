namespace Secretary.Domain.Exceptions;

/// <summary>Thrown when an entity is looked up under a different tenant than the caller's
/// own. In normal operation the EF Core global tenant filter (see Infrastructure) means this
/// should be unreachable — it exists as a defense-in-depth guard, not the primary control.</summary>
public sealed class TenantMismatchException : DomainException
{
    public TenantMismatchException(string entityName, object id)
        : base($"{entityName} '{id}' does not belong to the current tenant.")
    {
    }
}
