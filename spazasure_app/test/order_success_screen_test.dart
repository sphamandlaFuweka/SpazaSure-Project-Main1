import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/features/cart/screens/order_success_screen.dart';
import 'package:spazasure_app/services/order_service.dart';

void main() {
  Future<void> show(WidgetTester tester, String method) async {
    await tester.pumpWidget(
      MaterialApp(
        home: OrderSuccessScreen(
          success: OrderSuccess(
            orderNumber: 'ORD-1042',
            paymentMethod: method,
            total: 447.99,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('card payment shows the thank-you screen with the order number', (
    tester,
  ) async {
    await show(tester, OrderService.paystack);

    expect(find.text('Thank you for your order'), findsOneWidget);
    expect(find.text('ORD-1042'), findsOneWidget);
    expect(find.text('Track your order'), findsOneWidget);
    expect(find.text('Back to home'), findsOneWidget);
  });

  testWidgets('cash on delivery says how much cash to have ready', (
    tester,
  ) async {
    await show(tester, OrderService.cashOnDelivery);

    expect(find.text('Order placed!'), findsOneWidget);
    expect(find.textContaining('R447.99 in cash'), findsOneWidget);
  });
}
