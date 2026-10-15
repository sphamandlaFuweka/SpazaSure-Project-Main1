using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Tests.Helpers;
using SpazaSure.UserService.Controllers;
using Xunit;

namespace SpazaSure.Tests.Features;

public class AdminProjectsControllerTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    private static (AdminProjectsController ctrl, SpazaSureDbContext db) CreateController()
    {
        var db = DbContextHelper.CreateInMemoryDb();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, AdminId.ToString()), new Claim(ClaimTypes.Role, "admin") };
        var ctrl = new AdminProjectsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };
        return (ctrl, db);
    }

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    public async Task Create_ValidTask_IsStoredWithCreator()
    {
        var (ctrl, db) = CreateController();

        var result = await ctrl.Create(new PmRecordRequest("task", Json("""{"title":"Venue/Hall","owner":"bh","due":"2026-10-31"}""")));

        result.Should().BeOfType<OkObjectResult>();
        var row = db.PmRecords.Single();
        row.Kind.Should().Be("task");
        row.CreatedBy.Should().Be(AdminId);
        JsonDocument.Parse(row.Data).RootElement.GetProperty("title").GetString().Should().Be("Venue/Hall");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invoice")]
    public async Task Create_UnknownKind_IsRejected(string? kind)
    {
        var (ctrl, db) = CreateController();

        var result = await ctrl.Create(new PmRecordRequest(kind, Json("""{"title":"x"}""")));

        result.Should().BeOfType<BadRequestObjectResult>();
        db.PmRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_DataThatIsNotAnObject_IsRejected()
    {
        var (ctrl, _) = CreateController();

        var result = await ctrl.Create(new PmRecordRequest("task", Json("""["not","an","object"]""")));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_OversizedRecord_IsRejected()
    {
        var (ctrl, _) = CreateController();
        var big = new string('a', 70 * 1024);

        var result = await ctrl.Create(new PmRecordRequest("lesson", Json($$"""{"title":"{{big}}"}""")));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_ReplacesDataAndKeepsKind()
    {
        var (ctrl, db) = CreateController();
        await ctrl.Create(new PmRecordRequest("risk", Json("""{"title":"Platform not ready","l":"3","i":"3"}""")));
        var id = db.PmRecords.Single().Id;

        var result = await ctrl.Update(id, new PmRecordUpdateRequest(Json("""{"title":"Platform not ready","l":"2","i":"3","status":"Mitigating"}""")));

        result.Should().BeOfType<OkObjectResult>();
        var row = db.PmRecords.Single();
        row.Kind.Should().Be("risk");
        JsonDocument.Parse(row.Data).RootElement.GetProperty("status").GetString().Should().Be("Mitigating");
    }

    [Fact]
    public async Task Update_MissingRecord_ReturnsNotFound()
    {
        var (ctrl, _) = CreateController();

        var result = await ctrl.Update(Guid.NewGuid(), new PmRecordUpdateRequest(Json("""{"title":"x"}""")));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_RemovesRecord()
    {
        var (ctrl, db) = CreateController();
        await ctrl.Create(new PmRecordRequest("lesson", Json("""{"title":"Bring mobile data for demos"}""")));
        var id = db.PmRecords.Single().Id;

        var result = await ctrl.Delete(id);

        result.Should().BeOfType<OkObjectResult>();
        db.PmRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateMany_SavesAllOrNothing()
    {
        var (ctrl, db) = CreateController();
        var good = new List<PmRecordRequest>
        {
            new("project", Json("""{"name":"NDZ event"}""")),
            new("task", Json("""{"title":"Venue/Hall"}""")),
        };
        var withBadRecord = new List<PmRecordRequest>(good) { new("unknown", Json("""{"x":1}""")) };

        (await ctrl.CreateMany(new PmBulkRequest(withBadRecord))).Should().BeOfType<BadRequestObjectResult>();
        db.PmRecords.Should().BeEmpty();

        (await ctrl.CreateMany(new PmBulkRequest(good))).Should().BeOfType<OkObjectResult>();
        db.PmRecords.Count().Should().Be(2);
    }

    [Fact]
    public async Task List_FiltersByKind_AndRejectsUnknownKind()
    {
        var (ctrl, _) = CreateController();
        await ctrl.Create(new PmRecordRequest("task", Json("""{"title":"a"}""")));
        await ctrl.Create(new PmRecordRequest("risk", Json("""{"title":"b"}""")));

        var ok = await ctrl.List("task") as OkObjectResult;
        ok.Should().NotBeNull();
        var body = JsonSerializer.Serialize(ok!.Value);
        body.Should().Contain("\"Kind\":\"task\"").And.NotContain("\"Kind\":\"risk\"");

        (await ctrl.List("invoice")).Should().BeOfType<BadRequestObjectResult>();
    }
}
