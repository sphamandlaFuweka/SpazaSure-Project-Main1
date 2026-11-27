import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/core/widgets/play_store_update_prompt.dart';

void main() {
  const updateChannel = MethodChannel('de.ffuf.in_app_update/methods');

  testWidgets('shows an optional prompt when Play reports an Android update', (
    tester,
  ) async {
    addTearDown(() {
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(updateChannel, null);
    });

    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(updateChannel, (call) async {
          expect(call.method, 'checkForUpdate');
          return {
            'updateAvailability': 2,
            'immediateAllowed': false,
            'immediateAllowedPreconditions': null,
            'flexibleAllowed': true,
            'flexibleAllowedPreconditions': null,
            'availableVersionCode': 27,
            'installStatus': 0,
            'packageName': 'com.spazasure.spazasure_app',
            'clientVersionStalenessDays': 1,
            'updatePriority': 0,
          };
        });

    final navigatorKey = GlobalKey<NavigatorState>();
    await tester.pumpWidget(
      MaterialApp(
        navigatorKey: navigatorKey,
        builder: (context, child) => PlayStoreUpdatePrompt(
          navigatorKey: navigatorKey,
          child: child ?? const SizedBox.shrink(),
        ),
        home: const Scaffold(body: Text('Home')),
      ),
    );
    await tester.pump(const Duration(seconds: 4));
    await tester.pumpAndSettle();

    expect(find.text('Update available'), findsOneWidget);
    expect(find.text('Not now'), findsOneWidget);
    expect(find.text('Update'), findsOneWidget);
    await tester.tap(find.text('Not now'));
    await tester.pumpAndSettle();
    expect(find.text('Update available'), findsNothing);
  }, variant: TargetPlatformVariant({TargetPlatform.android}));
}
