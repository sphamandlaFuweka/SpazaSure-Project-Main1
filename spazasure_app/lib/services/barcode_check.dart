/// GS1 mod-10 check digit, shared by EAN-8, UPC-A, EAN-13 and GTIN-14.
bool hasValidCheckDigit(String digits) {
  var sum = 0;
  for (var i = 0; i < digits.length - 1; i++) {
    final d = digits.codeUnitAt(digits.length - 2 - i) - 48;
    sum += i.isEven ? d * 3 : d;
  }
  return (10 - sum % 10) % 10 == digits.codeUnitAt(digits.length - 1) - 48;
}

final _digitsOnly = RegExp(r'^\d+$');

/// Whether a live camera read is worth looking up. A retail barcode whose check digit
/// fails is almost always a partial read (glare, a crease, the phone moving), so the
/// scanner keeps looking instead of reporting a "fake" barcode. QR codes, SKUs and
/// other formats are accepted as read.
bool isPlausibleScan(String code) {
  final c = code.trim();
  if (c.isEmpty) return false;
  if (!_digitsOnly.hasMatch(c)) return true;
  if (const {8, 12, 13, 14}.contains(c.length)) return hasValidCheckDigit(c);
  return true;
}
