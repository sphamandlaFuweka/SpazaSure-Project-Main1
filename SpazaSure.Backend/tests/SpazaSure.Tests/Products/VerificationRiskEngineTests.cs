using FluentAssertions;
using SpazaSure.ProductService.Services;
using Xunit;

namespace SpazaSure.Tests.Products;

public class VerificationRiskEngineTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static RiskInput Registered(
        bool recalled = false, DateOnly? expiry = null, bool? nameMatches = null, int reports = 0) =>
        new(true, false, recalled, expiry, nameMatches, reports);

    [Fact]
    public void RegisteredProduct_WithNoConcerns_IsLowRisk()
    {
        var result = VerificationRiskEngine.Assess(Registered(expiry: Today.AddDays(200)), Today);

        result.Level.Should().Be("low");
        result.Score.Should().Be(0);
        result.Indicators.Should().BeEmpty();
    }

    [Fact]
    public void RecalledProduct_IsHighRisk()
    {
        var result = VerificationRiskEngine.Assess(Registered(recalled: true), Today);

        result.Level.Should().Be("high");
        result.Indicators.Should().Contain(i => i.Code == "recalled");
    }

    [Fact]
    public void ExpiredProduct_IsSuspicious()
    {
        var result = VerificationRiskEngine.Assess(Registered(expiry: Today.AddDays(-1)), Today);

        result.Level.Should().Be("suspicious");
        result.Indicators.Should().Contain(i => i.Code == "expired");
    }

    [Fact]
    public void UnknownProduct_IsReviewNotHighRisk()
    {
        var result = VerificationRiskEngine.Assess(new RiskInput(false, false, false, null, null, 0), Today);

        result.Level.Should().Be("review");
        result.Indicators.Should().Contain(i => i.Code == "unknown_product");
    }

    [Fact]
    public void ProductOnlyInOpenFoodFacts_IsLowRisk()
    {
        var result = VerificationRiskEngine.Assess(new RiskInput(false, true, false, null, null, 0), Today);

        result.Level.Should().Be("low");
    }

    [Fact]
    public void NameMismatchWithPriorReports_RaisesRisk()
    {
        var result = VerificationRiskEngine.Assess(Registered(nameMatches: false, reports: 3), Today);

        result.Score.Should().Be(50);
        result.Level.Should().Be("review");
        result.Indicators.Should().Contain(i => i.Code == "name_mismatch");
        result.Indicators.Should().Contain(i => i.Code == "prior_reports");
    }

    [Fact]
    public void Thresholds_AreConfigurable()
    {
        var result = VerificationRiskEngine.Assess(
            Registered(reports: 1), Today, new RiskThresholds(Review: 5, Suspicious: 8, High: 20));

        result.Score.Should().Be(10);
        result.Level.Should().Be("suspicious");
    }

    [Theory]
    [InlineData("Coke 2L", "Coke", true)]
    [InlineData("Coke", "coke 2l", true)]
    [InlineData("Coke", "Fanta", false)]
    [InlineData("Coke", "", true)]
    public void NamesMatch_ComparesCaseInsensitively(string registered, string entered, bool expected) =>
        VerificationRiskEngine.NamesMatch(registered, entered).Should().Be(expected);

    [Fact]
    public void IdentityConfidence_CountsRegisteredNameWordsFoundInPackagingText()
    {
        var full = VerificationRiskEngine.IdentityConfidence("Coca-Cola Original 2L", "COCA COLA ORIGINAL TASTE 2 LITRES");
        var none = VerificationRiskEngine.IdentityConfidence("Coca-Cola Original 2L", "Sunflower cooking oil 750ml pack");

        full.Should().Be(1.0);
        none.Should().Be(0.0);
    }

    [Fact]
    public void IdentityConfidence_WithTooLittleText_IsInconclusive() =>
        VerificationRiskEngine.IdentityConfidence("Coca-Cola", "cola").Should().BeNull();
}
