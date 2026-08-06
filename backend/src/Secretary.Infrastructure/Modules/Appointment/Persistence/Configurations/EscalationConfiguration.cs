using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

public sealed class EscalationConfiguration : IEntityTypeConfiguration<Escalation>
{
    public void Configure(EntityTypeBuilder<Escalation> builder)
    {
        builder.ToTable("Escalations", DbSchemas.Appointment);
        builder.ConfigureBaseEntity();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.EscalationStatus });

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Client>().WithMany().HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.AcceptedByAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
