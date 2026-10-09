namespace SpazaSure.ProductService.Services;

public sealed record VerificationCheck(string Label, string Status, string Detail);

public sealed record VerificationRisk(
    int Score, string Level, string Headline,
    List<string> Indicators, List<VerificationCheck> Checks, List<string> Tips);

public sealed class VerificationInput
{
    public required string Code { get; init; }
    public bool InSpazaSureRegistry { get; init; }
    public bool InGlobalDatabase { get; init; }
    public bool Recalled { get; init; }
    public bool SupplierVerified { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public string? BatchNumber { get; init; }
    public bool BatchTracked { get; init; }
    public bool PackagingScanned { get; init; }
    public bool PackagingBarcodeMatches { get; init; } = true;
    public bool HasIngredients { get; init; }
    public bool IsFood { get; init; }
    public bool CloneSuspected { get; init; }

    /// <summary>none, required, valid, invalid, reused or locked (scratch-off PIN result).</summary>
    public string PinStatus { get; init; } = "none";
}

/// <summary>Rule-based risk scoring. A score is an indicator, never proof of authenticity.</summary>
public static class VerificationRiskEngine
{
    public static VerificationRisk Evaluate(VerificationInput i, DateOnly today)
    {
        var score = 0;
        var indicators = new List<string>();
        var checks = new List<VerificationCheck>();

        // Barcode structure
        var digits = i.Code.All(char.IsDigit);
        var validLength = digits && i.Code.Length is 8 or 12 or 13 or 14;
        var checksumOk = validLength && HasValidCheckDigit(i.Code);
        if (digits)
        {
            if (!validLength)
            {
                score += 20; indicators.Add("The barcode has an unusual length for a retail product.");
                checks.Add(new("Barcode format", "fail", $"{i.Code.Length} digits is not a standard EAN/UPC length."));
            }
            else if (!checksumOk)
            {
                score += 30; indicators.Add("The barcode check digit is invalid, which is common with forged or mistyped codes.");
                checks.Add(new("Barcode check digit", "fail", "The last digit does not match the rest of the number."));
            }
            else
            {
                checks.Add(new("Barcode check digit", "pass", "The barcode is structurally valid."));
            }
        }
        else
        {
            checks.Add(new("Barcode format", "warn", "This is a SKU or QR code, not a standard barcode."));
        }

        // Registry presence
        if (i.InSpazaSureRegistry)
            checks.Add(new("SpazaSure registry", "pass", "Registered and approved by SpazaSure."));
        else if (i.InGlobalDatabase)
        {
            score += 10;
            checks.Add(new("SpazaSure registry", "warn", "Not registered with SpazaSure, but found in a global product database."));
        }
        else
        {
            score += 35; indicators.Add("This product was not found in SpazaSure or global product databases.");
            checks.Add(new("Product databases", "fail", "No record found anywhere."));
        }

        // Supplier
        if (i.InSpazaSureRegistry)
        {
            if (i.SupplierVerified)
                checks.Add(new("Supplier", "pass", "Supplied by a verified SpazaSure supplier."));
            else
            {
                score += 15; indicators.Add("The supplier of this product has not been verified.");
                checks.Add(new("Supplier", "warn", "Supplier is not verified."));
            }
        }

        // Recall
        switch (i.PinStatus)
        {
            case "valid":
                checks.Add(new("Scratch-off PIN", "pass", "PIN confirmed. This is the first time this item was verified."));
                break;
            case "required":
                checks.Add(new("Scratch-off PIN", "unknown", "Scratch the panel and enter the PIN to finish verifying this item."));
                break;
            case "invalid":
                score += 40; indicators.Add("The PIN you entered does not match this item.");
                checks.Add(new("Scratch-off PIN", "fail", "Wrong PIN."));
                break;
            case "reused":
                score += 55; indicators.Add("This item was already verified before, so the pack may be a copy or refill.");
                checks.Add(new("Scratch-off PIN", "fail", "This code was already used."));
                break;
            case "locked":
                score += 60; indicators.Add("Too many wrong PIN attempts were made on this code.");
                checks.Add(new("Scratch-off PIN", "fail", "Code locked after repeated wrong PINs."));
                break;
        }

        if (i.CloneSuspected)
        {
            score += 45; indicators.Add("This code was scanned far away from another recent scan, so it may be copied.");
            checks.Add(new("Scan locations", "fail", "The same code appeared in two distant places too quickly."));
        }

        if (i.Recalled)
        {
            score += 60; indicators.Add("This product or one of its batches has an active recall.");
            checks.Add(new("Recall status", "fail", "Active recall found."));
        }
        else if (i.InSpazaSureRegistry)
            checks.Add(new("Recall status", "pass", "No recalls found."));

        // Expiry
        if (i.ExpiryDate is { } exp)
        {
            if (exp < today)
            {
                score += 50; indicators.Add($"Expiry date {exp:yyyy-MM-dd} has passed.");
                checks.Add(new("Expiry date", "fail", "Product is expired."));
            }
            else if (exp <= today.AddDays(30))
            {
                score += 10; indicators.Add($"Expires soon ({exp:yyyy-MM-dd}).");
                checks.Add(new("Expiry date", "warn", "Expires within 30 days."));
            }
            else
                checks.Add(new("Expiry date", "pass", $"Valid until {exp:yyyy-MM-dd}."));
        }
        else if (i.IsFood)
        {
            score += 5;
            checks.Add(new("Expiry date", "unknown", "No expiry date provided. Scan the packaging or enter it manually."));
        }

        // Batch
        if (!string.IsNullOrWhiteSpace(i.BatchNumber))
        {
            checks.Add(new("Batch number", i.BatchTracked ? "pass" : "unknown",
                i.BatchTracked ? $"Batch {i.BatchNumber} is tracked by SpazaSure." : $"Batch {i.BatchNumber} read from pack; not tracked by SpazaSure."));
        }
        else if (i.IsFood)
        {
            checks.Add(new("Batch number", "unknown", "No batch number provided."));
        }

        // Packaging scan
        if (i.PackagingScanned)
        {
            if (!i.PackagingBarcodeMatches)
            {
                score += 30; indicators.Add("The text printed on the packaging does not match this product.");
                checks.Add(new("Packaging text", "fail", "Packaging does not match the registered product."));
            }
            else
                checks.Add(new("Packaging text", "pass", "Packaging text is consistent with the product."));
        }

        if (i.IsFood && !i.HasIngredients && i.InGlobalDatabase)
        {
            score += 5;
            checks.Add(new("Ingredient list", "unknown", "No ingredient list on record."));
        }

        score = Math.Min(score, 100);
        var (level, headline) = score switch
        {
            >= 60 => ("high", "High risk: do not buy or use this product"),
            >= 35 => ("suspicious", "Suspicious: this product needs a closer look"),
            >= 15 => ("review", "Some details could not be confirmed"),
            _ => ("low", "No warning signs found"),
        };

        var tips = new List<string>
        {
            "Compare the logo, colours and print quality with a pack you trust.",
            "Check that the batch number and expiry date are clearly printed, not smudged or stickered over.",
            "Make sure the seal and packaging are intact.",
            "Be careful if the price is far below what other shops charge.",
        };
        if (level is "suspicious" or "high")
            tips.Insert(0, "Do not buy or consume this product. Report it so SpazaSure can investigate.");

        return new VerificationRisk(score, level, headline, indicators, checks, tips);
    }

    private static bool HasValidCheckDigit(string code)
    {
        var sum = 0;
        for (var idx = 0; idx < code.Length - 1; idx++)
        {
            var d = code[code.Length - 2 - idx] - '0';
            sum += idx % 2 == 0 ? d * 3 : d;
        }
        return (10 - sum % 10) % 10 == code[^1] - '0';
    }
}
