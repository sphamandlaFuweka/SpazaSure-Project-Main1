using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace SpazaSure.Shared.Storage;

public class S3StorageOptions
{
    public string BucketName { get; set; } = "";

    /// <summary>
    /// Leave null/empty for real AWS S3. Set this for S3-compatible providers:
    ///   Cloudflare R2:       https://&lt;account-id&gt;.r2.cloudflarestorage.com
    ///   DigitalOcean Spaces: https://&lt;region&gt;.digitaloceanspaces.com
    ///   MinIO (self-hosted): http://localhost:9000 (or wherever it runs)
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>AWS region (e.g. "us-east-1"), or "auto" for R2. Ignored by some providers.</summary>
    public string? Region { get; set; }

    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";

    /// <summary>
    /// The base URL clients use to actually fetch files — e.g. a Cloudflare R2
    /// public bucket URL/custom domain, a DO Spaces CDN endpoint, or an S3
    /// static-site/CloudFront domain. Required.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Needed for R2/MinIO/most S3-compatible providers; AWS S3 itself works with either.</summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Set true only on providers that support per-object ACLs the same way
    /// AWS S3 does (e.g. DigitalOcean Spaces). Cloudflare R2 does NOT support
    /// object ACLs — for R2, make the whole bucket public instead (via a
    /// custom domain or the r2.dev public URL in the Cloudflare dashboard)
    /// and leave this false, or PutObject calls will fail outright.
    /// </summary>
    public bool UsePublicReadAcl { get; set; } = false;
}

/// <summary>
/// Object storage backed by any S3-compatible API. Persists across
/// redeploys/restarts/scaling events, unlike LocalFileStorageService — this
/// is what production should actually use (Storage:Provider = "s3").
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _client;
    private readonly S3StorageOptions _opts;

    public S3FileStorageService(IOptions<S3StorageOptions> options)
    {
        _opts = options.Value;

        if (string.IsNullOrWhiteSpace(_opts.BucketName))
            throw new InvalidOperationException("Storage:S3:BucketName is required when Storage:Provider is \"s3\".");
        if (string.IsNullOrWhiteSpace(_opts.PublicBaseUrl))
            throw new InvalidOperationException("Storage:S3:PublicBaseUrl is required when Storage:Provider is \"s3\".");

        var config = new AmazonS3Config { ForcePathStyle = _opts.ForcePathStyle };
        if (!string.IsNullOrWhiteSpace(_opts.ServiceUrl)) config.ServiceURL = _opts.ServiceUrl;
        if (!string.IsNullOrWhiteSpace(_opts.Region)) config.AuthenticationRegion = _opts.Region;

        _client = new AmazonS3Client(_opts.AccessKey, _opts.SecretKey, config);
    }

    public async Task<string> SaveAsync(Stream content, string key, string contentType, CancellationToken ct = default)
    {
        key = key.TrimStart('/');
        var request = new PutObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = key,
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
        };
        if (_opts.UsePublicReadAcl) request.CannedACL = S3CannedACL.PublicRead;

        await _client.PutObjectAsync(request, ct);
        return $"{_opts.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    public async Task DeleteAsync(string urlOrKey, CancellationToken ct = default)
    {
        var prefix = _opts.PublicBaseUrl.TrimEnd('/') + "/";
        var key = urlOrKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? urlOrKey[prefix.Length..]
            : urlOrKey.TrimStart('/');

        if (string.IsNullOrEmpty(key)) return;
        await _client.DeleteObjectAsync(_opts.BucketName, key, ct);
    }
}
