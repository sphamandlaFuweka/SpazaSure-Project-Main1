import 'package:flutter/material.dart';

/// Use instead of Navigator.pushNamed / pushReplacementNamed to get logo transition.
/// Example: LogoPageRoute.push(context, '/home', const MainShell());
class LogoPageRoute {
  static void push(BuildContext context, Widget page) {
    Navigator.of(context).push(logoRoute(page));
  }

  static void pushReplacement(BuildContext context, Widget page) {
    Navigator.of(context).pushReplacement(logoRoute(page));
  }

  static PageRouteBuilder logoRoute(Widget page, {RouteSettings? settings}) {
    return PageRouteBuilder(
      settings: settings,
      transitionDuration: const Duration(milliseconds: 600),
      reverseTransitionDuration: const Duration(milliseconds: 300),
      pageBuilder: (_, __, ___) => page,
      transitionsBuilder: (context, animation, secondaryAnimation, child) {
        // Overlay fades in then out; child fades in after overlay fades out
        return Stack(
          children: [
            FadeTransition(
              opacity: Tween<double>(begin: 0, end: 1).animate(
                CurvedAnimation(
                  parent: animation,
                  curve: const Interval(0.5, 1.0, curve: Curves.easeIn),
                ),
              ),
              child: child,
            ),
            _LogoOverlay(animation: animation),
          ],
        );
      },
    );
  }
}

class _LogoOverlay extends StatelessWidget {
  final Animation<double> animation;
  const _LogoOverlay({required this.animation});

  @override
  Widget build(BuildContext context) {
    // Overlay: fade in 0→0.4, hold, fade out 0.6→1.0
    final overlayOpacity = TweenSequence<double>([
      TweenSequenceItem(tween: Tween(begin: 0.0, end: 1.0), weight: 30),
      TweenSequenceItem(tween: ConstantTween(1.0), weight: 30),
      TweenSequenceItem(tween: Tween(begin: 1.0, end: 0.0), weight: 40),
    ]).animate(animation);

    final logoScale = TweenSequence<double>([
      TweenSequenceItem(
        tween: Tween(begin: 0.4, end: 1.0).chain(CurveTween(curve: Curves.elasticOut)),
        weight: 50,
      ),
      TweenSequenceItem(tween: ConstantTween(1.0), weight: 30),
      TweenSequenceItem(
        tween: Tween(begin: 1.0, end: 0.8).chain(CurveTween(curve: Curves.easeIn)),
        weight: 20,
      ),
    ]).animate(animation);

    return AnimatedBuilder(
      animation: animation,
      builder: (_, __) {
        if (overlayOpacity.value == 0) return const SizedBox.shrink();
        return Opacity(
          opacity: overlayOpacity.value,
          child: Container(
            color: const Color(0xF00D3B0F),
            child: Center(
              child: Transform.scale(
                scale: logoScale.value,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 100,
                      height: 100,
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(24),
                        boxShadow: [
                          BoxShadow(
                            color: const Color(0xFF4CAF50).withValues(alpha: 0.5),
                            blurRadius: 40,
                            spreadRadius: 4,
                          ),
                        ],
                      ),
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(24),
                        child: Image.asset(
                          'assets/images/spazasure_logo.jpg',
                          fit: BoxFit.cover,
                        ),
                      ),
                    ),
                    const SizedBox(height: 16),
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: List.generate(3, (i) => _Dot(index: i, animation: animation)),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}

class _Dot extends StatelessWidget {
  final int index;
  final Animation<double> animation;
  const _Dot({required this.index, required this.animation});

  @override
  Widget build(BuildContext context) {
    final offset = TweenSequence<double>([
      TweenSequenceItem(tween: ConstantTween(0.0), weight: 20.0 + index * 10),
      TweenSequenceItem(
        tween: Tween(begin: 0.0, end: -6.0).chain(CurveTween(curve: Curves.easeInOut)),
        weight: 15,
      ),
      TweenSequenceItem(
        tween: Tween(begin: -6.0, end: 0.0).chain(CurveTween(curve: Curves.easeInOut)),
        weight: 15,
      ),
      TweenSequenceItem(tween: ConstantTween(0.0), weight: 50.0 - index * 10),
    ]).animate(animation);

    return AnimatedBuilder(
      animation: animation,
      builder: (_, __) => Transform.translate(
        offset: Offset(0, offset.value),
        child: Container(
          width: 6,
          height: 6,
          margin: const EdgeInsets.symmetric(horizontal: 3),
          decoration: const BoxDecoration(
            color: Color(0xFF4CAF50),
            shape: BoxShape.circle,
          ),
        ),
      ),
    );
  }
}
