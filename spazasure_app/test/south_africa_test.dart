import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/core/geo/south_africa.dart';

void main() {
  test('finds the province for major cities', () {
    expect(
      SouthAfrica.provinceAt(-29.86, 31.02)?.name,
      'KwaZulu-Natal',
    ); // Durban
    expect(
      SouthAfrica.provinceAt(-26.2, 28.04)?.name,
      'Gauteng',
    ); // Johannesburg
    expect(
      SouthAfrica.provinceAt(-33.92, 18.42)?.name,
      'Western Cape',
    ); // Cape Town
    expect(
      SouthAfrica.provinceAt(-29.12, 26.21)?.name,
      'Free State',
    ); // Bloemfontein
  });

  test('returns null outside South Africa', () {
    expect(SouthAfrica.provinceAt(51.5, -0.12), isNull); // London
  });

  test('every province sits inside the national bounds', () {
    for (final p in SouthAfrica.provinces) {
      expect(SouthAfrica.bounds.contains(p.box.south, p.box.west), isTrue);
      expect(SouthAfrica.bounds.contains(p.box.north, p.box.east), isTrue);
    }
  });
}
