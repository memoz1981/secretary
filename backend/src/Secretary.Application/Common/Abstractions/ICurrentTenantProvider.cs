namespace Secretary.Application.Abstractions;

/// <summary>Resolves the tenant the current request is scoped to, from the caller's JWT
/// claims. Consumed by Infrastructure's EF Core global query filter — this is the one place
/// an ambient/ambient-looking service is justified, since every tenant-scoped query needs it
/// and threading an explicit parameter through every repository call would just be
/// boilerplate with the same value every time. Application services still never touch JWTs
/// or HTTP directly (see ICurrentUserContext for the "who is calling" equivalent, which
/// controllers resolve and pass explicitly instead).</summary>
public interface ICurrentTenantProvider
{
    /// <summary>Null for platform-admin callers, who aren't scoped to any single tenant.</summary>
    int? TenantId { get; }
}
