using Secretary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Secretary.Infrastructure.Persistence.Configurations;

/// <summary>The Orders module's tables, kept in one file because they are one shape: small,
/// mostly text, and only meaningful together. Splitting seven of these into seven files would
/// mean seven headers and no more clarity.</summary>
public sealed class MeasurementUnitConfiguration : IEntityTypeConfiguration<MeasurementUnit>
{
    public void Configure(EntityTypeBuilder<MeasurementUnit> builder)
    {
        builder.ToTable("Units", DbSchemas.Orders);
        builder.ConfigureBaseEntity();
        builder.Property(u => u.Name).HasMaxLength(20).IsRequired();

        // The one table in the module with no TenantId: a kilogram is a kilogram. Unique on the
        // name so the seed cannot be applied twice.
        builder.HasIndex(u => u.Name).IsUnique();
    }
}

public sealed class OrderSettingsConfiguration : IEntityTypeConfiguration<OrderSettings>
{
    public void Configure(EntityTypeBuilder<OrderSettings> builder)
    {
        builder.ToTable("OrderSettings", DbSchemas.Orders);
        builder.ConfigureBaseEntity();

        // One row per tenant. A second would be a second delivery promise.
        builder.HasIndex(s => s.TenantId).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", DbSchemas.Orders);
        builder.ConfigureBaseEntity();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Aliases).HasMaxLength(500);
        builder.Property(p => p.UnitPrice).HasColumnType("decimal(10,2)");

        // Same precision as an order line, so a cap can be expressed in whatever the product is
        // measured in — 2.5 kg is a sensible maximum, 2.5 bidons is not, and the unit decides.
        builder.Property(p => p.MaxOrderQuantity).HasPrecision(12, 3);

        builder.HasIndex(p => p.TenantId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MeasurementUnit>().WithMany().HasForeignKey(p => p.MeasurementUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", DbSchemas.Orders);
        builder.ConfigureBaseEntity();
        builder.Property(c => c.Name).HasMaxLength(200);

        builder.HasIndex(c => c.TenantId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerPhoneNumberConfiguration : IEntityTypeConfiguration<CustomerPhoneNumber>
{
    public void Configure(EntityTypeBuilder<CustomerPhoneNumber> builder)
    {
        builder.ToTable("CustomerPhoneNumbers", DbSchemas.Orders);
        builder.ConfigureBaseEntity();
        builder.Property(p => p.PhoneNumber).HasMaxLength(32).IsRequired();

        // The lookup that identifies a caller, so it is indexed rather than scanned. Not unique:
        // a household landline can legitimately belong to two customers, which is exactly why
        // the phone route asks a confirming question instead of identifying outright.
        builder.HasIndex(p => p.PhoneNumber);
        builder.HasIndex(p => new { p.CustomerId, p.PhoneNumber }).IsUnique();

        builder.HasOne<Customer>().WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses", DbSchemas.Orders);
        builder.ConfigureBaseEntity();
        builder.Property(a => a.Label).HasMaxLength(50);
        builder.Property(a => a.District).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Area).HasMaxLength(100);
        builder.Property(a => a.Street).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Lane).HasMaxLength(50);
        builder.Property(a => a.Building).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Apartment).HasMaxLength(20);
        builder.Property(a => a.Landmark).HasMaxLength(300);
        builder.Property(a => a.SpokenText).HasMaxLength(1000);
        builder.Property(a => a.StreetNormalized).HasMaxLength(200).IsRequired();
        builder.Property(a => a.BuildingNormalized).HasMaxLength(20).IsRequired();

        // The tuple an address is matched on. All four, because house 12 exists on every lane of
        // a street — matching without the lane finds the wrong customer in precisely the areas
        // where a misdelivery is hardest to undo.
        builder.HasIndex(a => new { a.District, a.StreetNormalized, a.LaneNumber, a.BuildingNormalized });
        builder.HasIndex(a => a.CustomerId);

        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", DbSchemas.Orders);
        builder.ConfigureBaseEntity();
        builder.Property(o => o.Notes).HasMaxLength(1000);

        builder.HasIndex(o => new { o.TenantId, o.PlacedAt });
        builder.HasIndex(o => o.CustomerId);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(o => o.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerAddress>().WithMany().HasForeignKey(o => o.CustomerAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        // Owned by the order in the aggregate sense: lines are loaded and saved with it, and an
        // order can never be persisted without them.
        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines", DbSchemas.Orders);
        builder.ConfigureBaseEntity();

        // decimal(12,3): kilos and cubic metres are fractional, and an integer column would have
        // silently rounded two and a half kilos to two.
        builder.Property(l => l.Quantity).HasPrecision(12, 3);

        builder.HasIndex(l => l.OrderId);
        builder.HasOne<Product>().WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The order line's call log — ord.Calls, its own table beside app.Calls rather than a
/// Module column on it. Same rule as every other table here: a module owns its own, and these
/// two records carry foreign keys that cannot both live on one row.</summary>
public sealed class OrderCallConfiguration : IEntityTypeConfiguration<OrderCall>
{
    public void Configure(EntityTypeBuilder<OrderCall> builder)
    {
        builder.ToTable("Calls", DbSchemas.Orders);
        builder.ConfigureBaseEntity();
        builder.Property(c => c.CallerPhoneNumber).HasMaxLength(32).IsRequired();
        builder.Property(c => c.RecordingUrl).IsRequired();

        // No explicit column type for Transcript — every provider maps an unbounded string
        // sensibly on its own, and hardcoding one breaks these same configurations on SQLite.
        builder.Property(c => c.AgentModel).HasMaxLength(64).IsRequired();

        // decimal(18,8), not money or a float: a call lands around three cents, but the line
        // items behind it are fractions of a cent and a month's spend is their sum.
        builder.Property(c => c.CostUsd).HasPrecision(18, 8);

        // A read-only projection over the six token columns, not state of its own.
        builder.Ignore(c => c.TokenUsage);

        builder.HasIndex(c => new { c.TenantId, c.StartedAt });
        builder.HasIndex(c => c.RelatedOrderId);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Order>().WithMany().HasForeignKey(c => c.RelatedOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
