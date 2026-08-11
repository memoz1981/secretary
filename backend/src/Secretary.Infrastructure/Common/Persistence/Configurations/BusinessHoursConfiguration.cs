using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

public sealed class BusinessHoursConfiguration : IEntityTypeConfiguration<BusinessHours>
{
    public void Configure(EntityTypeBuilder<BusinessHours> builder)
    {
        builder.ToTable("BusinessHours", DbSchemas.Common);
        builder.ConfigureBaseEntity();

        // IsClosed is derived from the pair, not stored — one fact, so the two cannot disagree.
        builder.Ignore(h => h.IsClosed);

        // A day can only appear once per tenant. Without this, two rows for Tuesday would each
        // look authoritative and availability would depend on row order.
        builder.HasIndex(h => new { h.TenantId, h.DayOfWeek }).IsUnique();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(h => h.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
