/// One detail we expected on the pack, and whether the photo's text showed it.
class PackCheck {
  final String label;
  final String expected;
  final bool found;

  const PackCheck(this.label, this.expected, this.found);
}

class PackMatch {
  final List<PackCheck> checks;
  final bool readAnyText;

  const PackMatch(this.checks, {required this.readAnyText});

  int get matched => checks.where((c) => c.found).length;
  int get total => checks.length;
  List<PackCheck> get missing => checks.where((c) => !c.found).toList();
}

final _nonAlnum = RegExp(r'[^a-z0-9]+');
final _nonDigit = RegExp(r'\D');
final _amount = RegExp(
  r'(\d+(?:[.,]\d+)?)\s*(kg|g|mg|ml|cl|l|litres?|liters?|lt)\b',
  caseSensitive: false,
);

// Words in product names that say nothing about which product it is
const _genericWords = {
  'with',
  'from',
  'pack',
  'flavour',
  'flavor',
  'original',
  'classic',
  'new',
  'the',
  'and',
  'for',
  'free',
  'extra',
  'plus',
  'value',
  'size',
};

String _norm(String s) => s.toLowerCase().replaceAll(_nonAlnum, ' ').trim();
String _squash(String s) => s.toLowerCase().replaceAll(_nonAlnum, '');

/// Amounts in a text as millilitres or grams, e.g. "2 L" → 2000, "500g" → 500.
List<double> amountsIn(String text) {
  final out = <double>[];
  for (final m in _amount.allMatches(text)) {
    final n = double.tryParse(m.group(1)!.replaceAll(',', '.'));
    if (n == null) continue;
    final unit = m.group(2)!.toLowerCase();
    final factor = switch (unit) {
      'kg' => 1000.0,
      'mg' => 0.001,
      'cl' => 10.0,
      'l' || 'lt' || 'litre' || 'litres' || 'liter' || 'liters' => 1000.0,
      _ => 1.0, // g, ml
    };
    out.add(n * factor);
  }
  return out;
}

/// Compares the text read from a photo of the customer's pack with the product we
/// expected. Matching text shows it is the same product, not that it is genuine:
/// counterfeits copy the words too, so the photos still need comparing by eye.
PackMatch comparePackText({
  required String packText,
  String? name,
  String? brand,
  String? quantity,
  String? barcode,
}) {
  final text = _norm(packText);
  final squashed = _squash(packText);
  final checks = <PackCheck>[];

  final brandName = brand
      ?.split(',')
      .map((b) => b.trim())
      .firstWhere((b) => b.isNotEmpty, orElse: () => '');
  final brandKey = brandName == null ? '' : _squash(brandName);
  if (brandKey.length >= 2) {
    checks.add(PackCheck('Brand', brandName!, squashed.contains(brandKey)));
  }

  if (name != null && name.trim().isNotEmpty) {
    final brandWords = _norm(brandName ?? '').split(' ').toSet();
    final words = _norm(name)
        .split(' ')
        .where(
          (w) =>
              w.length >= 4 &&
              !_genericWords.contains(w) &&
              !brandWords.contains(w) &&
              !RegExp(r'^\d').hasMatch(w),
        )
        .toSet();
    if (words.isNotEmpty) {
      final hits = words.where((w) => text.contains(w)).length;
      // Packs rarely print the full listing name, so half its distinctive words is enough
      checks.add(
        PackCheck('Product name', name.trim(), hits * 2 >= words.length),
      );
    }
  }

  final expectedAmounts = quantity == null
      ? const <double>[]
      : amountsIn(quantity);
  if (expectedAmounts.isNotEmpty) {
    final want = expectedAmounts.first;
    final onPack = amountsIn(packText);
    checks.add(
      PackCheck(
        'Size',
        quantity!.trim(),
        onPack.any((a) => (a - want).abs() <= want * 0.01),
      ),
    );
  }

  final digits = barcode?.replaceAll(_nonDigit, '') ?? '';
  if (digits.length >= 8) {
    // Printed under the bars with spaces, e.g. "6 001067 089827"
    checks.add(
      PackCheck(
        'Barcode number',
        digits,
        packText.replaceAll(_nonDigit, '').contains(digits),
      ),
    );
  }

  return PackMatch(checks, readAnyText: text.isNotEmpty);
}
