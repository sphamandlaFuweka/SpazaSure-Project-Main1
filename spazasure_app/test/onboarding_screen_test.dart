import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:spazasure_app/features/onboarding/screens/onboarding_screen.dart';

/// Set SHOT_DIR to save what each page looks like.
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

void main() {
  setUpAll(() => GoogleFonts.config.allowRuntimeFetching = false);

  testWidgets('walks through every page to the login', (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MaterialApp(
        home: const RepaintBoundary(
          key: ValueKey('shot'),
          child: OnboardingScreen(),
        ),
        routes: {'/login': (_) => const Text('login page')},
      ),
    );
    // Let the entrance animations finish (the cards float forever, so no pumpAndSettle)
    for (var f = 0; f < 20; f++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
    expect(tester.takeException(), isNull);
    await _shot(tester, 'page1');

    for (var i = 2; i <= 4; i++) {
      await tester.tap(find.text('Next'));
      await tester.pump(const Duration(milliseconds: 250));
      if (i == 2) await _shot(tester, 'mid-swipe');
      await tester.pump(const Duration(seconds: 1));
      expect(tester.takeException(), isNull);
      await _shot(tester, 'page$i');
    }

    expect(find.text('Skip'), findsNothing);
    await tester.tap(find.text('Get Started'));
    await tester.pump();
    await tester.pump(const Duration(seconds: 1));
    expect(find.text('login page'), findsOneWidget);
  });

  testWidgets('fits a small phone', (tester) async {
    tester.view.physicalSize = const Size(320, 568);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(const MaterialApp(home: OnboardingScreen()));
    await tester.pump(const Duration(seconds: 1));
    expect(tester.takeException(), isNull);
  });
}
