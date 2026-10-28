import 'package:spazasure_app/services/barcode_check.dart';

/// What a scanned barcode or QR code is.
enum ScannedCodeKind {
  /// EAN/UPC barcode on a retail product.
  retailBarcode,

  /// A SpazaSure product QR (SPZQR-...) or per-unit code (SZ...).
  spazaSure,

  /// GS1 data with the product number and often the batch and expiry: a GS1 Digital
  /// Link QR or a GS1 DataMatrix / GS1-128 element string.
  gs1,
  url,
  payment,
  wifi,
  contact,
  email,
  phone,
  sms,
  location,

  /// Short text that may be a supplier's SKU.
  sku,
  text,
}

/// A scanned code, decoded so the app can explain it and, for products, check it.
class ScannedCode {
  final String raw;
  final ScannedCodeKind kind;
  final String title;
  final String explanation;

  /// Labelled values read out of the code, in display order.
  final List<MapEntry<String, String>> details;

  /// Things that make the code worth treating with care, e.g. a link that hides its target.
  final List<String> warnings;

  /// The code to look up in SpazaSure, when this is (or carries) a product code.
  final String? productCode;
  final String? batch;
  final DateTime? expiry;

  /// Where a link goes, for opening it.
  final Uri? link;

  const ScannedCode({
    required this.raw,
    required this.kind,
    required this.title,
    required this.explanation,
    this.details = const [],
    this.warnings = const [],
    this.productCode,
    this.batch,
    this.expiry,
    this.link,
  });

  bool get isProduct => productCode != null;

  static ScannedCode parse(String input) {
    final raw = input.trim();
    final upper = raw.toUpperCase();
    final lower = raw.toLowerCase();

    if (RegExp(r'^\d+$').hasMatch(raw) &&
        const {8, 12, 13, 14}.contains(raw.length)) {
      return ScannedCode(
        raw: raw,
        kind: ScannedCodeKind.retailBarcode,
        title: 'Product barcode',
        explanation:
            'A ${raw.length}-digit retail barcode (GTIN). It identifies the product, not the individual pack.',
        productCode: raw,
        warnings: [
          if (!hasValidCheckDigit(raw))
            'The last digit (the check digit) is wrong. Genuine barcodes always pass this check.',
        ],
      );
    }
    if (upper.startsWith('SPZQR-') ||
        RegExp(r'^SZ[A-Z0-9]{14}$').hasMatch(upper)) {
      return ScannedCode(
        raw: raw,
        kind: ScannedCodeKind.spazaSure,
        title: 'SpazaSure product code',
        explanation: upper.startsWith('SPZQR-')
            ? 'A SpazaSure QR code issued to a supplier for one batch of a product.'
            : 'A SpazaSure code printed on a single pack, with a scratch-off PIN.',
        productCode: raw,
      );
    }

    final gs1 = _gs1(raw);
    if (gs1 != null) return gs1;

    if (upper.startsWith('000201')) return _emvPayment(raw);
    if (upper.startsWith('WIFI:')) return _wifi(raw);
    if (upper.startsWith('BEGIN:VCARD') || upper.startsWith('MECARD:')) {
      return _contact(raw);
    }
    if (lower.startsWith('mailto:') || upper.startsWith('MATMSG:')) {
      final to = RegExp(
        r'^(?:mailto:|MATMSG:TO:)([^?;]+)',
        caseSensitive: false,
      ).firstMatch(raw)?.group(1);
      return ScannedCode(
        raw: raw,
        kind: ScannedCodeKind.email,
        title: 'Email address',
        explanation: 'Scanning this starts an email.',
        details: [if (to != null) MapEntry('To', to)],
      );
    }
    if (lower.startsWith('tel:')) {
      return ScannedCode(
        raw: raw,
        kind: ScannedCodeKind.phone,
        title: 'Phone number',
        explanation:
            'Scanning this offers to call a number. Check it is the company printed on the pack before calling.',
        details: [MapEntry('Number', raw.substring(4))],
      );
    }
    if (lower.startsWith('smsto:') || lower.startsWith('sms:')) {
      final parts = raw.split(':');
      return ScannedCode(
        raw: raw,
        kind: ScannedCodeKind.sms,
        title: 'Text message',
        explanation:
            'Scanning this prepares an SMS. Premium-rate SMS codes can cost money; check the number first.',
        details: [
          if (parts.length > 1) MapEntry('Number', parts[1]),
          if (parts.length > 2) MapEntry('Message', parts.sublist(2).join(':')),
        ],
      );
    }
    if (lower.startsWith('geo:')) {
      return ScannedCode(
        raw: raw,
        kind: ScannedCodeKind.location,
        title: 'Map location',
        explanation: 'A GPS position.',
        details: [MapEntry('Coordinates', raw.substring(4).split('?').first)],
        link: Uri.tryParse(raw),
      );
    }

    final uri = _asUrl(raw);
    if (uri != null) return _url(raw, uri);

    if (!raw.contains(RegExp(r'\s')) && raw.length <= 40) {
      return ScannedCode(
        raw: raw,
        kind: ScannedCodeKind.sku,
        title: 'Product or stock code',
        explanation:
            'Not a standard barcode. It may be a supplier\'s SKU, so SpazaSure checks it against registered products.',
        productCode: raw,
      );
    }
    return ScannedCode(
      raw: raw,
      kind: ScannedCodeKind.text,
      title: 'Plain text',
      explanation:
          'This code only holds text. It does not link to a product or prove anything about it.',
      details: [
        MapEntry('Text', raw.length > 400 ? '${raw.substring(0, 400)}…' : raw),
      ],
    );
  }

