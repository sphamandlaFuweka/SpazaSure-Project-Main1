import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:spazasure_app/core/widgets/product_card.dart';
import 'package:spazasure_app/models/models.dart';

void main() {
  setUpAll(() => GoogleFonts.config.allowRuntimeFetching = false);

  // Everything a card can show at once
  const full = Product(
    id: 'p1',
    name: 'Iwisa Super Maize Meal Extra Long Name 10kg Family Pack',
    description: '',
    sku: '',
    price: 129.99,
    discountPrice: 109.99,
    imageUrl: '',
    categoryId: 'c1',
    categoryName: 'Grains',
    supplierId: 's1',
    supplierName: 'SpazaSure Wholesale Distributors (Pty) Ltd',
    isNearby: true,
    supplierCity: 'Mitchells Plain, Cape Town',
    supplierTier: 'gold',
    branchStockNote: 'Only 3 left at the branch near you, more at head office',
  );

  testWidgets('fits three to a row on a small phone', (tester) async {
    tester.view.physicalSize = const Size(360, 740);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: GridView.builder(
          padding: const EdgeInsets.all(12),
          gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: 3,
            mainAxisExtent: ProductCard.gridHeight,
            crossAxisSpacing: 8,
            mainAxisSpacing: 8,
          ),
          itemCount: 6,
          itemBuilder: (_, __) => const ProductCard(product: full),
        ),
      ),
    ));

    // A RenderFlex overflow is reported as an exception
    expect(tester.takeException(), isNull);
    expect(find.byType(ProductCard), findsNWidgets(6));
    expect(tester.getSize(find.byType(ProductCard).first).width, lessThan(115));
  });
}
