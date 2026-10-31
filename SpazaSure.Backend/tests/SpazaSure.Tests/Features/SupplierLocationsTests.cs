using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>The Shops Map shows every place suppliers deliver from that is on the map.</summary>
public class SupplierLocationsTests
{
    [Fact]
    public async Task HeadOfficesAndActiveBranchesOnTheMap_AreListed()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var supplier = new Supplier
        {
            UserId = Guid.NewGuid(), CompanyName = "Bulk Co", ContactPerson = "S", Phone = "0821000000", Email = "s@test.com",
            Status = "verified", City = "Johannesburg", Latitude = -26.2041, Longitude = 28.0473,
        };
        var noPin = new Supplier { UserId = Guid.NewGuid(), CompanyName = "No Pin Ltd", ContactPerson = "N", Phone = "0821000001", Email = "n@test.com", Status = "verified" };
        db.AddRange(supplier, noPin,
            new SupplierBranch { SupplierId = supplier.Id, Name = "Durban warehouse", City = "Durban", Latitude = -29.8587, Longitude = 31.0218 },
            new SupplierBranch { SupplierId = supplier.Id, Name = "Old depot", City = "Pretoria", Latitude = -25.74, Longitude = 28.18, IsActive = false });
        db.SaveChanges();

        var result = await new AdminController(db, null!, new EventPublisher("__nonexistent__", "x", "x")).SupplierLocations();

        var json = System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value);
        json.Should().Contain("Head office").And.Contain("Durban warehouse");
        json.Should().NotContain("Old depot").And.NotContain("No Pin Ltd"); // inactive, and not on the map
    }
}
