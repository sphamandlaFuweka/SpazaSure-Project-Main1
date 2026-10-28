import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/services/packaging_insights.dart';
import 'package:spazasure_app/services/packaging_text_parser.dart';
import 'package:spazasure_app/services/scanned_code.dart';

void main() {
  group('ScannedCode', () {
    test('retail barcode is a product code', () {
      final c = ScannedCode.parse('6009523606526');
      expect(c.kind, ScannedCodeKind.retailBarcode);
      expect(c.productCode, '6009523606526');
      expect(c.warnings, isEmpty);
    });

    test('barcode with a wrong check digit is flagged', () {
      final c = ScannedCode.parse('6009523606527');
      expect(c.kind, ScannedCodeKind.retailBarcode);
      expect(c.warnings, isNotEmpty);
    });

    test('SpazaSure QR and unit codes are product codes', () {
      expect(ScannedCode.parse('SPZQR-ABC123').kind, ScannedCodeKind.spazaSure);
      expect(ScannedCode.parse('SZABCDEFGH234567').kind, ScannedCodeKind.spazaSure);
    });

    test('GS1 Digital Link gives GTIN, batch and expiry', () {
      final c = ScannedCode.parse('https://id.gs1.org/01/06009523606526/10/LOT42?17=271231');
      expect(c.kind, ScannedCodeKind.gs1);
      expect(c.productCode, '6009523606526');
      expect(c.batch, 'LOT42');
      expect(c.expiry, DateTime(2027, 12, 31));
    });

    test('GS1 element string with FNC1 separators', () {
      final c = ScannedCode.parse('01060095236065261727120010AB12\u001d21XYZ');
      expect(c.kind, ScannedCodeKind.gs1);
      expect(c.productCode, '6009523606526');
      // Day 00 means the end of the month
      expect(c.expiry, DateTime(2027, 12, 31));
      expect(c.batch, 'AB12');
      expect(c.details.any((d) => d.key == 'Serial number' && d.value == 'XYZ'), isTrue);
    });

    test('GS1 element string in brackets', () {
      final c = ScannedCode.parse('(01)06009523606526(17)280630(10)B77');
      expect(c.productCode, '6009523606526');
      expect(c.batch, 'B77');
      expect(c.expiry, DateTime(2028, 6, 30));
    });

    test('ordinary link is explained, not looked up', () {
      final c = ScannedCode.parse('https://www.tigerbrands.com/promo');
      expect(c.kind, ScannedCodeKind.url);
      expect(c.isProduct, isFalse);
      expect(c.warnings, isEmpty);
    });

    test('risky links get warnings', () {
      expect(ScannedCode.parse('http://bit.ly/abc').warnings.length, greaterThanOrEqualTo(2));
      expect(ScannedCode.parse('https://xn--tgerbrands-x9a.com/login').warnings, isNotEmpty);
    });

    test('payment codes are recognised', () {
      expect(ScannedCode.parse('https://pos.snapscan.io/qr/abc').kind, ScannedCodeKind.payment);
      final emv = ScannedCode.parse('000201010211' '5303710' '5909Spaza Joe' '6007Soweto' '6304ABCD');
      expect(emv.kind, ScannedCodeKind.payment);
      expect(emv.details.any((d) => d.value == 'Spaza Joe'), isTrue);
    });

    test('Wi-Fi and contacts', () {
      final w = ScannedCode.parse('WIFI:S:ShopNet;T:WPA;P:secret;;');
      expect(w.kind, ScannedCodeKind.wifi);
      expect(w.details.first.value, 'ShopNet');
      expect(ScannedCode.parse('BEGIN:VCARD\nFN:Jane Doe\nEND:VCARD').kind, ScannedCodeKind.contact);
    });

    test('short text may be a SKU, long text is just text', () {
      expect(ScannedCode.parse('SKU-001').kind, ScannedCodeKind.sku);
      expect(ScannedCode.parse('Thank you for buying our product today').kind, ScannedCodeKind.text);
    });
  });

  group('PackagingInsights', () {
    test('reads the label and flags problems', () {
      const text = '''
GENTLE CREAM
Ingrediants: Aqua, Paraffinum Liquidum, Petrolatum, Parfum.
Manufactured by: Tiger Brands Limited, 3010 William Nicol Drive, Bryanston
Consumer careline 0860 101 107  tigercsd@tigerbrands.com
Keep out of reach of children.
Batch: 99P027060  Best before 12/2027
500 ml
''';
      final r = PackagingInsights.analyze(PackagingTextParser.parse(text));
      final a = r['analysis'] as Map;
      expect(a['manufacturer'], contains('Tiger Brands Limited'));
      expect(a['contacts'], contains('tigercsd@tigerbrands.com'));
      expect(a['packSize'], '500 ml');
      expect(a['batchNumber'], '99P027060');
      expect((a['redFlags'] as List).join(), contains('ingrediants'));
      expect(r['source'], 'packaging_device');
    });

    test('a blank photo cannot be judged', () {
      final r = PackagingInsights.analyze(const PackagingScan(rawText: ''));
      expect((r['analysis'] as Map)['verdict'], 'cannot_tell');
    });
  });
}
