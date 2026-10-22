using FluentAssertions;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using Xunit;

namespace SpazaSure.Tests.Shared;

public class SupplierReachTests
{
    // Johannesburg CBD, Soweto (~13 km away), Pretoria (~55 km away), Durban
    private static SpazaShop SowetoShop(string? province = "Gauteng") =>
        new() { Province = province, Latitude = -26.2485, Longitude = 27.8540 };
    private static Supplier Supplier(string? province, double? lat, double? lng) =>
        new() { CompanyName = "Test", Province = province, Latitude = lat, Longitude = lng };

    [Fact]
    public void SupplierInTheSameProvince_AndWithinReach_CanServeTheShop() =>
        SupplierReach.CanServe(SowetoShop(), Supplier("Gauteng", -26.2041, 28.0473), 50m).Should().BeTrue();

    [Fact]
    public void SupplierTooFarAway_CannotServeTheShop_EvenInTheSameProvince() =>
        SupplierReach.CanServe(SowetoShop(), Supplier("Gauteng", -25.7479, 28.2293), 50m).Should().BeFalse();

    [Fact]
    public void SupplierInAnotherProvince_CannotServeTheShop_EvenWhenClose() =>
        // A supplier just over a provincial border: close on the map, but a different province
        SupplierReach.CanServe(SowetoShop(), Supplier("North West", -26.2041, 28.0473), 50m).Should().BeFalse();

    [Fact]
    public void WithoutAMapPosition_TheProvinceDecides()
    {
        SupplierReach.CanServe(SowetoShop(), Supplier("Gauteng", null, null), 50m).Should().BeTrue();
        SupplierReach.CanServe(SowetoShop(), Supplier("KwaZulu-Natal", null, null), 50m).Should().BeFalse();
    }

    [Fact]
    public void ProvinceNames_MatchRegardlessOfSpellingOrAbbreviation()
    {
        var durbanShop = new SpazaShop { Province = "KwaZulu-Natal" };
        SupplierReach.CanServe(durbanShop, Supplier("Kwazulu Natal", null, null), 50m).Should().BeTrue();
        SupplierReach.CanServe(durbanShop, Supplier("KZN", null, null), 50m).Should().BeTrue();
        SupplierReach.CanServe(SowetoShop("gauteng"), Supplier("GP", null, null), 50m).Should().BeTrue();
    }

    [Fact]
    public void TheReachDistance_IsTheAdminSetting() =>
        SupplierReach.CanServe(SowetoShop(), Supplier("Gauteng", -25.7479, 28.2293), 100m).Should().BeTrue();

    [Fact]
    public void DistanceKm_MatchesKnownDistance() =>
        // Johannesburg to Pretoria is roughly 54 km in a straight line
        SupplierReach.DistanceKm(-26.2041, 28.0473, -25.7479, 28.2293).Should().BeApproximately(54, 2);
}