  // ── GS1 ──────────────────────────────────────────────────────────────────

  static const _gs1Names = {
    '01': 'Product number (GTIN)',
    '02': 'Contained product (GTIN)',
    '10': 'Batch / lot',
    '11': 'Production date',
    '13': 'Packaging date',
    '15': 'Best before',
    '16': 'Sell by',
    '17': 'Expiry date',
    '21': 'Serial number',
    '30': 'Quantity',
    '37': 'Units in the case',
    '310': 'Net weight (kg)',
    '400': 'Order number',
    '422': 'Country of origin',
  };

  // Fixed-length GS1 application identifiers (data length after the AI).
  static const _fixed = {
    '00': 18,
    '01': 14,
    '02': 14,
    '11': 6,
    '12': 6,
    '13': 6,
    '15': 6,
    '16': 6,
    '17': 6,
    '20': 2,
  };

  static ScannedCode? _gs1(String raw) {
    final values = <String, String>{};
    final uri = Uri.tryParse(raw);
    if (uri != null && uri.hasScheme && uri.scheme.startsWith('http')) {
      // GS1 Digital Link: https://brand.example/01/<gtin>/10/<batch>?17=<yymmdd>
      final segs = uri.pathSegments;
      final at = segs.indexOf('01');
      if (at < 0 ||
          at + 1 >= segs.length ||
          !RegExp(r'^\d{8,14}$').hasMatch(segs[at + 1])) {
        return null;
      }
      for (var i = at; i + 1 < segs.length; i += 2) {
        values[segs[i]] = Uri.decodeComponent(segs[i + 1]);
      }
      uri.queryParameters.forEach((k, v) {
        if (RegExp(r'^\d{2,4}$').hasMatch(k)) values[k] = v;
      });
    } else {
      // Element string: "]d2" / "]C1" symbology prefix, FNC1 as the GS character, or "(01)…" text.
      var s = raw.replaceFirst(RegExp(r'^\][A-Za-z]\d'), '');
      if (s.startsWith('(')) {
        for (final m in RegExp(r'\((\d{2,4})\)([^(]+)').allMatches(s)) {
          values[m[1]!] = m[2]!.trim();
        }
      } else {
        if (!s.startsWith('01') && !s.startsWith('02')) return null;
        while (s.isNotEmpty) {
          final ai = s.length >= 2 ? s.substring(0, 2) : s;
          final fixed = _fixed[ai];
          if (fixed != null) {
            if (s.length < 2 + fixed) return null;
            values[ai] = s.substring(2, 2 + fixed);
            s = _dropSeparator(s.substring(2 + fixed));
            continue;
          }
          // Measures (31xx-36xx): a 4-digit AI with 6 fixed digits.
          if (RegExp(r'^3[1-6]\d\d\d{6}').hasMatch(s)) {
            values[s.substring(0, 4)] = s.substring(4, 10);
            s = _dropSeparator(s.substring(10));
            continue;
          }
          // Variable length: a 2-digit AI (10, 21, 30, 37) or a 3/4-digit one, ended by GS.
          final len = RegExp(r'^(10|21|22|30|37)').hasMatch(s)
              ? 2
              : (RegExp(r'^(3\d\d)').hasMatch(s) ? 4 : 3);
          if (s.length <= len) break;
          final key = s.substring(0, len);
          final end = s.indexOf('\u001d', len);
          values[key] = end < 0 ? s.substring(len) : s.substring(len, end);
          s = end < 0 ? '' : s.substring(end + 1);
        }
      }
    }

    final gtin = values['01'] ?? values['02'];
    if (gtin == null || !RegExp(r'^\d{8,14}$').hasMatch(gtin)) return null;
    // SpazaSure stores the retail barcode, which is the GTIN-14 without its leading zero.
    final code = gtin.length == 14 && gtin.startsWith('0')
        ? gtin.substring(1)
        : gtin;
    final expiry = _gs1Date(values['17']) ?? _gs1Date(values['15']);

    return ScannedCode(
      raw: raw,
      kind: ScannedCodeKind.gs1,
      title: raw.startsWith('http')
          ? 'GS1 Digital Link QR'
          : 'GS1 product code',
      explanation:
          'A global product code that carries the barcode number${values.containsKey('10') ? ', the batch' : ''}'
          '${expiry != null ? ' and the expiry date' : ''}. Brands use these so each batch can be traced and recalled.',
      details: [
        for (final e in values.entries)
          MapEntry(
            _gs1Names[e.key] ??
                (e.key.startsWith('310')
                    ? 'Net weight (kg)'
                    : 'GS1 field ${e.key}'),
            _isDateAi(e.key) ? _fmt(_gs1Date(e.value)) ?? e.value : e.value,
          ),
      ],
      warnings: [
        if (!hasValidCheckDigit(gtin))
          'The product number fails its check digit, so the code was misprinted or made up.',
        if (expiry != null && expiry.isBefore(DateTime.now()))
          'The expiry date in the code has passed.',
      ],
      productCode: code,
      batch: values['10'],
      expiry: expiry,
      link: raw.startsWith('http') ? uri : null,
    );
  }

