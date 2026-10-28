import 'dart:math';
import 'package:flutter/material.dart';

/// The SpazaSure pinwheel, built up from its four coloured pieces: a white disc
/// pops up, each piece flies in from its own corner and spins into place, the
/// thin arcs swing round after them, and the whole logo gives a little squash
/// as it locks together.
///
/// The pieces are layers cut from the logo (assets/images/logo_parts), all the
/// same size as the full logo, so they line up exactly when they land.
class AnimatedLogo extends StatefulWidget {
  final double size;
  final Duration delay;

  /// Draws the white disc behind the logo, so the navy piece shows on dark backgrounds.
  final bool disc;
  final VoidCallback? onDone;

  const AnimatedLogo({
    super.key,
    this.size = 140,
    this.delay = Duration.zero,
    this.disc = true,
    this.onDone,
  });

  static const duration = Duration(milliseconds: 1700);

  @override
  State<AnimatedLogo> createState() => _AnimatedLogoState();
}

class _Piece {
  final String name;

  /// Where the piece flies in from, as a fraction of the logo's size.
  final Offset from;
  final double start;
  const _Piece(this.name, this.from, this.start);
}

class _AnimatedLogoState extends State<AnimatedLogo>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: AnimatedLogo.duration,
  );

  // Each colour comes from the corner it sits in
  static const _pieces = [
    _Piece('red', Offset(-1.4, -1.2), 0.06),
    _Piece('navy', Offset(1.4, -1.2), 0.12),
    _Piece('yellow', Offset(1.4, 1.2), 0.18),
    _Piece('green', Offset(-1.4, 1.2), 0.24),
  ];

  @override
  void initState() {
    super.initState();
    _c.addStatusListener((s) {
      if (s == AnimationStatus.completed) widget.onDone?.call();
    });
    Future.delayed(widget.delay, () {
      if (!mounted) return;
      if (MediaQuery.maybeOf(context)?.disableAnimations ?? false) {
        _c.value = 1;
        widget.onDone?.call();
      } else {
        _c.forward();
      }
    });
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  /// 0→1 over the part of the timeline from [start] to [end].
  double _span(double start, double end, [Curve curve = Curves.linear]) =>
      curve.transform(((_c.value - start) / (end - start)).clamp(0.0, 1.0));

  @override
  Widget build(BuildContext context) {
    final s = widget.size;
    return SizedBox(
      width: s,
      height: s,
      child: AnimatedBuilder(
        animation: _c,
        builder: (context, _) {
          // The whole logo turns into place, then squashes as it locks
          final turn = (1 - _span(0.05, 0.72, Curves.easeOutCubic)) * -pi * 0.6;
          final lock = _span(0.72, 0.9);
          final squash = 1 + sin(lock * pi) * 0.08;
          final disc = _span(0, 0.3, Curves.elasticOut);
          return Stack(
            clipBehavior: Clip.none,
            alignment: Alignment.center,
            children: [
              if (widget.disc)
                Transform.scale(
                  scale: disc,
                  child: Container(
                    width: s,
                    height: s,
                    decoration: BoxDecoration(
                      color: Colors.white,
                      shape: BoxShape.circle,
                      boxShadow: [
                        BoxShadow(
                          color: Colors.black.withValues(alpha: 0.18),
                          blurRadius: s * 0.18,
                          offset: Offset(0, s * 0.08),
                        ),
                      ],
                    ),
                  ),
                ),
              Transform.scale(
                scale: squash,
                child: Transform.rotate(
                  angle: turn,
                  child: SizedBox(
                    width: s * (widget.disc ? 0.82 : 1),
                    height: s * (widget.disc ? 0.82 : 1),
                    child: Stack(
                      fit: StackFit.expand,
                      children: [
                        for (final p in _pieces) _arc(p),
                        for (final p in _pieces) _piece(p, s),
                      ],
                    ),
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _piece(_Piece p, double s) {
    final t = _span(p.start, p.start + 0.42, Curves.easeOutBack);
    final shown = _span(p.start, p.start + 0.12);
    return Opacity(
      opacity: shown,
      child: Transform.translate(
        offset: Offset(p.from.dx * s, p.from.dy * s) * (1 - t),
        child: Transform.rotate(
          angle: (1 - t) * pi * 0.9,
          child: Transform.scale(
            scale: 0.4 + 0.6 * t,
            child: Image.asset(
              'assets/images/logo_parts/piece_${p.name}.png',
              fit: BoxFit.contain,
            ),
          ),
        ),
      ),
    );
  }

  /// The thin arcs swing round the logo after their pieces have landed.
  Widget _arc(_Piece p) {
    final t = _span(p.start + 0.3, p.start + 0.62, Curves.easeOutCubic);
    return Opacity(
      opacity: t,
      child: Transform.rotate(
        angle: (1 - t) * -pi * 0.7,
        child: Image.asset(
          'assets/images/logo_parts/arc_${p.name}.png',
          fit: BoxFit.contain,
        ),
      ),
    );
  }
}

/// "SpazaSure" with each letter popping up in turn, like a logo reveal.
class BouncyWordmark extends StatelessWidget {
  final String text;
  final TextStyle style;

  /// Letters from this index on take [highlight] instead of the style's colour.
  final int highlightFrom;
  final Color? highlight;
  final Duration delay;

  const BouncyWordmark({
    super.key,
    this.text = 'SpazaSure',
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
