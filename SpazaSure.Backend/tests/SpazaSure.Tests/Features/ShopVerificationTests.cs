using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class ShopVerificationTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Theory]
    [InlineData("SS482913", "SS482913")]
    [InlineData("ss-482 913", "SS482913")]
    [InlineData("482913", "SS482913")]
    [InlineData("https://portal.spazasure.co.za/verify/SS482913", "SS482913")]
    [InlineData("SS4829", null)]
    [InlineData("hello", null)]
    [InlineData("", null)]
    public void Normalize_AcceptsTypedAndScannedShopNumbers(string input, string? expected)
    {
        ShopVerification.Normalize(input).Should().Be(expected);
    }

    private static List<ShopDocument> AllApproved(DateOnly? expiry = null) =>
    [
        new() { DocType = "business_permit", Status = "approved", ExpiryDate = expiry },
        new() { DocType = "health_certificate", Status = "approved" },
    ];

    private static SpazaShop Shop(string status = "verified", string? identityCheck = ShopVerification.IdentityApproved) =>
        new() { Status = status, IdentityCheckStatus = identityCheck };

    [Fact]
    public void VerifiedShopWithAllDocumentsApproved_GetsTheGreenSticker()
    {
        var result = ShopVerification.Evaluate(Shop(), AllApproved(new DateOnly(2027, 3, 1)), Today);

        result.Verified.Should().BeTrue();
        result.Outstanding.Should().BeEmpty();
        result.ValidUntil.Should().Be(new DateOnly(2027, 3, 1));
    }

    [Fact]
    public void ExpiredDocument_RemovesTheSticker()
    {
        var result = ShopVerification.Evaluate(Shop(), AllApproved(new DateOnly(2026, 1, 1)), Today);

        result.Verified.Should().BeFalse();
        result.Outstanding.Should().ContainSingle(o => o.Contains("expired"));
    }

    [Fact]
    public void ShopNotYetVerifiedByAdmin_IsNotVerified()
    {
        var result = ShopVerification.Evaluate(Shop("pending"), AllApproved(), Today);

        result.Verified.Should().BeFalse();
        result.Outstanding.Should().Contain("Shop verification by SpazaSure");
    }

    [Fact]
    public void MissingAndPendingDocuments_AreListed()
    {
        var docs = new List<ShopDocument> { new() { DocType = "business_permit", Status = "pending" } };

        var result = ShopVerification.Evaluate(Shop(), docs, Today);

        result.Verified.Should().BeFalse();
        result.Outstanding.Should().BeEquivalentTo(["Business permit (waiting for review)", "Health certificate"]);
    }

    [Theory]
    [InlineData(null, "Owner identity check")]
    [InlineData("In Progress", "Owner identity check")]
    [InlineData("In Review", "Owner identity check (being reviewed)")]
    [InlineData("Declined", "Owner identity check (not passed, try again)")]
    public void OwnerWithoutAnApprovedIdentityCheck_IsNotVerified(string? identityCheck, string expected)
    {
        var result = ShopVerification.Evaluate(Shop(identityCheck: identityCheck), AllApproved(), Today);

        result.Verified.Should().BeFalse();
        result.Outstanding.Should().Equal(expected);
    }

    [Fact]
    public void SuspendedShop_IsNeverVerified()
    {
        ShopVerification.Evaluate(Shop("suspended"), AllApproved(), Today).Verified.Should().BeFalse();
    }

    [Fact]
    public async Task PublicLookup_ShowsVerifiedShop_WithoutPrivateDetails()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var shop = new SpazaShop
        {
            UserId = Guid.NewGuid(), ShopName = "Mama Thandi's Spaza", OwnerName = "Thandi", OwnerIdNumber = "9001010000000",
            Phone = "0820009000", Address = "9 Test St", City = "Soweto", Province = "Gauteng", Status = "verified",
            ShopCode = "SS123456", IdentityCheckStatus = ShopVerification.IdentityApproved,
        };
        db.SpazaShops.Add(shop);
        foreach (var doc in AllApproved()) { doc.ShopId = shop.Id; db.ShopDocuments.Add(doc); }
        await db.SaveChangesAsync();

        var result = await new PublicShopVerificationController(db).Verify("ss 123-456");

        var json = JsonSerializer.Serialize(((OkObjectResult)result).Value);
        json.Should().Contain("\"verified\":true").And.Contain("Mama Thandi").And.Contain("Soweto, Gauteng");
        json.Should().NotContain("9001010000000").And.NotContain("0820009000").And.NotContain("9 Test St");
    }

    [Fact]
    public async Task PublicLookup_UnknownNumber_IsNotFound()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        (await new PublicShopVerificationController(db).Verify("SS999999")).Should().BeOfType<NotFoundObjectResult>();
        (await new PublicShopVerificationController(db).Verify("abc")).Should().BeOfType<BadRequestObjectResult>();
    }
}
