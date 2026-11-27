using FluentAssertions;
using SpazaSure.ProductService.Services;
using Xunit;

namespace SpazaSure.Tests.Products;

public class PackagingVisionServiceTests
{
    [Fact]
    public void RequiresOpusReview_WhenFirstPassIsClearAndPositive_ReturnsFalse()
    {
        PackagingVisionService.RequiresOpusReview(new PackagingAnalysis
        {
            ImageQuality = "good",
            Verdict = "looks_genuine",
        }).Should().BeFalse();
    }

    [Theory]
    [InlineData("good", "some_concerns")]
    [InlineData("good", "likely_counterfeit")]
    [InlineData("good", "cannot_tell")]
    [InlineData("blurry", "looks_genuine")]
    [InlineData("partial", "looks_genuine")]
    public void RequiresOpusReview_WhenFirstPassIsUncertainOrConcerning_ReturnsTrue(
        string imageQuality, string verdict)
    {
        PackagingVisionService.RequiresOpusReview(new PackagingAnalysis
        {
            ImageQuality = imageQuality,
            Verdict = verdict,
        }).Should().BeTrue();
    }

    [Fact]
    public void RequiresOpusReview_WhenFirstPassHasRedFlags_ReturnsTrue()
    {
        PackagingVisionService.RequiresOpusReview(new PackagingAnalysis
        {
            ImageQuality = "good",
            Verdict = "looks_genuine",
            RedFlags = ["Different-looking batch mark"],
        }).Should().BeTrue();
    }

    [Fact]
    public void PreferMoreCautiousResult_DoesNotDowngradeSonnetConcern()
    {
        var sonnet = new PackagingAnalysis { Verdict = "some_concerns" };
        var opus = new PackagingAnalysis { Verdict = "looks_genuine" };

        PackagingVisionService.PreferMoreCautiousResult(sonnet, opus).Should().BeSameAs(sonnet);
    }

    [Fact]
    public void PreferMoreCautiousResult_UsesOpusWhenItIsMoreCautious()
    {
        var sonnet = new PackagingAnalysis { Verdict = "looks_genuine" };
        var opus = new PackagingAnalysis { Verdict = "some_concerns" };

        PackagingVisionService.PreferMoreCautiousResult(sonnet, opus).Should().BeSameAs(opus);
    }

    [Fact]
    public void PreferMoreCautiousResult_DoesNotDropSonnetRedFlags()
    {
        var sonnet = new PackagingAnalysis { Verdict = "looks_genuine", RedFlags = ["Odd batch code"] };
        var opus = new PackagingAnalysis { Verdict = "looks_genuine" };

        var result = PackagingVisionService.PreferMoreCautiousResult(sonnet, opus);
        result.Verdict.Should().Be("some_concerns");
        result.RedFlags.Should().Contain("Odd batch code");
    }

    [Fact]
    public void EstimateCostUsd_UsesSonnetRates()
    {
        PackagingVisionService.EstimateCostUsd("claude-sonnet-5-5", 1_000_000, 100_000)
            .Should().Be(3m);
    }

    [Fact]
    public void EstimateCostUsd_UsesOpusRates()
    {
        PackagingVisionService.EstimateCostUsd("claude-opus-5-5", 1_000_000, 100_000)
            .Should().Be(6m);
    }

    [Fact]
    public void EstimateCostUsd_ReturnsNullForUnknownModels()
    {
        PackagingVisionService.EstimateCostUsd("unknown-model", 100, 50).Should().BeNull();
    }
}