  // A fixed-length field may still be followed by an (unneeded) GS separator.
  static String _dropSeparator(String s) =>
      s.startsWith('\u001d') ? s.substring(1) : s;

  static bool _isDateAi(String ai) =>
      const {'11', '13', '15', '16', '17'}.contains(ai);

  static DateTime? _gs1Date(String? v) {
    if (v == null || !RegExp(r'^\d{6}$').hasMatch(v)) return null;
    final y = 2000 + int.parse(v.substring(0, 2));
    final m = int.parse(v.substring(2, 4));
    final d = int.parse(v.substring(4, 6));
    if (m < 1 || m > 12) return null;
    // Day 00 means the end of the month.
    return d == 0 ? DateTime(y, m + 1, 0) : DateTime(y, m, d);
  }

  static String? _fmt(DateTime? d) => d?.toIso8601String().substring(0, 10);

  // ── Links ────────────────────────────────────────────────────────────────

  static const _shorteners = {
    'bit.ly',
    'tinyurl.com',
    't.co',
    'goo.gl',
    'ow.ly',
    'is.gd',
    'buff.ly',
    'cutt.ly',
    'rb.gy',
    'shorturl.at',
    'tiny.cc',
    'qrco.de',
    'rebrand.ly',
    's.id',
    'linktr.ee',
  };

