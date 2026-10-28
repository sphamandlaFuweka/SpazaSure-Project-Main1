using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>What was read off an uploaded document (by DocumentReader).</summary>
public sealed class DocumentReading
{
    [JsonPropertyName("readable")] public bool Readable { get; set; }
    /// <summary>What the document actually is, e.g. "South African smart ID card".</summary>
    [JsonPropertyName("documentKind")] public string? DocumentKind { get; set; }
    [JsonPropertyName("matchesRequestedType")] public bool MatchesRequestedType { get; set; }
    [JsonPropertyName("holderName")] public string? HolderName { get; set; }
    [JsonPropertyName("businessName")] public string? BusinessName { get; set; }
    [JsonPropertyName("idNumber")] public string? IdNumber { get; set; }
    [JsonPropertyName("registrationNumber")] public string? RegistrationNumber { get; set; }
    [JsonPropertyName("taxNumber")] public string? TaxNumber { get; set; }
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("issueDate")] public string? IssueDate { get; set; }
    [JsonPropertyName("expiryDate")] public string? ExpiryDate { get; set; }
    [JsonPropertyName("issuer")] public string? Issuer { get; set; }
    [JsonPropertyName("tamperingSigns")] public List<string> TamperingSigns { get; set; } = [];
    [JsonPropertyName("notes")] public string? Notes { get; set; }
}

/// <summary>What a document should say, taken from the profile of the account that uploaded it.</summary>
public sealed record DocumentProfile(
    string DocType,
    string Label,
    IReadOnlyList<string> PersonNames,
    IReadOnlyList<string> BusinessNames,
    string? IdNumber = null,
    string? RegistrationNumber = null,
    string? Address = null,
    /// <summary>The expiry date someone typed when uploading (driver documents).</summary>
    DateOnly? EnteredExpiry = null);

public sealed record DocumentFinding(string Label, string Status, string Detail);

/// <summary>The outcome: passed, warning (an admin should look) or failed (it doesn't belong to this account, or isn't valid).</summary>
public sealed record DocumentVerdict(
    string Status, string Summary, List<DocumentFinding> Findings, DateOnly? Expiry, string? DocumentNumber, bool Expired);

/// <summary>
/// Compares a document with the profile: right kind of document, the profile's name and ID or
/// registration number, in date, and not already used by another account. Plain rules, so a
/// document is never passed or failed on the model's say-so alone.
/// </summary>
public static class DocumentRules
{
    private enum Holder { Person, Business, Either }

    /// <summary>How each document type is checked.</summary>
    private sealed record Rule(string Description, Holder Holder, bool HasIdNumber = false, bool HasRegistration = false,
        bool MustExpire = false, int? MaxAgeMonths = null, bool HasAddress = false);

    private static Rule RuleFor(string docType) => docType switch
    {
        "id_document" => new("a South African ID document (green ID book or smart ID card) or a passport", Holder.Person, HasIdNumber: true),
        "cipc_certificate" => new("a CIPC company registration certificate (CoR14.3 or similar)", Holder.Business, HasRegistration: true),
        "tax_clearance" => new("a SARS tax compliance status (TCS) letter or tax clearance certificate", Holder.Business, MaxAgeMonths: 12),
        "bee_certificate" => new("a B-BBEE certificate or sworn affidavit", Holder.Business, MustExpire: true, MaxAgeMonths: 12),
        "product_license" => new("a product licence, permit or registration certificate", Holder.Business),
        "business_permit" => new("a municipal business or trading permit/licence", Holder.Either, HasAddress: true),
        "health_certificate" or "health_cert" => new("a health certificate / certificate of acceptability for food premises", Holder.Either, HasAddress: true),
        "lease" => new("a lease or rental agreement for the shop premises", Holder.Either, HasAddress: true),
        "licence" => new("a South African driving licence card", Holder.Person, MustExpire: true),
        "prdp" => new("a professional driving permit (PrDP)", Holder.Person, MustExpire: true),
        _ => new("the requested document", Holder.Either),
    };

    /// <summary>The description given to the document reader, so it can say whether the upload is the right kind.</summary>
    public static string Describe(string docType) => RuleFor(docType).Description;

