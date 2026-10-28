using System.Linq;

namespace SpazaSure.ProductService.Services;

public static class BarcodeService
{
    // EAN-13 encoding tables
    private static readonly string[] L = ["0001101","0011001","0010011","0111101","0100011","0110001","0101111","0111011","0110111","0001011"];
    private static readonly string[] G = ["0100111","0110011","0011011","0100001","0011101","0111001","0000101","0010001","0001001","0010111"];
    private static readonly string[] R = ["1110010","1100110","1101100","1000010","1011100","1001110","1010000","1000100","1001000","1110100"];
    private static readonly string[] Parity = ["LLLLLL","LLGLGG","LLGGLG","LLGGGL","LGLLGG","LGGLLG","LGGGLL","LGLGLG","LGLGGL","LGGLGL"];

    /// <summary>
    /// Prefix of the codes SpazaSure makes for products that came without a barcode. GS1 keeps
    /// 200-299 for in-store use, so these can never match a real product's barcode.
    /// </summary>
    public const string GeneratedPrefix = "290";

    /// <summary>
    /// Makes an EAN-13 for a product that has no barcode of its own. Only for those: a product's
    /// printed barcode must be kept, or scanning the pack won't find it.
    /// </summary>
    public static string GenerateEan13(Guid productId)
    {
        var digits9 = (Seed(productId) % 1_000_000_000L).ToString().PadLeft(9, '0');
        var digits12 = GeneratedPrefix + digits9;
        return digits12 + CheckDigitFor(digits12);
    }

    // Codes made before GeneratedPrefix existed: 11 digits from the product id plus a check digit.
    private static string LegacyEan13(Guid productId)
    {
        var digits11 = (Seed(productId) % 100_000_000_000L).ToString().PadLeft(11, '0')[..11];
        return digits11 + CalculateCheckDigit(digits11);
    }

    private static long Seed(Guid productId)
    {
        var bytes = productId.ToByteArray();
        return Math.Abs(BitConverter.ToInt64(bytes, 0) ^ BitConverter.ToInt64(bytes, 8));
    }

    /// <summary>True when SpazaSure made this barcode rather than it coming from the pack.</summary>
    public static bool IsGenerated(Guid productId, string? barcode) =>
        !string.IsNullOrEmpty(barcode) && (barcode == GenerateEan13(productId) || barcode == LegacyEan13(productId));

    /// <summary>The digits of a typed or scanned barcode (spaces and dashes dropped), or null when empty.</summary>
    public static string? Normalize(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        var digits = new string(barcode.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        return digits.Length == 0 ? null : digits;
    }

    /// <summary>Why a printed barcode can't be right, or null when it is a valid EAN-8, UPC-A, EAN-13 or GTIN-14.</summary>
    public static string? Problem(string digits)
    {
        if (!digits.All(char.IsDigit)) return "A barcode has only numbers. Type the digits printed under the bars.";
        if (digits.Length is not (8 or 12 or 13 or 14))
            return $"That barcode has {digits.Length} digits. Product barcodes have 8, 12, 13 or 14; check you typed them all.";
        if (CheckDigitFor(digits[..^1]) != digits[^1].ToString())
            return "That barcode's last digit doesn't match the rest, so a digit was probably mistyped. Check it against the pack.";
        return null;
    }

    // GS1 mod-10 check digit for EAN-8, UPC-A, EAN-13 and GTIN-14 (weights 3,1 from the right).
    private static string CheckDigitFor(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
            sum += (body[body.Length - 1 - i] - '0') * (i % 2 == 0 ? 3 : 1);
        return ((10 - sum % 10) % 10).ToString();
    }

    public static string CalculateCheckDigit(string d11)
    {
        var sum = 0;
        for (var i = 0; i < 11; i++)
            sum += (d11[i] - '0') * (i % 2 == 0 ? 1 : 3);
        var r = sum % 10;
        return (r == 0 ? 0 : 10 - r).ToString();
    }

    /// <summary>Renders an EAN-13 barcode as an inline SVG string.</summary>
    public static string RenderSvg(string ean13)
    {
        // Pad or trim to exactly 13 digits so we never throw
        if (string.IsNullOrWhiteSpace(ean13))
            ean13 = "0000000000000";

        ean13 = new string(ean13.Where(char.IsDigit).ToArray());
        if (ean13.Length < 13) ean13 = ean13.PadLeft(13, '0');
        // A GTIN-14 for a single unit is the EAN-13 with a leading 0; dropping the end would lose the check digit
        if (ean13.Length == 14 && ean13[0] == '0') ean13 = ean13[1..];
        if (ean13.Length > 13) ean13 = ean13[..13];

        const int bw = 2;       // bar width px
        const int bh = 80;      // bar height px
        const int qz = 10;      // quiet zone px
        const int fs = 11;      // font size
        const int th = 16;      // text area height

        var first = ean13[0] - '0';
        var parity = Parity[first];

        var bits = new System.Text.StringBuilder("101"); // left guard
        for (var i = 1; i <= 6; i++)
        {
            var d = ean13[i] - '0';
            bits.Append(parity[i - 1] == 'L' ? L[d] : G[d]);
        }
        bits.Append("01010"); // centre guard
        for (var i = 7; i <= 12; i++)
            bits.Append(R[ean13[i] - '0']);
        bits.Append("101"); // right guard

        var pattern = bits.ToString();
        var totalW = pattern.Length * bw + qz * 2;
        var totalH = bh + th;

        var rects = new System.Text.StringBuilder();
        for (var i = 0; i < pattern.Length; i++)
            if (pattern[i] == '1')
                rects.Append($"<rect x=\"{qz + i * bw}\" y=\"0\" width=\"{bw}\" height=\"{bh}\" fill=\"#000\"/>");

        var textY = bh + fs + 2;
        var leftCx  = qz + (3 + 21) * bw;
        var rightCx = qz + (3 + 42 + 5 + 21) * bw;

        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{totalW}\" height=\"{totalH}\" viewBox=\"0 0 {totalW} {totalH}\">" +
               $"<rect width=\"{totalW}\" height=\"{totalH}\" fill=\"white\"/>" +
               rects +
               $"<text x=\"{qz - 2}\" y=\"{textY}\" font-family=\"monospace\" font-size=\"{fs}\" text-anchor=\"middle\">{ean13[0]}</text>" +
               $"<text x=\"{leftCx}\" y=\"{textY}\" font-family=\"monospace\" font-size=\"{fs}\" text-anchor=\"middle\" letter-spacing=\"1\">{ean13[1..7]}</text>" +
               $"<text x=\"{rightCx}\" y=\"{textY}\" font-family=\"monospace\" font-size=\"{fs}\" text-anchor=\"middle\" letter-spacing=\"1\">{ean13[7..]}</text>" +
               "</svg>";
    }
}
