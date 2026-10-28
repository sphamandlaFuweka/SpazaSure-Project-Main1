import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:spazasure_app/core/theme/app_theme.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';
import 'package:spazasure_app/features/auth/screens/login_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';

/// Set SHOT_DIR to save pictures.
const _shotDir = String.fromEnvironment('SHOT_DIR');

Future<void> _shot(WidgetTester tester, String name) async {
  if (_shotDir.isEmpty) return;
  final boundary = tester.renderObject<RenderRepaintBoundary>(
    find.byKey(const ValueKey('shot')),
  );
  await tester.runAsync(() async {
    final image = await boundary.toImage(pixelRatio: 1);
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    File('$_shotDir/$name.png').writeAsBytesSync(bytes!.buffer.asUint8List());
  });
}

Future<void> _settle(WidgetTester tester) async {
  // The hero cards float forever, so no pumpAndSettle
  for (var i = 0; i < 30; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

void main() {
  setUpAll(() => GoogleFonts.config.allowRuntimeFetching = false);

  testWidgets('switches between retailer and customer sign in', (tester) async {
    SharedPreferences.setMockInitialValues({});
    tester.view.physicalSize = const Size(390, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ChangeNotifierProvider(
        create: (_) => AuthProvider(),
        child: MaterialApp(
          theme: AppTheme.light,
          home: const RepaintBoundary(
            key: ValueKey('shot'),
            child: LoginScreen(),
          ),
        ),
      ),
    );
    final context = tester.element(find.byType(LoginScreen));
    // Only for pictures: loading assets for real also lets google_fonts try (and fail) in tests
    if (_shotDir.isNotEmpty) {
      await tester.runAsync(() async {
        for (final c in ['red', 'navy', 'yellow', 'green']) {
          await precacheImage(
            AssetImage('assets/images/logo_parts/piece_$c.png'),
            context,
          );
          await precacheImage(
            AssetImage('assets/images/logo_parts/arc_$c.png'),
            context,
          );
        }
      });
    }
    await _settle(tester);
    expect(find.text('Retailer Sign In'), findsOneWidget);
    await _shot(tester, 'login_retailer');

    await tester.tap(find.text('Customer'));
    await _settle(tester);
    expect(find.text('Customer Sign In'), findsOneWidget);
    await _shot(tester, 'login_customer');

    // An empty phone number is caught before anything is sent
    await tester.tap(find.text('Sign In'));
    await tester.pump();
    expect(find.text('Enter your phone number'), findsOneWidget);
    await _settle(tester);
  });

  testWidgets(
    'brand bar: blue with white text, unless a screen picks its own colour',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: RepaintBoundary(
            key: const ValueKey('shot'),
            child: Scaffold(
              appBar: BrandAppBar(
                title: const Text('My Orders'),
                actions: [
                  IconButton(icon: const Icon(Icons.refresh), onPressed: () {}),
                ],
              ),
              body: const SizedBox.expand(),
            ),
          ),
        ),
      );
      final bar = tester.widget<AppBar>(find.byType(BrandAppBar));
      expect(bar.backgroundColor, isNot(Colors.black));
      expect(bar.foregroundColor, Colors.white);
      await _shot(tester, 'brand_bar');

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            appBar: BrandAppBar(
              title: const Text('Scan'),
              backgroundColor: Colors.black,
            ),
          ),
        ),
      );
      final camera = tester.widget<AppBar>(find.byType(BrandAppBar));
      expect(camera.backgroundColor, Colors.black);
      expect(camera.flexibleSpace, isNull);
    },
  );
}
