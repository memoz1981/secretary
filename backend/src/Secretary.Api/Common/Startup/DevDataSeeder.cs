using Secretary.Application.Abstractions;
using Secretary.Application.Abstractions.Persistence;
using Secretary.Domain.Entities;
using NodaTime;

namespace Secretary.Api.Startup;

/// <summary>Creates the first platform-admin account on a database that has none.
///
/// Every controller requires authentication and there is no registration endpoint, so a freshly
/// migrated database is unreachable — there is no account to log in with and no way to make one.
/// The old solution never hit this because its admin was inserted by hand once and simply
/// persisted; that is not a thing a new clone, a new developer, or the PostgreSQL database at
/// deploy can rely on.
///
/// Two conditions, both required: Development only, and only when the Accounts table is empty.
/// It therefore cannot overwrite a real account, and cannot run in Staging or Production at all.
/// The credentials are in configuration (DevSeed:Email / DevSeed:Password) with the defaults
/// below — which are committed, and so are a development convenience, never a secret.</summary>
public static class DevDataSeeder
{
    public const string DefaultEmail = "admin@sekretar.az";
    public const string DefaultPassword = "Admin123!";

    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment, ILogger logger)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        using var scope = services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existing = await unitOfWork.Accounts.GetAllAsync(CancellationToken.None);
        if (existing.Count > 0)
        {
            return;
        }

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var email = configuration["DevSeed:Email"] ?? DefaultEmail;
        var password = configuration["DevSeed:Password"] ?? DefaultPassword;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        // Through the real hasher rather than a precomputed hash, so the seeded password stays
        // valid if the hashing parameters ever change.
        var admin = Account.CreatePlatformAdmin(
            "Platform Admin", email, hasher.Hash(password), clock.GetCurrentInstant());

        await unitOfWork.Accounts.AddAsync(admin, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        logger.LogWarning(
            "No accounts existed, so a development platform admin was created: {Email} (password from DevSeed:Password, default is in DevDataSeeder). Development only.",
            email);
    }
}
