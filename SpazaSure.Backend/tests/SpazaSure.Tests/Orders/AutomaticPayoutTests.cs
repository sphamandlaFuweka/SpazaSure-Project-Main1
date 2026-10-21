using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Services;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

public class AutomaticPayoutTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A fake Paystack that records what it was asked to do.</summary>
    private sealed class FakePaystack : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public string TransferStatus { get; set; } = "pending";
        public string? TransferError { get; set; }
        public string? RecipientError { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Calls.Add($"{request.Method} {path} {(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct))}");
            string Json(bool ok, string data, string? message = null) =>
                "{\"status\":" + (ok ? "true" : "false") + ",\"message\":\"" + (message ?? "ok") + "\",\"data\":" + data + "}";
            var (status, body) = path switch
            {
                "/transferrecipient" when RecipientError is not null => (HttpStatusCode.BadRequest, Json(false, "null", RecipientError)),
                "/transferrecipient" => (HttpStatusCode.OK, Json(true, "{\"recipient_code\":\"RCP_test\"}")),
                "/transfer" when TransferError is not null => (HttpStatusCode.BadRequest, Json(false, "null", TransferError)),
                "/transfer" => (HttpStatusCode.OK, Json(true, "{\"status\":\"" + TransferStatus + "\",\"transfer_code\":\"TRF_1\"}")),
                _ => (HttpStatusCode.NotFound, Json(false, "null", "not found")),
            };
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static PaystackTransferService Transfers(FakePaystack fake) =>
        new(new HttpClient(fake), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Paystack:SecretKey"] = "sk_test_x",
            ["Paystack:AutomaticPayouts"] = "true",
        }).Build());

    private static (SpazaSureDbContext Db, Supplier Supplier) Setup(string? bankAccount = "62812345678", string? branch = "250655")
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "supplier");
        var user = new User { Email = "pay@test.com", Phone = "0820007000", PasswordHash = "h", RoleId = role.Id };
        var supplier = new Supplier
        {
            UserId = user.Id, CompanyName = "Fresh Foods", ContactPerson = "Bob", Phone = "0820007000", Email = "pay@test.com",
            BankName = "FNB", BankAccount = bankAccount, BankBranchCode = branch, Status = "verified",
        };
        var shopUser = new User { Phone = "0820008000", PasswordHash = "h", RoleId = role.Id };
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Mama's", OwnerName = "Thandi", Phone = "0820008000", Address = "1 St" };
        db.AddRange(user, supplier, shopUser, shop);
        // Two delivered orders past the 3-day delay (R1,000 + R500 of goods), one still in the delay window
        foreach (var (subtotal, deliveredDaysAgo) in new[] { (1000m, 5), (500m, 4), (300m, 1) })
            db.Orders.Add(new Order
            {
                SupplierId = supplier.Id, ShopId = shop.Id, OrderNumber = "SPZ-" + Guid.NewGuid().ToString("N")[..8],
                Status = "delivered", PaymentMethod = "paystack", PaymentStatus = "paid",
                Subtotal = subtotal, DeliveryFee = 150m, TotalAmount = subtotal + 200m, DeliveredAt = Now.AddDays(-deliveredDaysAgo),
            });
        db.SaveChanges();
        return (db, supplier);
    }

    [Fact]
    public async Task AvailablePayouts_AreSentAsOneTransfer_ForTheGoodsOnly()
    {
        var (db, supplier) = Setup();
        var fake = new FakePaystack();

        var result = await AutomaticPayouts.RunOnceAsync(db, Transfers(fake), Events, Now, CancellationToken.None);

        result.Started.Should().Be(1);
        fake.Calls.Should().ContainSingle(c => c.StartsWith("POST /transferrecipient") && c.Contains("\"type\":\"basa\"") && c.Contains("250655"));
        fake.Calls.Should().ContainSingle(c => c.StartsWith("POST /transfer ") && c.Contains("\"amount\":150000") && c.Contains("RCP_test"));
        (await db.Suppliers.SingleAsync()).PaystackRecipientCode.Should().Be("RCP_test");
        var orders = await db.Orders.ToListAsync();
        orders.Where(o => o.PayoutInitiatedAt != null).Should().HaveCount(2).And.OnlyContain(o => o.PaidOutAt == null);
        orders.Single(o => o.Subtotal == 300m).PayoutInitiatedAt.Should().BeNull(); // still in the delay window

        // A second run doesn't pay the same orders again
        await AutomaticPayouts.RunOnceAsync(db, Transfers(fake), Events, Now, CancellationToken.None);
        fake.Calls.Count(c => c.StartsWith("POST /transfer ")).Should().Be(1);
    }

    [Fact]
    public async Task WebhookSuccess_MarksTheOrdersPaidOut()
    {
        var (db, _) = Setup();
        await AutomaticPayouts.RunOnceAsync(db, Transfers(new FakePaystack()), Events, Now, CancellationToken.None);
        var reference = (await db.Orders.FirstAsync(o => o.PayoutReference != null)).PayoutReference!;

        await SupplierPayoutResults.ApplyAsync(db, Events, reference, true, null, CancellationToken.None);

        (await db.Orders.CountAsync(o => o.PaidOutAt != null)).Should().Be(2);
    }

    [Fact]
    public async Task FailedTransfer_IsNotRetried_UntilTheBankDetailsChangeOrAnAdminRetries()
    {
        var (db, supplier) = Setup();
        var fake = new FakePaystack();
        await AutomaticPayouts.RunOnceAsync(db, Transfers(fake), Events, Now, CancellationToken.None);
        var reference = (await db.Orders.FirstAsync(o => o.PayoutReference != null)).PayoutReference!;

        await SupplierPayoutResults.ApplyAsync(db, Events, reference, false, "the transfer was failed (invalid account)", CancellationToken.None);
        await AutomaticPayouts.RunOnceAsync(db, Transfers(fake), Events, Now, CancellationToken.None);

        fake.Calls.Count(c => c.StartsWith("POST /transfer ")).Should().Be(1);
        (await db.Orders.Where(o => o.PayoutReference != null).ToListAsync())
            .Should().HaveCount(2).And.OnlyContain(o => SupplierPayoutResults.IsFailed(o.PayoutReference));
        (await db.Suppliers.SingleAsync()).PaystackRecipientCode.Should().BeNull();

        // Fixed bank details (or an admin retry) clear the failure; the next run pays again
        await SupplierPayoutResults.ClearFailuresAsync(db, supplier.Id, CancellationToken.None);
        await db.SaveChangesAsync();
        await AutomaticPayouts.RunOnceAsync(db, Transfers(fake), Events, Now, CancellationToken.None);
        fake.Calls.Count(c => c.StartsWith("POST /transfer ")).Should().Be(2);
    }

    [Fact]
    public async Task LowPaystackBalance_ReleasesTheOrdersForTheNextRun()
    {
        var (db, _) = Setup();
        var fake = new FakePaystack { TransferError = "Your balance is insufficient for this transfer" };

        await AutomaticPayouts.RunOnceAsync(db, Transfers(fake), Events, Now, CancellationToken.None);

        (await db.Orders.AnyAsync(o => o.PayoutInitiatedAt != null || o.PayoutReference != null)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "250655")]
    [InlineData("62812345678", null)]
    [InlineData("62812345678", "12")]
    public async Task SupplierWithoutCompleteBankDetails_IsSkipped(string? account, string? branch)
    {
        var (db, _) = Setup(account, branch);
        var fake = new FakePaystack();

        var result = await AutomaticPayouts.RunOnceAsync(db, Transfers(fake), Events, Now, CancellationToken.None);

        result.Skipped.Should().Be(1);
        fake.Calls.Should().BeEmpty();
    }
}
