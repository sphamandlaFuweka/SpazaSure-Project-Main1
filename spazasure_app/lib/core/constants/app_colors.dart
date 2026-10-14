import 'package:flutter/material.dart';

/// SpazaSure brand palette — the four logo colours, matching spazasure.co.za.
class AppColors {
  // Brand blue (primary)
  static const primary = Color(0xFF25449A);
  static const primaryLight = Color(0xFF3F5DB0);
  static const primaryDark = Color(0xFF1A3278);
  static const primaryTint = Color(0xFFE8ECF8); // pale blue fills & borders

  // Brand green (growth / success)
  static const secondary = Color(0xFF078C50);
  static const secondaryLight = Color(0xFF4DD898);
  static const secondaryTint = Color(0xFFE6F5EE);

  // Brand yellow (energy / primary call to action)
  static const accent = Color(0xFFF8B217);
  static const accentDark = Color(0xFFE8A510);
  static const accentTint = Color(0xFFFEF5E0);

  // Brand red
  static const red = Color(0xFFE02526);
  static const redTint = Color(0xFFFCEAEA);

  // Status
  static const success = Color(0xFF078C50);
  static const warning = Color(0xFFF8B217);
  static const error = Color(0xFFE02526);
  static const info = Color(0xFF25449A);

  // Neutrals ("ink" text scale from the website)
  static const background = Color(0xFFF5F7FF);
  static const surface = Color(0xFFFFFFFF);
  static const textPrimary = Color(0xFF17244F);
  static const ink = Color(0xFF3A3A5E);
  static const ink2 = Color(0xFF5A5A7A);
  static const ink3 = Color(0xFF8888AA);
  static const textSecondary = Color(0xFF5A5A7A);
  static const textHint = Color(0xFF8888AA);
  static const divider = Color(0xFFE2E7F5);
  static const disabled = Color(0xFF9E9E9E);

  // Order Status Colors
  static const statusPending = Color(0xFFF8B217);
  static const statusConfirmed = Color(0xFF25449A);
  static const statusProcessing = Color(0xFF078C50);
  static const statusDispatched = Color(0xFF3F5DB0);
  static const statusDelivered = Color(0xFF078C50);
  static const statusCancelled = Color(0xFFE02526);

  /// Signature hero gradient (deep brand blue), used for headers and banners.
  static const heroGradient = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [primary, primaryDark],
  );
}
