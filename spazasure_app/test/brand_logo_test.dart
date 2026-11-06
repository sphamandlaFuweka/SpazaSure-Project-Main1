import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/core/widgets/brand_logo.dart';

void main() {
  testWidgets('the logo is the original artwork and never moves', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(home: Center(child: BrandLogo(size: 150))),
    );

    final image = tester.widget<Image>(find.byType(Image)).image;
    expect((image as AssetImage).assetName, 'assets/images/spazasure_logo.png');
    expect(tester.hasRunningAnimations, isFalse);
    expect(find.byType(Transform), findsNothing);
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
    for (final letter in 'SPAZASURE'.split('')) {
      expect(find.text(letter), findsWidgets);
    }
  });
}
