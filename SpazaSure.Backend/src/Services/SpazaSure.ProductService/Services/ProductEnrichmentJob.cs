using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;

namespace SpazaSure.ProductService.Services;

public sealed record EnrichmentResult(
    Guid ProductId, string Name, string? Supplier, string? Code,
    string Status, List<string> Filled, string? Note, string? Source);

public sealed record EnrichmentProgress(
    bool Running, bool DryRun, DateTime? StartedAt, DateTime? FinishedAt,
    int Total, int Processed, Dictionary<string, int> Counts, List<EnrichmentResult> Results);

/// <summary>
/// Runs <see cref="ProductEnricher"/> over every product in the background, one product at a time
/// and slowly, because the open databases ask apps not to hammer them. An admin starts it and
/// polls its progress. A dry run reports what would change without saving anything.
/// </summary>
public sealed class ProductEnrichmentJob(IServiceScopeFactory scopes, ILogger<ProductEnrichmentJob> logger)
{
    /// <summary>Pause between lookups; each can query up to four databases.</summary>
    public static TimeSpan Pause { get; set; } = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private EnrichmentProgress _progress = new(false, false, null, null, 0, 0, [], []);

    public EnrichmentProgress Progress
    {
        get { lock (_gate) return _progress with { Counts = new(_progress.Counts), Results = [.. _progress.Results] }; }
    }

    /// <summary>Starts a run. False when one is already running.</summary>
    public bool TryStart(bool dryRun, Guid? supplierId = null)
    {
        lock (_gate)
        {
            if (_progress.Running) return false;
            _progress = new(true, dryRun, DateTime.UtcNow, null, 0, 0, [], []);
        }
        _ = Task.Run(() => RunAsync(dryRun, supplierId, CancellationToken.None));
        return true;
    }

    public async Task RunAsync(bool dryRun, Guid? supplierId, CancellationToken ct)
    {
        try
        {
            List<Guid> ids;
            using (var scope = scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SpazaSureDbContext>();
                var query = db.Products.AsNoTracking();
                if (supplierId is { } s) query = query.Where(p => p.SupplierId == s);
                ids = await query.OrderBy(p => p.CreatedAt).Select(p => p.Id).ToListAsync(ct);
            }
            lock (_gate) _progress = _progress with { Total = ids.Count };

            foreach (var id in ids)
            {
                var (result, lookedUp) = await EnrichOneAsync(id, dryRun, ct);
                lock (_gate)
                {
                    var counts = _progress.Counts;
                    counts[result.Status] = counts.GetValueOrDefault(result.Status) + 1;
                    _progress.Results.Add(result);
                    _progress = _progress with { Processed = _progress.Processed + 1 };
                }
                if (lookedUp) await Task.Delay(Pause, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Product enrichment run failed");
        }
        finally
        {
            lock (_gate) _progress = _progress with { Running = false, FinishedAt = DateTime.UtcNow };
        }
    }

    private async Task<(EnrichmentResult Result, bool LookedUp)> EnrichOneAsync(Guid id, bool dryRun, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpazaSureDbContext>();
        var lookup = scope.ServiceProvider.GetRequiredService<OpenFoodFactsService>();

        var product = await db.Products.Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return (new(id, "(deleted)", null, null, "invalid", [], "The product was deleted during the run.", null), false);

        EnrichmentResult Result(string status, List<string>? filled = null, string? note = null, string? source = null, string? code = null) =>
            new(product.Id, product.Name, product.Supplier?.CompanyName, code, status, filled ?? [], note, source);

        var code = ProductEnricher.LookupCode(product);
        if (code is null)
            return (Result("no_barcode", note: "No real pack barcode (only a SpazaSure-made code or none), so there is nothing to look up."), false);

        OpenProductLookup found;
        try
        {
            found = await lookup.LookupAsync(code, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Lookup failed for product {ProductId}", product.Id);
            return (Result("lookup_failed", note: "The product databases could not be reached.", code: code), true);
        }

        if (found.Product is not { } record)
            return (found.Unavailable
                ? Result("lookup_failed", note: "A product database could not be reached; try again later.", code: code)
                : Result("not_found", note: "None of the open product databases has this barcode.", code: code), true);

        if (!ProductEnricher.SameProduct(product, record))
            return (Result("mismatch",
                note: $"The barcode belongs to \"{record.Name ?? record.GenericName}\"{(record.Brands is { } b ? $" ({b})" : "")}. Check the barcode before filling anything in.",
                source: record.DataSource, code: code), true);

        if (ProductEnricher.SizeConflict(product, record) is { } otherSize)
            return (Result("mismatch",
                note: $"The barcode belongs to a {otherSize} pack of \"{record.Name}\", not this listing's size. Check the barcode before filling anything in.",
                source: record.DataSource, code: code), true);

        var (filled, error) = ProductEnricher.Apply(product, record, code);
        if (error is not null)
            return (Result("invalid", note: error, source: record.DataSource, code: code), true);
        if (filled.Count == 0)
            return (Result("up_to_date", note: "Every section the database could fill already has information.", source: record.DataSource, code: code), true);

        if (!dryRun)
        {
            product.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return (Result("filled", filled, source: record.DataSource, code: code), true);
    }
}
