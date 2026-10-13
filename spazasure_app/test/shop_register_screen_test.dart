import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/features/auth/screens/register_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';

void main() {
  Widget buildScreen() => ChangeNotifierProvider(
    create: (_) => AuthProvider(),
    child: const MaterialApp(home: RegisterScreen()),
  );

  testWidgets('shop registration starts with clear representative fields', (
    tester,
  ) async {
    await tester.pumpWidget(buildScreen());
    await tester.pumpAndSettle();

    expect(find.text('Shop owner application'), findsOneWidget);
    expect(find.text('Representative details'), findsOneWidget);
    expect(find.text('First name *'), findsOneWidget);
    expect(find.text('Mobile number *'), findsOneWidget);
    expect(find.text('Confirm password *'), findsOneWidget);

    final firstField = tester.widget<TextField>(find.byType(TextField).first);
    expect(firstField.style?.color, const Color(0xFF17244F));

    final continueButton = tester.widget<ElevatedButton>(
      find.byType(ElevatedButton),
    );
    expect(continueButton.style?.foregroundColor?.resolve({}), Colors.white);
    expect(
      continueButton.style?.backgroundColor?.resolve({}),
      const Color(0xFF25449A),
    );
  });

  testWidgets('shop registration validates required representative data', (
    tester,
  ) async {
    await tester.pumpWidget(buildScreen());
    await tester.pumpAndSettle();
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    expect(
      find.text('Enter the representative’s first name and surname.'),
      findsOneWidget,
    );
    expect(find.text('Representative details'), findsOneWidget);
  });

  testWidgets('valid representative details continue to shop identity', (
    tester,
  ) async {
    await tester.pumpWidget(buildScreen());
    await tester.pumpAndSettle();
    final fields = find.byType(TextField);
    await tester.enterText(fields.at(0), 'Amina');
    await tester.enterText(fields.at(1), 'Mokoena');
    await tester.enterText(fields.at(2), '0821234567');
    await tester.enterText(fields.at(5), 'safePass123');
    await tester.enterText(fields.at(6), 'safePass123');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    expect(find.text('Shop identity'), findsOneWidget);
    expect(find.text('Shop name *'), findsOneWidget);
    expect(find.text('Business type *'), findsOneWidget);
  });
}
