using Net.Codecrete.QrCodeGenerator;

namespace SpazaSure.ProductService.Services;

public static class QrCodeService
{
    private const string TokenPrefix = "SPZQR-";

    public static string GenerateToken() =>
        $"{TokenPrefix}{Guid.NewGuid():N}".ToUpperInvariant();

    public static string RenderSvg(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return QrCode.EncodeText(token, QrCode.Ecc.Medium).ToSvgString(4);
    }
}