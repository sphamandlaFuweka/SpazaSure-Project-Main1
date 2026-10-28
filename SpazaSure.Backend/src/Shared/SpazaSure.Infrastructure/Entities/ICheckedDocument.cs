namespace SpazaSure.Infrastructure.Entities;

/// <summary>A compliance document that is checked against its owner's profile (see DocumentChecks).</summary>
public interface ICheckedDocument
{
    string CheckStatus { get; set; }
    string? CheckSummary { get; set; }
    string? CheckDetails { get; set; }
    DateTime? CheckedAt { get; set; }
    string? DocumentNumber { get; set; }
}
