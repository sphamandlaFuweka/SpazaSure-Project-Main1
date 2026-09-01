import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/main.dart';

void main() {
  testWidgets('App launches', (WidgetTester tester) async {
    await tester.pumpWidget(const SpazaSureApp());
    expect(find.text('SpazaSure'), findsOneWidget);
  });
}
