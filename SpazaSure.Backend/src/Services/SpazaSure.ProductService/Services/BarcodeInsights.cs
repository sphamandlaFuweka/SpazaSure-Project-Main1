namespace SpazaSure.ProductService.Services;

/// <summary>What the barcode number itself says, worked out offline.</summary>
public sealed record BarcodeInsight(
    string Format,
    bool IsRetailBarcode,
    bool? CheckDigitValid,
    string? Prefix,
    string? RegisteredIn,
    string Kind,
    string Explanation);

/// <summary>
/// Reads a scanned code without any database: its format, whether the check digit is right,
/// and the GS1 member country the brand registered it with. The GS1 prefix shows where the
/// barcode was registered, not necessarily where the product was made.
/// </summary>
public static class BarcodeInsights
{
    public static BarcodeInsight Analyze(string code)
    {
        code = code.Trim();
        if (code.Length == 0 || !code.All(char.IsDigit))
            return new("Other code", false, null, null, null, "other",
                "This is not a standard retail barcode. It may be a QR code, a SKU or a shop's own code.");

        var format = code.Length switch
        {
            8 => "EAN-8",
            12 => "UPC-A",
            13 => "EAN-13",
            14 => "GTIN-14 (outer case)",
            _ => null,
        };
        if (format is null)
            return new($"{code.Length}-digit number", false, null, null, null, "other",
                $"{code.Length} digits is not a standard retail barcode length (8, 12, 13 or 14).");

        var checkOk = CheckDigitValid(code);
        // Bring every length to the 13-digit form the GS1 prefix table uses
        var gtin13 = code.Length switch
        {
            12 => "0" + code,
            14 => code[1..],
            _ => code,
        };

        if (code.Length == 8)
        {
            var kind8 = code[0] is '0' or '2' ? "in_store" : "product";
            var country8 = kind8 == "product" ? CountryFor(code[..3]) : null;
            return new(format, true, checkOk, code[..3], country8, kind8,
                Explain(kind8, country8, code[..3], checkOk));
        }

        var prefix = gtin13[..3];
        var kind = KindFor(prefix);
        var country = kind == "product" ? CountryFor(prefix) : null;
        return new(format, true, checkOk, prefix, country, kind, Explain(kind, country, prefix, checkOk));
    }

