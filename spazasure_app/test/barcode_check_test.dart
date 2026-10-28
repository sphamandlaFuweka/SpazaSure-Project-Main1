import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/services/barcode_check.dart';

void main() {
  test('accepts real retail barcodes of every GS1 length', () {
    expect(isPlausibleScan('6001067089827'), isTrue); // EAN-13
    expect(isPlausibleScan('036000291452'), isTrue); // UPC-A
    expect(isPlausibleScan('96385074'), isTrue); // EAN-8
    expect(isPlausibleScan('16001067089824'), isTrue); // GTIN-14
  });

  test('rejects a misread retail barcode so the camera keeps scanning', () {
    expect(isPlausibleScan('6001067089820'), isFalse);
    expect(
      isPlausibleScan('6001067O89827'),
      isTrue,
    ); // not all digits: treated as another code
  });

  test('accepts QR codes, SKUs and odd-length numbers as read', () {
    expect(isPlausibleScan('SPZ-QR-ABC123'), isTrue);
    expect(isPlausibleScan('SKU-001'), isTrue);
    expect(isPlausibleScan('123456789'), isTrue);
  });

  test('ignores empty reads', () {
    expect(isPlausibleScan('   '), isFalse);
  });
}
