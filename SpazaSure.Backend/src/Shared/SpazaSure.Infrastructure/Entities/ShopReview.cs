using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class ShopReview : BaseEntity
{
    public Guid ShopId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public SpazaShop Shop { get; set; } = null!;
    public User Reviewer { get; set; } = null!;
}
