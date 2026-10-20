using FluentAssertions;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Shared;

public class PrivateFilesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private const string IdDocument = "/uploads/shop-docs/2b9f/id_document_20261020120000.pdf";

    public PrivateFilesTests() => PrivateFiles.Configure("unit-test-jwt-secret");

    private static (string Path, string? Exp, string? Sig) Split(string link)
    {
        var parts = link.Split('?');
        var query = parts.Length > 1
            ? parts[1].Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1])
            : new Dictionary<string, string>();
        return (parts[0], query.GetValueOrDefault("exp"), query.GetValueOrDefault("sig"));
    }

    [Fact]
    public void SignedLink_OpensThePrivateFile_ForThirtyMinutes()
    {
        var (path, exp, sig) = Split(PrivateFiles.Link(IdDocument, Now)!);

        PrivateFiles.IsAllowed(path, exp, sig, Now.AddMinutes(29)).Should().BeTrue();
        PrivateFiles.IsAllowed(path, exp, sig, Now.AddMinutes(31)).Should().BeFalse();
    }

    [Theory]
    [InlineData("/uploads/shop-docs/2b9f/id_document.pdf")]
    [InlineData("/uploads/supplier-docs/77aa/cipc_certificate.pdf")]
    [InlineData("/compliance-uploads/report-photos/u1/photo.jpg")]
    [InlineData("/uploads//shop-docs/2b9f/id_document.pdf")]   // doubled slash
    [InlineData("/UPLOADS/Shop-Docs/2b9f/id_document.pdf")]   // other casing
    public void PrivateFile_WithoutASignature_IsRefused(string path)
    {
        PrivateFiles.IsAllowed(path, null, null, Now).Should().BeFalse();
    }

    [Fact]
    public void Signature_ForOneFile_DoesNotOpenAnother()
    {
        var (_, exp, sig) = Split(PrivateFiles.Link(IdDocument, Now)!);

        PrivateFiles.IsAllowed("/uploads/shop-docs/other-shop/id_document_20261020120000.pdf", exp, sig, Now).Should().BeFalse();
    }

    [Fact]
    public void ChangedExpiryOrSignature_IsRefused()
    {
        var (path, exp, sig) = Split(PrivateFiles.Link(IdDocument, Now)!);
        var later = (long.Parse(exp!) + 86_400).ToString();

        PrivateFiles.IsAllowed(path, later, sig, Now).Should().BeFalse();
        PrivateFiles.IsAllowed(path, exp, new string('0', sig!.Length), Now).Should().BeFalse();
        PrivateFiles.IsAllowed(path, exp, "not-hex", Now).Should().BeFalse();
    }

    [Theory]
    [InlineData("/uploads/products/abc.png")]
    [InlineData("/uploads/supplier-logos/abc.png")]
    [InlineData("/uploads/profile-photos/abc.jpg")]
    public void PublicFiles_StayPublic_AndAreNotSigned(string path)
    {
        PrivateFiles.IsAllowed(path, null, null, Now).Should().BeTrue();
        PrivateFiles.Link(path, Now).Should().Be(path);
    }

    [Fact]
    public void ExternalLinks_AreLeftAlone_AndOldSignaturesAreReplaced()
    {
        PrivateFiles.Link("https://bucket.example/shop-docs/x.pdf", Now).Should().Be("https://bucket.example/shop-docs/x.pdf");
        PrivateFiles.Link(null, Now).Should().BeNull();

        var resigned = PrivateFiles.Link(PrivateFiles.Link(IdDocument, Now), Now.AddMinutes(10))!;
        resigned.Should().StartWith(IdDocument + "?exp=");
        resigned.Split('?').Should().HaveCount(2);
    }
}
