using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Reads an uploaded compliance document with Claude's vision model: what kind of document it is,
/// whose name and numbers are on it, its dates, and any signs it was edited. Only the document is
/// sent, never the profile, so the reading isn't steered towards the answer; DocumentRules compares.
/// Off (returns null) when no Anthropic API key is configured.
/// </summary>
public class DocumentReader(IConfiguration config, ILogger<DocumentReader> logger)
{
    private const string Model = "claude-opus-5-5";

    private const string Instructions = """
        You read compliance documents uploaded to SpazaSure, a South African marketplace for
        spaza shops and suppliers, so they can be checked against the account that uploaded them.

        Copy what is printed, exactly. Use null for anything you cannot read; never guess names,
        numbers or dates. Dates are YYYY-MM-DD (for a month-only date use the last day of the month).
        - holderName: the person the document is about (ID holder, licence holder, tenant, permit holder).
        - businessName: the company, close corporation or shop named on it, if any.
        - idNumber: a 13-digit SA ID number (or passport number), digits only.
        - registrationNumber: a CIPC registration number such as 2020/123456/07.
        - taxNumber: an income tax or VAT number, or a SARS tax compliance PIN.
        - expiryDate: when it stops being valid ("valid until", "expiry", a lease end date).
        readable is false for blurry, cut-off, blank or non-document images.
        matchesRequestedType says whether this really is the requested kind of document.
        tamperingSigns: only clear signs of editing you can see, e.g. text in a different font
        over a field, mismatched backgrounds behind names or dates, a photo pasted over another,
        or a screenshot of a template. Leave it empty if the document looks normal.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private string? ApiKey =>
        config["Anthropic:ApiKey"] is { Length: > 0 } key ? key : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Returns the reading, or null with a reason when the document can't be read automatically.</summary>
    public virtual async Task<(DocumentReading? Reading, string? Unavailable)> ReadAsync(
        byte[] file, string fileName, string requested, CancellationToken ct = default)
    {
        if (!Enabled) return (null, "Automatic checks are off (no Anthropic API key).");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        BetaContentBlockParam? block = ext switch
        {
            ".pdf" => new BetaRequestDocumentBlock { Source = new BetaBase64PdfSource { Data = Convert.ToBase64String(file) } },
            ".jpg" or ".jpeg" => Image("image/jpeg"),
            ".png" => Image("image/png"),
            ".webp" => Image("image/webp"),
            _ => null,
        };
        if (block is null) return (null, "Word files can't be checked automatically. Ask for a PDF or a photo instead.");

        BetaImageBlockParam Image(string mediaType) =>
            new() { Source = new BetaBase64ImageSource { MediaType = mediaType, Data = Convert.ToBase64String(file) } };

        var client = new AnthropicClient { ApiKey = ApiKey, Timeout = TimeSpan.FromSeconds(120) };
        try
        {
            var response = await client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 16000,
                Betas = ["server-side-fallback-2026-07-01"],
                // A declined request is re-served by a fallback model chosen by refusal category
                Fallbacks = new Default(),
                OutputConfig = new BetaOutputConfig
                {
                    Effort = Effort.Medium,
                    Format = new BetaJsonOutputFormat { Schema = Schema },
                },
                System = Instructions,
                Messages =
                [
                    new BetaUserMessageParam
                    {
                        Content = new List<BetaContentBlockParam>
                        {
                            block,
                            new BetaTextBlockParam { Text = $"The uploader says this is {requested}. Read it." },
                        },
                    },
                ],
            }, ct);

            if (response.StopReason == "refusal") return (null, "The document could not be read automatically.");
            var json = string.Concat(response.Content.Select(b => b.TryPickText(out var t) ? t.Text : null).Where(t => t is not null));
            var reading = JsonSerializer.Deserialize<DocumentReading>(json, JsonOptions);
            return reading is null ? (null, "The document could not be read automatically.") : (reading, null);
        }
        catch (AnthropicApiException ex)
        {
            logger.LogError(ex, "Document check failed");
            return (null, "The automatic check isn't available right now.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Document check could not be completed");
            return (null, "The automatic check isn't available right now.");
        }
    }

    private static JsonElement Text(string description) =>
        JsonSerializer.SerializeToElement(new { type = new[] { "string", "null" }, description });

    private static readonly Dictionary<string, JsonElement> Properties = new()
    {
        ["readable"] = JsonSerializer.SerializeToElement(new { type = "boolean" }),
        ["documentKind"] = Text("What the document actually is, e.g. 'South African smart ID card'"),
        ["matchesRequestedType"] = JsonSerializer.SerializeToElement(new { type = "boolean" }),
        ["holderName"] = Text("Full name of the person the document is about"),
        ["businessName"] = Text("Company or shop name on the document"),
        ["idNumber"] = Text("SA ID or passport number, digits only"),
        ["registrationNumber"] = Text("CIPC registration number"),
        ["taxNumber"] = Text("Tax, VAT or SARS PIN number"),
        ["address"] = Text("Address on the document"),
        ["issueDate"] = Text("Issue date, YYYY-MM-DD"),
        ["expiryDate"] = Text("Expiry or end date, YYYY-MM-DD"),
        ["issuer"] = Text("Who issued it, e.g. Department of Home Affairs, CIPC, SARS, a municipality"),
        ["tamperingSigns"] = JsonSerializer.SerializeToElement(new { type = "array", items = new { type = "string" } }),
        ["notes"] = Text("Anything else an admin should know"),
    };

    private static readonly Dictionary<string, JsonElement> Schema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(Properties),
        ["required"] = JsonSerializer.SerializeToElement(Properties.Keys),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };
}
