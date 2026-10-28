import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/features/customer/screens/compare_pack_screen.dart';
import 'package:spazasure_app/features/customer/widgets/customer_verification_result.dart';
import 'package:spazasure_app/features/customer/widgets/product_gallery.dart';

// A 1x1 PNG, the way suppliers' uploads can be stored inline
const _dataImage =
    'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=';

/// Opens one of the folded "More details" sections of the result.
Future<void> _open(WidgetTester tester, String section) async {
  await tester.ensureVisible(find.text(section));
  await tester.tap(find.text(section));
  await tester.pumpAndSettle();
}

Future<void> _show(WidgetTester tester, Map<String, dynamic> product) async {
  tester.view.physicalSize = const Size(390 * 3, 844 * 3);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: CustomerVerificationResult(
            product: product,
            fallbackCode: '6001067089827',
            onReport: () {},
            onReportWithPhoto: (_, __) {},
          ),
        ),
      ),
    ),
  );
}

void main() {
  test('uses the database gallery, or else the supplier photos', () {
    final fromDatabase = GalleryPhoto.fromProduct({
      'images': ['https://x/front.jpg'],
      'gallery': [
        {
          'url': 'https://x/front.jpg',
          'label': 'Front of pack',
          'source': 'Open Food Facts',
        },
        {
          'url': 'https://x/ingredients.jpg',
          'label': 'Ingredients',
          'source': 'Open Food Facts',
        },
      ],
    });
    expect(fromDatabase.map((p) => p.label), ['Front of pack', 'Ingredients']);

    final fromSupplier = GalleryPhoto.fromProduct({
      'images': ['/uploads/products/a.jpg', _dataImage],
    });
    expect(fromSupplier.map((p) => p.label), [
      'Supplier photo 1',
      'Supplier photo 2',
    ]);
    expect(fromSupplier.first.source, 'SpazaSure supplier');
  });

  testWidgets('shows supplier photos and opens the side-by-side comparison', (
    tester,
  ) async {
    await _show(tester, {
      'source': 'spazasure_barcode',
      'name': 'Sunlight Dishwashing Liquid',
      'quantity': '750 ml',
      'code': '6001087357296',
      'riskLevel': 'low',
      'images': ['/uploads/products/sunlight.jpg', _dataImage],
    });
    await _open(tester, 'Compare with photos');

    expect(find.text('Photos of the real product'), findsOneWidget);
    expect(find.text('Supplier photo 1'), findsOneWidget);
    expect(find.text('Photos: SpazaSure supplier'), findsOneWidget);
    expect(tester.takeException(), isNull);

    await tester.ensureVisible(find.text('Compare with my pack'));
    await tester.tap(find.text('Compare with my pack'));
    await tester.pumpAndSettle();

    expect(find.byType(ComparePackScreen), findsOneWidget);
    expect(find.text('Real product'), findsOneWidget);
    expect(find.text('Your pack'), findsOneWidget);
    expect(find.text('Tap to photograph your pack'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('offers a text check when there are no photos on record', (
    tester,
  ) async {
    await _show(tester, {
      'source': 'open_food_facts',
      'name': 'Iwisa Super Maize Meal',
      'riskLevel': 'low',
    });
    await _open(tester, 'Compare with photos');

    expect(find.text('Check your pack'), findsOneWidget);
    expect(find.text('Check the text on my pack'), findsOneWidget);
  });
}
