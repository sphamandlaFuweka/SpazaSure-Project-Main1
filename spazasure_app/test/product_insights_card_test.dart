import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/features/customer/widgets/customer_verification_result.dart';

// Shaped like the verify endpoint's response for a product only found in Open Food Facts.
final _cola = <String, dynamic>{
  'source': 'open_food_facts',
  'verdict': 'unknown',
  'code': '5449000000996',
  'name': 'Coca-Cola Original',
  'brand': 'Coca-Cola',
  'quantity': '2 l',
  'dataSource': 'Open Food Facts',
  'message':
      "This product is not in SpazaSure's registry, but product details were found in Open Food Facts.",
  'riskLevel': 'low',
  'riskHeadline': 'No warning signs found',
  'riskScore': 10,
  'insights': {
    'productType': 'food',
    'productTypeLabel': 'Food or drink',
    'summary': 'Coca-Cola Original (2 l). Food or drink, listed under sodas.',
    'highlights': [
      {
        'tone': 'warning',
        'text': 'High in sugar: 10.6 g per 100 ml (about 2½ teaspoons of sugar).',
      },
      {'tone': 'good', 'text': 'Low in salt.'},
    ],
    'nutritionBasis': '100 ml',
    'nutrition': [
      {'key': 'energy-kcal', 'label': 'Energy', 'value': 42, 'unit': 'kcal', 'level': null},
      {'key': 'sugars', 'label': 'Sugars', 'value': 10.6, 'unit': 'g', 'level': 'high'},
      {'key': 'salt', 'label': 'Salt', 'value': 0, 'unit': 'g', 'level': 'low'},
    ],
    'novaGroup': 4,
    'novaLabel': 'Ultra-processed food',
    'nutriScore': 'E',
    'nutriScoreLabel': 'Lowest nutritional quality',
    'additives': [
      {'code': 'E150D', 'name': 'Sulphite ammonia caramel', 'purpose': 'colour'},
      {'code': 'E338', 'name': 'Phosphoric acid', 'purpose': 'acid'},
    ],
    'diet': ['Vegan', 'No palm oil'],
    'traces': <String>[],
    'dataSource': 'Open Food Facts',
  },
  'barcodeInfo': {
    'format': 'EAN-13',
    'isRetailBarcode': true,
    'checkDigitValid': true,
    'prefix': '544',
    'registeredIn': 'Belgium and Luxembourg',
    'kind': 'product',
    'explanation':
        'Registered with GS1 Belgium and Luxembourg (prefix 544). This shows where the brand registered the barcode, not necessarily where it was made. The check digit is correct.',
  },
};

Future<void> _show(WidgetTester tester, Map<String, dynamic> product) async {
  tester.view.physicalSize = const Size(390 * 3, 844 * 3); // a typical phone
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: CustomerVerificationResult(
            product: product,
            fallbackCode: product['code'] as String,
            onReport: () {},
          ),
        ),
      ),
    ),
  );
}

void main() {
  testWidgets('explains a product that is only in a global database', (tester) async {
    await _show(tester, _cola);
    await tester.ensureVisible(find.text('Product information'));
    await tester.tap(find.text('Product information'));
    await tester.pumpAndSettle();

    expect(find.text('About this product'), findsOneWidget);
    expect(find.textContaining('about 2½ teaspoons of sugar'), findsOneWidget);
    expect(find.text('Nutrition per 100 ml'), findsOneWidget);
    expect(find.text('Nutri-Score E'), findsOneWidget);
    expect(find.text('NOVA 4'), findsOneWidget);
    expect(find.text('Additives (2)'), findsOneWidget);
    expect(find.text('No (found in Open Food Facts)'), findsOneWidget);

    await tester.ensureVisible(find.text('About the barcode'));
    await tester.tap(find.text('About the barcode'));
    await tester.pumpAndSettle();
    expect(find.text('Registered in Belgium and Luxembourg'), findsOneWidget);

    await tester.ensureVisible(find.text('What we checked'));
    await tester.tap(find.text('What we checked'));
    await tester.pumpAndSettle();
    expect(find.textContaining('product details were found in Open Food Facts'), findsOneWidget);
    expect(tester.takeException(), isNull); // no layout overflow

    await tester.ensureVisible(find.text('Additives (2)'));
    await tester.tap(find.text('Additives (2)'));
    await tester.pumpAndSettle();
    expect(find.text('Sulphite ammonia caramel · colour'), findsOneWidget);
  });

  testWidgets('tells a shop when the product is not sold on the marketplace', (tester) async {
    await _show(tester, {..._cola, 'notInMarketplace': true});

    expect(find.textContaining('Not sold on SpazaSure'), findsOneWidget);
    // Product details are folded under "More details" until opened
    expect(find.text('About this product'), findsNothing);
    await tester.ensureVisible(find.text('Product information'));
    await tester.tap(find.text('Product information'));
    await tester.pumpAndSettle();
    expect(find.text('About this product'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('shows a shop the marketplace listing with an order button', (tester) async {
    String? ordered;
    tester.view.physicalSize = const Size(390 * 3, 844 * 3);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            child: CustomerVerificationResult(
              product: {
                ..._cola,
                'source': 'spazasure_barcode',
                'requiresPin': true,
                'marketplace': {
                  'id': 'p-1',
                  'price': 32.5,
                  'unit': 'case',
                  'stockQty': 40,
                  'minOrderQty': 2,
                  'supplierName': 'Soweto Wholesale',
                  'supplierVerified': true,
                  'isAvailable': true,
                  'repeatScan': true,
                  'scannedAt': '2026-10-20T09:00:00Z',
                },
              },
              fallbackCode: '5449000000996',
              onReport: () {},
              onOrder: (id) => ordered = id,
            ),
          ),
        ),
      ),
    );

    expect(find.text('Sold on the SpazaSure marketplace'), findsOneWidget);
    expect(find.textContaining('R 32.50 per case'), findsOneWidget);
    expect(find.textContaining('Soweto Wholesale (verified)'), findsOneWidget);
    expect(find.textContaining('the code may have been copied'), findsOneWidget);
    // Shops can't redeem the customer's PIN, and are told not to scratch it
    expect(find.text('Check PIN'), findsNothing);
    expect(find.textContaining("Don't scratch it"), findsOneWidget);
    expect(tester.takeException(), isNull);

    await tester.ensureVisible(find.text('Order from Soweto Wholesale'));
    await tester.tap(find.text('Order from Soweto Wholesale'));
    expect(ordered, 'p-1');
  });

  testWidgets('still explains the barcode when no database has the product', (tester) async {
    await _show(tester, {
      'source': 'not_registered',
      'code': '6001234567892',
      'lookupUnavailable': true,
      'message':
          "This product isn't in SpazaSure's registry, and the global product databases can't be reached right now. Try again later.",
      'riskLevel': 'low',
      'barcodeInfo': {
        'format': 'EAN-13',
        'checkDigitValid': false,
        'registeredIn': 'South Africa',
        'explanation': 'Registered with GS1 South Africa (prefix 600).',
      },
    });

    expect(find.textContaining("can't be reached right now"), findsOneWidget);
    await tester.ensureVisible(find.text('About the barcode'));
    await tester.tap(find.text('About the barcode'));
    await tester.pumpAndSettle();
    expect(find.text('Registered in South Africa'), findsOneWidget);
    expect(find.text('Check digit wrong'), findsOneWidget);
    expect(find.text('About this product'), findsNothing);
    expect(tester.takeException(), isNull);
  });
}
