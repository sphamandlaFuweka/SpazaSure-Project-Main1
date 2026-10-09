using FluentAssertions;
using SpazaSure.ProductService.Services;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class AddressValidationTests
{
    [Fact]
    public void Missing_coordinates_are_allowed() =>
        AddressValidation.ValidateCoordinates(null, null).Should().BeNull();

    [Fact]
    public void Johannesburg_is_valid() =>
        AddressValidation.ValidateCoordinates(-26.2041, 28.0473).Should().BeNull();

    [Fact]
    public void Only_one_coordinate_is_rejected() =>
        AddressValidation.ValidateCoordinates(-26.2, null).Should().NotBeNull();

    [Fact]
    public void Outside_south_africa_is_rejected() =>
        AddressValidation.ValidateCoordinates(51.5, -0.12).Should().NotBeNull();
}

public class UnitCodeServiceTests
{
    [Fact]
    public void Codes_have_the_expected_shape_and_are_unique()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => UnitCodeService.NewCode()).ToList();
        codes.Should().OnlyContain(c => c.StartsWith("SZ") && c.Length == 16);
        codes.Distinct().Count().Should().Be(codes.Count);
    }

    [Fact]
    public void Pin_matches_after_normalising_dashes_case_and_lookalikes()
    {
        var pin = "ABCD1234EFGH";
        var hash = UnitCodeService.HashPin(pin, "secret");

        UnitCodeService.PinMatches(UnitCodeService.NormalizePin("abcd-1234-efgh"), hash, "secret").Should().BeTrue();
        UnitCodeService.PinMatches(UnitCodeService.NormalizePin("ABCD-1234-EFGI"), hash, "secret").Should().BeFalse();
        UnitCodeService.NormalizePin("O0Il").Should().Be("0011");
    }

    [Fact]
    public void Hash_depends_on_the_secret()
    {
        UnitCodeService.HashPin("ABCD1234EFGH", "a").Should().NotBe(UnitCodeService.HashPin("ABCD1234EFGH", "b"));
    }

    [Fact]
    public void Formatted_pin_has_three_groups()
    {
        UnitCodeService.FormatPin(UnitCodeService.NewPin()).Split('-').Should().HaveCount(3);
    }
}

public class VerificationRiskEngineTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);

    private static VerificationInput Registered(Action<VerificationInputBuilder>? tweak = null)
    {
        var b = new VerificationInputBuilder();
        tweak?.Invoke(b);
        return b.Build();
    }

    private sealed class VerificationInputBuilder
    {
        public string Code = "4006381333931";
        public bool InRegistry = true;
        public bool Recalled;
        public DateOnly? Expiry;
        public bool Clone;
        public string Pin = "none";

        public VerificationInput Build() => new()
        {
            Code = Code,
            InSpazaSureRegistry = InRegistry,
            InGlobalDatabase = true,
            SupplierVerified = true,
            Recalled = Recalled,
            ExpiryDate = Expiry,
            CloneSuspected = Clone,
            PinStatus = Pin,
        };
    }

    [Fact]
    public void Clean_registered_product_is_low_risk()
    {
        var r = VerificationRiskEngine.Evaluate(Registered(), Today);
        r.Level.Should().Be("low");
    }

    [Fact]
    public void Expired_product_is_flagged()
    {
        var r = VerificationRiskEngine.Evaluate(Registered(b => b.Expiry = Today.AddDays(-3)), Today);
        r.Indicators.Should().Contain(i => i.Contains("passed"));
        r.Score.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void Invalid_check_digit_is_flagged()
    {
        var r = VerificationRiskEngine.Evaluate(Registered(b => b.Code = "4006381333932"), Today);
        r.Checks.Should().Contain(c => c.Label == "Barcode check digit" && c.Status == "fail");
    }

    [Fact]
    public void Recalled_product_is_high_risk()
    {
        var r = VerificationRiskEngine.Evaluate(Registered(b => b.Recalled = true), Today);
        r.Level.Should().BeOneOf("suspicious", "high");
    }

    [Fact]
    public void Reused_pin_is_high_risk_and_valid_pin_passes()
    {
        VerificationRiskEngine.Evaluate(Registered(b => b.Pin = "reused"), Today).Score.Should().BeGreaterThanOrEqualTo(55);
        VerificationRiskEngine.Evaluate(Registered(b => b.Pin = "valid"), Today)
            .Checks.Should().Contain(c => c.Label == "Scratch-off PIN" && c.Status == "pass");
    }

    [Fact]
    public void Cloned_scan_adds_risk()
    {
        var clean = VerificationRiskEngine.Evaluate(Registered(), Today).Score;
        var cloned = VerificationRiskEngine.Evaluate(Registered(b => b.Clone = true), Today).Score;
        cloned.Should().BeGreaterThan(clean);
    }
}
