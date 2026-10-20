using FluentAssertions;
using SpazaSure.AuthService.DTOs;
using SpazaSure.AuthService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class SaValidationTests
{
    [Theory]
    [InlineData("8001015009087", true)]
    [InlineData("800101 5009 087", true)]
    [InlineData("9002150123088", true)]
    [InlineData("8001015009088", false)]  // wrong check digit
    [InlineData("8013015009087", false)]  // month 13
    [InlineData("8001015009387", false)]  // citizenship digit 3
    [InlineData("800101500908", false)]   // 12 digits
    [InlineData("80010150090871", false)] // 14 digits
    [InlineData("80010150O9087", false)]  // letter O
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IdNumber_IsCheckedForLengthDateAndCheckDigit(string? id, bool valid)
    {
        SaValidation.IsValidIdNumber(id).Should().Be(valid);
    }

    [Theory]
    [InlineData("082 123 4567", "+27821234567")]
    [InlineData("+27 82 123 4567", "+27821234567")]
    [InlineData("27821234567", "+27821234567")]
    [InlineData("(011) 555-1234", "+27115551234")]
    [InlineData("0821234567", "+27821234567")]
    [InlineData("082123456", null)]        // too short
    [InlineData("+44 20 7946 0958", null)] // not South African
    [InlineData("0921234567", null)]       // no SA numbers start with 09
    [InlineData("082-ABC-4567", null)]
    [InlineData("", null)]
    public void Phone_AcceptsCommonSouthAfricanForms(string phone, string? expected)
    {
        SaValidation.NormalizePhone(phone).Should().Be(expected);
    }

    [Theory]
    [InlineData("2020/123456/07", true)]
    [InlineData("1998/012345/23", true)]
    [InlineData("2020 / 123456 / 07", true)]
    [InlineData("2020/12345/07", false)]
    [InlineData("2020-123456-07", false)]
    [InlineData("1850/123456/07", false)]
    [InlineData("3020/123456/07", false)]
    [InlineData("", false)]
    public void CompanyRegistration_MustLookLikeACipcNumber(string registration, bool valid)
    {
        SaValidation.IsValidCompanyRegistration(registration).Should().Be(valid);
    }

    [Theory]
    [InlineData("+27820000002", null, "ID number")]           // ID missing
    [InlineData("+27820000002", "8001015009088", "ID number")] // ID with a wrong check digit
    [InlineData("+4420794609", "8001015009087", "mobile number")]
    public async Task ShopRegistration_WithAWrongIdOrPhone_IsRefused(string phone, string? idNumber, string mentions)
    {
        using var db = DbContextHelper.CreateInMemoryDb();
        var service = new ShopAuthService(db, ConfigHelper.Build());

        var result = await service.RegisterAsync(
            new ShopRegisterRequest(phone, "StrongPassword123", "Test Owner", "Test Shop", "1 Main Street", idNumber),
            "127.0.0.1");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain(mentions);
        db.SpazaShops.Should().BeEmpty();
    }
}
