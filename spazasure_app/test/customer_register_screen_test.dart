import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/features/auth/screens/customer_register_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';

void main() {
  testWidgets(
    'shows customer registration sections and separate marketing consent',
    (tester) async {
      tester.view.physicalSize = const Size(800, 1600);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(
        ChangeNotifierProvider(
          create: (_) => AuthProvider(),
          child: const MaterialApp(home: CustomerRegisterScreen()),
        ),
      );

      expect(find.text('Shop with confidence'), findsOneWidget);
      expect(find.text('First name *'), findsOneWidget);
      expect(find.text('Mobile number *'), findsOneWidget);
      expect(find.text('Confirm password *'), findsOneWidget);
      expect(
        find.textContaining('SMS verification codes are not required.'),
        findsOneWidget,
      );

      await tester.scrollUntilVisible(
        find.text('SECTION D'),
        300,
        scrollable: find.byType(Scrollable).first,
      );

      expect(find.text('Service notifications'), findsOneWidget);
      expect(
        find.text(
          'I would like to receive SpazaSure offers, promotions and marketing messages.',
        ),
        findsOneWidget,
      );
      await tester.scrollUntilVisible(
        find.textContaining('Terms and Privacy acknowledgements will be added'),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      expect(
        find.textContaining('Terms and Privacy acknowledgements will be added'),
        findsOneWidget,
      );
    },
  );
}
