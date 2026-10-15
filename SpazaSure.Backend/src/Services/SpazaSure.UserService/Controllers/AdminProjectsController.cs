using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// Admin Projects &amp; Impact workspace: project management, monitoring, evaluation &amp; learning (MEL)
/// and reporting. Every record is stored in pm_records with its fields in a JSON document; the admin
/// portal owns the shape of each kind. Routed by the gateway's /api/admin/{**} rule to this service.
/// </summary>
[ApiController]
[Route("api/admin/projects")]
[Authorize(Roles = "admin")]
public class AdminProjectsController(SpazaSureDbContext db) : ControllerBase
{
    internal static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
    {
        "project", "task", "milestone", "risk", "opportunity", "initiative", "person",
        "meeting", "meetingRecord", "indicator", "evaluation", "lesson", "framework", "settings",
    };

    internal const int MaxDataBytes = 64 * 1024;
    internal const int MaxBulkRecords = 500;

    /// <summary>Everything in the workspace, optionally limited to one kind.</summary>
    [HttpGet("records")]
    public async Task<IActionResult> List([FromQuery] string? kind)
    {
        if (kind is not null && !AllowedKinds.Contains(kind))
            return BadRequest(ApiResponse.Fail($"Unknown record kind '{kind}'."));

        var query = db.PmRecords.AsNoTracking();
        if (kind is not null) query = query.Where(r => r.Kind == kind);

        var rows = await query.OrderBy(r => r.CreatedAt).ToListAsync();
        return Ok(ApiResponse<object>.Ok(rows.Select(ToDto).ToList()));
    }

    [HttpPost("records")]
    public async Task<IActionResult> Create(PmRecordRequest req)
    {
        var error = Validate(req.Kind, req.Data, out var json);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));

        var record = new PmRecord { Kind = req.Kind!, Data = json!, CreatedBy = CurrentUserId, UpdatedBy = CurrentUserId };
        db.PmRecords.Add(record);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(ToDto(record), "Saved."));
    }

    /// <summary>Creates many records at once, e.g. importing the starter data from the management workbooks.</summary>
    [HttpPost("records/bulk")]
    public async Task<IActionResult> CreateMany(PmBulkRequest req)
    {
        if (req.Records is null || req.Records.Count == 0)
            return BadRequest(ApiResponse.Fail("Send at least one record."));
        if (req.Records.Count > MaxBulkRecords)
            return BadRequest(ApiResponse.Fail($"Send at most {MaxBulkRecords} records at a time."));

        var created = new List<PmRecord>();
        for (var i = 0; i < req.Records.Count; i++)
        {
            var item = req.Records[i];
            var error = Validate(item.Kind, item.Data, out var json);
            if (error is not null) return BadRequest(ApiResponse.Fail($"Record {i + 1}: {error}"));
            created.Add(new PmRecord { Kind = item.Kind!, Data = json!, CreatedBy = CurrentUserId, UpdatedBy = CurrentUserId });
        }

        db.PmRecords.AddRange(created);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(created.Select(ToDto).ToList(), $"{created.Count} records saved."));
    }

    [HttpPut("records/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PmRecordUpdateRequest req)
    {
        var record = await db.PmRecords.SingleOrDefaultAsync(r => r.Id == id);
        if (record is null) return NotFound(ApiResponse.Fail("That record no longer exists."));

        var error = Validate(record.Kind, req.Data, out var json);
        if (error is not null) return BadRequest(ApiResponse.Fail(error));

        record.Data = json!;
        record.UpdatedBy = CurrentUserId;
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(ToDto(record), "Saved."));
    }

    [HttpDelete("records/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var record = await db.PmRecords.SingleOrDefaultAsync(r => r.Id == id);
        if (record is null) return NotFound(ApiResponse.Fail("That record no longer exists."));

        db.PmRecords.Remove(record);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Deleted."));
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    internal static string? Validate(string? kind, JsonElement? data, out string? json)
    {
        json = null;
        if (string.IsNullOrWhiteSpace(kind)) return "Kind is required.";
        if (!AllowedKinds.Contains(kind)) return $"Unknown record kind '{kind}'.";
        if (data is null || data.Value.ValueKind != JsonValueKind.Object) return "Data must be a JSON object.";

        json = data.Value.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > MaxDataBytes) return "This record is too large (64 KB maximum).";
        return null;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static PmRecordDto ToDto(PmRecord r)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(r.Data) ? "{}" : r.Data);
        return new PmRecordDto(r.Id, r.Kind, doc.RootElement.Clone(), r.CreatedAt, r.UpdatedAt);
    }
}

public record PmRecordRequest(string? Kind, JsonElement? Data);
public record PmRecordUpdateRequest(JsonElement? Data);
public record PmBulkRequest(List<PmRecordRequest>? Records);
public record PmRecordDto(Guid Id, string Kind, JsonElement Data, DateTime CreatedAt, DateTime? UpdatedAt);
