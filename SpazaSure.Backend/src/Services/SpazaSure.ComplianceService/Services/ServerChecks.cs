using System.Globalization;
using System.Text.RegularExpressions;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.ComplianceService.Services;

/// <summary>
/// Checks that run entirely on the server through Didit's APIs, with no link for anyone to open:
/// a supplier's business (CIPC register and sanctions screening), and a shop owner's sanctions
/// screening after an admin has checked their ID in person.
/// </summary>
public static partial class ServerChecks
{
    public sealed record Outcome(string Status, string Notes);

    private static readonly string[] InactiveWords =
        ["deregist", "liquidat", "dissol", "inactive", "closed", "struck", "final deregistration", "in business rescue"];

    /// <summary>
    /// The supplier's company must be on the CIPC register with the same registration number, still
    /// active, and not on a sanctions or watch list. A different registered name goes to review.
    /// </summary>
    public static async Task<Outcome> CheckBusinessAsync(DiditService didit, Supplier supplier, bool sandbox, CancellationToken ct)
    {
        var registration = supplier.RegistrationNumber!;
        var vendorData = "supplier:" + supplier.Id;
        if (sandbox)
        {
            return new("In Review",
                $"Didit sandbox mode: simulated registry and screening results are hidden.\n" +
                $"Profile submitted for verification: {supplier.CompanyName} (CIPC registration {registration}).\n" +
                "Run this check with live Didit credentials before approving the business.");
        }

        var companies = await didit.SearchCompanyAsync(registration, vendorData, ct);
        var company = companies.FirstOrDefault(c => SaValidation.Digits(c.RegistrationNumber) == SaValidation.Digits(registration));
        if (company is null)
            return new("Declined", $"No company with registration number {registration} was found on the CIPC register.");

        if (company.Status is { } status && InactiveWords.Any(w => status.Contains(w, StringComparison.OrdinalIgnoreCase)))
            return new("Declined", $"The company is listed as \"{status}\" on the CIPC register, so it can't trade on SpazaSure.");

        var notes = new List<string> { $"Found on the CIPC register: {company.Name} ({company.RegistrationNumber}), status {company.Status ?? "unknown"}." };
        var review = false;
        if (!SameCompanyName(company.Name, supplier.CompanyName))
        {
            notes.Add($"The registered name \"{company.Name}\" is different from the name on the profile, \"{supplier.CompanyName}\". Check they are the same business.");
            review = true;
        }

        var aml = await didit.ScreenAmlAsync(company.Name ?? supplier.CompanyName, "company", null, null, vendorData, ct);
        switch (aml.Status)
        {
            case "Declined":
                notes.Insert(0, $"The company matched a sanctions or watch list ({aml.TotalHits} match(es), risk score {aml.Score:0}).");
                return new("Declined", string.Join('\n', notes));
            case "In Review":
                notes.Add($"Possible sanctions or watch-list match ({aml.TotalHits} match(es), risk score {aml.Score:0}). Review it in the Didit console.");
                review = true;
                break;
            default:
                notes.Add("Sanctions and watch-list screening: clear.");
                break;
        }
        return new(review ? "In Review" : "Approved", string.Join('\n', notes));
    }

    /// <summary>
    /// After an admin has seen the owner's original ID in person, screens the owner against
    /// sanctions, PEP and watch lists using their name, ID number and date of birth.
    /// </summary>
    public static async Task<Outcome> CheckOwnerInPersonAsync(DiditService didit, SpazaShop shop, string adminName, CancellationToken ct)
    {
        var aml = await didit.ScreenAmlAsync(shop.OwnerName, "person", DateOfBirth(shop.OwnerIdNumber), shop.OwnerIdNumber, "shop:" + shop.Id, ct);
        var verifiedBy = $"Original ID seen in person by {adminName} on {DateTime.UtcNow:d MMM yyyy}.";
        return aml.Status switch
        {
            "Declined" => new("Declined", $"The owner matched a sanctions or watch list ({aml.TotalHits} match(es), risk score {aml.Score:0}).\n{verifiedBy}"),
            "In Review" => new("In Review", $"Possible sanctions or watch-list match ({aml.TotalHits} match(es), risk score {aml.Score:0}). Review it in the Didit console.\n{verifiedBy}"),
            _ => new("Approved", $"{verifiedBy}\nSanctions and watch-list screening: clear."),
        };
    }

    /// <summary>The date of birth in a SA ID number (YYMMDD), as YYYY-MM-DD.</summary>
    public static string? DateOfBirth(string? idNumber)
    {
        if (!SaValidation.IsValidIdNumber(idNumber)) return null;
        var digits = SaValidation.Digits(idNumber);
        var yy = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var century = yy > DateTime.UtcNow.Year % 100 ? 1900 : 2000;
        return $"{century + yy:0000}-{digits[2..4]}-{digits[4..6]}";
    }

    /// <summary>Company names compared without case, punctuation and "(Pty) Ltd"-style suffixes.</summary>
    public static bool SameCompanyName(string? registered, string? profile) =>
        CoreName(registered) is { Length: > 0 } a && a == CoreName(profile);

    private static string CoreName(string? name) =>
        CompanySuffix().Replace(NonAlphanumeric().Replace((name ?? "").ToLowerInvariant(), " "), " ")
            .Replace(" ", "");

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"\b(pty|proprietary|ltd|limited|inc|incorporated|cc|npc|soc|rf|co)\b")]
    private static partial Regex CompanySuffix();
}
