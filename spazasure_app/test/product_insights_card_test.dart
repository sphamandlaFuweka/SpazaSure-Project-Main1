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

    expect(find.text('About this product'), findsOneWidget);
    expect(find.textContaining('about 2½ teaspoons of sugar'), findsOneWidget);
    expect(find.text('Nutrition per 100 ml'), findsOneWidget);
    expect(find.text('Nutri-Score E'), findsOneWidget);
    expect(find.text('NOVA 4'), findsOneWidget);
    expect(find.text('Additives (2)'), findsOneWidget);
    expect(find.text('Registered in Belgium and Luxembourg'), findsOneWidget);
    expect(find.text('Open Food Facts only (not on SpazaSure)'), findsOneWidget);
    expect(find.textContaining('found in Open Food Facts'), findsOneWidget);
    expect(tester.takeException(), isNull); // no layout overflow

    await tester.tap(find.text('Additives (2)'));
    await tester.pumpAndSettle();
    expect(find.text('Sulphite ammonia caramel · colour'), findsOneWidget);
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
    expect(find.text('Registered in South Africa'), findsOneWidget);
    expect(find.text('Check digit wrong'), findsOneWidget);
    expect(find.text('About this product'), findsNothing);
    expect(tester.takeException(), isNull);
  });
}
