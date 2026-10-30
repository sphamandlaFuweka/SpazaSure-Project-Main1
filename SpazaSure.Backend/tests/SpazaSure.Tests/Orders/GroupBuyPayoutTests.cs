using FluentAssertions;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>
/// Shops join a group buy at different times, but the supplier is paid for the whole group buy at
/// once: its money waits until the deal is finished and every one of its orders is ready.
/// </summary>
public class GroupBuyPayoutTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cutoff = Now.AddDays(-3); // the payout delay

    private static (Infrastructure.Data.SpazaSureDbContext Db, GroupBuy GroupBuy, Order First, Order Second) Setup(string groupStatus)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var supplier = new Supplier { UserId = Guid.NewGuid(), CompanyName = "Bulk Co", ContactPerson = "S", Phone = "0821000000", Email = "s@test.com" };
        var groupBuy = new GroupBuy { Title = "Maize month", SupplierId = supplier.Id, Status = groupStatus, ExpiresAt = Now.AddDays(-1) };
        Order NewOrder(string number) => new()
        {
            SupplierId = supplier.Id, ShopId = Guid.NewGuid(), OrderNumber = number, GroupBuyId = groupBuy.Id,
            Status = "delivered", DeliveredAt = Now.AddDays(-5), PaymentMethod = "wallet", PaymentStatus = "paid",
            Subtotal = 1000m, TotalAmount = 1100m, DeliveryType = "standard",
        };
        var first = NewOrder("GB-1");
        var second = NewOrder("GB-2");
        db.AddRange(supplier, groupBuy, first, second);
        db.SaveChanges();
        return (db, groupBuy, first, second);
    }

    [Fact]
    public async Task AReadyOrder_Waits_WhileAnotherOrderOfItsGroupBuyIsNotDelivered()
    {
        var (db, _, first, second) = Setup("completed");
        second.Status = "dispatched";
        second.DeliveredAt = null;
        db.SaveChanges();

        var held = await PayoutRules.GroupBuysStillOpenAsync(db, Cutoff);

        PayoutRules.Stage(first, Cutoff, held).Should().Be("awaiting_group");
        PayoutRules.Stage(second, Cutoff, held).Should().Be("awaiting_delivery");
    }

    [Fact]
    public async Task AGroupBuyStillRunning_HoldsItsOrders_AsMoreMayFollow()
    {
        var (db, _, first, _) = Setup("active");

        var held = await PayoutRules.GroupBuysStillOpenAsync(db, Cutoff);

        PayoutRules.Stage(first, Cutoff, held).Should().Be("awaiting_group");
    }

    [Fact]
    public async Task OnceEveryOrderIsReady_TheWholeGroupBuyIsReleasedTogether()
    {
        var (db, _, first, second) = Setup("completed");

        var held = await PayoutRules.GroupBuysStillOpenAsync(db, Cutoff);

        PayoutRules.Stage(first, Cutoff, held).Should().Be("available");
        PayoutRules.Stage(second, Cutoff, held).Should().Be("available");
    }

    [Fact]
    public async Task ACancelledOrder_DoesNotHoldUpTheRest()
    {
        var (db, _, first, second) = Setup("completed");
        second.Status = "cancelled";
        second.DeliveredAt = null;
        db.SaveChanges();

        var held = await PayoutRules.GroupBuysStillOpenAsync(db, Cutoff);

        PayoutRules.Stage(first, Cutoff, held).Should().Be("available");
    }
}
