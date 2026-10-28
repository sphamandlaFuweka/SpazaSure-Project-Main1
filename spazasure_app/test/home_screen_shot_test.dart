import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:spazasure_app/features/marketplace/screens/home_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/providers/product_provider.dart';

/// Renders the dashboard; set SHOT_DIR to save a picture of it.
const _shotDir = String.fromEnvironment('SHOT_DIR');

void main() {
  setUpAll(() => GoogleFonts.config.allowRuntimeFetching = false);

  // Pictures only: the test font is far wider than Nunito, so rows that fit on a phone overflow here
  testWidgets('dashboard picture', skip: _shotDir.isEmpty, (tester) async {
    final onError = FlutterError.onError;
    FlutterError.onError = (_) {};
    addTearDown(() => FlutterError.onError = onError);
    SharedPreferences.setMockInitialValues({});
    tester.view.physicalSize = const Size(390, 1100);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => AuthProvider()),
          ChangeNotifierProvider(create: (_) => CartProvider()),
          ChangeNotifierProvider(create: (_) => ProductProvider()),
        ],
        child: const MaterialApp(
          home: RepaintBoundary(key: ValueKey('shot'), child: HomeScreen()),
        ),
      ),
    );
    for (var i = 0; i < 20; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }

    {
      final boundary = tester.renderObject<RenderRepaintBoundary>(
        find.byKey(const ValueKey('shot')),
      );
      await tester.runAsync(() async {
        final image = await boundary.toImage(pixelRatio: 1);
        final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
        File(
          '$_shotDir/dashboard.png',
        ).writeAsBytesSync(bytes!.buffer.asUint8List());
      });
    }
    // Close the screen and let any network timeouts it started run out
    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump(const Duration(seconds: 40));
  });
}
