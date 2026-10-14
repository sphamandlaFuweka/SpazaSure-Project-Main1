using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>Active FAQs for the supplier Subscription page.</summary>
[ApiController]
[Route("api/supplier/faqs")]
[Authorize]
public class FaqController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var items = await db.Faqs.AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt)
            .Select(f => new { f.Id, f.Question, f.Answer, f.SortOrder, f.IsActive })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }
}

/// <summary>Admin management of the supplier FAQs.</summary>
[ApiController]
[Route("api/admin/faqs")]
[Authorize(Roles = "admin")]
public class AdminFaqController(SpazaSureDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAll()
    {
        var items = await db.Faqs.AsNoTracking()
            .OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt)
            .Select(f => new { f.Id, f.Question, f.Answer, f.SortOrder, f.IsActive })
            .ToListAsync();
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateFaqRequest req)
    {
        var sortOrder = req.SortOrder ?? (await db.Faqs.MaxAsync(f => (int?)f.SortOrder) ?? 0) + 1;
        var faq = new Faq
        {
            Question = req.Question.Trim(),
            Answer = req.Answer.Trim(),
            SortOrder = sortOrder,
            IsActive = true,
        };
        db.Faqs.Add(faq);
        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { faq.Id, faq.Question, faq.Answer, faq.SortOrder, faq.IsActive }, "FAQ added."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateFaqRequest req)
    {
        var faq = await db.Faqs.FindAsync(id);
        if (faq is null) return NotFound(ApiResponse.Fail("FAQ not found."));

        if (req.Question is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Question)) return BadRequest(ApiResponse.Fail("Question cannot be empty."));
            faq.Question = req.Question.Trim();
        }
        if (req.Answer is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Answer)) return BadRequest(ApiResponse.Fail("Answer cannot be empty."));
            faq.Answer = req.Answer.Trim();
        }
        if (req.SortOrder.HasValue) faq.SortOrder = req.SortOrder.Value;
        if (req.IsActive.HasValue) faq.IsActive = req.IsActive.Value;
        faq.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { faq.Id, faq.Question, faq.Answer, faq.SortOrder, faq.IsActive }, "FAQ updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var faq = await db.Faqs.FindAsync(id);
        if (faq is null) return NotFound(ApiResponse.Fail("FAQ not found."));
        db.Faqs.Remove(faq);
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("FAQ deleted."));
    }

    /// <summary>Sets the display order to match the given list of ids (first = top).</summary>
    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(ReorderFaqsRequest req)
    {
        var faqs = await db.Faqs.Where(f => req.Ids.Contains(f.Id)).ToListAsync();
        if (faqs.Count != req.Ids.Distinct().Count())
            return BadRequest(ApiResponse.Fail("One or more FAQs no longer exist. Refresh and try again."));

        for (var i = 0; i < req.Ids.Count; i++)
        {
            var faq = faqs.First(f => f.Id == req.Ids[i]);
            faq.SortOrder = i + 1;
            faq.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("FAQ order saved."));
    }
}

public record CreateFaqRequest(
    [Required, StringLength(300, MinimumLength = 3)] string Question,
    [Required, StringLength(4000, MinimumLength = 3)] string Answer,
    int? SortOrder = null);

public record UpdateFaqRequest(
    [StringLength(300)] string? Question = null,
    [StringLength(4000)] string? Answer = null,
    int? SortOrder = null,
    bool? IsActive = null);

public record ReorderFaqsRequest([Required, MinLength(1)] List<Guid> Ids);
