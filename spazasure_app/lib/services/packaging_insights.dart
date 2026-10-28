import 'package:spazasure_app/services/barcode_check.dart';
import 'package:spazasure_app/services/packaging_text_parser.dart';

/// Reads packaging text on the phone, for when the AI analysis can't be reached.
/// Returns the same shape as the server's packaging analysis so one screen shows both.
class PackagingInsights {
  static final _maker = RegExp(
    r'\b(?:manufactured|made|produced|packed|distributed|imported|marketed|mfd\.?)\s*(?:and\s+\w+\s*)?(?:by|for)\b[\s:]*(.+)',
    caseSensitive: false,
  );
  static final _company = RegExp(
    r"\b[\w&'\- ]{2,60}\s(?:\(pty\)\s*ltd|pty\s*ltd|limited|ltd|inc\.?|cc)\b",
    caseSensitive: false,
  );
  static final _phone = RegExp(
    r'(?:\+27|\b0)(?:\s?\(?\d\)?){8,10}\b|\b08[06]0\s?\d{3}\s?\d{3,4}\b',
  );
  static final _email = RegExp(r'\b[\w.+-]+@[\w-]+(?:\.[\w-]+)+\b');
  static final _web = RegExp(
    r'\b(?:www\.)?[a-z0-9-]+(?:\.[a-z0-9-]+)*\.(?:co\.za|com|org|za|net)\b',
    caseSensitive: false,
  );
  static final _size = RegExp(
    r'(?<![\w.])(\d+(?:[.,]\d+)?\s*(?:x\s*\d+(?:[.,]\d+)?\s*)?(?:kg|g|mg|ml|l|litre|litres|ltr)\b)',
    caseSensitive: false,
  );
  static final _ingredients = RegExp(
    r'\bingredients?\b\s*[:.\-]?\s*(.{10,600}?)(?:\.\s*(?:[A-Z][a-z]+\s*:|contains|allergen|store|warning|directions)|$)',
    caseSensitive: false,
    dotAll: true,
  );
  static final _origin = RegExp(
    r'\b(?:made|produced|manufactured|product)\s+(?:in|of)\s+(?:the\s+)?([A-Z][A-Za-z ]{2,30}?)(?:[.,;\n]|$)',
  );

  static const _allergens = {
    'milk': 'Milk',
    'dairy': 'Milk',
    'lactose': 'Milk',
    'egg': 'Eggs',
    'peanut': 'Peanuts',
    'tree nut': 'Tree nuts',
    'almond': 'Tree nuts',
    'cashew': 'Tree nuts',
    'soy': 'Soya',
    'soya': 'Soya',
    'wheat': 'Wheat (gluten)',
    'gluten': 'Gluten',
    'fish': 'Fish',
    'crustacean': 'Shellfish',
    'shellfish': 'Shellfish',
    'sesame': 'Sesame',
    'mustard': 'Mustard',
    'celery': 'Celery',
    'sulphite': 'Sulphites',
    'sulfite': 'Sulphites',
  };

  static const _certifications = {
    r'\bSABS\b': 'SABS mark',
    r'\bNRCS\b': 'NRCS (compulsory standards)',
    r'\bhal(?:a|aa)l\b|\bSANHA\b|\bMJC\b': 'Halaal',
    r'\bkosher\b|\bBeth Din\b': 'Kosher',
    r'\bSAHPRA\b|\bReg\.?\s*No\.?\s*[A-Z0-9/.\- ]{4,}':
        'SAHPRA / registration number',
    r'\bAct\s*54\s*of\s*1972\b': 'Foodstuffs Act 54 of 1972',
    r'\bAct\s*36\s*of\s*1947\b': 'Act 36 of 1947 registration',
    r'\bISO\s*\d{4,5}\b': 'ISO standard',
    r'\bHeart\s*(?:and|&)\s*Stroke\b|\bGI\s*Foundation\b':
        'Health foundation endorsement',
  };

  static const _claims = [
    'sugar free',
    'no added sugar',
    'fat free',
    'low fat',
    'gluten free',
    'organic',
    'natural',
    '100% natural',
    'preservative free',
    'no preservatives',
    'vegan',
    'vegetarian',
    'high protein',
    'source of fibre',
    'fortified',
    'kills 99.9%',
    'antibacterial',
    'dermatologically tested',
    'hypoallergenic',
    'clinically proven',
    'cures',
    'treats',
    'prevents',
    'boosts immunity',
  ];

