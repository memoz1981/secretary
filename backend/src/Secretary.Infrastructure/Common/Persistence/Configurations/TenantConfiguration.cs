using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ConfigureBaseEntity();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Timezone).HasMaxLength(100).IsRequired();
        builder.Property(t => t.PhoneLine).HasMaxLength(50);

        // Stored as its number, like every other enum here. Indexed because the tenant list
        // filters on it, and that list is the one screen the platform admin lives in.
        builder.Property(t => t.BusinessType).HasConversion<int?>();
        builder.HasIndex(t => t.BusinessType);
    }
}
