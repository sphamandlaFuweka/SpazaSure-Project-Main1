import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/core/widgets/animated_logo.dart';

/// Set SHOT_DIR to save frames of the animation.
const _shotDir = String.fromEnvironment('SHOT_DIR');

const _parts = [
  'piece_red',
  'piece_navy',
  'piece_yellow',
  'piece_green',
  'arc_red',
  'arc_navy',
  'arc_yellow',
  'arc_green',
];

void main() {
  testWidgets('builds the logo and says when it is done', (tester) async {
    var done = false;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          backgroundColor: const Color(0xFF25449A),
          body: Center(
            child: RepaintBoundary(
              key: const ValueKey('logo'),
              child: Container(
                color: const Color(0xFF25449A),
                padding: const EdgeInsets.all(60),
                child: AnimatedLogo(size: 150, onDone: () => done = true),
              ),
            ),
          ),
        ),
      ),
    );
    final context = tester.element(find.byType(AnimatedLogo));
    await tester.runAsync(() async {
      for (final p in _parts) {
        await precacheImage(
          AssetImage('assets/images/logo_parts/$p.png'),
          context,
        );
      }
    });

    // Every piece and arc of the logo is drawn
    expect(find.byType(Image), findsNWidgets(_parts.length));

    var elapsed = 0;
    for (final ms in [150, 350, 550, 800, 1100, 1750]) {
      await tester.pump(Duration(milliseconds: ms - elapsed));
      elapsed = ms;
      if (_shotDir.isNotEmpty) {
        final boundary = tester.renderObject<RenderRepaintBoundary>(
          find.byKey(const ValueKey('logo')),
        );
        await tester.runAsync(() async {
          final image = await boundary.toImage(pixelRatio: 1);
          final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
          File(
            '$_shotDir/logo_$ms.png',
          ).writeAsBytesSync(bytes!.buffer.asUint8List());
        });
      }
    }
    await tester.pump(const Duration(milliseconds: 200));
    expect(done, isTrue);
  });

  testWidgets('wordmark shows every letter', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: BouncyWordmark(
          style: TextStyle(fontSize: 30),
          highlight: Colors.amber,
        ),
      ),
    );
    await tester.pump(const Duration(seconds: 2));
    for (final letter in 'SpazaSure'.split('')) {
      expect(find.text(letter), findsWidgets);
    }
  });
}
