import 'package:flutter/material.dart';

/// Bold, flat brand shapes: a big quarter circle, a leaf, rings and a few dots.
/// Used on the welcome cards and the dashboard's cards.
class BrandPattern extends CustomPainter {
  /// Quarter circle, leaf, then rings and dots.
  final List<Color> colors;
  const BrandPattern(this.colors);

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    final fill = Paint()..style = PaintingStyle.fill;

    // Big quarter circle in the bottom right
    fill.color = colors[0];
    canvas.drawCircle(Offset(w * 1.05, h * 1.02), w * 0.62, fill);

    // Leaf shape across the top left
    fill.color = colors[1];
    final leaf = Path()
      ..moveTo(-w * 0.1, h * 0.38)
      ..quadraticBezierTo(w * 0.1, -h * 0.05, w * 0.62, -h * 0.02)
      ..quadraticBezierTo(w * 0.32, h * 0.3, -w * 0.1, h * 0.38)
      ..close();
    canvas.drawPath(leaf, fill);

    // Rings with a dot, like a button
    final ring = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3
      ..color = colors[2].withValues(alpha: 0.9);
    canvas.drawCircle(Offset(w * 0.82, h * 0.16), w * 0.08, ring);
    fill.color = colors[2];
    canvas.drawCircle(Offset(w * 0.82, h * 0.16), w * 0.03, fill);
    canvas.drawCircle(Offset(w * 0.16, h * 0.7), w * 0.12, ring);

    // Scattered small dots
    fill.color = colors[2].withValues(alpha: 0.55);
    for (final p in const [
      Offset(0.62, 0.3),
      Offset(0.3, 0.86),
      Offset(0.9, 0.55),
    ]) {
      canvas.drawCircle(Offset(w * p.dx, h * p.dy), 4, fill);
    }
  }

  @override
  bool shouldRepaint(BrandPattern old) => old.colors != colors;
}
