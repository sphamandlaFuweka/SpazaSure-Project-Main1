import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:spazasure_app/main.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/providers/cart_provider.dart';

void main() {
  testWidgets('App launches', (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues({});
    // Same providers as main(); the app-wide notification host needs the auth state
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => AuthProvider()..init()),
          ChangeNotifierProvider(create: (_) => CartProvider()),
        ],
        child: const SpazaSureApp(),
      ),
    );
    expect(find.text('SpazaSure'), findsOneWidget);
    // Let the splash animations and timers finish so no timers are left pending.
    await tester.pump(const Duration(seconds: 10));
    // Close the app and let any network timeouts it started run out
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump(const Duration(seconds: 40));
  });
}
