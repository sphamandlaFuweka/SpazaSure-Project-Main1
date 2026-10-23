import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/api_service.dart';

/// A white rounded card with an optional title row, used across Shop Academy.
class AcademyCard extends StatelessWidget {
  final String? title;
  final IconData? icon;
  final Widget? trailing;
  final Widget child;
  final EdgeInsetsGeometry margin;

  const AcademyCard({
    super.key,
    this.title,
    this.icon,
    this.trailing,
    required this.child,
    this.margin = const EdgeInsets.fromLTRB(16, 0, 16, 12),
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: margin,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.divider),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (title != null) ...[
            Row(
              children: [
                if (icon != null) ...[
                  Icon(icon, size: 18, color: AppColors.primary),
                  const SizedBox(width: 8),
                ],
                Expanded(child: Text(title!, style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w700))),
                ?trailing,
              ],
            ),
            const SizedBox(height: 12),
          ],
          child,
        ],
      ),
    );
  }
}

/// A small figure with a label underneath, e.g. "R 1 200.00 / Money in".
class AcademyStat extends StatelessWidget {
  final String value;
  final String label;
  final Color color;

  const AcademyStat(this.value, this.label, {super.key, this.color = AppColors.textPrimary});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FittedBox(
          fit: BoxFit.scaleDown,
          child: Text(value, style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w800, color: color)),
        ),
        const SizedBox(height: 2),
        Text(label, style: AppTextStyles.caption.copyWith(color: AppColors.textHint)),
      ],
    );
  }
}

/// Shows an error from the API (or a fallback) as a red snack bar.
void showAcademyError(BuildContext context, Object error, [String fallback = 'Something went wrong. Try again.']) {
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(
    content: Text(error is ApiException ? error.message : fallback),
    backgroundColor: AppColors.error,
  ));
}

void showAcademyMessage(BuildContext context, String message) {
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(
    content: Text(message),
    backgroundColor: AppColors.success,
  ));
}

/// Shown when a tab fails to load, with a retry button.
class AcademyLoadError extends StatelessWidget {
  final VoidCallback onRetry;

  const AcademyLoadError({super.key, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_rounded, size: 48, color: AppColors.textHint),
            const SizedBox(height: 12),
            Text('Could not load this. Check your connection.', style: AppTextStyles.body, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: onRetry, child: const Text('Try again')),
          ],
        ),
      ),
    );
  }
}

/// Decimal number field input decoration shared by the Shop Academy forms.
InputDecoration academyInput(String label, {String? hint, String? prefix}) => InputDecoration(
  labelText: label,
  hintText: hint,
  prefixText: prefix,
  filled: true,
  fillColor: AppColors.background,
  border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
  contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
);

/// Parses a typed amount like "1 200,50" or "1200.50".
double? parseAmount(String text) => double.tryParse(text.replaceAll(' ', '').replaceAll(',', '.'));
