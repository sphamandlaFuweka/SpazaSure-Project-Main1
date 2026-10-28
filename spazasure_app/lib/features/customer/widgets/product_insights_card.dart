import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';

String? _text(dynamic v) {
  final s = v?.toString().trim();
  return (s == null || s.isEmpty || s == 'null') ? null : s;
}

List<Map<String, dynamic>> _maps(dynamic v) =>
    (v as List?)
        ?.whereType<Map>()
        .map((m) => Map<String, dynamic>.from(m))
        .toList() ??
    const [];

List<String> _strings(dynamic v) =>
    (v as List?)?.map((e) => '$e').toList() ?? const [];

Color _levelColor(String? level) => switch (level) {
  'high' => AppColors.error,
  'moderate' => AppColors.warning,
  'low' => AppColors.success,
  _ => AppColors.textHint,
};

Widget _pill(String label, Color color) => Container(
  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
  decoration: BoxDecoration(
    color: color.withValues(alpha: 0.12),
    borderRadius: BorderRadius.circular(20),
  ),
  child: Text(
    label,
    style: AppTextStyles.caption.copyWith(
      color: color,
      fontWeight: FontWeight.w700,
    ),
  ),
);

// A Material surface (not a coloured box), so the additives tile's tap ripple shows
Widget _cardShell(Widget child) => Material(
  color: AppColors.surface,
  borderRadius: BorderRadius.circular(16),
  clipBehavior: Clip.antiAlias,
  child: Padding(
    padding: const EdgeInsets.all(14),
    child: SizedBox(width: double.infinity, child: child),
  ),
);

Widget _title(IconData icon, String text) => Row(
  children: [
    Icon(icon, size: 20, color: AppColors.primary),
    const SizedBox(width: 8),
    Expanded(
      child: Text(
        text,
        style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
      ),
    ),
  ],
);

/// Plain-language explanation of a product SpazaSure doesn't carry, built by the
/// server from the open product databases (the verify response's `insights`).
class ProductInsightsCard extends StatelessWidget {
  final Map<String, dynamic> insights;

  const ProductInsightsCard({super.key, required this.insights});