  // Health claims a food or cosmetic may not make in South Africa.
  static const _forbiddenClaims = [
    'cures',
    'treats',
    'prevents',
    'boosts immunity',
    'heals',
  ];

  static const _warnings = [
    'keep out of reach of children',
    'warning',
    'caution',
    'danger',
    'poison',
    'flammable',
    'do not swallow',
    'avoid contact with eyes',
    'for external use only',
    'not suitable for',
    'consult a doctor',
    'harmful',
  ];

  // Misspellings that show up on counterfeit packs.
  static const _misspellings = {
    'ingrediants': 'ingredients',
    'ingredents': 'ingredients',
    'manufacterd': 'manufactured',
    'manufatured': 'manufactured',
    'manufactered': 'manufactured',
    'expiery': 'expiry',
    'expirey': 'expiry',
    'bestbefore': 'best before',
    'flavor enhancer': 'flavour enhancer',
    'protien': 'protein',
    'nutritonal': 'nutritional',
    'nutrtional': 'nutritional',
    'vitamine': 'vitamin',
    'reciepe': 'recipe',
    'qaulity': 'quality',
    'quaility': 'quality',
    'guarentee': 'guarantee',
    'origional': 'original',
    'orignal': 'original',
    'premuim': 'premium',
    'cooking oil oil': 'cooking oil',
    'netweight': 'net weight',
    'shampo': 'shampoo',
    'detergant': 'detergent',
    'lotoin': 'lotion',
  };

