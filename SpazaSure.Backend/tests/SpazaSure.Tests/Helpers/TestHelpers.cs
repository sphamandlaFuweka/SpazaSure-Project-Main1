using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Tests.Helpers;

/// <summary>
/// Creates a fresh in-memory SpazaSureDbContext for each test.
/// </summary>
public static class DbContextHelper
{
    public static SpazaSureDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<SpazaSureDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new SpazaSureDbContext(options);
        SeedRoles(db);
        return db;
    }

    /// <summary>Seeds the three roles that production seeds on startup.</summary>
    private static void SeedRoles(SpazaSureDbContext db)
    {
        db.Roles.AddRange(
            new Role { Id = Guid.NewGuid(), Name = "supplier",    IsActive = true },
            new Role { Id = Guid.NewGuid(), Name = "spaza_owner", IsActive = true },
            new Role { Id = Guid.NewGuid(), Name = "admin",       IsActive = true }
        );
        db.SaveChanges();
    }
}

/// <summary>
/// Builds a minimal IConfiguration that mimics appsettings.json for tests.
/// </summary>
public static class ConfigHelper
{
    public static IConfiguration Build(Dictionary<string, string?>? overrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["Jwt:Secret"]               = "TestSecret_SpazaSure_32Characters!",
            ["Jwt:AccessExpiryMinutes"]  = "60",
            ["Jwt:RefreshExpiryDays"]    = "30",
        };

        if (overrides is not null)
            foreach (var kv in overrides)
                defaults[kv.Key] = kv.Value;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }
}

/// <summary>A Paystack transfers client with no key: automatic payouts off, nothing is sent.</summary>
public static class TestPaystack
{
    public static SpazaSure.OrderService.Services.PaystackTransferService Transfers() =>
        new(new HttpClient(), ConfigHelper.Build());
}
