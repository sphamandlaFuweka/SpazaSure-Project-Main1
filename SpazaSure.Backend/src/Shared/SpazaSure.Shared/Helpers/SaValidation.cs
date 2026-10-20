using System.Globalization;
using System.Text.RegularExpressions;

namespace SpazaSure.Shared.Helpers;

/// <summary>Checks South African ID numbers, phone numbers and CIPC company registration numbers.</summary>
public static partial class SaValidation
{
    /// <summary>
    /// A valid SA ID number: 13 digits, a real date of birth (YYMMDD), citizenship digit 0, 1 or 2,
    /// and a correct Luhn check digit. Spaces are ignored.
    /// </summary>
    public static bool IsValidIdNumber(string? idNumber)
    {
        var id = Digits(idNumber);
        if (id.Length != 13 || (idNumber ?? "").Any(c => !char.IsAsciiDigit(c) && c != ' ')) return false;
        if (!DateOnly.TryParseExact(id[..6], "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return false;
        if (id[10] is not ('0' or '1' or '2')) return false;

        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            var d = id[12 - i] - '0';
            if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
        }
        return sum % 10 == 0;
    }

    /// <summary>The ID number with spaces removed, for storing and comparing.</summary>
    public static string NormalizeIdNumber(string idNumber) => Digits(idNumber);

    /// <summary>
    /// A valid SA phone number in any common form (082 123 4567, +27 82 123 4567, 27821234567).
    /// Returns the +27 form, or null when it can't be a South African number.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Any(c => !char.IsAsciiDigit(c) && " +-()".IndexOf(c) < 0))
            return null;
        var digits = Digits(phone);
        var national = digits.StartsWith("27") && digits.Length == 11 ? digits[2..]
            : digits.StartsWith('0') && digits.Length == 10 ? digits[1..]
            : null;
        return national is not null && national[0] is >= '1' and <= '8' ? "+27" + national : null;
    }

    /// <summary>A CIPC registration number such as 2020/123456/07. Spaces are ignored.</summary>
    public static bool IsValidCompanyRegistration(string? registration)
    {
        var match = CompanyRegistration().Match((registration ?? "").Replace(" ", ""));
        return match.Success && int.Parse(match.Groups[1].Value) is var year
            && year >= 1900 && year <= DateTime.UtcNow.Year;
    }

    public static string NormalizeCompanyRegistration(string registration) => registration.Replace(" ", "");

    /// <summary>Digits only, so "2020/123456/07" and "2020123456 07" compare equal.</summary>
    public static string Digits(string? value) => new((value ?? "").Where(char.IsAsciiDigit).ToArray());

    [GeneratedRegex(@"^(\d{4})/(\d{6})/(\d{2})$")]
    private static partial Regex CompanyRegistration();
}
