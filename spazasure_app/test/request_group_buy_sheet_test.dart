import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/features/group_buy/widgets/request_group_buy_sheet.dart';

void main() {
  testWidgets('needs a product, then sends what the shop typed', (
    tester,
  ) async {
    String? sentProduct;
    int? sentQuantity;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: RequestGroupBuySheet(
            send: (product, quantity, note) async {
              sentProduct = product;
              sentQuantity = quantity;
            },
          ),
        ),
      ),
    );

    final button = find.widgetWithText(ElevatedButton, 'Send request');
    expect(tester.widget<ElevatedButton>(button).onPressed, isNull);

    await tester.enterText(find.byType(TextField).at(0), ' Maize meal 10kg ');
    await tester.enterText(find.byType(TextField).at(1), '40');
    await tester.pump();
    await tester.tap(button);
    await tester.pump();

    expect(sentProduct, 'Maize meal 10kg');
    expect(sentQuantity, 40);
  });
}
