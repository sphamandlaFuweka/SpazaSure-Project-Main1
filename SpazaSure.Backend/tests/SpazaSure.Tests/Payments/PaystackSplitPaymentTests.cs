using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Services;
using Xunit;

namespace SpazaSure.Tests.Payments;

public class PaystackSplitPaymentTests
{
    private sealed class FakePaystack : HttpMessageHandler
    {
        private int _subaccountNumber;
        public List<(string Path, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((path, body));
            var response = path switch
            {
                "/subaccount" => "{\"status\":true,\"data\":{\"subaccount_code\":\"ACCT_" +
                    Interlocked.Increment(ref _subaccountNumber) + "\"}}",
                "/split" => """{"status":true,"data":{"split_code":"SPL_checkout"}}""",
                "/transaction/initialize" => """{"status":true,"data":{"authorization_url":"https://checkout.paystack.com/test","reference":"SPZ-test"}}""",
                _ => """{"status":false,"message":"unexpected request"}"""
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "sk_test_unit",
            ["Paystack:CallbackUrls:ShopCheckout"] = "https://app.example.test/?payment=success"
        }).Build();

    [Fact]
    public async Task Checkout_SplitsEachSuppliersProductSubtotal_AndKeepsFeesWithSpazaSure()
    {
        var supplierOne = new Supplier
        {
            CompanyName = "Supplier One", Email = "one@example.test",
            BankAccount = "62812345678", BankBranchCode = "250655"
        };
        var supplierTwo = new Supplier
        {
            CompanyName = "Supplier Two", Email = "two@example.test",
            BankAccount = "123456789", BankBranchCode = "051001"
        };
        var orders = new[]
        {
            new Order { Supplier = supplierOne, SupplierId = supplierOne.Id, Subtotal = 120.25m, DeliveryFee = 25m, PlatformCommission = 5m, TotalAmount = 150.25m },
            new Order { Supplier = supplierOne, SupplierId = supplierOne.Id, Subtotal = 9.75m, DeliveryFee = 0m, PlatformCommission = 1m, TotalAmount = 10.75m },
            new Order { Supplier = supplierTwo, SupplierId = supplierTwo.Id, Subtotal = 80m, DeliveryFee = 10m, PlatformCommission = 4m, TotalAmount = 94m }
        };
        var fake = new FakePaystack();
        using var client = new HttpClient(fake);
        var config = Configuration();
        var splitService = new PaystackSplitPaymentService(client, config);
        var checkoutService = new OrderPaystackPaymentService(client, config);

        var splitCode = await splitService.CreateSplitAsync(orders, "SPZ-test");
        var checkout = await checkoutService.CreateCheckoutAsync(
            orders, "shop@example.test", "SPZ-test", splitCode);

        Assert.Equal("SPL_checkout", checkout.SplitCode);
        Assert.Equal(4, fake.Requests.Count);
        Assert.Equal(2, fake.Requests.Count(r => r.Path == "/subaccount"));

        using var split = JsonDocument.Parse(fake.Requests.Single(r => r.Path == "/split").Body);
        var splitRoot = split.RootElement;
        Assert.Equal("flat", splitRoot.GetProperty("type").GetString());
        Assert.Equal("ZAR", splitRoot.GetProperty("currency").GetString());
        Assert.Equal("account", splitRoot.GetProperty("bearer_type").GetString());
        var shares = splitRoot.GetProperty("subaccounts").EnumerateArray()
            .Select(s => s.GetProperty("share").GetInt64())
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(new long[] { 8000, 13000 }, shares);

        using var initialize = JsonDocument.Parse(fake.Requests.Single(r => r.Path == "/transaction/initialize").Body);
        Assert.Equal(25500, initialize.RootElement.GetProperty("amount").GetInt32());
        Assert.Equal("SPL_checkout", initialize.RootElement.GetProperty("split_code").GetString());
        Assert.Equal("ACCT_1", supplierOne.PaystackSubaccountCode);
        Assert.Equal("ACCT_2", supplierTwo.PaystackSubaccountCode);
    }

    [Fact]
    public async Task MissingSupplierBankDetails_FailsBeforeCreatingSplitOrCheckout()
    {
        var supplier = new Supplier { CompanyName = "No Bank", Email = "supplier@example.test" };
        var order = new Order { Supplier = supplier, SupplierId = supplier.Id, Subtotal = 50m, TotalAmount = 60m };
        var fake = new FakePaystack();
        using var client = new HttpClient(fake);
        var splitService = new PaystackSplitPaymentService(client, Configuration());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            splitService.CreateSplitAsync([order], "SPZ-test"));
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task ExistingSupplierSubaccount_IsReused()
    {
        var supplier = new Supplier
        {
            CompanyName = "Supplier", Email = "supplier@example.test",
            BankAccount = "62812345678", BankBranchCode = "250655",
            PaystackSubaccountCode = "ACCT_existing"
        };
        var order = new Order { Supplier = supplier, SupplierId = supplier.Id, Subtotal = 50m, TotalAmount = 60m };
        var fake = new FakePaystack();
        using var client = new HttpClient(fake);
        var splitService = new PaystackSplitPaymentService(client, Configuration());

        var splitCode = await splitService.CreateSplitAsync([order], "SPZ-test");

        Assert.Equal("SPL_checkout", splitCode);
        Assert.DoesNotContain(fake.Requests, r => r.Path == "/subaccount");
        using var split = JsonDocument.Parse(fake.Requests.Single().Body);
        Assert.Equal("ACCT_existing", split.RootElement.GetProperty("subaccounts")[0].GetProperty("subaccount").GetString());
    }
}
