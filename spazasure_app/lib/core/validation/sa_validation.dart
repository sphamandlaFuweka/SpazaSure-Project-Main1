/// South African ID and phone number checks. These mirror the server's rules
/// (SaValidation.cs) so people see mistakes before submitting.
class SaValidation {
  SaValidation._();

  /// 13 digits, a real date of birth (YYMMDD), citizenship digit 0, 1 or 2,
  /// and a correct Luhn check digit. Spaces are ignored.
  static bool isValidIdNumber(String? input) {
    final raw = input ?? '';
    if (RegExp(r'[^0-9 ]').hasMatch(raw)) return false;
    final id = raw.replaceAll(' ', '');
    if (id.length != 13) return false;

    final month = int.parse(id.substring(2, 4));
    final day = int.parse(id.substring(4, 6));
    // Any year works for the century check, except 29 February needs a leap year.
    final year = 2000 + int.parse(id.substring(0, 2));
    final date = DateTime(year, month, day);
    final leapYearFor29Feb = month == 2 && day == 29;
    if (month < 1 || month > 12 || day < 1 ||
        (date.month != month && !leapYearFor29Feb) ||
        (leapYearFor29Feb && year % 4 != 0)) {
      return false;
    }
    if (!'012'.contains(id[10])) return false;

    var sum = 0;
    for (var i = 0; i < 13; i++) {
      var d = int.parse(id[12 - i]);
      if (i.isOdd) {
        d *= 2;
        if (d > 9) d -= 9;
      }
      sum += d;
    }
    return sum % 10 == 0;
  }

  /// A South African number such as 082 123 4567, +27 82 123 4567 or 27821234567.
  static bool isValidPhone(String? input) {
    final raw = (input ?? '').trim();
    if (raw.isEmpty || RegExp(r'[^0-9 +\-()]').hasMatch(raw)) return false;
    final digits = raw.replaceAll(RegExp(r'\D'), '');
    final national = digits.startsWith('27') && digits.length == 11
        ? digits.substring(2)
        : digits.startsWith('0') && digits.length == 10
            ? digits.substring(1)
            // The registration form shows "+27" as a prefix, so people often type just the 9 digits.
            : digits.length == 9
                ? digits
                : null;
    return national != null && '12345678'.contains(national[0]);
  }
}