    public static DocumentVerdict Evaluate(DocumentReading r, DocumentProfile p, DateOnly today, string? numberUsedBy)
    {
        var rule = RuleFor(p.DocType);
        var findings = new List<DocumentFinding>();
        void Add(string label, string status, string detail) => findings.Add(new(label, status, detail));

        if (!r.Readable)
        {
            Add("Readable", "fail", "The document could not be read. Upload a clear, complete photo or PDF.");
            return Done(findings, null, null, false);
        }
        if (!r.MatchesRequestedType)
            Add("Document type", "fail", $"This looks like {Article(r.DocumentKind ?? "a different document")}, not {rule.Description}.");
        else
            Add("Document type", "pass", r.DocumentKind ?? p.Label);

        // Whose document it is
        var (wanted, read, whose) = rule.Holder switch
        {
            Holder.Person => (p.PersonNames, r.HolderName, "the person on the profile"),
            Holder.Business => (p.BusinessNames, r.BusinessName ?? r.HolderName, "the business on the profile"),
            _ => (p.PersonNames.Concat(p.BusinessNames).ToList(), r.BusinessName ?? r.HolderName, "the shop or its owner"),
        };
        var onDocument = rule.Holder == Holder.Either
            ? new[] { r.BusinessName, r.HolderName }.Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
            : string.IsNullOrWhiteSpace(read) ? [] : new List<string> { read! };
        var known = wanted.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        if (onDocument.Count == 0)
            Add("Name", "warn", "No name could be read on the document.");
        else if (known.Count == 0)
            Add("Name", "warn", $"The document is in the name of {onDocument[0]}, but the profile has no name to compare it with.");
        else if (onDocument.Any(d => known.Any(k => NamesMatch(d, k, rule.Holder == Holder.Business))))
            Add("Name", "pass", $"In the name of {onDocument[0]}, matching {whose}.");
        else
            Add("Name", "fail", $"The document is in the name of {onDocument[0]}, but the profile says {string.Join(" / ", known)}.");

        // ID and registration numbers
        string? number = null;
        if (rule.HasIdNumber)
        {
            var id = SaValidation.Digits(r.IdNumber);
            if (id.Length == 0) Add("ID number", "warn", "No ID number could be read.");
            else if (id.Length == 13 && !SaValidation.IsValidIdNumber(id))
                Add("ID number", "fail", $"{id} is not a valid South African ID number.");
            else if (!string.IsNullOrEmpty(p.IdNumber) && SaValidation.Digits(p.IdNumber) != id)
                Add("ID number", "fail", $"The ID number on the document ({Mask(id)}) is not the one on the profile ({Mask(SaValidation.Digits(p.IdNumber))}).");
            else
                Add("ID number", "pass", string.IsNullOrEmpty(p.IdNumber) ? $"{Mask(id)} (no ID number on the profile to compare)" : "Matches the profile.");
            if (id.Length > 0) number = id;
        }
        if (rule.HasRegistration)
        {
            var reg = NormalizeRegistration(r.RegistrationNumber);
            if (reg is null) Add("Registration number", "warn", "No registration number could be read.");
            else if (!string.IsNullOrEmpty(p.RegistrationNumber) && NormalizeRegistration(p.RegistrationNumber) != reg)
                Add("Registration number", "fail", $"The document shows {reg}, but the profile says {p.RegistrationNumber}.");
            else
                Add("Registration number", "pass", string.IsNullOrEmpty(p.RegistrationNumber) ? $"{reg} (no number on the profile to compare)" : "Matches the profile.");
            number = reg ?? number;
        }
        if (number is not null && numberUsedBy is not null)
            Add("Used elsewhere", "fail", $"This {(rule.HasIdNumber ? "ID" : "registration")} number is already on the documents of another account ({numberUsedBy}).");

        // Dates
        var expiry = ParseDate(r.ExpiryDate);
        var issued = ParseDate(r.IssueDate);
        var expired = expiry is { } e && e < today;
        if (expired)
            Add("Expiry", "fail", $"Expired on {expiry:d MMM yyyy}. Upload a current one.");
        else if (expiry is { } soon && soon <= today.AddDays(30))
            Add("Expiry", "warn", $"Expires soon, on {soon:d MMM yyyy}.");
        else if (expiry is { } valid)
            Add("Expiry", "pass", $"Valid until {valid:d MMM yyyy}.");
        else if (rule.MustExpire)
            Add("Expiry", "warn", "No expiry date could be read.");

        if (rule.MaxAgeMonths is { } months && expiry is null && issued is { } i && i < today.AddMonths(-months))
        {
            Add("Issue date", "fail", $"Issued on {i:d MMM yyyy}. It must be less than {months} months old.");
            expired = true;
        }
        if (p.EnteredExpiry is { } entered && expiry is { } readExpiry && entered != readExpiry)
            Add("Expiry", "warn", $"The expiry typed in ({entered:d MMM yyyy}) is not the one on the document ({readExpiry:d MMM yyyy}).");

        if (rule.HasAddress && !string.IsNullOrWhiteSpace(p.Address) && !string.IsNullOrWhiteSpace(r.Address)
            && !AddressesMatch(r.Address!, p.Address!))
            Add("Address", "warn", $"The document's address ({r.Address}) doesn't look like the shop's ({p.Address}).");

        foreach (var sign in r.TamperingSigns.Take(3))
            Add("Looks edited", "warn", sign);

        return Done(findings, expiry, number, expired);
    }

