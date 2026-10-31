using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using SpazaSureUserService.Controllers;
using SpazaSure.UserService.Services;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>Shoppers find a shop by typing or scanning its sticker's number, or by name or city.</summary>
public class CustomerShopSearchTests
{
    private static async Task<string> Search(string term)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        db.SpazaShops.AddRange(
            new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Mama's Spaza", OwnerName = "T", Phone = "0820000001", Address = "1 Rd", City = "Soweto", ShopCode = "SS482913", Status = "verified" },
            new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Corner Shop", OwnerName = "N", Phone = "0820000002", Address = "2 Rd", City = "Durban", ShopCode = "SS111222", Status = "verified" });
        db.SaveChanges();
        var result = await new CustomerShopsController(db, new AddressGeocodingSignal()).GetShops(term);
        return System.Text.Json.JsonSerializer.Serialize(((ObjectResult)result).Value);
    }

    [Theory]
    [InlineData("SS482913")]
    [InlineData("ss 482 913")]
    [InlineData("482913")]
    [InlineData("https://portal.spazasure.co.za/verify/SS482913")] // what the sticker's QR code holds
    public async Task AShopNumber_HoweverEntered_FindsThatShopOnly(string term)
    {
        var json = await Search(term);

        json.Should().Contain("Mama").And.NotContain("Corner Shop");
    }

    [Fact]
    public async Task NamesAndCities_AreFound_WhateverTheCapitals()
    {
        (await Search("mama")).Should().Contain("Mama");
        (await Search("DURBAN")).Should().Contain("Corner Shop");
    }
}
