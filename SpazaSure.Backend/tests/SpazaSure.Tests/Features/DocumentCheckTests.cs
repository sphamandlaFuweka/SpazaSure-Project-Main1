using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.ComplianceService.Controllers;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Features;

/// <summary>Uploaded documents are checked against the profile of the account that uploaded them.</summary>
public class DocumentCheckTests
{
    private static readonly DateOnly Today = new(2026, 10, 28);

    /// <summary>A valid SA ID number (Luhn check digit added) for a date of birth.</summary>
    private static string IdNumber(string yymmdd, string serial = "5009", string citizen = "0")
    {
        var body = yymmdd + serial + citizen + "8";
        for (var check = 0; check <= 9; check++)
            if (SaValidation.IsValidIdNumber(body + check)) return body + check;
        throw new InvalidOperationException();
    }

    private static DocumentReading Id(string name, string id) => new()
    {
        Readable = true, MatchesRequestedType = true, DocumentKind = "South African smart ID card",
        HolderName = name, IdNumber = id,
    };

    private static DocumentProfile ShopId(string owner, string? id) =>
        new("id_document", "ID document", [owner], ["Thandi's Spaza"], IdNumber: id);

    [Theory]
    [InlineData("THANDIWE PRECIOUS NKOSI", "Thandi Nkosi", true)]   // short form of the first name
    [InlineData("T P Nkosi", "Thandiwe Nkosi", true)]                // initials
    [InlineData("Nkosi, Thandiwe", "Thandiwe Nkosi", true)]          // surname first
    [InlineData("Sipho Dlamini", "Thandiwe Nkosi", false)]
    [InlineData("Thandiwe Dlamini", "Thandiwe Nkosi", false)]        // same first name, other person
    [InlineData("T P", "T P", false)]                                // initials alone prove nothing
    public void People_MatchAllowingInitialsAndShortNames(string onDocument, string onProfile, bool match) =>
        DocumentRules.NamesMatch(onDocument, onProfile).Should().Be(match);

    [Theory]
    [InlineData("BULK FOODS (PTY) LTD", "Bulk Foods", true)]
    [InlineData("Bulk Foods Distribution (Pty) Ltd", "Bulk Foods", true)]
    [InlineData("Mega Cash and Carry CC", "Bulk Foods", false)]
    public void Businesses_MatchIgnoringPtyLtd(string onDocument, string onProfile, bool match) =>
        DocumentRules.NamesMatch(onDocument, onProfile, business: true).Should().Be(match);

    [Fact]
    public void Id_MatchingTheProfile_Passes()
    {
        var id = IdNumber("900215");
        var v = DocumentRules.Evaluate(Id("Thandiwe Nkosi", id), ShopId("Thandiwe Nkosi", id), Today, null);
        v.Status.Should().Be("passed");
        v.DocumentNumber.Should().Be(id);
    }

    [Fact]
    public void SomeoneElsesId_Fails()
    {
        var mine = IdNumber("900215");
        var theirs = IdNumber("850630", "4123");
        DocumentRules.Evaluate(Id("Sipho Dlamini", theirs), ShopId("Thandiwe Nkosi", mine), Today, null)
            .Should().Match<DocumentVerdict>(v => v.Status == "failed" && v.Summary.Contains("Sipho Dlamini"));
        // Right name, wrong number: still not this owner's ID
        DocumentRules.Evaluate(Id("Thandiwe Nkosi", theirs), ShopId("Thandiwe Nkosi", mine), Today, null)
            .Findings.Should().Contain(f => f.Label == "ID number" && f.Status == "fail");
    }

    [Fact]
    public void MadeUpIdNumber_Fails()
    {
        var v = DocumentRules.Evaluate(Id("Thandiwe Nkosi", "9002155009081"), ShopId("Thandiwe Nkosi", null), Today, null);
        v.Findings.Should().Contain(f => f.Label == "ID number" && f.Status == "fail");
    }

    [Fact]
    public void IdAlreadyOnAnotherAccount_Fails()
    {
        var id = IdNumber("900215");
        var v = DocumentRules.Evaluate(Id("Thandiwe Nkosi", id), ShopId("Thandiwe Nkosi", id), Today, "shop Corner Spaza");
        v.Status.Should().Be("failed");
        v.Summary.Should().Contain("Corner Spaza");
    }

    [Fact]
    public void ExpiredLicence_FailsAndIsMarkedExpired()
    {
        var reading = new DocumentReading
        {
            Readable = true, MatchesRequestedType = true, HolderName = "Lerato Mokoena", ExpiryDate = "2025-03-31",
        };
        var v = DocumentRules.Evaluate(reading, new("licence", "driving licence", ["Lerato Mokoena"], []), Today, null);
        v.Status.Should().Be("failed");
        v.Expired.Should().BeTrue();
        v.Expiry.Should().Be(new DateOnly(2025, 3, 31));
    }

    [Fact]
    public void OldTaxClearance_WithoutAnExpiry_IsTooOld()
    {
        var reading = new DocumentReading
        {
            Readable = true, MatchesRequestedType = true, BusinessName = "Bulk Foods (Pty) Ltd", IssueDate = "2025-06-01",
        };
        var v = DocumentRules.Evaluate(reading, new("tax_clearance", "tax clearance", ["Musa"], ["Bulk Foods"]), Today, null);
        v.Expired.Should().BeTrue();
        v.Summary.Should().Contain("12 months");
    }

    [Fact]
    public void WrongKindOfDocument_Fails()
    {
        var reading = new DocumentReading
        {
            Readable = true, MatchesRequestedType = false, DocumentKind = "electricity bill", BusinessName = "Bulk Foods",
        };
        DocumentRules.Evaluate(reading, new("cipc_certificate", "CIPC certificate", ["Musa"], ["Bulk Foods"]), Today, null)
            .Summary.Should().Contain("electricity bill");
    }

