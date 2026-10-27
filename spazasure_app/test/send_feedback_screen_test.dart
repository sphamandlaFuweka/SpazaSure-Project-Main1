import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/features/feedback/send_feedback_screen.dart';
import 'package:spazasure_app/services/feedback_service.dart';

class _FakeFeedback extends FeedbackService {
  _FakeFeedback() : super('/test/feedback');

  final sent = <Map<String, Object?>>[];

  @override
  Future<void> send({
    required String type,
    required String message,
    int? rating,
    String? screen,
    List<int>? screenshotBytes,
    String? screenshotName,
  }) async {
    sent.add({'type': type, 'message': message, 'rating': rating, 'screen': screen});
  }
}

Future<_FakeFeedback> _open(WidgetTester tester) async {
  final fake = _FakeFeedback();
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => SendFeedbackScreen(service: fake, fromScreen: 'Scan result: 6001067089827'),
              ),
            ),
            child: const Text('open'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
  return fake;
}

void main() {
  testWidgets('asks for a little more before sending', (tester) async {
    final fake = await _open(tester);

    await tester.ensureVisible(find.byIcon(Icons.send_rounded));
    await tester.tap(find.byIcon(Icons.send_rounded));
    await tester.pump();

    expect(find.text('Please tell us a little more.'), findsOneWidget);
    expect(fake.sent, isEmpty);
  });

  testWidgets('sends the type, message, rating and where it came from', (tester) async {
    final fake = await _open(tester);

    await tester.tap(find.text('An idea'));
    await tester.enterText(find.byType(TextField), 'Please add a Zulu version of the app');
    await tester.tap(find.byTooltip('4 of 5'));
    await tester.pump();
    await tester.ensureVisible(find.byIcon(Icons.send_rounded));
    await tester.tap(find.byIcon(Icons.send_rounded));
    await tester.pumpAndSettle();

    expect(fake.sent.single, {
      'type': 'idea',
      'message': 'Please add a Zulu version of the app',
      'rating': 4,
      'screen': 'Scan result: 6001067089827',
    });
    expect(find.byType(SendFeedbackScreen), findsNothing); // closed after sending
    expect(find.textContaining('Thank you!'), findsOneWidget);
  });
}
