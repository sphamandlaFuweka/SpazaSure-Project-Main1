import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/services/packaging_text_parser.dart';

void main() {
  group('barcode', () {
    test('finds a valid EAN-13 in noisy text', () {
      final scan = PackagingTextParser.parse(
        'COCA COLA\n6001240100011 made in SA',
      );
      expect(scan.barcode, '6001240100011');
    });

    test('finds an EAN-13 split by spaces', () {
      final scan = PackagingTextParser.parse('6 001240 100011');
      expect(scan.barcode, '6001240100011');
    });

    test('rejects digit runs with a bad checksum', () {
      expect(PackagingTextParser.parse('1234567890123').barcode, isNull);
    });

    test('does not mistake a spaced date for an EAN-8', () {
      expect(PackagingTextParser.parse('EXP 12 05 2027').barcode, isNull);
    });
  });

  group('expiry', () {
    test('reads dd/mm/yyyy after EXP', () {
      expect(
        PackagingTextParser.parse('EXP: 30/04/2027').expiry,
        DateTime(2027, 4, 30),
      );
    });

    test('reads yyyy-mm-dd after best before', () {
      expect(
        PackagingTextParser.parse('Best before 2027-04-30').expiry,
        DateTime(2027, 4, 30),
      );
    });

    test('reads month names on the following line', () {
      expect(
        PackagingTextParser.parse('USE BY\n15 Mar 2027').expiry,
        DateTime(2027, 3, 15),
      );
    });

    test('month and year only means end of that month', () {
      expect(
        PackagingTextParser.parse('BB 02/2028').expiry,
        DateTime(2028, 2, 29),
      );
    });

    test('ignores dates with no expiry keyword', () {
      expect(PackagingTextParser.parse('Packed 30/04/2026').expiry, isNull);
    });

    test('rejects impossible dates', () {
      expect(PackagingTextParser.parse('EXP 31/02/2027').expiry, isNull);
    });
  });

  group('batch', () {
    test('reads lot and batch numbers', () {
      expect(PackagingTextParser.parse('LOT: ab12345').batch, 'AB12345');
      expect(PackagingTextParser.parse('Batch No. L2310A').batch, 'L2310A');
    });

    test('ignores words with no digits', () {
      expect(PackagingTextParser.parse('batch quality assured').batch, isNull);
    });
  });
}