    private static DocumentVerdict Done(List<DocumentFinding> findings, DateOnly? expiry, string? number, bool expired)
    {
        var fail = findings.FirstOrDefault(f => f.Status == "fail");
        var warn = findings.FirstOrDefault(f => f.Status == "warn");
        var status = fail is not null ? "failed" : warn is not null ? "warning" : "passed";
        var summary = fail?.Detail ?? warn?.Detail ?? "Matches the profile.";
        return new(status, summary.Length > 500 ? summary[..500] : summary, findings, expiry, number, expired);
    }

    // ── Names ───────────────────────────────────────────────────────────────

    private static readonly HashSet<string> Filler =
    [
        "pty", "ltd", "limited", "proprietary", "cc", "inc", "npc", "rf", "the", "and", "of", "trading", "as", "ta",
        "mr", "mrs", "ms", "miss", "dr", "prof", "spaza", "shop", "store", "tuck", "enterprise", "enterprises",
    ];

    public static List<string> Tokens(string name)
    {
        var plain = new string(name.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var spaced = new string(plain.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
        return spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !Filler.Contains(t))
            .Distinct()
            .ToList();
    }

    private static bool TokenMatches(string a, string b) =>
        a == b
        || (a.Length == 1 && b.StartsWith(a)) || (b.Length == 1 && a.StartsWith(b))      // initials: "S" ~ "Sipho"
        || (Math.Min(a.Length, b.Length) >= 4 && (a.StartsWith(b) || b.StartsWith(a))); // "Thandi" ~ "Thandiwe"

    /// <summary>
    /// Whether two names are the same person or business, allowing initials, extra names and
    /// "(Pty) Ltd". People need two names in common (or the only one given); businesses most of theirs.
    /// </summary>
    public static bool NamesMatch(string onDocument, string onProfile, bool business = false)
    {
        var a = Tokens(onDocument);
        var b = Tokens(onProfile);
        if (a.Count == 0 || b.Count == 0) return false;
        var (shorter, longer) = a.Count <= b.Count ? (a, b) : (b, a);
        var matched = shorter.Count(s => longer.Any(l => TokenMatches(s, l)));
        // At least one full word must match, so two sets of initials never count as a match
        var fullWord = shorter.Any(s => s.Length > 1 && longer.Any(l => l.Length > 1 && TokenMatches(s, l)));
        if (!fullWord) return false;
        return business
            ? matched >= Math.Max(1, (int)Math.Ceiling(shorter.Count * 0.6))
            : matched >= Math.Min(2, shorter.Count);
    }

    private static bool AddressesMatch(string a, string b)
    {
        var ta = Tokens(a).Where(t => t.Length > 2 || t.Any(char.IsDigit)).ToList();
        var tb = Tokens(b).Where(t => t.Length > 2 || t.Any(char.IsDigit)).ToList();
        if (ta.Count == 0 || tb.Count == 0) return true;
        return ta.Count(t => tb.Contains(t)) >= Math.Min(2, Math.Min(ta.Count, tb.Count));
    }

    // ── Numbers and dates ───────────────────────────────────────────────────

    /// <summary>CIPC numbers as YYYY/NNNNNN/NN, whatever separators were used; null when it isn't one.</summary>
    public static string? NormalizeRegistration(string? value)
    {
        var digits = SaValidation.Digits(value);
        return digits.Length == 12 ? $"{digits[..4]}/{digits[4..10]}/{digits[10..]}" : null;
    }

    private static string Mask(string id) => id.Length > 6 ? id[..6] + new string('*', id.Length - 6) : id;

    private static string Article(string text) =>
        text.StartsWith("a ", StringComparison.OrdinalIgnoreCase) || text.StartsWith("an ", StringComparison.OrdinalIgnoreCase)
            ? text : ("aeiou".Contains(char.ToLowerInvariant(text[0])) ? "an " : "a ") + text;

    public static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
