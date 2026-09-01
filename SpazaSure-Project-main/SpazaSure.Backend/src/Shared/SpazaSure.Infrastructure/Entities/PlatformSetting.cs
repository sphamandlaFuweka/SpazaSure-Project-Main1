using SpazaSure.Shared.Models;

namespace SpazaSure.Infrastructure.Entities;

public class PlatformSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