  @override
  Widget build(BuildContext context) {
    final i = insights;
    final highlights = _maps(i['highlights']);
    final nutrition = _maps(i['nutrition']);
    final additives = _maps(i['additives']);
    final diet = _strings(i['diet']);
    final nutriScore = _text(i['nutriScore']);
    final nova = (i['novaGroup'] as num?)?.toInt();
    final source = _text(i['dataSource']);

    return _cardShell(
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _title(Icons.info_outline_rounded, 'About this product'),
          if (_text(i['summary']) != null) ...[
            const SizedBox(height: 8),
            Text(_text(i['summary'])!, style: AppTextStyles.bodySmall),
          ],
          if (highlights.isNotEmpty) ...[
            const SizedBox(height: 10),
            for (final h in highlights) _highlight(h),
          ],
          if (nutriScore != null || nova != null) ...[
            const SizedBox(height: 6),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                if (nutriScore != null)
                  _scoreChip(
                    'Nutri-Score $nutriScore',
                    _text(i['nutriScoreLabel']),
                    _nutriColor(nutriScore),
                  ),
                if (nova != null)
                  _scoreChip(
                    'NOVA $nova',
                    _text(i['novaLabel']),
                    nova >= 4
                        ? AppColors.error
                        : nova == 3
                        ? AppColors.warning
                        : AppColors.success,
                  ),
              ],
            ),
          ],
          if (nutrition.isNotEmpty) ...[
            const SizedBox(height: 14),
            Text(
              'Nutrition per ${_text(i['nutritionBasis']) ?? '100 g'}',
              style: AppTextStyles.bodySmall.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 6),
            for (final n in nutrition) _nutrientRow(n),
          ],
          if (diet.isNotEmpty) ...[
            const SizedBox(height: 10),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final d in diet)
                  _pill(
                    d,
                    d.startsWith('Contains') || d.startsWith('Not')
                        ? AppColors.textSecondary
                        : AppColors.success,
                  ),
              ],
            ),
          ],
          if (additives.isNotEmpty)
            ExpansionTile(
              tilePadding: EdgeInsets.zero,
              childrenPadding: const EdgeInsets.only(bottom: 4),
              expandedCrossAxisAlignment: CrossAxisAlignment.start,
              title: Text(
                'Additives (${additives.length})',
                style: AppTextStyles.bodySmall.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
              children: [for (final a in additives) _additiveRow(a)],
            ),
          if (source != null) ...[
            const SizedBox(height: 6),
            Text(
              'Source: $source, a free community database. Details may be incomplete; '
              'the label on the pack is always the final word.',
              style: AppTextStyles.caption.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
          ],
        ],
      ),
    );
  }

  static Color _nutriColor(String grade) => switch (grade) {
    'A' || 'B' => AppColors.success,
    'C' => AppColors.warning,
    _ => AppColors.error,
  };

  Widget _highlight(Map<String, dynamic> h) {
    final (icon, color) = switch (h['tone']) {
      'warning' => (Icons.warning_amber_rounded, AppColors.warning),
      'good' => (Icons.check_circle_outline, AppColors.success),
      _ => (Icons.info_outline, AppColors.textHint),
    };
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: color),
          const SizedBox(width: 8),
          Expanded(
            child: Text(_text(h['text']) ?? '', style: AppTextStyles.bodySmall),
          ),
        ],
      ),
    );
  }

  Widget _scoreChip(String score, String? label, Color color) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.1),
      borderRadius: BorderRadius.circular(10),
      border: Border.all(color: color.withValues(alpha: 0.35)),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          score,
          style: AppTextStyles.caption.copyWith(
            color: color,
            fontWeight: FontWeight.w800,
          ),
        ),
        if (label != null)
          Text(
            label,
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textSecondary,
              fontSize: 11,
            ),
          ),
      ],
    ),
  );

  Widget _nutrientRow(Map<String, dynamic> n) {
    final level = _text(n['level']);
    final value = (n['value'] as num?)?.toDouble();
    final valueText = value == null
        ? '-'
        : '${value % 1 == 0 ? value.toStringAsFixed(0) : value.toString()} ${_text(n['unit']) ?? ''}';
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Expanded(
            child: Text(
              _text(n['label']) ?? '',
              style: AppTextStyles.caption.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
          ),
          Text(
            valueText,
            style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w700),
          ),
          SizedBox(
            width: 84,
            child: Align(
              alignment: Alignment.centerRight,
              child: level == null
                  ? const SizedBox.shrink()
                  : _pill(
                      '${level[0].toUpperCase()}${level.substring(1)}',
                      _levelColor(level),
                    ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _additiveRow(Map<String, dynamic> a) {
    final name = _text(a['name']);
    final purpose = _text(a['purpose']);
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 64,
            child: Text(
              _text(a['code']) ?? '',
              style: AppTextStyles.caption.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          Expanded(
            child: Text(
              [
                name ?? 'Approved food additive',
                if (purpose != null) purpose,
              ].join(' · '),
              style: AppTextStyles.caption,
            ),
          ),
        ],
      ),
    );
  }
}

/// What the barcode number itself says (format, check digit, GS1 country),
/// from the verify response's `barcodeInfo`.
class BarcodeInfoCard extends StatelessWidget {
  final Map<String, dynamic> info;

  const BarcodeInfoCard({super.key, required this.info});

  @override
  Widget build(BuildContext context) {
    final checkOk = info['checkDigitValid'] as bool?;
    final country = _text(info['registeredIn']);
    return _cardShell(
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _title(Icons.qr_code_2_rounded, 'About the barcode'),
          const SizedBox(height: 8),
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: [
              if (_text(info['format']) != null)
                _pill(_text(info['format'])!, AppColors.primary),
              if (country != null)
                _pill('Registered in $country', AppColors.primary),
              if (checkOk != null)
                _pill(
                  checkOk ? 'Check digit correct' : 'Check digit wrong',
                  checkOk ? AppColors.success : AppColors.error,
                ),
            ],
          ),
          if (_text(info['explanation']) != null) ...[
            const SizedBox(height: 8),
            Text(
              _text(info['explanation'])!,
              style: AppTextStyles.caption.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
