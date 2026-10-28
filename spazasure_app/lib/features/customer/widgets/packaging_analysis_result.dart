import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';

/// Shows what was read off a packaging photo, by the AI analysis or (offline) on the phone.
class PackagingAnalysisResult extends StatelessWidget {
  final Map<String, dynamic> result;

  /// Checks the barcode read off the pack in SpazaSure; null when there is none.
  final VoidCallback? onVerifyBarcode;
  final VoidCallback? onReport;
  final VoidCallback? onRetake;

  const PackagingAnalysisResult({
    super.key,
    required this.result,
    this.onVerifyBarcode,
    this.onReport,
    this.onRetake,
  });

  static String? _s(dynamic v) {
    final s = v?.toString().trim();
    return s == null || s.isEmpty || s == 'null' ? null : s;
  }

  static List<String> _list(dynamic v) =>
      v is List ? v.map((e) => e.toString()).where((e) => e.trim().isNotEmpty).toList() : const [];

  @override
  Widget build(BuildContext context) {
    final a = Map<String, dynamic>.from(result['analysis'] as Map? ?? const {});
    final onDevice = result['source'] == 'packaging_device';
    final level = _s(result['riskLevel']) ?? 'unknown';
    final verdict = _s(a['verdict']) ?? 'cannot_tell';
    final color = switch (level) {
      'high' => AppColors.error,
      'medium' => AppColors.warning,
      'low' => AppColors.success,
      _ => AppColors.textSecondary,
    };
    final headline = switch (verdict) {
      'likely_counterfeit' => 'This pack looks counterfeit',
      'some_concerns' => 'Some things on this pack need checking',
      'looks_genuine' => level == 'low' ? 'This pack looks genuine' : 'The pack looks right, with a few gaps',
      _ => 'Not enough could be seen to judge',
    };
    final checks = (result['checks'] as List? ?? const [])
        .whereType<Map>()
        .map((c) => Map<String, dynamic>.from(c))
        .toList();
    final registry = result['registryProduct'] as Map?;
    final database = result['databaseProduct'] as Map?;
    final name = [_s(a['brand']), _s(a['productName'])].whereType<String>().join(' ');

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _card(
          tint: color,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(
                    switch (level) {
                      'high' => Icons.dangerous_rounded,
                      'medium' => Icons.report_problem_rounded,
                      'low' => Icons.verified_rounded,
                      _ => Icons.help_outline_rounded,
                    },
                    color: color,
                    size: 30,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      headline,
                      style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w800, color: color),
                    ),
                  ),
                ],
              ),
              if (_s(a['summary']) != null) ...[
                const SizedBox(height: 10),
                Text(_s(a['summary'])!, style: AppTextStyles.caption),
              ],
              const SizedBox(height: 8),
              Text(
                onDevice
                    ? 'Read on your phone. The full photo analysis is not available right now.'
                    : 'Photo analysed by SpazaSure AI. It can make mistakes, so check anything important yourself.',
                style: AppTextStyles.caption.copyWith(color: AppColors.textHint, fontSize: 11),
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        _card(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(name.isEmpty ? 'Product' : name, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w800)),
              if (_s(a['variant']) != null || _s(a['packSize']) != null)
                Text(
                  [_s(a['variant']), _s(a['packSize'])].whereType<String>().join(' · '),
                  style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
                ),
              const SizedBox(height: 10),
              _row(Icons.factory_outlined, 'Made by', _s(a['manufacturer'])),
              _row(Icons.place_outlined, 'Address', _s(a['manufacturerAddress'])),
              _row(Icons.public, 'Origin', _s(a['countryOfOrigin'])),
              _row(Icons.call_outlined, 'Contact', _list(a['contacts']).join(', ')),
              _row(Icons.qr_code_rounded, 'Barcode', _s(result['barcode']) ?? _s(a['barcodeDigits'])),
              _row(Icons.inventory_2_outlined, 'Batch', _s(a['batchNumber'])),
              _row(Icons.event, 'Expiry', _s(a['expiryDate'])),
              _row(Icons.precision_manufacturing_outlined, 'Made on', _s(a['manufactureDate'])),
              _row(Icons.translate, 'Languages', _list(a['languages']).join(', ')),
            ],
          ),
        ),
        if (checks.isNotEmpty) ...[
          const SizedBox(height: 12),
          _section('Checks', [for (final c in checks) _checkRow(c)]),
        ],
        if (registry != null || database != null) ...[
          const SizedBox(height: 12),
          _card(
            tint: AppColors.secondary,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  registry != null ? 'Found on SpazaSure' : 'Found in ${_s(database!['dataSource']) ?? 'a product database'}',
                  style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: 6),
                if (registry != null) ...[
                  _row(Icons.label_outline, 'Product', _s(registry['name'])),
                  _row(Icons.storefront_outlined, 'Supplier',
                      '${_s(registry['supplierName']) ?? ''}${registry['supplierVerified'] == true ? ' (verified)' : ''}'),
                  _row(Icons.factory_outlined, 'Maker', _s(registry['manufacturer'])),
                ] else ...[
                  _row(Icons.label_outline, 'Product', [_s(database!['brand']), _s(database['name'])].whereType<String>().join(' ')),
                  _row(Icons.scale_outlined, 'Size', _s(database['quantity'])),
                ],
              ],
            ),
          ),
        ],
        if (_list(a['redFlags']).isNotEmpty) ...[
          const SizedBox(height: 12),
          _bullets('Warning signs', _list(a['redFlags']), Icons.flag_rounded, AppColors.error),
        ],
        if (_list(a['goodSigns']).isNotEmpty) ...[
          const SizedBox(height: 12),
          _bullets('Good signs', _list(a['goodSigns']), Icons.check_circle_outline, AppColors.success),
        ],
        if (_s(a['ingredients']) != null || _list(a['allergens']).isNotEmpty) ...[
          const SizedBox(height: 12),
          _section('Ingredients', [
            if (_s(a['ingredients']) != null) Text(_s(a['ingredients'])!, style: AppTextStyles.caption),
            if (_list(a['allergens']).isNotEmpty) ...[
              const SizedBox(height: 8),
              Wrap(
                spacing: 6,
                runSpacing: 6,
                children: [for (final x in _list(a['allergens'])) _pill(x, AppColors.error)],
              ),
            ],
          ]),
        ],
        if (_list(a['certifications']).isNotEmpty || _list(a['claims']).isNotEmpty) ...[
          const SizedBox(height: 12),
          _section('Marks and claims', [
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final x in _list(a['certifications'])) _pill(x, AppColors.secondary),
                for (final x in _list(a['claims'])) _pill(x, AppColors.primary),
              ],
            ),
          ]),
        ],
        if (_list(a['warnings']).isNotEmpty || _s(a['usage']) != null) ...[
          const SizedBox(height: 12),
          _section('On the label', [
            for (final w in _list(a['warnings'])) _row(Icons.warning_amber_rounded, 'Warning', w),
            _row(Icons.menu_book_outlined, 'Use', _s(a['usage'])),
          ]),
        ],
        if (_list(a['tips']).isNotEmpty) ...[
          const SizedBox(height: 12),
          _bullets('What to check next', _list(a['tips']), Icons.lightbulb_outline, AppColors.primary),
        ],
        const SizedBox(height: 16),
        if (onVerifyBarcode != null)
          FilledButton.icon(
            onPressed: onVerifyBarcode,
            icon: const Icon(Icons.verified_user_rounded),
            label: const Text('Check this barcode on SpazaSure'),
          ),
        if (onReport != null) ...[
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: onReport,
            icon: Icon(Icons.report_outlined, color: level == 'high' ? AppColors.error : null),
            label: const Text('Report this product'),
          ),
        ],
        if (onRetake != null) ...[
          const SizedBox(height: 8),
          TextButton.icon(
            onPressed: onRetake,
            icon: const Icon(Icons.photo_camera_outlined),
            label: const Text('Photograph another side'),
          ),
        ],
      ],
    );
  }

  Widget _section(String title, List<Widget> children) => _card(
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700)),
        const SizedBox(height: 8),
        ...children,
      ],
    ),
  );

  Widget _bullets(String title, List<String> items, IconData icon, Color color) => _section(title, [
    for (final i in items)
      Padding(
        padding: const EdgeInsets.only(bottom: 6),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, size: 16, color: color),
            const SizedBox(width: 8),
            Expanded(child: Text(i, style: AppTextStyles.caption)),
          ],
        ),
      ),
  ]);

  Widget _card({required Widget child, Color? tint}) => Container(
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: tint == null ? AppColors.surface : tint.withValues(alpha: 0.08),
      borderRadius: BorderRadius.circular(16),
      border: tint == null ? null : Border.all(color: tint.withValues(alpha: 0.3)),
    ),
    child: child,
  );

  Widget _pill(String label, Color color) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.12),
      borderRadius: BorderRadius.circular(20),
    ),
    child: Text(label, style: AppTextStyles.caption.copyWith(color: color, fontWeight: FontWeight.w700)),
  );

  Widget _row(IconData icon, String label, String? value) {
    if (value == null || value.trim().isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 15, color: AppColors.textHint),
          const SizedBox(width: 8),
          Text('$label: ', style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary)),
          Expanded(child: Text(value, style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600))),
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
                Text(_s(c['label']) ?? '', style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w700)),
                Text(_s(c['detail']) ?? '', style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
