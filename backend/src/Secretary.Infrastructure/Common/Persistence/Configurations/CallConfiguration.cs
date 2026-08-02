using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

public sealed class CallConfiguration : IEntityTypeConfiguration<Call>
{
    public void Configure(EntityTypeBuilder<Call> builder)
    {
        builder.ConfigureBaseEntity();
        builder.Property(c => c.CallerPhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(c => c.RecordingUrl).IsRequired();
        // No explicit column type for Transcript — every provider maps an unbounded string
        // sensibly on its own (text on PostgreSQL), and hardcoding one broke running these
        // same entity configurations against SQLite in tests.

        builder.Property(c => c.AgentModel).HasMaxLength(64).IsRequired();

        // decimal(18,8), not money or a float: a single realtime call lands around $0.30, but
        // individual line items (a few hundred cached tokens at $0.40/M) are fractions of a
        // cent, and those are what a month's spend is the sum of. Eight decimal places keep
        // each call's stored price exact rather than pre-rounded.
        builder.Property(c => c.CostUsd).HasPrecision(18, 8);

        // TokenUsage is a read-only projection over the six token columns, not state of its
        // own — mapping it would create a duplicate set of columns.
        builder.Ignore(c => c.TokenUsage);

        builder.HasIndex(c => new { c.TenantId, c.StartedAt });
        builder.HasIndex(c => c.RelatedAppointmentId);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Client>().WithMany().HasForeignKey(c => c.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(c => c.RelatedAppointmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
