using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

public sealed class TenantModuleConfiguration : IEntityTypeConfiguration<TenantModule>
{
    public void Configure(EntityTypeBuilder<TenantModule> builder)
    {
        builder.ConfigureBaseEntity();

        // One row per tenant per module, ever. Revoking flips Status; it never deletes, so a
        // re-grant reuses the same row and the history stays intact.
        builder.HasIndex(m => new { m.TenantId, m.Module }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