  static const _paymentHosts = {
    'snapscan.io': 'SnapScan',
    'pos.snapscan.io': 'SnapScan',
    'zapper.com': 'Zapper',
    'payf.st': 'PayFast',
    'payfast.co.za': 'PayFast',
    'yoco.com': 'Yoco',
    'paystack.com': 'Paystack',
    'ikhokha.com': 'iKhokha',
  };

  static Uri? _asUrl(String raw) {
    if (raw.contains(RegExp(r'\s'))) return null;
    final withScheme =
        RegExp(r'^[a-z][a-z0-9+.-]*://', caseSensitive: false).hasMatch(raw)
        ? raw
        : RegExp(
            r'^(www\.)?[a-z0-9-]+(\.[a-z0-9-]+)+(/.*)?$',
            caseSensitive: false,
          ).hasMatch(raw)
        ? 'https://$raw'
        : null;
    final uri = withScheme == null ? null : Uri.tryParse(withScheme);
    return uri != null && uri.host.isNotEmpty ? uri : null;
  }

  static ScannedCode _url(String raw, Uri uri) {
    final host = uri.host.toLowerCase();
    final bare = host.startsWith('www.') ? host.substring(4) : host;
    final payment = _paymentHosts.entries
        .where((e) => bare == e.key || bare.endsWith('.${e.key}'))
        .map((e) => e.value)
        .firstOrNull;
    if (payment != null) {
      return ScannedCode(
        raw: raw,
        kind: ScannedCodeKind.payment,
        title: '$payment payment code',
        explanation:
            'Scanning this opens a $payment payment. It says nothing about a product. '
            'Only pay if you trust the shop, and check the shop name and amount in the app before you confirm.',
        details: [
          MapEntry('Website', host),
          ...uri.queryParameters.entries.take(4),
        ],
        warnings: [
          if (uri.scheme != 'https') 'The link is not secure (no https).',
        ],
        link: uri,
      );
    }

    final warnings = <String>[
      if (uri.scheme != 'https')
        'The link is not secure (no https), so anything you type can be read by others.',
      if (_shorteners.contains(bare))
        'This is a short link that hides where it really goes.',
      if (RegExp(r'^\d+\.\d+\.\d+\.\d+$').hasMatch(host))
        'The link goes to a bare internet address, not a company website.',
      if (host.contains('xn--'))
        'The web address uses look-alike letters, a trick used by fake sites.',
      if (uri.userInfo.isNotEmpty)
        'The link hides its real address behind an "@" sign.',
      if (RegExp(
        r'(login|verify|account|password|bank|otp|wallet|claim|prize|winner)',
        caseSensitive: false,
      ).hasMatch('${uri.path}${uri.query}'))
        'The link asks about logins, banking or prizes. Product packs never need your password or OTP.',
      if (raw.length > 200) 'The link is unusually long.',
    ];
    return ScannedCode(
      raw: raw,
      kind: ScannedCodeKind.url,
      title: 'Website link',
      explanation: warnings.isEmpty
          ? 'Opens $host. A link on a pack usually goes to the brand\'s site, recipes or a competition. '
                'It does not prove the product is genuine: anyone can print a QR code.'
          : 'Opens $host. Be careful with this one; see the warnings below.',
      details: [
        MapEntry('Website', host),
        if (uri.path.length > 1)
          MapEntry(
            'Page',
            uri.path.length > 60 ? '${uri.path.substring(0, 60)}…' : uri.path,
          ),
      ],
      warnings: warnings,
      link: uri,
    );
  }

