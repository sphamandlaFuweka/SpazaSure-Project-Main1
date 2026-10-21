using FluentAssertions;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Shared;

public class PaymentEmailTests
{
    private static readonly Guid UserId = Guid.Parse("8f2b0c1e-0000-4000-8000-000000000001");

    [Fact]
    public void AShopsOwnEmail_IsUsed()
    {
        PaymentEmail.For(" thandi@example.co.za ", "0821234567", UserId).Should().Be("thandi@example.co.za");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void WithoutAnEmail_PaystackGetsAnAddressFromThePhoneNumber(string? email)
    {
        PaymentEmail.For(email, "082 123 4567", UserId).Should().Be("27821234567@pay.spazasure.co.za");
    }

    [Fact]
    public void WithoutEmailOrValidPhone_TheAccountIdIsUsed()
    {
        PaymentEmail.For(null, "deleted-abc", UserId).Should().Be("8f2b0c1e000040008000000000000001@pay.spazasure.co.za");
    }
}
