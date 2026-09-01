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
            ["AfricasTalking:Username"]  = "sandbox",
            ["AfricasTalking:ApiKey"]    = "test-api-key",
            ["AfricasTalking:SenderId"]  = "SpazaSure",
            ["AfricasTalking:Sandbox"]   = "true"
        };

        if (overrides is not null)
            foreach (var kv in overrides)
                defaults[kv.Key] = kv.Value;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }
}
