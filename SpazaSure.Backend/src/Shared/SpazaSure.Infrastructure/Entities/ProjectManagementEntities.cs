using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

/// <summary>
/// One record in the admin Projects &amp; Impact workspace (a project, task, risk, indicator, lesson, ...).
/// The record's fields live in <see cref="Data"/> as a JSON object so the management and MEL
/// framework can evolve without a schema migration for every new field. <see cref="Kind"/>
/// says what the record is; the allowed kinds are listed in AdminProjectsController.
/// </summary>
public class PmRecord : BaseEntity
{
    public string Kind { get; set; } = string.Empty;

    /// <summary>JSON object, stored as jsonb.</summary>
    public string Data { get; set; } = "{}";

    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
