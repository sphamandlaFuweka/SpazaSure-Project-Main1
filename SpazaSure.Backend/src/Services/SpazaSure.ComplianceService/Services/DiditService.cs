using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpazaSure.ComplianceService.Services;

/// <summary>
/// Didit identity checks (https://docs.didit.me). Shop owners get a KYC session (ID document,
/// selfie, Home Affairs, sanctions) and suppliers a KYB session (CIPC registration, directors,
/// sanctions). Each check runs on Didit's hosted page; the result arrives by signed webhook.
/// </summary>
public sealed class DiditService(HttpClient httpClient, IConfiguration config)
{
    private const string DefaultBaseUrl = "https://verification.didit.me/";
    private static readonly TimeSpan WebhookMaxAge = TimeSpan.FromMinutes(5);

    private string ApiKey => config["Didit:ApiKey"] ?? string.Empty;
    private string WebhookSecret => config["Didit:WebhookSecret"] ?? string.Empty;
    private Uri BaseUri => new(config["Didit:BaseUrl"] is { Length: > 0 } url ? url : DefaultBaseUrl);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(WebhookSecret);

    public string ShopWorkflowId => config["Didit:ShopWorkflowId"] ?? string.Empty;
    public string SupplierWorkflowId => config["Didit:SupplierWorkflowId"] ?? string.Empty;
    public string? ShopCallbackUrl => NullIfEmpty(config["Didit:ShopCallbackUrl"]);
    public string? SupplierCallbackUrl => NullIfEmpty(config["Didit:SupplierCallbackUrl"]);

    public sealed record Session(string SessionId, string Url, string Status);
    public sealed record Decision(string Status, string? Notes);

    public async Task<Session> CreateSessionAsync(
        string workflowId, string vendorData, string? callbackUrl, object? expectedDetails, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new InvalidOperationException("The Didit workflow ID is not configured.");

        var body = new Dictionary<string, object> { ["workflow_id"] = workflowId, ["vendor_data"] = vendorData, ["language"] = "en" };
        if (callbackUrl is not null) body["callback"] = callbackUrl;
        if (expectedDetails is not null) body["expected_details"] = expectedDetails;

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, "v3/session/"))
        {
            Content = JsonContent.Create(body),
        };
        using var response = await SendAsync(request, ct);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = json.RootElement;
        return new Session(
            root.GetProperty("session_id").GetString()!,
            root.GetProperty("url").GetString()!,
            root.TryGetProperty("status", out var status) ? status.GetString() ?? "Not Started" : "Not Started");
    }

    /// <summary>Fetches a session's current result, for when a webhook was missed or is still on its way.</summary>
    public async Task<Decision> GetDecisionAsync(string sessionId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, $"v3/session/{Uri.EscapeDataString(sessionId)}/decision/"));
        using var response = await SendAsync(request, ct);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return new Decision(json.RootElement.GetProperty("status").GetString() ?? "Not Started", ExtractNotes(json.RootElement));
    }

    /// <summary>
    /// Checks Didit's X-Signature header: HMAC-SHA256 of the raw body with the webhook secret, hex
    /// encoded, sent within the last five minutes (X-Timestamp, Unix seconds).
    /// </summary>
    public bool IsValidWebhook(ReadOnlySpan<byte> payload, string? signature, string? timestamp, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(WebhookSecret) || string.IsNullOrWhiteSpace(signature)) return false;
        if (!long.TryParse(timestamp, out var sentAt) ||
            (now - DateTimeOffset.FromUnixTimeSeconds(sentAt)).Duration() > WebhookMaxAge)
            return false;

        byte[] supplied;
        try { supplied = Convert.FromHexString(signature.Trim()); }
        catch (FormatException) { return false; }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), payload);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    /// <summary>
    /// Collects the warnings Didit raised across every check (ID document, liveness, face match,
    /// Home Affairs, registry, sanctions...) as one line each, for the admin to read.
    /// Feature results are top-level arrays (e.g. id_verifications[]) whose items carry warnings[].
    /// </summary>
    public static string? ExtractNotes(JsonElement decision)
    {
        if (decision.ValueKind != JsonValueKind.Object) return null;
        var notes = new List<string>();
        foreach (var feature in decision.EnumerateObject())
        {
            if (feature.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in feature.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("warnings", out var warnings) ||
                    warnings.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var warning in warnings.EnumerateArray())
                {
                    var text = warning.ValueKind == JsonValueKind.Object && warning.TryGetProperty("short_description", out var d)
                        ? d.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(text) && !notes.Contains(text)) notes.Add(text);
                }
            }
        }
        if (notes.Count == 0) return null;
        var joined = string.Join('\n', notes);
        return joined.Length <= 2000 ? joined : joined[..2000];
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("Didit is not configured.");
        request.Headers.Add("x-api-key", ApiKey);
        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();
            throw new HttpRequestException(
                $"Didit returned {(int)response.StatusCode}: {(detail.Length > 300 ? detail[..300] : detail)}");
        }
        return response;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