    [Fact]
    public void CipcNumberMustBeTheProfiles()
    {
        var reading = new DocumentReading
        {
            Readable = true, MatchesRequestedType = true, BusinessName = "Bulk Foods (Pty) Ltd", RegistrationNumber = "2019 / 654321 / 07",
        };
        var profile = new DocumentProfile("cipc_certificate", "CIPC certificate", ["Musa"], ["Bulk Foods"], RegistrationNumber: "2020/123456/07");
        DocumentRules.Evaluate(reading, profile, Today, null).Findings
            .Should().Contain(f => f.Label == "Registration number" && f.Status == "fail");
    }

    [Fact]
    public void UnreadableUpload_Fails() =>
        DocumentRules.Evaluate(new DocumentReading { Readable = false }, ShopId("Thandiwe Nkosi", null), Today, null)
            .Status.Should().Be("failed");

    // ── The whole flow: upload checked, uploader told, admin approval gated ──

    private sealed class FakeReader(DocumentReading? reading)
        : DocumentReader(new ConfigurationBuilder().Build(), NullLogger<DocumentReader>.Instance)
    {
        public override Task<(DocumentReading? Reading, string? Unavailable)> ReadAsync(
            byte[] file, string fileName, string requested, CancellationToken ct = default) =>
            Task.FromResult(reading is null ? ((DocumentReading?)null, (string?)"off") : (reading, null));
    }

    private static (SpazaSureDbContext Db, SpazaShop Shop, ShopDocument Doc) Shop(string? ownerId)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var user = new User { Email = "doc@test.com", Phone = "0820001111", PasswordHash = "h", RoleId = db.Roles.First(r => r.Name == "spaza_owner").Id };
        var shop = new SpazaShop { UserId = user.Id, ShopName = "Thandi's Spaza", OwnerName = "Thandiwe Nkosi", OwnerIdNumber = ownerId, Phone = "0820001111", Address = "1 Main Rd",
            // Documents can only be approved once the owner has passed the Didit identity check
            IdentityCheckStatus = ShopVerification.IdentityApproved };
        var doc = new ShopDocument { ShopId = shop.Id, DocType = "id_document", DocUrl = "/uploads/shop-docs/x/id.jpg", Status = "pending", CheckStatus = "checking" };
        db.AddRange(user, shop, doc);
        db.SaveChanges();
        return (db, shop, doc);
    }

    private static DocumentChecks Checks(SpazaSureDbContext db, DocumentReading? reading) =>
        new(db, new FakeReader(reading), new EventPublisher("__nonexistent__", "x", "x"), NullLogger<DocumentChecks>.Instance);

    [Fact]
    public async Task SomeoneElsesId_IsFlagged_AndOnlyApprovedWithAReason()
    {
        var id = IdNumber("900215");
        var (db, _, doc) = Shop(id);
        await Checks(db, Id("Sipho Dlamini", IdNumber("850630", "4123"))).CheckShopDocumentAsync(doc.Id, [1], "id.jpg");

        doc.CheckStatus.Should().Be("failed");
        doc.Status.Should().Be("pending"); // flagged for review, not rejected outright
        var compliance = new ComplianceController(db, new EventPublisher("__nonexistent__", "x", "x"));
        (await compliance.Approve(doc.Id)).Should().BeOfType<ConflictObjectResult>();
        (await compliance.Approve(doc.Id, new ApproveRequest("Owner changed their name; marriage certificate seen")))
            .Should().BeOfType<OkObjectResult>();
        doc.Status.Should().Be("approved");
        doc.CheckSummary.Should().Contain("marriage certificate");
    }

    [Fact]
    public async Task ExpiredDocument_IsRejectedStraightAway()
    {
        var (db, _, doc) = Shop(null);
        doc.DocType = "business_permit";
        db.SaveChanges();
        var permit = new DocumentReading
        {
            Readable = true, MatchesRequestedType = true, BusinessName = "Thandi's Spaza", ExpiryDate = "2024-12-31",
        };
        await Checks(db, permit).CheckShopDocumentAsync(doc.Id, [1], "permit.pdf");

        doc.Status.Should().Be("rejected");
        doc.RejectionNote.Should().Contain("Expired");
        doc.ExpiryDate.Should().Be(new DateOnly(2024, 12, 31));
    }

    [Fact]
    public async Task SameIdOnAnotherShop_FailsTheCheck()
    {
        var id = IdNumber("900215");
        var (db, _, doc) = Shop(id);
        db.SpazaShops.Add(new SpazaShop { UserId = Guid.NewGuid(), ShopName = "Corner Spaza", OwnerName = "Thandiwe Nkosi", OwnerIdNumber = id, Phone = "0820002222", Address = "2 Side St" });
        db.SaveChanges();

        await Checks(db, Id("Thandiwe Nkosi", id)).CheckShopDocumentAsync(doc.Id, [1], "id.jpg");

        doc.CheckStatus.Should().Be("failed");
        doc.CheckSummary.Should().Contain("Corner Spaza");
    }

    [Fact]
    public async Task WhenTheReaderIsOff_AdminsReviewAsBefore()
    {
        var (db, _, doc) = Shop(null);
        await Checks(db, null).CheckShopDocumentAsync(doc.Id, [1], "id.jpg");

        doc.CheckStatus.Should().Be("unavailable");
        var compliance = new ComplianceController(db, new EventPublisher("__nonexistent__", "x", "x"));
        (await compliance.Approve(doc.Id)).Should().BeOfType<OkObjectResult>();
    }
}
