using Secretary.Domain.Enums;

namespace Secretary.Application.Abstractions;

/// <summary>Which modules the calling tenant has, cached for the life of one request.
///
/// Read on nearly every request — the authorization policy consults it before a module's
/// endpoints run — so it must not be a query per check. Cached per request rather than carried
/// as a JWT claim on purpose: a claim would mean an admin's grant or revocation did nothing
/// until the tenant next logged in, which for a revocation is the wrong way round.</summary>
public interface ICurrentTenantModules
{
    Task<IReadOnlyList<Module>> GetEnabledAsync(CancellationToken cancellationToken);

    Task<bool> HasAsync(Module module, CancellationToken cancellationToken);
}
