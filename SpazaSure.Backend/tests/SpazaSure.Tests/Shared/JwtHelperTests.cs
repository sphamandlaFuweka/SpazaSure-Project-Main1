using FluentAssertions;
using SpazaSure.Shared.Helpers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace SpazaSure.Tests.Shared;

public class JwtHelperTests
{
    private const string Secret = "TestSecret_SpazaSure_32Characters!";

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        var token = JwtHelper.GenerateAccessToken(
            "user-123", "supplier", new[] { "products:read" }, Secret, 60);

        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void GenerateAccessToken_ContainsCorrectSubClaim()
    {
        var userId = Guid.NewGuid().ToString();
        var token  = JwtHelper.GenerateAccessToken(userId, "supplier", Array.Empty<string>(), Secret, 60);
        var jwt    = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Subject.Should().Be(userId);
    }

    [Fact]
    public void GenerateAccessToken_ContainsRoleClaim()
    {
        var token = JwtHelper.GenerateAccessToken("u1", "admin", Array.Empty<string>(), Secret, 60);
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "admin");
    }

    [Fact]
    public void GenerateAccessToken_ContainsPermissionClaims()
    {
        var permissions = new[] { "products:read", "orders:write" };
        var token = JwtHelper.GenerateAccessToken("u1", "supplier", permissions, Secret, 60);
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var permClaims = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value);
        permClaims.Should().BeEquivalentTo(permissions);
    }

    [Fact]
    public void GenerateAccessToken_RespectsExpiryMinutes()
    {
        var before = DateTime.UtcNow.AddMinutes(29);
        var token  = JwtHelper.GenerateAccessToken("u1", "supplier", Array.Empty<string>(), Secret, 30);
        var after  = DateTime.UtcNow.AddMinutes(31);
        var jwt    = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.ValidTo.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public void GenerateRefreshToken_Returns88CharBase64String()
    {
        var token = JwtHelper.GenerateRefreshToken();

        token.Should().NotBeNullOrEmpty();
        // 64 bytes base64-encoded = 88 chars
        token.Length.Should().Be(88);
    }

    [Fact]
    public void GenerateRefreshToken_IsUniqueOnEachCall()
    {
        var t1 = JwtHelper.GenerateRefreshToken();
        var t2 = JwtHelper.GenerateRefreshToken();

        t1.Should().NotBe(t2);
    }

    [Fact]
    public void HashToken_SameInput_ReturnsSameHash()
    {
        var input = "some-token-value";
        JwtHelper.HashToken(input).Should().Be(JwtHelper.HashToken(input));
    }

    [Fact]
    public void HashToken_DifferentInputs_ReturnDifferentHashes()
    {
        JwtHelper.HashToken("abc").Should().NotBe(JwtHelper.HashToken("xyz"));
    }

    [Fact]
    public void HashToken_ReturnsLowercaseHexString()
    {
        var hash = JwtHelper.HashToken("test");
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }
}
