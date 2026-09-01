using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.OrderService.Controllers;
using SpazaSure.Shared.Models;
using SpazaSure.Tests.Helpers;
using System.Security.Claims;
using Xunit;

namespace SpazaSure.Tests.Orders;

public class OrderStatusTransitionTests
{
    private static (OrdersController ctrl, SpazaSure.Infrastructure.Data.SpazaSureDbContext db, Guid supplierId) CreateController()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var role = db.Roles.First(r => r.Name == "supplier");
        var user = new User { Email = "orders@test.com", Phone = "0820001000", PasswordHash = "hash", RoleId = role.Id };
        db.Users.Add(user);
        var supplier = new Supplier { UserId = user.Id, CompanyName = "Test Supplier", ContactPerson = "Bob", Phone = "0820001000", Email = "orders@test.com" };
        db.Suppliers.Add(supplier);
        var shopUser = new User { Email = "shop@test.com", Phone = "0820002000", PasswordHash = "h", RoleId = role.Id };
        db.Users.Add(shopUser);
        var shop = new SpazaShop { UserId = shopUser.Id, ShopName = "Test Shop", OwnerName = "Alice", Phone = "0820002000", Address = "1 Test St" };
        db.SpazaShops.Add(shop);
        db.SaveChanges();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
        var ctrl = new OrdersController(db, new SpazaSure.Shared.Helpers.EventPublisher("__nonexistent__", "x", "x"))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };
        return (ctrl, db, supplier.Id);
    }

    private static Order SeedOrder(SpazaSure.Infrastructure.Data.SpazaSureDbContext db, Guid supplierId, string status = "pending")
    {
        var shop = db.SpazaShops.First();
        var order = new Order { SupplierId = supplierId, ShopId = shop.Id, OrderNumber = "ORD-" + Guid.NewGuid().ToString("N")[..8].ToUpper(), Status = status, TotalAmount = 100m, DeliveryType = "standard" };
        db.Orders.Add(order);
        db.SaveChanges();
        return order;
    }

    [Theory]
    [InlineData("pending",    "processing")]
    [InlineData("pending",    "cancelled")]
    [InlineData("processing", "dispatched")]
    [InlineData("processing", "cancelled")]
    [InlineData("dispatched", "delivered")]
    public async Task UpdateStatus_AllowedTransitions_ReturnOk(string from, string to)
    {
        var (ctrl, db, supplierId) = CreateController();
        var order = SeedOrder(db, supplierId, from);
        var result = await ctrl.UpdateStatus(order.Id, new UpdateStatusRequest(to, null));
        result.Should().BeOfType<OkObjectResult>();
        var refreshed = await db.Orders.FindAsync(order.Id);
        refreshed!.Status.Should().Be(to);
    }

    [Theory]
    [InlineData("pending",   "delivered")]
    [InlineData("dispatched","processing")]
    [InlineData("delivered", "cancelled")]
    [InlineData("cancelled", "pending")]
    public async Task UpdateStatus_InvalidTransitions_ReturnBadRequest(string from, string to)
    {
        var (ctrl, db, supplierId) = CreateController();
        var order = SeedOrder(db, supplierId, from);
        var result = await ctrl.UpdateStatus(order.Id, new UpdateStatusRequest(to, null));
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateStatus_NonExistentOrder_ReturnsNotFound()
    {
        var (ctrl, _, _) = CreateController();
        var result = await ctrl.UpdateStatus(Guid.NewGuid(), new UpdateStatusRequest("processing", null));
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateStatus_Cancel_StoresRejectionReason()
    {
        var (ctrl, db, supplierId) = CreateController();
        var order = SeedOrder(db, supplierId, "pending");
        await ctrl.UpdateStatus(order.Id, new UpdateStatusRequest("cancelled", "Out of stock"));
        var refreshed = await db.Orders.FindAsync(order.Id);
        refreshed!.RejectionReason.Should().Be("Out of stock");
    }

    [Fact]
    public async Task Get_ExistingOrder_ReturnsOk()
    {
        var (ctrl, db, supplierId) = CreateController();
        var order = SeedOrder(db, supplierId);
        var result = await ctrl.Get(order.Id);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Get_UnknownOrderId_ReturnsNotFound()
    {
        var (ctrl, _, _) = CreateController();
        var result = await ctrl.Get(Guid.NewGuid());
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