  static Map<String, dynamic> analyze(PackagingScan scan) {
    final text = scan.rawText;
    final lower = text.toLowerCase();
    final lines = text
        .split('\n')
        .map((l) => l.trim())
        .where((l) => l.isNotEmpty)
        .toList();

    final makerLine = _maker
        .firstMatch(text)
        ?.group(1)
        ?.split('\n')
        .first
        .trim();
    final manufacturer =
        _company.firstMatch(makerLine ?? '')?.group(0)?.trim() ??
        _company.firstMatch(text)?.group(0)?.trim() ??
        makerLine;
    final contacts = <String>{
      ..._phone.allMatches(text).map((m) => m.group(0)!.trim()),
      ..._email.allMatches(text).map((m) => m.group(0)!),
      ..._web
          .allMatches(text)
          .map((m) => m.group(0)!.toLowerCase())
          .where((w) => !w.contains('@')),
    }.toList();
    final ingredients = _ingredients
        .firstMatch(text)
        ?.group(1)
        ?.replaceAll('\n', ' ')
        .trim();
    final allergens = <String>{
      for (final e in _allergens.entries)
        if (RegExp(
          '\\b${e.key}',
          caseSensitive: false,
        ).hasMatch(ingredients ?? lower))
          e.value,
    }.toList();
    final certifications = <String>{
      for (final e in _certifications.entries)
        if (RegExp(e.key, caseSensitive: false).hasMatch(text)) e.value,
    }.toList();
    final claims = _claims.where(lower.contains).toList();
    final warnings = <String>{
      for (final w in _warnings)
        if (lower.contains(w))
          lines.firstWhere((l) => l.toLowerCase().contains(w), orElse: () => w),
    }.toList();
    final misspelled = _misspellings.entries
        .where((e) => lower.contains(e.key))
        .toList();
    final forbidden = _forbiddenClaims
        .where((c) => RegExp('\\b$c\\b').hasMatch(lower))
        .toList();

    final productName = _guessName(lines);
    final barcode = scan.barcode;
    final expired =
        scan.expiry != null && scan.expiry!.isBefore(DateTime.now());

    final redFlags = <String>[
      for (final m in misspelled)
        'Spelling mistake on the pack: "${m.key}" instead of "${m.value}".',
      for (final c in forbidden)
        'The pack claims it "$c" something. Foods and cosmetics may not make medical claims.',
      if (barcode != null && !hasValidCheckDigit(barcode))
        'The barcode $barcode fails its check digit.',
      if (expired) 'The expiry date has passed.',
    ];
    final goodSigns = <String>[
      if (manufacturer != null) 'Names the company responsible: $manufacturer.',
      if (contacts.isNotEmpty) 'Prints contact details (${contacts.first}).',
      if (scan.batch != null) 'Has a batch code (${scan.batch}).',
      if (scan.expiry != null && !expired) 'Has an expiry date.',
      for (final c in certifications) 'Shows a $c.',
    ];

    final checks = <Map<String, String>>[
      {
        'label': 'Manufacturer',
        'status': manufacturer != null ? 'pass' : 'warn',
        'detail':
            manufacturer ??
            'No manufacturer or importer could be read. South African retail packs must name one; check the other sides.',
      },
      {
        'label': 'Contact details',
        'status': contacts.isNotEmpty ? 'pass' : 'warn',
        'detail': contacts.isNotEmpty
            ? contacts.join(', ')
            : 'No careline, email or website was found.',
      },
      if (scan.expiry != null)
        {
          'label': 'Expiry date',
          'status': expired ? 'fail' : 'pass',
          'detail': expired
              ? 'Expired. Do not sell or use it.'
              : 'Good until ${_date(scan.expiry!)}.',
        }
      else
        {
          'label': 'Expiry date',
          'status': 'warn',
          'detail': 'No expiry or best-before date was found in this photo.',
        },
      {
        'label': 'Batch code',
        'status': scan.batch != null ? 'pass' : 'warn',
        'detail': scan.batch ?? 'No batch or lot code was found in this photo.',
      },
      if (misspelled.isNotEmpty)
        {
          'label': 'Spelling',
          'status': 'fail',
          'detail': '${misspelled.length} spelling mistake(s) on the pack.',
        },
    ];

    final fails = checks.where((c) => c['status'] == 'fail').length;
    final warns = checks.where((c) => c['status'] == 'warn').length;
    final verdict = text.trim().length < 20
        ? 'cannot_tell'
        : fails > 0 || forbidden.isNotEmpty
        ? 'some_concerns'
        : warns >= 3
        ? 'cannot_tell'
        : 'looks_genuine';

    return {
      'source': 'packaging_device',
      'barcode': barcode,
      'riskLevel': switch (verdict) {
        'some_concerns' => fails > 1 ? 'high' : 'medium',
        'looks_genuine' => warns > 0 ? 'medium' : 'low',
        _ => 'unknown',
      },
      'checks': checks,
      'analysis': {
        'imageQuality': text.trim().length < 20 ? 'blurry' : 'good',
        'productName': productName,
        'brand': null,
        'packSize': _size.firstMatch(text)?.group(1),
        'productType': 'other',
        'manufacturer': manufacturer,
        'contacts': contacts,
        'countryOfOrigin': _origin.firstMatch(text)?.group(1)?.trim(),
        'barcodeDigits': barcode,
        'batchNumber': scan.batch,
        'expiryDate': scan.expiry == null ? null : _date(scan.expiry!),
        'ingredients': ingredients,
        'allergens': allergens,
        'claims': claims,
        'certifications': certifications,
        'warnings': warnings,
        'verdict': verdict,
        'redFlags': redFlags,
        'goodSigns': goodSigns,
        'summary': text.trim().length < 20
            ? 'Hardly any text could be read. Retake the photo closer, in good light, with the label flat.'
            : 'Read on your phone from the text on the pack. This checks the label details but cannot judge '
                  'logos, print quality or seals; look at those yourself.',
        'tips': [
          if (scan.expiry == null)
            'Find the expiry date (often on the bottom, lid or seam) and photograph it too.',
          if (manufacturer == null)
            'Turn the pack and photograph the side with the manufacturer\'s name and address.',
          'Compare the logo, colours and print sharpness with a pack you know is genuine.',
          'Check that seals and lids are intact and dates have not been covered with a sticker.',
        ],
      },
    };
  }

  // The product name is usually one of the first short lines in capitals or title case.
  static String? _guessName(List<String> lines) {
    for (final l in lines.take(8)) {
      final letters = l.replaceAll(RegExp(r'[^A-Za-z]'), '');
      if (letters.length < 3 || l.length > 40) continue;
      if (RegExp(
        r'ingredient|warning|net|mass|exp|batch|best|www|@|\d{5}',
        caseSensitive: false,
      ).hasMatch(l)) {
        continue;
      }
      return l;
    }
    return null;
  }

  static String _date(DateTime d) => d.toIso8601String().substring(0, 10);
}
