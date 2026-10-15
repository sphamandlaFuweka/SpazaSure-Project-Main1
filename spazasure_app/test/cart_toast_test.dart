import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/widgets/cart_toast.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/providers/cart_provider.dart';

Product _product(
  String id, {
  String name = 'Maize meal 10kg',
  double price = 100,
  int stock = 5,
}) => Product(
  id: id,
  name: name,
  description: '',
  sku: 'SKU-$id',
  price: price,
  imageUrl: '',
  categoryId: 'c',
  categoryName: 'Groceries',
  supplierId: 's',
  supplierName: 'Bulk Co',
  stockQuantity: stock,
);

/// A tiny shop screen with buttons that add products, like the product cards do.
Widget _app(CartProvider cart, List<Product> products) =>
    ChangeNotifierProvider.value(
      value: cart,
      child: MaterialApp(
        routes: {'/cart': (_) => const Scaffold(body: Text('Cart screen'))},
        home: Scaffold(
          body: Builder(
            builder: (context) => Column(
              children: [
                for (final p in products)
                  TextButton(
                    onPressed: () => CartToast.add(context, p, 1),
                    child: Text('add ${p.id}'),
                  ),
              ],
            ),
          ),
        ),
      ),
    );

void main() {
  testWidgets('shows what was added and the cart total, then hides by itself', (
    tester,
  ) async {
    final cart = CartProvider();
    await tester.pumpWidget(_app(cart, [_product('a')]));

    await tester.tap(find.text('add a'));
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Added to cart'), findsOneWidget);
    expect(find.text('1 × Maize meal 10kg'), findsOneWidget);
    expect(find.text('1 item · R100.00'), findsOneWidget);
    expect(find.text('View cart'), findsOneWidget);

    await tester.pump(const Duration(seconds: 4));
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('Added to cart'), findsNothing);
  });

  testWidgets(
    'adding again updates the same card instead of stacking messages',
    (tester) async {
      final cart = CartProvider();
      await tester.pumpWidget(
        _app(cart, [
          _product('a'),
          _product('b', name: 'Sugar 2kg', price: 40),
        ]),
      );

      await tester.tap(find.text('add a'));
      await tester.pump(const Duration(milliseconds: 300));
      await tester.tap(find.text('add a'));
      await tester.pump(const Duration(milliseconds: 300));

      expect(find.text('Added to cart'), findsOneWidget);
      expect(find.text('2 × Maize meal 10kg'), findsOneWidget);
      expect(find.text('2 items · R200.00'), findsOneWidget);

      // A different product replaces the card's contents, still only one card
      await tester.tap(find.text('add b'));
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('Added to cart'), findsOneWidget);
      expect(find.text('1 × Sugar 2kg'), findsOneWidget);
      expect(find.text('3 items · R240.00'), findsOneWidget);

      // Each add restarts the timer: still visible 2s after the last add
      await tester.pump(const Duration(seconds: 2));
      expect(find.text('Added to cart'), findsOneWidget);
      await tester.pump(const Duration(seconds: 2));
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('Added to cart'), findsNothing);
    },
  );

  testWidgets('explains stock problems instead of adding', (tester) async {
    final cart = CartProvider();
    await tester.pumpWidget(_app(cart, [_product('a', stock: 1)]));

    await tester.tap(find.text('add a'));
    await tester.pump(const Duration(milliseconds: 300));
    await tester.tap(find.text('add a'));
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Couldn\'t add'), findsOneWidget);
    expect(find.text('You already have all 1 in your cart'), findsOneWidget);
    expect(cart.count, 1);

    await tester.pump(const Duration(seconds: 4));
    await tester.pump(const Duration(milliseconds: 300));
  });

  testWidgets('View cart opens the cart and closes the card', (tester) async {
    final cart = CartProvider();
    await tester.pumpWidget(_app(cart, [_product('a')]));

    await tester.tap(find.text('add a'));
    await tester.pump(const Duration(milliseconds: 300));
    await tester.tap(find.text('View cart'));
    await tester.pumpAndSettle();

    expect(find.text('Cart screen'), findsOneWidget);
    expect(find.text('Added to cart'), findsNothing);
  });
}
