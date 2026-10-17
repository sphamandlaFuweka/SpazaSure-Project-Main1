using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.ProductService.Services;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Orders;

/// <summary>Stock follows orders; categories are managed by admins and filter product lists.</summary>
public class OrderStockAndCategoryTests
{
    private static (SpazaSureDbContext Db, Product Product, Order Order) Seed(string method, string paymentStatus = "pending")
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var product = new Product { Name = "Rice 2kg", Sku = "R2", Price = 40m, StockQty = 100 };
        var order = new Order
        {
            OrderNumber = "SPZ-T", Status = "pending", PaymentMethod = method, PaymentStatus = paymentStatus,
            Items = [new OrderItem { ProductId = product.Id, Quantity = 10, UnitPrice = 40m, LineTotal = 400m }]
        };
        db.AddRange(product, order);
        db.SaveChanges();
        return (db, product, order);
    }

    [Theory]
    [InlineData("cash_on_delivery", "pending", true)]
    [InlineData("wallet", "paid", true)]
    [InlineData("invoice", "pending", true)]
    [InlineData("paystack", "pending", false)] // abandoned card checkouts don't lock stock
    [InlineData("paystack", "paid", true)]
    public void Holds_DependsOnPaymentCommitment(string method, string paymentStatus, bool holds)
    {
        var (_, _, order) = Seed(method, paymentStatus);
        OrderStock.Holds(order).Should().Be(holds);
    }

    [Fact]
    public async Task Cancelling_ReturnsStock_AndReinstating_TakesItAgain()
    {
        var (db, product, order) = Seed("cash_on_delivery");
        await OrderStock.SyncAsync(db, order, heldBefore: false);
        product.StockQty.Should().Be(90);

        var held = OrderStock.Holds(order);
        order.Status = "cancelled";
        await OrderStock.SyncAsync(db, order, held);
        product.StockQty.Should().Be(100);

        held = OrderStock.Holds(order);
        order.Status = "pending";
        await OrderStock.SyncAsync(db, order, held);
        product.StockQty.Should().Be(90);
    }

    [Fact]
    public async Task CardPayment_TakesStockWhenPaid()
    {
        var (db, product, order) = Seed("paystack");
        await OrderStock.SyncAsync(db, order, heldBefore: false);
        product.StockQty.Should().Be(100);

        var held = OrderStock.Holds(order);
        order.PaymentStatus = "paid";
        await OrderStock.SyncAsync(db, order, held);
        product.StockQty.Should().Be(90);
    }

    [Fact]
    public async Task OrderNumbers_AreUniqueAndFormatted()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var numbers = new HashSet<string>();
        for (var i = 0; i < 50; i++) numbers.Add(await OrderNumbers.NewAsync(db));
        numbers.Should().HaveCount(50);
        numbers.Should().OnlyContain(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^SPZ-\d{4}-[A-Z2-9]{6}$"));
    }

    [Fact]
    public async Task Categories_CreateHideAndDeleteRules()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var ctrl = new AdminCategoriesController(db);

        (await ctrl.Create(new CategoryRequest("Pet food & care", null, null))).Should().BeOfType<OkObjectResult>();
        (await ctrl.Create(new CategoryRequest("pet food & care", null, null))).Should().BeOfType<BadRequestObjectResult>();
        var category = db.Categories.Single();
        category.Slug.Should().Be("pet-food-and-care");

        db.Products.Add(new Product { Name = "Dog food", Sku = "DF", Price = 1, CategoryId = category.Id });
        db.SaveChanges();
        (await ctrl.Delete(category.Id)).Should().BeOfType<BadRequestObjectResult>(); // still used: hide instead
        (await ctrl.Update(category.Id, new CategoryRequest(null, null, false))).Should().BeOfType<OkObjectResult>();
        db.Categories.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public void CategoryFilter_HandlesAllNoneAndIds()
    {
        var a = Guid.NewGuid();
        var products = new[]
        {
            new Product { Name = "A", CategoryId = a },
            new Product { Name = "B", CategoryId = Guid.NewGuid() },
            new Product { Name = "C", CategoryId = null },
        }.AsQueryable();

        ProductCategoryFilter.Apply(products, null).Should().HaveCount(3);
        ProductCategoryFilter.Apply(products, "all").Should().HaveCount(3);
        ProductCategoryFilter.Apply(products, "none").Single().Name.Should().Be("C");
        ProductCategoryFilter.Apply(products, a.ToString()).Single().Name.Should().Be("A");
        ProductCategoryFilter.Apply(products, "not-a-guid").Should().BeEmpty();
    }
}
