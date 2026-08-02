using Secretary.Application.Abstractions;

namespace Secretary.Application.Tests.TestSupport;

public sealed class FakeCurrentTenantProvider : ICurrentTenantProvider
{
    public FakeCurrentTenantProvider(int? tenantId) => TenantId = tenantId;

    public int? TenantId { get; }
}