  // ── Other formats ────────────────────────────────────────────────────────

  /// EMVCo merchant QR (used by bank apps and SnapScan/Zapper-style scan-to-pay): TLV fields.
  static ScannedCode _emvPayment(String raw) {
    final fields = <String, String>{};
    var i = 0;
    while (i + 4 <= raw.length) {
      final tag = raw.substring(i, i + 2);
      final len = int.tryParse(raw.substring(i + 2, i + 4));
      if (len == null || i + 4 + len > raw.length) break;
      fields[tag] = raw.substring(i + 4, i + 4 + len);
      i += 4 + len;
    }
    return ScannedCode(
      raw: raw,
      kind: ScannedCodeKind.payment,
      title: 'Scan-to-pay code',
      explanation:
          'A merchant payment QR. It says nothing about a product. Check the shop name and amount in your banking app before you pay.',
      details: [
        if (fields['59'] != null) MapEntry('Pays', fields['59']!),
        if (fields['60'] != null) MapEntry('City', fields['60']!),
        if (fields['54'] != null) MapEntry('Amount', fields['54']!),
        if (fields['53'] == '710') const MapEntry('Currency', 'Rand (ZAR)'),
      ],
    );
  }

  static Map<String, String> _fieldsAfter(String raw, String prefix) {
    final out = <String, String>{};
    for (final part
        in raw.substring(prefix.length).split(RegExp(r'(?<!\\);'))) {
      final at = part.indexOf(':');
      if (at > 0) {
        out[part.substring(0, at).toUpperCase()] = part
            .substring(at + 1)
            .replaceAll(r'\;', ';');
      }
    }
    return out;
  }

  static ScannedCode _wifi(String raw) {
    final f = _fieldsAfter(raw, 'WIFI:');
    return ScannedCode(
      raw: raw,
      kind: ScannedCodeKind.wifi,
      title: 'Wi-Fi network',
      explanation:
          'Joins a Wi-Fi network. Product packaging never needs this; only join networks you trust.',
      details: [
        if (f['S'] != null) MapEntry('Network', f['S']!),
        MapEntry(
          'Security',
          (f['T'] ?? '').isEmpty || f['T'] == 'nopass'
              ? 'Open (no password)'
              : f['T']!,
        ),
        if ((f['P'] ?? '').isNotEmpty) MapEntry('Password', f['P']!),
      ],
    );
  }

  static ScannedCode _contact(String raw) {
    String? field(String name) => RegExp(
      '^$name[^:\n]*:(.+)\$',
      multiLine: true,
      caseSensitive: false,
    ).firstMatch(raw)?.group(1)?.trim();
    final mecard = raw.toUpperCase().startsWith('MECARD:')
        ? _fieldsAfter(raw, 'MECARD:')
        : const <String, String>{};
    final name =
        mecard['N'] ?? field('FN') ?? field('N')?.replaceAll(';', ' ').trim();
    return ScannedCode(
      raw: raw,
      kind: ScannedCodeKind.contact,
      title: 'Contact card',
      explanation: 'Saves a contact to your phone.',
      details: [
        if (name != null) MapEntry('Name', name),
        if ((mecard['ORG'] ?? field('ORG')) != null)
          MapEntry('Company', (mecard['ORG'] ?? field('ORG'))!),
        if ((mecard['TEL'] ?? field('TEL')) != null)
          MapEntry('Phone', (mecard['TEL'] ?? field('TEL'))!),
        if ((mecard['EMAIL'] ?? field('EMAIL')) != null)
          MapEntry('Email', (mecard['EMAIL'] ?? field('EMAIL'))!),
        if ((mecard['URL'] ?? field('URL')) != null)
          MapEntry('Website', (mecard['URL'] ?? field('URL'))!),
      ],
    );
  }
}
