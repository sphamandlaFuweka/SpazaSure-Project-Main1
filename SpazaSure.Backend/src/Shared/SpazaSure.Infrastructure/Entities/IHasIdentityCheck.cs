namespace SpazaSure.Infrastructure.Entities;

/// <summary>A shop owner or supplier verified with Didit; see SpazaShop and Supplier.</summary>
public interface IHasIdentityCheck
{
    Guid UserId { get; }
    string? IdentityCheckSessionId { get; set; }
    string? IdentityCheckStatus { get; set; }
    string? IdentityCheckNotes { get; set; }
    DateTime? IdentityCheckUpdatedAt { get; set; }
}
