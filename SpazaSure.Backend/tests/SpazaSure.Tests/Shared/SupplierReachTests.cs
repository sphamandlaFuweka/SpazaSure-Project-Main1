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

    // A Cape Town supplier with warehouses in Durban and Johannesburg
    private static Supplier CapeTownSupplierWithBranches() => new()
    {
        CompanyName = "Test", Province = "Western Cape", City = "Cape Town", Latitude = -33.9249, Longitude = 18.4241,
        Branches =
        [
            new SupplierBranch { Name = "Durban warehouse", City = "Durban", Province = "KwaZulu-Natal", Latitude = -29.8587, Longitude = 31.0218 },
            new SupplierBranch { Name = "JHB warehouse", City = "Johannesburg", Province = "Gauteng", Latitude = -26.2041, Longitude = 28.0473 },
        ],
    };

    [Fact]
    public void ABranchNearTheShop_LetsTheSupplierServeIt_EvenWhenTheHeadOfficeIsFar()
    {
        var supplier = CapeTownSupplierWithBranches();
        SupplierReach.CanServe(SowetoShop(), supplier, 50m).Should().BeTrue();
        SupplierReach.NearestServing(SowetoShop(), supplier, 50m)!.Name.Should().Be("JHB warehouse");
    }

    [Fact]
    public void APausedBranch_ServesNoShops()
    {
        var supplier = CapeTownSupplierWithBranches();
        foreach (var branch in supplier.Branches) branch.IsActive = false;
        SupplierReach.CanServe(SowetoShop(), supplier, 50m).Should().BeFalse();
    }

    [Fact]
    public void AShopFarFromEveryBranch_IsNotServed()
    {
        // Polokwane, Limpopo
        var polokwaneShop = new SpazaShop { Province = "Limpopo", Latitude = -23.9045, Longitude = 29.4689 };
        SupplierReach.CanServe(polokwaneShop, CapeTownSupplierWithBranches(), 50m).Should().BeFalse();
    }

    private static GroupBuy DurbanGroupBuy(decimal radiusKm = 40m) => new()
    {
        Title = "Durban deal", AreaCity = "Durban", AreaProvince = "KwaZulu-Natal",
        AreaLatitude = -29.8587, AreaLongitude = 31.0218, AreaRadiusKm = radiusKm,
        Supplier = CapeTownSupplierWithBranches(),
    };

    [Fact]
    public void GroupBuyArea_LetsInShopsWithinTheRadius()
    {
        // Umlazi is about 20 km from central Durban
        var umlaziShop = new SpazaShop { Province = "KwaZulu-Natal", City = "Durban", Latitude = -29.9700, Longitude = 30.8800 };
        GroupBuyArea.Contains(DurbanGroupBuy(), umlaziShop, 50m).Should().BeTrue();
    }

    [Fact]
    public void GroupBuyArea_KeepsOutShopsBeyondTheRadius_EvenInTheSameProvince()
    {
        // Pietermaritzburg is about 70 km from Durban
        var pmbShop = new SpazaShop { Province = "KwaZulu-Natal", City = "Pietermaritzburg", Latitude = -29.6006, Longitude = 30.3794 };
        GroupBuyArea.Contains(DurbanGroupBuy(), pmbShop, 50m).Should().BeFalse();
        GroupBuyArea.Contains(DurbanGroupBuy(100m), pmbShop, 50m).Should().BeTrue();
    }

    [Fact]
    public void GroupBuyArea_KeepsOutShopsInOtherCities_ThatTheSupplierServesFromOtherBranches() =>
        // The supplier delivers to Soweto from JHB, but this campaign is for Durban shops only
        GroupBuyArea.Contains(DurbanGroupBuy(), SowetoShop(), 50m).Should().BeFalse();

    [Fact]
    public void GroupBuyArea_WithoutAMapPosition_TheCityDecides()
    {
        GroupBuyArea.Contains(DurbanGroupBuy(), new SpazaShop { Province = "KZN", City = "durban" }, 50m).Should().BeTrue();
        GroupBuyArea.Contains(DurbanGroupBuy(), new SpazaShop { Province = "KZN", City = "Pinetown" }, 50m).Should().BeFalse();
        GroupBuyArea.Contains(DurbanGroupBuy(), new SpazaShop { Province = "KZN" }, 50m).Should().BeFalse();
    }

    [Fact]
    public void GroupBuysFromBeforeAreas_UseTheSuppliersReach()
    {
        var legacy = new GroupBuy { Title = "Old", Supplier = CapeTownSupplierWithBranches() };
        GroupBuyArea.Contains(legacy, SowetoShop(), 50m).Should().BeTrue();
    }

    [Fact]
    public void GroupBuyArea_DescribesWhereItIsOpen() =>
        GroupBuyArea.Describe(DurbanGroupBuy()).Should().Be("shops within 40 km of Durban, KwaZulu-Natal");
}
