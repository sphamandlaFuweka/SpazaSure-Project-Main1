import 'package:flutter/material.dart';

/// The app's one background shape: a faint circle in the bottom right corner, kept
/// quiet so it never competes with the text on top of it.
class BrandPattern extends CustomPainter {
  const BrandPattern();

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    canvas.drawCircle(
      Offset(w * 1.05, h * 1.02),
      w * 0.62,
      Paint()..color = Colors.white.withValues(alpha: 0.05),
    );
  }

  @override
  bool shouldRepaint(BrandPattern old) => false;
}
