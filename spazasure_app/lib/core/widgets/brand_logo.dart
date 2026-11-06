import 'dart:math';
import 'package:flutter/material.dart';

/// The SpazaSure logo as designed, on a white disc so the navy piece shows on blue.
/// It never moves.
class BrandLogo extends StatelessWidget {
  final double size;

  const BrandLogo({super.key, this.size = 140});

  @override
  Widget build(BuildContext context) => Container(
    width: size,
    height: size,
    padding: EdgeInsets.all(size * 0.09),
    decoration: const BoxDecoration(
      color: Colors.white,
      shape: BoxShape.circle,
    ),
    child: Image.asset('assets/images/spazasure_logo.png', fit: BoxFit.contain),
  );
}

/// "SPAZASURE" with each letter popping up in turn, like a logo reveal.
class BouncyWordmark extends StatelessWidget {
  final String text;
  final TextStyle style;

  /// Letters from this index on take [highlight] instead of the style's colour.
  final int highlightFrom;
  final Color? highlight;
  final Duration delay;

  const BouncyWordmark({
    super.key,
    this.text = 'SPAZASURE',
    required this.style,
    this.highlightFrom = 5,
    this.highlight,
    this.delay = Duration.zero,
  });

  @override
  Widget build(BuildContext context) {
    final still = MediaQuery.maybeOf(context)?.disableAnimations ?? false;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (var i = 0; i < text.length; i++)
          _Letter(
            letter: text[i],
            style: i >= highlightFrom && highlight != null
                ? style.copyWith(color: highlight)
                : style,
            delay: delay + Duration(milliseconds: 55 * i),
            still: still,
          ),
      ],
    );
  }
}

class _Letter extends StatefulWidget {
  final String letter;
  final TextStyle style;
  final Duration delay;
  final bool still;

  const _Letter({
    required this.letter,
    required this.style,
    required this.delay,
    required this.still,
  });

  @override
  State<_Letter> createState() => _LetterState();
}

class _LetterState extends State<_Letter> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 700),
    value: widget.still ? 1 : 0,
  );

  @override
  void initState() {
    super.initState();
    if (!widget.still) {
      Future.delayed(widget.delay, () {
        if (mounted) _c.forward();
      });
    }
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: _c,
    builder: (_, child) {
      final rise = Curves.elasticOut.transform(_c.value);
      // Lands a bit squashed, then springs back to shape
      final squash = 1 + sin(_c.value * pi) * 0.18;
      return Opacity(
        opacity: (_c.value * 4).clamp(0.0, 1.0),
        child: Transform.translate(
          offset: Offset(0, (1 - rise) * 40),
          child: Transform.scale(
            scaleX: squash,
            scaleY: 2 - squash,
            alignment: Alignment.bottomCenter,
            child: child,
          ),
        ),
      );
    },
    child: Text(widget.letter, style: widget.style),
  );
}
