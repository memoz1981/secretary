using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ConfigureBaseEntity();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(320).IsRequired();
        builder.Property(a => a.PasswordHash).IsRequired();

        // Simplification flagged for backend review: email unique globally, not per-tenant.
        // Revisit if two different tenants legitimately need to share a login email.
        builder.HasIndex(a => a.Email).IsUnique();
        builder.HasIndex(a => a.TenantId);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
