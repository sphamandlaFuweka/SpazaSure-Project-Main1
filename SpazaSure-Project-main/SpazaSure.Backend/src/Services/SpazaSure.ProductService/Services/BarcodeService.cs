using System.Linq;

namespace SpazaSure.ProductService.Services;

public static class BarcodeService
{
    // EAN-13 encoding tables
    private static readonly string[] L = ["0001101","0011001","0010011","0111101","0100011","0110001","0101111","0111011","0110111","0001011"];
    private static readonly string[] G = ["0100111","0110011","0011011","0100001","0011101","0111001","0000101","0010001","0001001","0010111"];
    private static readonly string[] R = ["1110010","1100110","1101100","1000010","1011100","1001110","1010000","1000100","1001000","1110100"];
    private static readonly string[] Parity = ["LLLLLL","LLGLGG","LLGGLG","LLGGGL","LGLLGG","LGGLLG","LGGGLL","LGLGLG","LGLGGL","LGGLGL"];

    /// <summary>Generates a unique EAN-13 barcode string for a product.</summary>
    public static string GenerateEan13(Guid productId)
    {
        var bytes = productId.ToByteArray();
        var seed  = Math.Abs(BitConverter.ToInt64(bytes, 0) ^ BitConverter.ToInt64(bytes, 8));
        var digits11 = (seed % 100_000_000_000L).ToString().PadLeft(11, '0')[..11];
        return digits11 + CalculateCheckDigit(digits11);
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
