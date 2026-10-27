import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/services/pack_match.dart';

void main() {
  test('matches the text printed on a genuine-looking pack', () {
    final m = comparePackText(
      packText:
          'COCA COLA\nOriginal Taste\nSoft drink 2L\nIngredients: carbonated water\n5 449000 000996',
      name: 'Coca-Cola Original Taste',
      brand: 'Coca-Cola, The Coca-Cola Company',
      quantity: '2 l',
      barcode: '5449000000996',
    );

    expect(m.readAnyText, isTrue);
    expect(m.checks.map((c) => c.label), [
      'Brand',
      'Product name',
      'Size',
      'Barcode number',
    ]);
    expect(m.matched, 4);
  });

  test('flags a different size and a different barcode', () {
    final m = comparePackText(
      packText: 'Coca-Cola Original Taste 500 ml 6001234567892',
      name: 'Coca-Cola Original Taste',
      brand: 'Coca-Cola',
      quantity: '2 l',
      barcode: '5449000000996',
    );

    expect(m.missing.map((c) => c.label), ['Size', 'Barcode number']);
    expect(m.matched, 2);
  });

  test('spots a look-alike brand', () {
    final m = comparePackText(
      packText: 'Koka Kola cola drink 2L',
      name: 'Coca-Cola',
      brand: 'Coca-Cola',
    );

    expect(m.checks.single.label, 'Brand');
    expect(m.checks.single.found, isFalse);
  });

  test('converts units so 2 L equals 2000 ml and 1 kg equals 1000 g', () {
    expect(amountsIn('2 L'), [2000]);
    expect(amountsIn('2000ml'), [2000]);
    expect(amountsIn('1kg'), [1000]);
    expect(amountsIn('2,5 kg'), [2500]);
    expect(
      comparePackText(packText: 'Net 1000 g', quantity: '1 kg').matched,
      1,
    );
  });

  test('skips checks it has nothing to compare against', () {
    final m = comparePackText(packText: '', name: 'Maize meal');

    expect(m.readAnyText, isFalse);
    expect(m.checks.single.label, 'Product name');
  });
}
