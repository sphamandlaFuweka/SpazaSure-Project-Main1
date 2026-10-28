import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/core/widgets/motion.dart';

class _Counter extends StatefulWidget {
  const _Counter();
  @override
  State<_Counter> createState() => _CounterState();
}

class _CounterState extends State<_Counter> {
  int taps = 0;
  @override
  Widget build(BuildContext context) => TextButton(
    onPressed: () => setState(() => taps++),
    child: Text('taps $taps'),
  );
}

void main() {
  testWidgets('switching tabs fades without resetting the tabs', (
    tester,
  ) async {
    var index = 0;
    late StateSetter setIndex;
    await tester.pumpWidget(
      MaterialApp(
        home: StatefulBuilder(
          builder: (context, setState) {
            setIndex = setState;
            return TabFade(
              index: index,
              child: IndexedStack(
                index: index,
                children: const [_Counter(), Text('second')],
              ),
            );
          },
        ),
      ),
    );

    await tester.tap(find.text('taps 0'));
    await tester.pump();
    setIndex(() => index = 1);
    await tester.pumpAndSettle();
    setIndex(() => index = 0);
    await tester.pumpAndSettle();

    expect(find.text('taps 1'), findsOneWidget);
  });

  testWidgets('list items appear at once when animations are off', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MediaQuery(
        data: MediaQueryData(disableAnimations: true),
        child: Directionality(
          textDirection: TextDirection.ltr,
          child: Entrance(index: 3, child: Text('item')),
        ),
      ),
    );
    // No fade wrapper: the item is fully shown on the first frame
    expect(find.byType(Opacity), findsNothing);
    expect(find.byType(FadeTransition), findsNothing);
    expect(find.text('item'), findsOneWidget);
  });
}
