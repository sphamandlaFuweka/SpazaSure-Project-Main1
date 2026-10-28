import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';

/// Shared motion for the app. Everything here stays still when the phone's
/// "remove animations" accessibility setting is on.
bool _reduceMotion(BuildContext context) =>
    MediaQuery.maybeOf(context)?.disableAnimations ?? false;

/// Fades and slides a list or grid item in when it first appears. Items further
/// down start a little later, so a screen of them ripples in.
class Entrance extends StatelessWidget {
  final int index;
  final Widget child;

  const Entrance({super.key, required this.index, required this.child});

  @override
  Widget build(BuildContext context) {
    if (_reduceMotion(context)) return child;
    // Items scrolled into view later only wait for their place in the row
    final delay = Duration(milliseconds: (index % 9) * 45);
    return child
        .animate()
        .fadeIn(duration: 320.ms, delay: delay, curve: Curves.easeOut)
        .slideY(
          begin: 0.12,
          end: 0,
          duration: 380.ms,
          delay: delay,
          curve: Curves.easeOutCubic,
        );
  }
}

/// Shrinks slightly while pressed, like a physical button.
class Pressable extends StatefulWidget {
  final Widget child;
  final VoidCallback? onTap;
  final double scale;

  const Pressable({
    super.key,
    required this.child,
    this.onTap,
    this.scale = 0.96,
  });

  @override
  State<Pressable> createState() => _PressableState();
}

class _PressableState extends State<Pressable> {
  bool _down = false;

  void _set(bool down) {
    if (_down != down) setState(() => _down = down);
  }

  @override
  Widget build(BuildContext context) => GestureDetector(
    onTap: widget.onTap,
    onTapDown: widget.onTap == null ? null : (_) => _set(true),
    onTapUp: (_) => _set(false),
    onTapCancel: () => _set(false),
    child: AnimatedScale(
      scale: _down && !_reduceMotion(context) ? widget.scale : 1,
      duration: const Duration(milliseconds: 120),
      curve: Curves.easeOut,
      child: widget.child,
    ),
  );
}

/// Fades the shell's body in when the bottom tab changes. The child (an
/// IndexedStack) is kept, so every tab keeps its scroll position and data.
class TabFade extends StatefulWidget {
  final int index;
  final Widget child;

  const TabFade({super.key, required this.index, required this.child});

  @override
  State<TabFade> createState() => _TabFadeState();
}

class _TabFadeState extends State<TabFade> with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 260),
    value: 1,
  );
  late final Animation<double> _curve = CurvedAnimation(
    parent: _controller,
    curve: Curves.easeOutCubic,
  );

  @override
  void didUpdateWidget(TabFade oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.index != widget.index && !_reduceMotion(context)) {
      _controller.forward(from: 0);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => FadeTransition(
    opacity: _curve,
    child: SlideTransition(
      position: Tween(
        begin: const Offset(0, 0.015),
        end: Offset.zero,
      ).animate(_curve),
      child: widget.child,
    ),
  );
}
