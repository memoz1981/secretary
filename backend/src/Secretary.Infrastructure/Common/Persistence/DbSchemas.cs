namespace Secretary.Infrastructure.Persistence;

/// <summary>Which database schema a table lives in.
///
/// A module owns its data outright — a schema per module rather than a discriminator column on
/// shared tables. The separation is then structural: there is no filter to forget, and
/// `app.Clients` and a future `inf.Clients` are simply different tables holding different
/// people.
///
/// Only what is genuinely tenant-level stays in the default schema: the tenant itself, the
/// accounts that sign in, and the record of which modules the tenant holds. Everything else
/// belongs to a module, including Clients, Calls and Escalations — a caller who books a haircut
/// and a caller who asks a question are the module's own, not the platform's.
///
/// The consequence, decided rather than stumbled into: the same person contacting two modules
/// is two rows in two tables. And when a second module ships, a call that touched both will
/// need a rule about which schema records it.</summary>
public static class DbSchemas
{
    /// <summary>Tenants, Accounts, TenantModules. Left as the provider's default so nothing
    /// tenant-level has to be qualified.</summary>
    public const string Common = "dbo";

    /// <summary>The Appointment module: appointments, providers, service offerings, and the
    /// clients, calls and escalations that belong to them.</summary>
    public const string Appointment = "app";

    /// <summary>The Orders module: products, units, orders, and its own customers with their
    /// phone numbers and delivery addresses. A customer here is not a client in "app" — the
    /// person who orders water and the person who books a haircut are different records, which
    /// is the whole reason a module owns a schema.</summary>
    public const string Orders = "ord";
}
