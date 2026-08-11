using Secretary.Application.Abstractions;
using Secretary.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Secretary.Infrastructure.Tests.TestSupport;

/// <summary>An in-memory SQLite connection per test, kept open for the test's lifetime (a
/// SQLite in-memory DB disappears the moment its one connection closes) — gives real LINQ
/// translation, unlike a mocked DbSet.</summary>
public sealed class SqliteDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public AppDbContext CreateContext(int? currentTenant)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, SchemaFlatteningModelCustomizer>()
            .Options;

        var context = new AppDbContext(options, new FixedTenantProvider(currentTenant));
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>SQLite has no schemas, so EF drops them and two tables that differ only by
    /// schema collide — app.Calls and ord.Calls both try to be "Calls" and EnsureCreated fails
    /// on the second. Qualifying the name restores what the schema was doing.
    ///
    /// Only for these tests. Every real provider keeps the schemas, which is the whole point of
    /// having them: a module owns its tables.</summary>
    private sealed class SchemaFlatteningModelCustomizer : RelationalModelCustomizer
    {
        public SchemaFlatteningModelCustomizer(ModelCustomizerDependencies dependencies)
            : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var schema = entity.GetSchema();
                var table = entity.GetTableName();
                if (schema is not null && table is not null)
                {
                    entity.SetTableName($"{schema}_{table}");
                    entity.SetSchema(null);
                }
            }
        }
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FixedTenantProvider : ICurrentTenantProvider
    {
        public FixedTenantProvider(int? tenantId) => TenantId = tenantId;
        public int? TenantId { get; }
    }
}