    /// <summary>GS1 mod-10 check digit, the same rule for EAN-8, UPC-A, EAN-13 and GTIN-14.</summary>
    public static bool CheckDigitValid(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length - 1; i++)
        {
            var d = digits[digits.Length - 2 - i] - '0';
            sum += i % 2 == 0 ? d * 3 : d;
        }
        return (10 - sum % 10) % 10 == digits[^1] - '0';
    }

    private static string Explain(string kind, string? country, string prefix, bool checkOk)
    {
        var check = checkOk
            ? "The check digit is correct."
            : "The check digit is wrong, which happens with forged or misprinted barcodes. Compare the number under the bars with what was scanned.";
        return kind switch
        {
            "in_store" => $"A shop's own code (prefix {prefix}), used for items it weighs, packs or prices itself, such as meat or bread. {check}",
            "book" => $"A book barcode (ISBN). {check}",
            "magazine" => $"A magazine or newspaper barcode (ISSN). {check}",
            "coupon" => $"A coupon or refund barcode, not a product. {check}",
            "restricted" => $"A restricted-use code (prefix {prefix}), not a normal retail product barcode. {check}",
            _ when country is not null => $"Registered with GS1 {country} (prefix {prefix}). This shows where the brand registered the barcode, not necessarily where it was made. {check}",
            _ => $"Prefix {prefix} is not assigned to any GS1 country, which is unusual for a genuine retail product. {check}",
        };
    }

    private static string KindFor(string prefix)
    {
        var n = int.Parse(prefix);
        return n switch
        {
            >= 20 and <= 29 => "in_store",
            >= 40 and <= 49 => "restricted",
            >= 50 and <= 59 => "coupon",
            >= 200 and <= 299 => "in_store",
            977 => "magazine",
            978 or 979 => "book",
            980 => "coupon",
            >= 981 and <= 984 => "coupon",
            >= 990 => "coupon",
            _ => "product",
        };
    }

    /// <summary>GS1 member organisation for a 3-digit prefix (GS1 General Specifications list).</summary>
    public static string? CountryFor(string prefix)
    {
        if (prefix.Length != 3 || !int.TryParse(prefix, out var n)) return null;
        foreach (var (from, to, name) in Ranges)
            if (n >= from && n <= to) return name;
        return null;
    }

    private static readonly (int From, int To, string Name)[] Ranges =
    [
        (0, 19, "USA and Canada"), (30, 39, "USA"), (60, 139, "USA and Canada"),
        (300, 379, "France"), (380, 380, "Bulgaria"), (383, 383, "Slovenia"), (385, 385, "Croatia"),
        (387, 387, "Bosnia and Herzegovina"), (389, 389, "Montenegro"), (390, 390, "Kosovo"),
        (400, 440, "Germany"), (450, 459, "Japan"), (460, 469, "Russia"), (470, 470, "Kyrgyzstan"),
        (471, 471, "Taiwan"), (474, 474, "Estonia"), (475, 475, "Latvia"), (476, 476, "Azerbaijan"),
        (477, 477, "Lithuania"), (478, 478, "Uzbekistan"), (479, 479, "Sri Lanka"), (480, 480, "Philippines"),
        (481, 481, "Belarus"), (482, 482, "Ukraine"), (483, 483, "Turkmenistan"), (484, 484, "Moldova"),
        (485, 485, "Armenia"), (486, 486, "Georgia"), (487, 487, "Kazakhstan"), (488, 488, "Tajikistan"),
        (489, 489, "Hong Kong"), (490, 499, "Japan"), (500, 509, "United Kingdom"), (520, 521, "Greece"),
        (528, 528, "Lebanon"), (529, 529, "Cyprus"), (530, 530, "Albania"), (531, 531, "North Macedonia"),
        (535, 535, "Malta"), (539, 539, "Ireland"), (540, 549, "Belgium and Luxembourg"), (560, 560, "Portugal"),
        (569, 569, "Iceland"), (570, 579, "Denmark"), (590, 590, "Poland"), (594, 594, "Romania"),
        (599, 599, "Hungary"), (600, 601, "South Africa"), (603, 603, "Ghana"), (604, 604, "Senegal"),
        (605, 605, "Uganda"), (606, 606, "Angola"), (607, 607, "Oman"), (608, 608, "Bahrain"),
        (609, 609, "Mauritius"), (611, 611, "Morocco"), (612, 612, "Somalia"), (613, 613, "Algeria"),
        (615, 615, "Nigeria"), (616, 616, "Kenya"), (617, 617, "Cameroon"), (618, 618, "Côte d'Ivoire"),
        (619, 619, "Tunisia"), (620, 620, "Tanzania"), (621, 621, "Syria"), (622, 622, "Egypt"),
        (623, 623, "Brunei"), (624, 624, "Libya"), (625, 625, "Jordan"), (626, 626, "Iran"),
        (627, 627, "Kuwait"), (628, 628, "Saudi Arabia"), (629, 629, "United Arab Emirates"),
        (630, 630, "Qatar"), (631, 631, "Namibia"), (632, 632, "Rwanda"), (640, 649, "Finland"),
        (680, 681, "China"), (690, 699, "China"), (700, 709, "Norway"), (729, 729, "Israel"),
        (730, 739, "Sweden"), (740, 740, "Guatemala"), (741, 741, "El Salvador"), (742, 742, "Honduras"),
        (743, 743, "Nicaragua"), (744, 744, "Costa Rica"), (745, 745, "Panama"), (746, 746, "Dominican Republic"),
        (750, 750, "Mexico"), (754, 755, "Canada"), (759, 759, "Venezuela"), (760, 769, "Switzerland and Liechtenstein"),
        (770, 771, "Colombia"), (773, 773, "Uruguay"), (775, 775, "Peru"), (777, 777, "Bolivia"),
        (778, 779, "Argentina"), (780, 780, "Chile"), (784, 784, "Paraguay"), (786, 786, "Ecuador"),
        (789, 790, "Brazil"), (800, 839, "Italy"), (840, 849, "Spain and Andorra"), (850, 850, "Cuba"),
        (858, 858, "Slovakia"), (859, 859, "Czechia"), (860, 860, "Serbia"), (865, 865, "Mongolia"),
        (867, 867, "North Korea"), (868, 869, "Türkiye"), (870, 879, "Netherlands"), (880, 881, "South Korea"),
        (883, 883, "Myanmar"), (884, 884, "Cambodia"), (885, 885, "Thailand"), (888, 888, "Singapore"),
        (890, 890, "India"), (893, 893, "Vietnam"), (894, 894, "Bangladesh"), (896, 896, "Pakistan"),
        (899, 899, "Indonesia"), (900, 919, "Austria"), (930, 939, "Australia"), (940, 949, "New Zealand"),
        (950, 950, "GS1 Global Office"), (951, 951, "GS1 Global Office (EPC)"), (955, 955, "Malaysia"),
        (958, 958, "Macau"),
    ];
}
