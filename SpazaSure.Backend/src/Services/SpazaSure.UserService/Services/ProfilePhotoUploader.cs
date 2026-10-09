using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Storage;

namespace SpazaSure.UserService.Services;

public static class ProfilePhotoUploader
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Validates and stores a profile picture, replacing the old one. Returns the new URL or an error.</summary>
    public static async Task<(string? Url, string? Error)> SaveAsync(
        IFileStorageService storage, User user, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return (null, "No picture was provided.");
        if (file.Length > MaxBytes) return (null, "The picture must be under 5MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return (null, "Only JPG, PNG and WEBP pictures are allowed.");
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && file.ContentType != "application/octet-stream")
            return (null, "That file is not an image.");

        var previous = user.ProfilePhotoUrl;
        var key = $"profile-photos/{user.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        await using var stream = file.OpenReadStream();
        var url = await storage.SaveAsync(stream, key, file.ContentType, ct);
        user.ProfilePhotoUrl = url;

        if (!string.IsNullOrEmpty(previous))
        {
            try { await storage.DeleteAsync(previous, ct); } catch { /* an orphaned old file is harmless */ }
        }
        return (url, null);
    }
}
