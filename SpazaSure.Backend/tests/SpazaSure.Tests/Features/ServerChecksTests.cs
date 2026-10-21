using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SpazaSure.ComplianceService.Services;
using SpazaSure.Infrastructure.Entities;
using Xunit;

namespace SpazaSure.Tests.Features;

public class ServerChecksTests
{
    private static DiditService Didit(string companiesJson, string amlStatus = "Approved", int amlHits = 0)
    {
        var handler = new FakeDidit(request => request.RequestUri!.AbsolutePath switch
        {
            "/v3/kyb/search/" => """{"request_id":"r1","kyb_registry":{"search_resolved":true,"companies":""" + companiesJson + "}}",
            "/v3/aml/" => """{"request_id":"r2","aml":{"status":""" + $"\"{amlStatus}\",\"total_hits\":{amlHits},\"score\":{(amlHits > 0 ? 88 : 0)},\"hits\":[]}}}}",
            _ => throw new InvalidOperationException(request.RequestUri.ToString()),
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Didit:ApiKey"] = "k" }).Build();
        return new DiditService(new HttpClient(handler), config);
    }

    private static Supplier Supplier(string name = "Fresh Foods SA (Pty) Ltd") =>
        new() { CompanyName = name, RegistrationNumber = "2020/123456/07" };

    private const string Match = """[{"name":"FRESH FOODS SA PROPRIETARY LIMITED","registration_number":"2020/123456/07","status":"In Business","type":"Private Company"}]""";

    [Fact]
    public async Task ActiveCompanyWithTheSameNumber_AndClearScreening_IsApproved()
    {
        var outcome = await ServerChecks.CheckBusinessAsync(Didit(Match), Supplier(), sandbox: false, CancellationToken.None);

        outcome.Status.Should().Be("Approved");
        outcome.Notes.Should().Contain("Found on the CIPC register").And.Contain("screening: clear");
    }

    [Fact]
    public async Task SandboxCheck_ShowsProfileBeingChecked_NotSyntheticRegistryData()
    {
        const string sandboxCompany = """[{"name":"Sandbox Holdings Ltd","registration_number":"2025/717375/07","status":"Active"}]""";

        var outcome = await ServerChecks.CheckBusinessAsync(Didit(sandboxCompany), Supplier(), sandbox: true, CancellationToken.None);

        outcome.Status.Should().Be("In Review");
        outcome.Notes.Should().Contain("Fresh Foods SA (Pty) Ltd")
            .And.Contain("2020/123456/07")
            .And.Contain("live Didit credentials");
        outcome.Notes.Should().NotContain("Sandbox Holdings")
            .And.NotContain("screening: clear");
    }

    [Fact]
    public async Task NoCompanyWithThatNumber_IsDeclined()
    {
        var other = """[{"name":"Other Co","registration_number":"2019/654321/07","status":"In Business"}]""";

        var outcome = await ServerChecks.CheckBusinessAsync(Didit(other), Supplier(), sandbox: false, CancellationToken.None);

        outcome.Status.Should().Be("Declined");
        outcome.Notes.Should().Contain("No company with registration number 2020/123456/07");
    }

    [Fact]
    public async Task DeregisteredCompany_IsDeclined()
    {
        var deregistered = Match.Replace("In Business", "Final Deregistration");

        (await ServerChecks.CheckBusinessAsync(Didit(deregistered), Supplier(), false, CancellationToken.None)).Status.Should().Be("Declined");
    }

    [Fact]
    public async Task DifferentRegisteredName_GoesToReview()
    {
        var outcome = await ServerChecks.CheckBusinessAsync(Didit(Match), Supplier("Thabo's Wholesale"), false, CancellationToken.None);

        outcome.Status.Should().Be("In Review");
        outcome.Notes.Should().Contain("different from the name on the profile");
    }

    [Theory]
    [InlineData("Declined", "Declined")]
    [InlineData("In Review", "In Review")]
    public async Task SanctionsMatch_DeclinesOrGoesToReview(string amlStatus, string expected)
    {
        var outcome = await ServerChecks.CheckBusinessAsync(Didit(Match, amlStatus, amlHits: 1), Supplier(), false, CancellationToken.None);

        outcome.Status.Should().Be(expected);
        outcome.Notes.Should().Contain("sanctions or watch");
    }

    [Fact]
    public async Task OwnerSeenInPerson_WithClearScreening_IsApproved_AndRecordsWhoChecked()
    {
        var shop = new SpazaShop { OwnerName = "Thandi Mokoena", OwnerIdNumber = "8001015009087" };

        var outcome = await ServerChecks.CheckOwnerInPersonAsync(Didit("[]"), shop, "Sipho (admin)", CancellationToken.None);

        outcome.Status.Should().Be("Approved");
        outcome.Notes.Should().Contain("Original ID seen in person by Sipho (admin)");
    }

    [Theory]
    [InlineData("8001015009087", "1980-01-01")]
    [InlineData("0503125009087", "2005-03-12")]
    [InlineData("8001015009088", null)]
    public void DateOfBirth_IsReadFromTheIdNumber(string id, string? expected)
    {
        ServerChecks.DateOfBirth(id).Should().Be(expected);
    }

    [Theory]
    [InlineData("FRESH FOODS SA PROPRIETARY LIMITED", "Fresh Foods SA (Pty) Ltd", true)]
    [InlineData("Mama's Spaza CC", "mamas spaza", true)]
    [InlineData("Fresh Foods SA (Pty) Ltd", "Fresh Fruit SA (Pty) Ltd", false)]
    [InlineData(null, "Anything", false)]
    public void CompanyNames_AreComparedWithoutSuffixesAndPunctuation(string? registered, string profile, bool same)
    {
        ServerChecks.SameCompanyName(registered, profile).Should().Be(same);
    }

    private sealed class FakeDidit(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(respond(request), Encoding.UTF8, "application/json"),
            });
    }
}
