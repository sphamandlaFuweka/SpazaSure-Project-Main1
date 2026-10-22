using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Shared.Helpers;
using SpazaSure.Tests.Helpers;
using Xunit;

namespace SpazaSure.Tests.Orders;

public class ShopOrderRatingTests
{
    private static readonly EventPublisher Events = new("__nonexistent__", "x", "x");

    private static (SpazaSureDbContext Db, ShopOrderRatingController Controller, Order Order) Setup(string status)
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var shopRole = db.Roles.Single(r => r.Name == "spaza_owner");
        var supplierRole = db.Roles.Single(r => r.Name == "supplier");
        var owner = new User { Phone = "0820001000", PasswordHash = "hash", RoleId = shopRole.Id };
        var supplierUser = new User { Phone = "0820002000", PasswordHash = "hash", RoleId = supplierRole.Id };
        var shop = new SpazaShop
        {
            UserId = owner.Id,
            ShopName = "Corner Shop",
            OwnerName = "Shop Owner",
            Phone = owner.Phone,
            Address = "1 Main Street",
        };
        var supplier = new Supplier
        {
            UserId = supplierUser.Id,
            CompanyName = "Fresh Foods",
            ContactPerson = "Supplier",
            Phone = supplierUser.Phone,
            Email = "supplier@example.test",
        };
        var order = new Order
        {
            ShopId = shop.Id,
            SupplierId = supplier.Id,
            OrderNumber = "ORD-1001",
            Status = status,
            PaymentMethod = "wallet",
            PaymentStatus = "paid",
        };
        db.AddRange(owner, supplierUser, shop, supplier, order);
        db.SaveChanges();

        var controller = new ShopOrderRatingController(db, Events)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString())], "Test")),
                },
            },
        };
        return (db, controller, order);
    }

    [Fact]
    public async Task Rating_IsRejectedBeforeDelivery()
    {
        var (_, controller, order) = Setup("pending");

        var result = await controller.RateDelivery(order.Id, new RateDeliveryRequest(5, 5, null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 6)]
    public async Task Rating_RejectsValuesOutsideOneToFive(int delivery, int supplier)
    {
        var (_, controller, order) = Setup("delivered");

        var result = await controller.RateDelivery(order.Id, new RateDeliveryRequest(delivery, supplier, null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task IssueReport_IsRejectedBeforeDelivery()
    {
        var (_, controller, order) = Setup("pending");

        var result = await controller.ReportIssue(order.Id, new ReportIssueRequest("Damaged Items", "The items were damaged."));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("Unexpected", "Some issue")]
    [InlineData("Damaged Items", "   ")]
    public async Task IssueReport_RejectsInvalidTypeOrEmptyDescription(string issueType, string description)
    {
        var (_, controller, order) = Setup("delivered");

        var result = await controller.ReportIssue(order.Id, new ReportIssueRequest(issueType, description));

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
