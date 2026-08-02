using Secretary.Application.Abstractions;
using Secretary.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
            .Options;

        var context = new AppDbContext(options, new FixedTenantProvider(currentTenant));
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FixedTenantProvider : ICurrentTenantProvider
    {
        public FixedTenantProvider(int? tenantId) => TenantId = tenantId;
        public int? TenantId { get; }
    }
}
