using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ConfigureBaseEntity();
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.Property(a => a.IdempotencyKey).HasMaxLength(200);

        // Two distinct indexes over the same three columns — the "(properties, name)" overload
        // is required (rather than HasIndex(...).HasDatabaseName(...)) because EF Core matches
        // repeated HasIndex calls over an identical property list to the *same* index unless a
        // name is supplied up front, which would silently collapse these into one.
        builder.HasIndex(a => new { a.TenantId, a.ProviderId, a.Start }, "IX_Appointments_TenantId_ProviderId_Start");
        builder.HasIndex(a => a.ClientId);

        // Identifiers are double-quoted, which is the one form PostgreSQL and SQLite both
        // accept. Unquoted, PostgreSQL folds the name to lowercase and then cannot find the
        // PascalCase column EF actually created. SQL Server's bracket convention ("[Col]")
        // works on neither.
        builder.HasIndex(a => new { a.TenantId, a.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        // DB-level backstop against an exact-duplicate booking (same provider, same tenant,
        // same start instant) slipping through a race that beats the in-transaction
        // FindOverlappingAsync check — see backend/README.md's "Things flagged" section for why
        // this doesn't (and, on SQL Server, can't easily) replace that check for true interval
        // overlap: a unique index can only catch identical start times, not overlapping ranges
        // with different starts. "AppointmentStatus <> 2" is AppointmentStatus.Cancelled's
        // ordinal — a cancelled appointment must not block a new one from reusing that exact
        // slot. Kept alongside (not instead of) the plain index above, since
        // GetForDateRangeAsync (the Calendar page) queries across every status.
        builder.HasIndex(
                a => new { a.TenantId, a.ProviderId, a.Start }, "IX_Appointments_TenantId_ProviderId_Start_Unique_NotCancelled")
            .IsUnique()
            .HasFilter("\"AppointmentStatus\" <> 2");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Client>().WithMany().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Provider>().WithMany().HasForeignKey(a => a.ProviderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceOffering>().WithMany().HasForeignKey(a => a.ServiceOfferingId).OnDelete(DeleteBehavior.Restrict);
    }
}
