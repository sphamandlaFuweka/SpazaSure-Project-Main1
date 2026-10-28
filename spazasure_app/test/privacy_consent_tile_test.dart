import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/core/widgets/privacy_widgets.dart';

void main() {
  testWidgets('the privacy consent checkbox can be ticked and unticked', (
    tester,
  ) async {
    var accepted = false;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: StatefulBuilder(
            builder: (context, setState) => PrivacyConsentTile(
              value: accepted,
              includesIdentityCheck: true,
              onChanged: (value) => setState(() => accepted = value),
            ),
          ),
        ),
      ),
    );

    expect(
      tester.widget<Checkbox>(find.byType(Checkbox)).onChanged,
      isNotNull,
    ); // enabled
    await tester.tap(find.byType(Checkbox));
    await tester.pump();
    expect(accepted, isTrue);

    await tester.tap(find.textContaining('I have read the privacy notice'));
    await tester.pump();
    expect(accepted, isFalse);
  });
}
