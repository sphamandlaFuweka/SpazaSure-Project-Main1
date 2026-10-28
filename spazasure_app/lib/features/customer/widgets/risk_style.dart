import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';

/// One look for every risk level, so a result reads like a traffic light:
/// green is safe, amber needs a check, orange is suspicious, red is high risk.
class RiskStyle {
  final Color color;
  final IconData icon;
  final String label;

  /// What the shop owner or customer should do, in one sentence.
  final String action;

  /// 1 to 4 on the traffic-light bar; 0 when the risk is unknown.
  final int step;

  const RiskStyle(this.color, this.icon, this.label, this.action, this.step);

  static const amber = Color(0xFFC98A00);
  static const orange = Color(0xFFE8650E);

  static const steps = [AppColors.success, amber, orange, AppColors.error];

  /// Accepts the barcode check's levels (low, review, suspicious, high) and the
  /// packaging check's (low, medium, high, unknown).
  static RiskStyle of(String? level) => switch (level) {
    'low' => const RiskStyle(
      AppColors.success,
      Icons.verified_rounded,
      'Low risk',
      'No warning signs. It is fine to buy, sell or use.',
      1,
    ),
    'review' => const RiskStyle(
      amber,
      Icons.help_rounded,
      'Check first',
      'Some details could not be confirmed. Check the expiry date and the pack before you sell or use it.',
      2,
    ),
    'suspicious' || 'medium' => const RiskStyle(
      orange,
      Icons.report_problem_rounded,
      'Suspicious',
      'Do not sell or use it until you have checked it. Compare it with a pack you trust, or report it.',
      3,
    ),
    'high' => const RiskStyle(
      AppColors.error,
      Icons.dangerous_rounded,
      'High risk',
      'Do not buy, sell or use this product. Report it to SpazaSure.',
      4,
    ),
    _ => const RiskStyle(
      AppColors.textSecondary,
      Icons.help_outline_rounded,
      'Not sure',
      'Not enough could be checked. Try another photo or scan the barcode.',
      0,
    ),
  };

  bool get isDanger => step >= 3;
}

/// The four-step traffic-light bar under a verdict, with the current step lit.
class RiskMeter extends StatelessWidget {
  final RiskStyle style;

  const RiskMeter({super.key, required this.style});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            for (var i = 0; i < RiskStyle.steps.length; i++) ...[
              if (i > 0) const SizedBox(width: 4),
              Expanded(
                child: Container(
                  height: 8,
                  decoration: BoxDecoration(
                    color: i + 1 == style.step
                        ? RiskStyle.steps[i]
                        : RiskStyle.steps[i].withValues(alpha: 0.18),
                    borderRadius: BorderRadius.circular(4),
                  ),
                ),
              ),
            ],
          ],
        ),
        const SizedBox(height: 4),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text('Safe', style: TextStyle(fontSize: 11, color: AppColors.success, fontWeight: FontWeight.w600)),
            Text('High risk', style: TextStyle(fontSize: 11, color: AppColors.error, fontWeight: FontWeight.w600)),
          ],
        ),
      ],
    );
  }
}
