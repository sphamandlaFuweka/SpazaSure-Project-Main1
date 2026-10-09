import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';

/// Full product verification report shown to customers after a lookup.
class CustomerVerificationResult extends StatelessWidget {
  final Map<String, dynamic> product;
  final String fallbackCode;
  final DateTime? enteredExpiry;
  final String? rewardMessage;
  final bool rewardDuplicate;
  final VoidCallback onReport;
  final VoidCallback? onCheckExpiry;

  const CustomerVerificationResult({
    super.key,
    required this.product,
    required this.fallbackCode,
    required this.onReport,
    this.enteredExpiry,
    this.rewardMessage,
    this.rewardDuplicate = false,
    this.onCheckExpiry,
  });

  static String? _text(dynamic v) {
    final s = v?.toString().trim();
    return (s == null || s.isEmpty || s == 'null') ? null : s;
  }

  static List<String> _list(dynamic v) =>
      (v as List?)?.map((e) => '$e').toList() ?? const [];

  @override
  Widget build(BuildContext context) {
    final p = product;
    final source = _text(p['source']) ?? 'not_registered';
    final notRecognised = source == 'not_registered';
    final recalled = p['isRecalled'] == true;
    final expiryText =
        _text(p['expiryDate']) ??
        enteredExpiry?.toIso8601String().substring(0, 10);

    final level =
        _text(p['riskLevel']) ?? _fallbackLevel(recalled, notRecognised);
    final score = (p['riskScore'] as num?)?.toInt();
    final color = _levelColor(level);
    final headline =
        _text(p['riskHeadline']) ??
        (recalled
            ? 'This product has an active recall'
            : notRecognised
            ? 'Product not recognised'
            : 'Product found in the SpazaSure registry');

    final indicators = _list(p['indicators']);
    final tips = _list(p['tips']);
    final checks =
        (p['checks'] as List?)
            ?.whereType<Map>()
            .map((c) => Map<String, dynamic>.from(c))
            .toList() ??
        <Map<String, dynamic>>[];
    final allergens = _list(p['allergens']);
    final matched = _list((p['allergyWarning'] as Map?)?['matchedAllergens']);
    final images = _list(p['images']);
    final name = _text(p['name']) ?? 'Unknown product';
    final description = _text(p['description']);
    final ingredients = _text(p['ingredients']);
    final suspicious = level == 'suspicious' || level == 'high' || recalled;
    final needsExpiry = onCheckExpiry != null && expiryText == null;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (rewardMessage != null) _rewardBanner(),
        _riskBanner(level, color, headline, score),
        if (matched.isNotEmpty) ...[
          const SizedBox(height: 12),
          _card(
            tint: AppColors.error,
            child: Row(
              children: [
                const Icon(Icons.no_food_rounded, color: AppColors.error),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    'Allergy warning: contains ${matched.join(', ')}',
                    style: AppTextStyles.body.copyWith(
                      fontWeight: FontWeight.w700,
                      color: AppColors.error,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
        const SizedBox(height: 12),
        _card(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (images.isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(right: 12),
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(12),
                        child: Image.network(
                          images.first,
                          width: 72,
                          height: 72,
                          fit: BoxFit.cover,
                          errorBuilder: (_, __, ___) => const SizedBox.shrink(),
                        ),
                      ),
                    ),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          name,
                          style: AppTextStyles.body.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        if (_text(p['brand']) != null)
                          Text(
                            _text(p['brand'])!,
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textSecondary,
                            ),
                          ),
                        if (description != null && description != name) ...[
                          const SizedBox(height: 4),
                          Text(
                            description,
                            maxLines: 4,
                            overflow: TextOverflow.ellipsis,
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textSecondary,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              _row(
                Icons.qr_code_2_rounded,
                'Barcode',
                _text(p['code']) ?? fallbackCode,
              ),
              _row(Icons.category_outlined, 'Category', _text(p['category'])),
              _row(
                Icons.scale_outlined,
                'Size',
                _text(p['quantity']) ?? _text(p['unit']),
              ),
              _row(Icons.public, 'Made in', _text(p['origin'])),
              _row(Icons.travel_explore, 'Sold in', _text(p['countriesSold'])),
              _row(Icons.eco_outlined, 'Labels', _text(p['labels'])),
              _row(
                Icons.favorite_border,
                'Nutri-Score',
                _text(p['nutriScore'])?.toUpperCase(),
              ),
              _row(
                Icons.inventory_2_outlined,
                'Batch',
                _text(p['batchNumber']),
              ),
              _row(Icons.event, 'Expiry', expiryText),
              _row(
                Icons.storefront_outlined,
                'Registry',
                _registryLabel(source),
              ),
            ],
          ),
        ),
        if (_text(p['supplierName']) != null) ...[
          const SizedBox(height: 12),
          _card(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        'Supplier',
                        style: AppTextStyles.body.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                    _pill(
                      p['supplierVerified'] == true ? 'Verified' : 'Unverified',
                      p['supplierVerified'] == true
                          ? AppColors.success
                          : AppColors.warning,
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                _row(Icons.business, 'Company', _text(p['supplierName'])),
                _row(
                  Icons.place_outlined,
                  'Location',
                  [
                    _text(p['supplierCity']),
                    _text(p['supplierProvince']),
                  ].whereType<String>().join(', '),
                ),
              ],
            ),
          ),
        ],
        if (allergens.isNotEmpty || ingredients != null) ...[
          const SizedBox(height: 12),
          _card(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Ingredients and allergens',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                if (allergens.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      for (final a in allergens)
                        _pill(
                          a,
                          matched.contains(a)
                              ? AppColors.error
                              : AppColors.warning,
                        ),
                    ],
                  ),
                ],
                if (ingredients != null) ...[
                  const SizedBox(height: 8),
                  Text(ingredients, style: AppTextStyles.caption),
                ],
              ],
            ),
          ),
        ],
        if (indicators.isNotEmpty) ...[
          const SizedBox(height: 12),
          _card(
            tint: color,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Warning signs',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                    color: color,
                  ),
                ),
                const SizedBox(height: 8),
                for (final i in indicators)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(
                          Icons.warning_amber_rounded,
                          size: 18,
                          color: color,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(i, style: AppTextStyles.bodySmall),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
        ],
        if (checks.isNotEmpty || needsExpiry) ...[
          const SizedBox(height: 12),
          _card(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'What we checked',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 8),
                for (final c in checks) _checkRow(c),
                if (needsExpiry)
                  OutlinedButton.icon(
                    onPressed: onCheckExpiry,
                    icon: const Icon(Icons.event, size: 18),
                    label: const Text('Check expiry date'),
                  ),
              ],
            ),
          ),
        ],
        if (tips.isNotEmpty) ...[
          const SizedBox(height: 12),
          _card(
            child: ExpansionTile(
              tilePadding: EdgeInsets.zero,
              initiallyExpanded: suspicious,
              title: Text(
                'How to check this product yourself',
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
              ),
              childrenPadding: const EdgeInsets.only(bottom: 4),
              expandedCrossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (final t in tips)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Text('\u2022 $t', style: AppTextStyles.bodySmall),
                  ),
              ],
            ),
          ),
        ],
        const SizedBox(height: 16),
        suspicious || notRecognised
            ? FilledButton.icon(
                onPressed: onReport,
                style: FilledButton.styleFrom(
                  backgroundColor: suspicious ? AppColors.error : null,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                ),
                icon: const Icon(Icons.flag_rounded),
                label: Text(
                  suspicious
                      ? 'Report suspicious product'
                      : 'Report this product',
                ),
              )
            : OutlinedButton.icon(
                onPressed: onReport,
                icon: const Icon(Icons.flag_outlined),
                label: const Text('Report a problem with this product'),
              ),
        const SizedBox(height: 10),
        Text(
          _text(p['disclaimer']) ??
              'This check is a guide only. It does not guarantee authenticity or product safety.',
          textAlign: TextAlign.center,
          style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
        ),
      ],
    );
  }

  static String _fallbackLevel(bool recalled, bool notRecognised) =>
      recalled ? 'high' : (notRecognised ? 'review' : 'low');

  static Color _levelColor(String level) => switch (level) {
    'high' => AppColors.error,
    'suspicious' => AppColors.error,
    'review' => AppColors.warning,
    _ => AppColors.success,
  };

  static String? _registryLabel(String source) => switch (source) {
    'spazasure_qr' => 'SpazaSure QR (batch tracked)',
    'spazasure_barcode' => 'SpazaSure registry',
    'open_food_facts' => 'Global database only',
    _ => 'Not found',
  };

  Widget _riskBanner(String level, Color color, String headline, int? score) {
    final icon = switch (level) {
      'high' => Icons.dangerous_rounded,
      'suspicious' => Icons.report_problem_rounded,
      'review' => Icons.help_outline_rounded,
      _ => Icons.verified_rounded,
    };
    return _card(
      tint: color,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, color: color, size: 30),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  headline,
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w800,
                    color: color,
                  ),
                ),
              ),
            ],
          ),
          if (score != null) ...[
            const SizedBox(height: 12),
            ClipRRect(
              borderRadius: BorderRadius.circular(6),
              child: LinearProgressIndicator(
                value: score / 100,
                minHeight: 8,
                color: color,
                backgroundColor: color.withValues(alpha: 0.15),
              ),
            ),
            const SizedBox(height: 4),
            Text(
              'Risk score $score / 100 (lower is safer)',
              style: AppTextStyles.caption.copyWith(color: color),
            ),
          ],
        ],
      ),
    );
  }

  Widget _rewardBanner() {
    final c = rewardDuplicate ? AppColors.warning : AppColors.success;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: _card(
        tint: c,
        child: Row(
          children: [
            Icon(
              rewardDuplicate ? Icons.info_outline : Icons.stars_rounded,
              color: c,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                rewardMessage!,
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _card({required Widget child, Color? tint}) => Container(
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: tint == null ? AppColors.surface : tint.withValues(alpha: 0.08),
      borderRadius: BorderRadius.circular(16),
      border: tint == null
          ? null
          : Border.all(color: tint.withValues(alpha: 0.3)),
    ),
    child: child,
  );

  Widget _pill(String label, Color color) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
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

  Widget _row(IconData icon, String label, String? value) {
    if (value == null || value.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 15, color: AppColors.textHint),
          const SizedBox(width: 8),
          Text(
            '$label: ',
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: AppTextStyles.caption.copyWith(
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _checkRow(Map<String, dynamic> c) {
    final (icon, color) = switch (c['status']) {
      'pass' => (Icons.check_circle, AppColors.success),
      'fail' => (Icons.cancel, AppColors.error),
      'warn' => (Icons.error_outline, AppColors.warning),
      _ => (Icons.help_outline, AppColors.textHint),
    };
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: color),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${c['label']}',
                  style: AppTextStyles.bodySmall.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
                ),
                Text(
                  '${c['detail'] ?? ''}',
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
