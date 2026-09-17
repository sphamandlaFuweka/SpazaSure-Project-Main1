using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class CustomerScanEvent : BaseEntity
{
    public Guid CustomerUserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public string Source { get; set; } = "unknown";
    public int PointsAwarded { get; set; }
    public User Customer { get; set; } = null!;
    public Product? Product { get; set; }
}

public class CustomerRewardTransaction : BaseEntity
{
    public Guid CustomerUserId { get; set; }
    public int Points { get; set; }
    public string Type { get; set; } = "scan";
    public string? Reference { get; set; }
    public User Customer { get; set; } = null!;
}

public class CustomerVoucher : BaseEntity
{
    public Guid CustomerUserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; } = 20m;
    public string Status { get; set; } = "active";
    public DateTime? RedeemedAt { get; set; }
    public Guid? RedeemedByUserId { get; set; }
    public User Customer { get; set; } = null!;
}
