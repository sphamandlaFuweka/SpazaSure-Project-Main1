using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class FaqControllerTests
{
    private static (AdminFaqController Admin, FaqController Supplier, SpazaSure.Infrastructure.Data.SpazaSureDbContext Db) Create()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        return (new AdminFaqController(db), new FaqController(db), db);
    }

    [Fact]
    public async Task Create_AppendsToEnd_AndSupplierSeesOnlyActive()
    {
        var (admin, supplier, db) = Create();
        await admin.Create(new CreateFaqRequest("First question?", "First answer."));
        await admin.Create(new CreateFaqRequest("Second question?", "Second answer."));

        var all = await db.Faqs.OrderBy(f => f.SortOrder).ToListAsync();
        all.Select(f => f.SortOrder).Should().Equal(1, 2);

        await admin.Update(all[0].Id, new UpdateFaqRequest(IsActive: false));

        var result = await supplier.List();
        result.Should().BeOfType<OkObjectResult>();
        (await db.Faqs.CountAsync(f => f.IsActive)).Should().Be(1);
    }

    [Fact]
    public async Task Update_ChangesTextAndRejectsBlank()
    {
        var (admin, _, db) = Create();
        await admin.Create(new CreateFaqRequest("Old question?", "Old answer."));
        var id = (await db.Faqs.SingleAsync()).Id;

        await admin.Update(id, new UpdateFaqRequest(Question: "New question?", Answer: "New answer."));
        var blank = await admin.Update(id, new UpdateFaqRequest(Question: "   "));

        var faq = await db.Faqs.SingleAsync();
        faq.Question.Should().Be("New question?");
        faq.Answer.Should().Be("New answer.");
        blank.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Reorder_SetsSortOrderFromListPosition()
    {
        var (admin, _, db) = Create();
        await admin.Create(new CreateFaqRequest("A question?", "A"));
        await admin.Create(new CreateFaqRequest("B question?", "B"));
        await admin.Create(new CreateFaqRequest("C question?", "C"));
        var ids = await db.Faqs.OrderBy(f => f.SortOrder).Select(f => f.Id).ToListAsync();

        var result = await admin.Reorder(new ReorderFaqsRequest([ids[2], ids[0], ids[1]]));

        result.Should().BeOfType<OkObjectResult>();
        var ordered = await db.Faqs.OrderBy(f => f.SortOrder).Select(f => f.Answer).ToListAsync();
        ordered.Should().Equal("C", "A", "B");
    }

    [Fact]
    public async Task Reorder_WithUnknownId_IsRejected()
    {
        var (admin, _, _) = Create();
        await admin.Create(new CreateFaqRequest("A question?", "A"));

        var result = await admin.Reorder(new ReorderFaqsRequest([Guid.NewGuid()]));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Delete_RemovesFaq_AndUnknownIsNotFound()
    {
        var (admin, _, db) = Create();
        await admin.Create(new CreateFaqRequest("Delete me?", "Yes."));
        var id = (await db.Faqs.SingleAsync()).Id;

        (await admin.Delete(id)).Should().BeOfType<OkObjectResult>();
        (await admin.Delete(id)).Should().BeOfType<NotFoundObjectResult>();
        (await db.Faqs.CountAsync()).Should().Be(0);
    }
}
