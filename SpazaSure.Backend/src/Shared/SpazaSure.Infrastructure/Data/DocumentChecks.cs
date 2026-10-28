using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Checks every uploaded compliance document against the profile of the account that uploaded it,
/// so people can't pass with someone else's documents or with invalid or out-of-date ones:
///  - expired (or too old) documents are rejected straight away, with the reason;
///  - a different name, ID or registration number, the wrong kind of document, or a number already
///    on another account's documents fails the check: the uploader is told, admins are alerted, and
///    an admin can only approve it with a reason;
///  - anything that couldn't be confirmed is a warning for the admin.
/// Runs after the upload has been saved (see Queue), from the file that was uploaded.
/// </summary>
public class DocumentChecks(SpazaSureDbContext db, DocumentReader reader, EventPublisher events, ILogger<DocumentChecks> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Runs a check in the background with its own database context, so the upload returns at once.
    /// Pass the request's services (HttpContext.RequestServices); does nothing where checks aren't set up.
    /// </summary>
    public static void Queue(IServiceProvider? services, Func<DocumentChecks, Task> check)
    {
        if (services?.GetService<IServiceProviderIsService>()?.IsService(typeof(DocumentChecks)) != true) return;
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            using var scope = scopes.CreateScope();
            try { await check(scope.ServiceProvider.GetRequiredService<DocumentChecks>()); }
            catch (Exception ex) { scope.ServiceProvider.GetService<ILogger<DocumentChecks>>()?.LogError(ex, "Document check crashed"); }
        });
    }

    public async Task CheckShopDocumentAsync(Guid documentId, byte[] file, string fileName)
    {
        var doc = await db.ShopDocuments.Include(d => d.Shop).FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc?.DocUrl is null) return;
        var url = doc.DocUrl;
        var shop = doc.Shop;
        var profile = new DocumentProfile(doc.DocType, Label(doc.DocType), [shop.OwnerName], [shop.ShopName],
            IdNumber: shop.OwnerIdNumber,
            Address: string.Join(", ", new[] { shop.Address, shop.City }.Where(s => !string.IsNullOrWhiteSpace(s))));

        var result = await RunAsync(profile, file, fileName, n => UsedByAsync(n, shopId: shop.Id));
        if (result is null) return;
        await db.Entry(doc).ReloadAsync();
        if (doc.DocUrl != url) return; // replaced by a newer upload while this one was being read

        Apply(result.Value, doc);
        if (result.Value.Verdict is { } v && v.Expiry is { } expiry) doc.ExpiryDate = expiry;
        var rejected = Reject(result.Value, () => { doc.Status = "rejected"; doc.RejectionNote = doc.CheckSummary; });
        await db.SaveChangesAsync();
        await TellAsync(result.Value, rejected, shop.UserId, shop.ShopName, doc.DocType, doc.Id);
    }

    public async Task CheckSupplierDocumentAsync(Guid documentId, byte[] file, string fileName)
    {
        var doc = await db.SupplierDocuments.Include(d => d.Supplier).FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return;
        var url = doc.DocUrl;
        var s = doc.Supplier;
        var profile = new DocumentProfile(doc.DocType, Label(doc.DocType), [s.ContactPerson], [s.CompanyName],
            RegistrationNumber: s.RegistrationNumber,
            Address: string.Join(", ", new[] { s.Address, s.City }.Where(x => !string.IsNullOrWhiteSpace(x))));

        var result = await RunAsync(profile, file, fileName, n => UsedByAsync(n, supplierId: s.Id));
        if (result is null) return;
        await db.Entry(doc).ReloadAsync();
        if (doc.DocUrl != url) return;

        Apply(result.Value, doc);
        if (result.Value.Verdict is { } v && v.Expiry is { } expiry) doc.ExpiryDate = expiry;
        var rejected = Reject(result.Value, () => { doc.Status = "rejected"; doc.RejectionNote = doc.CheckSummary; });
        await db.SaveChangesAsync();
        await TellAsync(result.Value, rejected, s.UserId, s.CompanyName, doc.DocType, doc.Id);
    }

    /// <summary>Driver documents are uploaded by admins, so the result is shown on the Drivers page rather than sent to anyone.</summary>
    public async Task CheckDriverDocumentAsync(Guid documentId, byte[] file, string fileName)
    {
        var doc = await db.DriverDocuments.Include(d => d.Driver).FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return;
        var url = doc.DocUrl;
        var profile = new DocumentProfile(doc.DocType, Label(doc.DocType), [doc.Driver.FullName], [],
            EnteredExpiry: doc.ExpiryDate);

        var result = await RunAsync(profile, file, fileName, n => UsedByAsync(n, driverId: doc.DriverId));
        if (result is null) return;
        await db.Entry(doc).ReloadAsync();
        if (doc.DocUrl != url) return;

        Apply(result.Value, doc);
        // Trust the earlier date: a typo must not keep an expired licence "valid"
        if (result.Value.Verdict is { Expiry: { } read } && (doc.ExpiryDate is null || read < doc.ExpiryDate)) doc.ExpiryDate = read;
        await db.SaveChangesAsync();
        if (doc.CheckStatus == "failed")
            await AdminAlerts.NotifyAdminsAsync(db, events, "compliance", "Driver document didn't pass the check",
                $"{doc.Driver.FullName}'s {Label(doc.DocType)}: {doc.CheckSummary}", doc.DriverId.ToString());
    }

    // ── Shared steps ────────────────────────────────────────────────────────

    private readonly record struct CheckResult(DocumentVerdict? Verdict, DocumentReading? Reading, string? Unavailable, string? OtherKindUse);

    private async Task<CheckResult?> RunAsync(DocumentProfile profile, byte[] file, string fileName,
        Func<string, Task<(string? SameKind, string? OtherKind)>> usedBy)
    {
        var (reading, unavailable) = await reader.ReadAsync(file, fileName, DocumentRules.Describe(profile.DocType));
        if (reading is null) return new CheckResult(null, null, unavailable, null);

        var number = profile.DocType == "cipc_certificate"
            ? DocumentRules.NormalizeRegistration(reading.RegistrationNumber)
            : SaValidation.Digits(reading.IdNumber) is { Length: > 0 } id ? id : null;
        var (sameKind, otherKind) = number is null ? (null, null) : await usedBy(number);
        var verdict = DocumentRules.Evaluate(reading, profile, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2)), sameKind);
        if (otherKind is not null)
        {
            // e.g. a shop owner who is also a SpazaSure driver: possible, but worth a look
            verdict.Findings.Add(new("Used elsewhere", "warn", $"The same number is on another SpazaSure account ({otherKind})."));
            if (verdict.Status == "passed") verdict = verdict with { Status = "warning", Summary = verdict.Findings[^1].Detail };
        }
        return new CheckResult(verdict, reading, null, otherKind);
    }

    private static void Apply(CheckResult result, ICheckedDocument doc)
    {
        doc.CheckedAt = DateTime.UtcNow;
        if (result.Verdict is not { } v)
        {
            doc.CheckStatus = "unavailable";
            doc.CheckSummary = result.Unavailable;
            doc.CheckDetails = null;
            return;
        }
        doc.CheckStatus = v.Status;
        doc.CheckSummary = v.Summary;
        doc.DocumentNumber = v.DocumentNumber;
        doc.CheckDetails = JsonSerializer.Serialize(new { findings = v.Findings, read = result.Reading }, Json);
    }

    /// <summary>Expired or too-old documents can't be approved at all, so they are rejected now.</summary>
    private static bool Reject(CheckResult result, Action reject)
    {
        if (result.Verdict is not { Expired: true }) return false;
        reject();
        return true;
    }

    private async Task TellAsync(CheckResult result, bool rejected, Guid userId, string account, string docType, Guid docId)
    {
        if (result.Verdict is not { Status: "failed" } v) return;
        events.PublishNotification(
            supplierId: userId.ToString(),
            type: "compliance",
            title: rejected ? "Document rejected" : "Please check your document",
            message: $"We checked your {Label(docType)}: {v.Summary} " +
                     (rejected ? "Upload a valid, current one." : "If it's wrong, upload the correct document; otherwise SpazaSure will review it."),
            priority: "high",
            referenceId: docId.ToString(),
            routingKey: rejected ? "compliance.rejected" : "compliance.flagged");
        await AdminAlerts.NotifyAdminsAsync(db, events, "compliance",
            rejected ? "Document rejected automatically" : "Document didn't match the profile",
            $"{account}'s {Label(docType)}: {v.Summary}", docId.ToString());
    }

    /// <summary>
    /// Another account with the same ID or registration number on its documents or profile.
    /// Same kind (shop and shop) fails the check; another kind (a shop owner who also drives) is a warning.
    /// </summary>
    private async Task<(string? SameKind, string? OtherKind)> UsedByAsync(
        string number, Guid? shopId = null, Guid? supplierId = null, Guid? driverId = null)
    {
        var shop = await db.ShopDocuments.AsNoTracking()
                       .Where(d => d.DocumentNumber == number && d.ShopId != shopId).Select(d => d.Shop.ShopName).FirstOrDefaultAsync()
                   ?? await db.SpazaShops.AsNoTracking()
                       .Where(s => s.OwnerIdNumber == number && s.Id != shopId).Select(s => s.ShopName).FirstOrDefaultAsync();
        var supplier = await db.SupplierDocuments.AsNoTracking()
                           .Where(d => d.DocumentNumber == number && d.SupplierId != supplierId).Select(d => d.Supplier.CompanyName).FirstOrDefaultAsync()
                       ?? await db.Suppliers.AsNoTracking()
                           .Where(s => s.RegistrationNumber == number && s.Id != supplierId).Select(s => s.CompanyName).FirstOrDefaultAsync();
        var driver = await db.DriverDocuments.AsNoTracking()
            .Where(d => d.DocumentNumber == number && d.DriverId != driverId).Select(d => d.Driver.FullName).FirstOrDefaultAsync();

        var (same, other) = shopId is not null ? (Named("shop", shop), Named("supplier", supplier) ?? Named("driver", driver))
            : supplierId is not null ? (Named("supplier", supplier), Named("shop", shop) ?? Named("driver", driver))
            : (Named("driver", driver), Named("shop", shop) ?? Named("supplier", supplier));
        return (same, other);

        static string? Named(string kind, string? name) => name is null ? null : $"{kind} {name}";
    }

    public static string Label(string docType) => docType switch
    {
        "business_permit" => "business permit",
        "health_cert" or "health_certificate" => "health certificate",
        "lease" => "lease agreement",
        "id_document" => "ID document",
        "cipc_certificate" => "CIPC certificate",
        "tax_clearance" => "tax clearance",
        "bee_certificate" => "B-BBEE certificate",
        "product_license" => "product licence",
        "licence" => "driving licence",
        "prdp" => "professional driving permit",
        _ => docType.Replace('_', ' '),
    };
}
