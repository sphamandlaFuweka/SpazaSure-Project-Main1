import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:spazasure_app/features/auth/screens/login_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';

void main() {
  setUpAll(() => GoogleFonts.config.allowRuntimeFetching = false);

  for (final h in [760.0, 900.0]) {
    testWidgets('fit at 360x$h', (tester) async {
      final onError = FlutterError.onError;
      FlutterError.onError = (_) {};
      addTearDown(() => FlutterError.onError = onError);
      SharedPreferences.setMockInitialValues({});
      tester.view.physicalSize = Size(360, h);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        ChangeNotifierProvider(
          create: (_) => AuthProvider(),
          child: const MaterialApp(home: LoginScreen()),
        ),
      );
      for (var i = 0; i < 30; i++) {
        await tester.pump(const Duration(milliseconds: 100));
      }
      final pos = tester
          .state<ScrollableState>(find.byType(Scrollable).first)
          .position;
      // ignore: avoid_print
      print('HEIGHT $h -> content ${pos.viewportDimension + pos.maxScrollExtent}, overflow ${pos.maxScrollExtent}');
    });
  }
}
