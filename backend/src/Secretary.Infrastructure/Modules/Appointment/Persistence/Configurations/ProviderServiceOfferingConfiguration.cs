using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

public sealed class ProviderServiceOfferingConfiguration : IEntityTypeConfiguration<ProviderServiceOffering>
{
    public void Configure(EntityTypeBuilder<ProviderServiceOffering> builder)
    {
        builder.ToTable("ProviderServiceOfferings", DbSchemas.Appointment);
        builder.ConfigureBaseEntity();

        // One row per provider×offering pair — the checkbox state lives in Status.
        builder.HasIndex(p => new { p.ProviderId, p.ServiceOfferingId }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.ServiceOfferingId });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Provider>().WithMany().HasForeignKey(p => p.ProviderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceOffering>().WithMany().HasForeignKey(p => p.ServiceOfferingId).OnDelete(DeleteBehavior.Restrict);
    }
}
