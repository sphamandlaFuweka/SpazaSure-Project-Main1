namespace SpazaSure.Shared.Storage;

/// <summary>
/// Abstraction over where uploaded files (compliance documents, logos, etc.)
/// actually live. Register LocalFileStorageService for local development and
/// S3FileStorageService (works with AWS S3, Cloudflare R2, DigitalOcean
/// Spaces, or MinIO) for QA/Production — see Storage:Provider in appsettings.
/// </summary>
public interface IFileStorageService
{
    /// <param name="content">The file's contents. Not disposed by the implementation — caller owns it.</param>
    /// <param name="key">Relative path under the storage root, e.g. "supplier-docs/{supplierId}/{fileName}".</param>
    /// <param name="contentType">MIME type, e.g. from IFormFile.ContentType.</param>
    /// <returns>A URL (S3-backed) or app-relative path (local) the client can use to fetch the file.</returns>
    Task<string> SaveAsync(Stream content, string key, string contentType, CancellationToken ct = default);

    /// <param name="urlOrKey">Either the value SaveAsync previously returned, or a bare key — implementations accept both.</param>
    Task DeleteAsync(string urlOrKey, CancellationToken ct = default);
}
