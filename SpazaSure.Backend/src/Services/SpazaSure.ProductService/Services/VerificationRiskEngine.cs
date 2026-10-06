using System.Text.RegularExpressions;

namespace SpazaSure.ProductService.Services;

public sealed record RiskThresholds(int Review = 21, int Suspicious = 51, int High = 76);

public sealed record RiskInput(
    bool RegisteredInSpazaSure,
    bool FoundInOpenFoodFacts,
    bool IsRecalled,
    DateOnly? ExpiryDate,
    bool? NameMatches,
    int PriorReports);

public sealed record RiskIndicator(string Code, string Severity, string Message);

public sealed record RiskCheck(string Label, string Status);

public sealed record RiskAssessment(
    int Score,
    string Level,
    IReadOnlyList<RiskIndicator> Indicators,
    IReadOnlyList<RiskCheck> Checks);

/// <summary>
/// Scores a scan as a risk level rather than a genuine/fake verdict — the
/// result is guidance for the customer, never a counterfeit determination.
/// </summary>
public static class VerificationRiskEngine
{
    public static RiskAssessment Assess(RiskInput input, DateOnly today, RiskThresholds? thresholds = null)
    {
        thresholds ??= new RiskThresholds();
        var score = 0;
        var indicators = new List<RiskIndicator>();
        var checks = new List<RiskCheck>();

        if (input.RegisteredInSpazaSure)
        {
            checks.Add(new("Product found in SpazaSure registry", "pass"));
        }
        else if (input.FoundInOpenFoodFacts)
        {
            score += 15;
            checks.Add(new("Product found in SpazaSure registry", "warn"));
            indicators.Add(new("not_in_registry", "info",
                "Not in the SpazaSure registry; basic details come from Open Food Facts."));
        }
        else
        {
            score += 35;
            checks.Add(new("Product found in SpazaSure registry", "unknown"));
            indicators.Add(new("unknown_product", "warning",
                "Product could not be found in any product database we check."));
        }

        if (input.IsRecalled)
        {
            score += 100;
            checks.Add(new("No known safety alerts or recalls", "fail"));
            indicators.Add(new("recalled", "critical", "This product or batch has an active safety recall."));
        }
        else if (input.RegisteredInSpazaSure)
        {
            checks.Add(new("No known safety alerts or recalls", "pass"));
        }

        if (input.ExpiryDate is { } expiry)
        {
            if (expiry < today)
            {
                score += 55;
                checks.Add(new("Expiry date is valid", "fail"));
                indicators.Add(new("expired", "critical", $"The expiry date ({expiry:yyyy-MM-dd}) has passed."));
            }
            else
            {
                checks.Add(new("Expiry date is valid", "pass"));
                if (expiry <= today.AddDays(30))
                    indicators.Add(new("expiring_soon", "info", "This product expires within 30 days."));
            }
        }
        else
        {
            checks.Add(new("Expiry date is valid", "unknown"));
        }

        if (input.NameMatches is { } matches)
        {
            if (matches)
            {
                checks.Add(new("Product name matches the barcode", "pass"));
            }
            else
            {
                score += 30;
                checks.Add(new("Product name matches the barcode", "fail"));
                indicators.Add(new("name_mismatch", "warning",
                    "The product name differs from the one registered for this barcode."));
            }
        }

        if (input.PriorReports > 0)
        {
            score += input.PriorReports switch { <= 2 => 10, <= 5 => 20, _ => 30 };
            indicators.Add(new("prior_reports", "warning",
                $"{input.PriorReports} other customer report(s) exist for this product."));
        }

        score = Math.Clamp(score, 0, 100);
        var level = score >= thresholds.High ? "high"
            : score >= thresholds.Suspicious ? "suspicious"
            : score >= thresholds.Review ? "review"
            : "low";

        return new RiskAssessment(score, level, indicators, checks);
    }

    /// <summary>Case-insensitive containment either way, so "Coke 2L" still matches "Coke".</summary>
    public static bool NamesMatch(string? registered, string? entered)
    {
        if (string.IsNullOrWhiteSpace(registered) || string.IsNullOrWhiteSpace(entered)) return true;
        var a = registered.Trim();
        var b = entered.Trim();
        return a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
               b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Share of the registered name's words found in OCR'd packaging text, or null
    /// when the text is too short to say anything (OCR is noisy).
    /// </summary>
    public static double? IdentityConfidence(string? registeredName, string? packagingText)
    {
        if (string.IsNullOrWhiteSpace(registeredName) || packagingText is null || packagingText.Trim().Length < 10)
            return null;
        var words = Regex.Matches(registeredName.ToLowerInvariant(), "[a-z0-9]+")
            .Select(m => m.Value).Where(w => w.Length >= 3).Distinct().ToList();
        if (words.Count == 0) return null;
        var text = packagingText.ToLowerInvariant();
        return words.Count(w => text.Contains(w)) / (double)words.Count;
    }
}
