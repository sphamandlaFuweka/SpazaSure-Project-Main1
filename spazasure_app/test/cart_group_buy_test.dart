import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/features/cart/widgets/group_buy_cart_card.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/services/group_buy_service.dart';

GroupBuyCartEntry _entry() => GroupBuyCartEntry(
  group: GroupBuy.fromJson(const {'id': 'deal-1', 'title': 'Maize month'}),
  items: const [
    {'groupBuyProductId': 'line-1', 'quantity': 12},
  ],
  quote: const GroupBuyQuote(
    lines: [
      GroupBuyQuoteLine(
        productName: 'Maize meal 10kg',
        quantity: 12,
        unitPrice: 200,
        lineTotal: 2400,
      ),
    ],
    itemsTotal: 2400,
    deliveryFee: 150,
    total: 2550,
    walletBalance: 0,
  ),
);

void main() {
  test('a group buy counts on the cart badge, apart from the normal order', () {
    final cart = CartProvider()..setGroupBuy(_entry());

    expect(cart.badgeCount, 12);
    expect(cart.count, 0);
    expect(cart.isEmpty, isTrue);

    // Paying for a normal order must leave the group buy in the cart
    cart.clear();
    expect(cart.groupBuy, isNotNull);

    cart.clearGroupBuy();
    expect(cart.badgeCount, 0);
  });

  testWidgets('the cart shows the group buy and offers delivery and payment', (
    tester,
  ) async {
    await tester.pumpWidget(
      ChangeNotifierProvider(
        create: (_) => CartProvider(),
        child: MaterialApp(
          home: Scaffold(body: GroupBuyCartCard(entry: _entry())),
        ),
      ),
    );

    expect(find.text('Maize month'), findsOneWidget);
    expect(find.text('12 x Maize meal 10kg'), findsOneWidget);
    expect(find.text('Add delivery and pay'), findsOneWidget);
  });
}
