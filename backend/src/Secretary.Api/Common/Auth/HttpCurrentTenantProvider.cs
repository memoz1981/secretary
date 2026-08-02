using Secretary.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Secretary.Api.Auth;

/// <summary>Resolves the current tenant from the caller's JWT claims. Registered as scoped
/// and injected into AppDbContext (via the Application-layer ICurrentTenantProvider
/// interface) so the EF Core global tenant filter has something to key on, without
/// Infrastructure or Application ever referencing HttpContext directly.</summary>
public sealed class HttpCurrentTenantProvider : ICurrentTenantProvider
{
    public int? TenantId { get; }

    public HttpCurrentTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        var claim = httpContextAccessor.HttpContext?.User.FindFirst(AppClaimTypes.TenantId);
        TenantId = claim is not null && int.TryParse(claim.Value, out var id)
            ? id
            : null;
    }
}
