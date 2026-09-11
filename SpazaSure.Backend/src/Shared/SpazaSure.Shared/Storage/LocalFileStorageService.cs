using Microsoft.Extensions.Configuration;

namespace SpazaSure.Shared.Storage;

/// <summary>
/// Writes uploaded files to a local "uploads" folder on disk. This is the
/// default (Storage:Provider = "local" or unset) and matches the project's
/// original behavior — fine for local development, where the filesystem
/// persists across restarts.
///
/// DO NOT use this in production on Render, Fly.io, or similar platforms:
/// their filesystems are ephemeral, so every redeploy (or every restart on
/// Fly.io, or every scale event on Render) silently wipes anything written
/// here. Use S3FileStorageService instead (Storage:Provider = "s3").
///
/// The URL prefix is configurable (Storage:Local:UrlPrefix, default
/// "/uploads") because more than one service can use local storage, and the
/// Gateway can only route a given path prefix to ONE cluster — UserService
/// owns "/uploads", so any other service using local storage needs its own
/// distinct prefix (with a matching static-file mapping and Gateway route)
/// to actually be reachable. See ComplianceService's Program.cs for an
/// example ("/compliance-uploads").
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;
    private readonly string _urlPrefix;

    public LocalFileStorageService(IConfiguration config)
    {
        _root = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _urlPrefix = config["Storage:Local:UrlPrefix"]?.TrimEnd('/') is { Length: > 0 } p ? p : "/uploads";
    }

    public async Task<string> SaveAsync(Stream content, string key, string contentType, CancellationToken ct = default)
    {
        var relative = key.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);

        return $"{_urlPrefix}/{key.TrimStart('/')}";
    }

    public Task DeleteAsync(string urlOrKey, CancellationToken ct = default)
    {
        var prefix = _urlPrefix + "/";
        var key = urlOrKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? urlOrKey[prefix.Length..]
            : urlOrKey.TrimStart('/');

        var fullPath = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
