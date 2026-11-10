using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Shared;

public class JwtSecretTests
{
    private static IConfiguration Config(string? secret) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = secret }).Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("SpazaSure_JWT_Secret_Key_32Chars_Min!")]
    [InlineData("REPLACE_WITH_PROD_JWT_SECRET_MIN_32_CHARS_LONG")]
    [InlineData("change_me_prod_jwt_secret_min_32_chars_long")]
    public void Require_OutsideDevelopment_RejectsMissingWeakOrDefaultSecrets(string? secret)
    {
        var act = () => JwtSecret.Require(Config(secret), isDevelopment: false);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Require_OutsideDevelopment_AcceptsStrongSecret()
    {
        var strong = "q8Yf2nV0sLk3pR7tW1zX5cB9mH4jD6gA0eU2iO8yT3rE";
        JwtSecret.Require(Config(strong), isDevelopment: false).Should().Be(strong);
    }

    [Fact]
    public void Require_InDevelopment_AllowsTheCommittedDevSecret()
    {
        JwtSecret.Require(Config("SpazaSure_JWT_Secret_Key_32Chars_Min!"), isDevelopment: true)
            .Should().Be("SpazaSure_JWT_Secret_Key_32Chars_Min!");
    }
}
