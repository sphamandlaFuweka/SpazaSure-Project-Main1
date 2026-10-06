class PackagingScan {
  final String rawText;
  final String? barcode;
  final DateTime? expiry;
  final String? batch;

  const PackagingScan({
    required this.rawText,
    this.barcode,
    this.expiry,
    this.batch,
  });
}

/// Extracts verifiable fields from OCR text read off product packaging.
class PackagingTextParser {
  static const _months = {
    'jan': 1,
    'feb': 2,
    'mar': 3,
    'apr': 4,
    'may': 5,
    'jun': 6,
    'jul': 7,
    'aug': 8,
    'sep': 9,
    'oct': 10,
    'nov': 11,
    'dec': 12,
  };

  static final _expiryKeyword = RegExp(
    r'\b(?:best\s*before(?:\s*end)?|use\s*by|expiry(?:\s*date)?|exp\.?|bbe|bb)\b[\s:.\-]*',
    caseSensitive: false,
  );

  static final _batchPattern = RegExp(
    r'\b(?:batch(?:\s*(?:no|number|nr))?|lot(?:\s*(?:no|number))?|b/n|l/n)\b\.?\s*[:#.\-]?\s*([A-Z0-9][A-Z0-9\-/]{2,19})',
    caseSensitive: false,
  );

  static PackagingScan parse(String text) => PackagingScan(
    rawText: text,
    barcode: _barcode(text),
    expiry: _expiry(text),
    batch: _batch(text),
  );

  static bool isValidGtin(String digits) {
    if (!RegExp(r'^\d{8}$|^\d{12,13}$').hasMatch(digits)) return false;
    var sum = 0;
    for (var i = 0; i < digits.length - 1; i++) {
      final digit = int.parse(digits[digits.length - 2 - i]);
      sum += digit * (i.isEven ? 3 : 1);
    }
    return (10 - sum % 10) % 10 == int.parse(digits[digits.length - 1]);
  }

  static String? _barcode(String text) {
    for (final match in RegExp(r'\d[\d ]{6,16}\d').allMatches(text)) {
      final raw = match.group(0)!;
      final digits = raw.replaceAll(' ', '');
      // An 8-digit run with spaces is usually a date, not an EAN-8.
      if (digits.length == 8 && raw.contains(' ')) continue;
      if (isValidGtin(digits)) return digits;
    }
    return null;
  }

  static DateTime? _expiry(String text) {
    for (final keyword in _expiryKeyword.allMatches(text)) {
      final end = keyword.end + 30 > text.length
          ? text.length
          : keyword.end + 30;
      final date = _parseDate(text.substring(keyword.end, end));
      if (date != null) return date;
    }
    return null;
  }

  static DateTime? _parseDate(String s) {
    var m = RegExp(
      r'(?<!\d)(\d{4})[/.\-](\d{1,2})[/.\-](\d{1,2})(?!\d)',
    ).firstMatch(s);
    if (m != null) {
      return _build(int.parse(m[1]!), int.parse(m[2]!), int.parse(m[3]!));
    }

    m = RegExp(
      r'(?<!\d)(\d{1,2})[/.\- ](\d{1,2})[/.\- ](\d{2,4})(?!\d)',
    ).firstMatch(s);
    if (m != null) {
      return _build(_year(m[3]!), int.parse(m[2]!), int.parse(m[1]!));
    }

    m = RegExp(
      r'(?<!\d)(\d{1,2})\s*([A-Za-z]{3})[A-Za-z]*[\s.,\-/]*(\d{2,4})(?!\d)',
    ).firstMatch(s);
    final dayMonth = m == null ? null : _months[m[2]!.toLowerCase()];
    if (m != null && dayMonth != null) {
      return _build(_year(m[3]!), dayMonth, int.parse(m[1]!));
    }

    m = RegExp(
      r'([A-Za-z]{3})[A-Za-z]*[\s.,\-/]*(\d{2,4})(?!\d)',
    ).firstMatch(s);
    final month = m == null ? null : _months[m[1]!.toLowerCase()];
    if (m != null && month != null) return _endOfMonth(_year(m[2]!), month);

    m = RegExp(r'(?<!\d)(\d{1,2})[/.\-](\d{4})(?!\d)').firstMatch(s);
    if (m != null) return _endOfMonth(int.parse(m[2]!), int.parse(m[1]!));

    return null;
  }

  static int _year(String y) =>
      y.length == 2 ? 2000 + int.parse(y) : int.parse(y);

  static DateTime? _build(int year, int month, int day) {
    if (month < 1 || month > 12 || day < 1) return null;
    final date = DateTime(year, month, day);
    return date.month == month ? date : null;
  }

  static DateTime? _endOfMonth(int year, int month) =>
      month < 1 || month > 12 ? null : DateTime(year, month + 1, 0);

  static String? _batch(String text) {
    for (final match in _batchPattern.allMatches(text)) {
      final value = match.group(1)!.toUpperCase();
      final isKeyword = RegExp(r'^(EXP|BB|BEST|USE|MFG|PROD)').hasMatch(value);
      if (!isKeyword && RegExp(r'\d').hasMatch(value)) return value;
    }
    return null;
  }
}
