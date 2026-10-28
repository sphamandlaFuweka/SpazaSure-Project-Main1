import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/widgets/risk_style.dart';

/// Shows what was read off a packaging photo, by the AI analysis or (offline) on the phone.
/// The verdict and next step come first; the details are folded away.
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

  static List<String> _list(dynamic v) => v is List
      ? v.map((e) => e.toString()).where((e) => e.trim().isNotEmpty).toList()
      : const [];

  @override
  Widget build(BuildContext context) {
    final a = Map<String, dynamic>.from(result['analysis'] as Map? ?? const {});
    final onDevice = result['source'] == 'packaging_device';
    final style = RiskStyle.of(_s(result['riskLevel']));
    final verdict = _s(a['verdict']) ?? 'cannot_tell';
    final headline = switch (verdict) {
      'likely_counterfeit' => 'This pack looks counterfeit',
      'some_concerns' => 'Some things on this pack need checking',
      'looks_genuine' =>
        style.step == 1
            ? 'This pack looks genuine'
            : 'The pack looks right, with a few gaps',
      _ => 'Not enough could be seen to judge',
    };
    final checks = (result['checks'] as List? ?? const [])
        .whereType<Map>()
        .map((c) => Map<String, dynamic>.from(c))
        .toList();
    final failed = [
      for (final c in checks)
        if (c['status'] == 'fail') _s(c['detail']),
    ].whereType<String>();
    final redFlags = _list(a['redFlags']);
    final goodSigns = _list(a['goodSigns']);
    // The main reasons: what's wrong first, otherwise what looked right
    final reasons = <String>{...failed, ...redFlags}.toList();
    final shownReasons = reasons.isNotEmpty ? reasons : goodSigns;
    final registry = result['registryProduct'] as Map?;
    final database = result['databaseProduct'] as Map?;
    final name = [
      _s(a['brand']),
      _s(a['productName']),
    ].whereType<String>().join(' ');

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // 1. The answer
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: style.color.withValues(alpha: 0.1),
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: style.color, width: 1.5),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: style.color,
                      shape: BoxShape.circle,
                    ),
                    child: Icon(style.icon, color: Colors.white, size: 26),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          style.label.toUpperCase(),
                          style: AppTextStyles.caption.copyWith(
                            color: style.color,
                            fontWeight: FontWeight.w800,
                            letterSpacing: 0.8,
                          ),
                        ),
                        Text(
                          headline,
                          style: AppTextStyles.body.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              RiskMeter(style: style),
              const SizedBox(height: 12),
              Text(
                style.action,
                style: AppTextStyles.body.copyWith(
                  fontWeight: FontWeight.w700,
                  color: style.color,
                ),
              ),
              if (shownReasons.isNotEmpty) ...[
                const SizedBox(height: 10),
                for (final r in shownReasons.take(3))
                  Padding(
                    padding: const EdgeInsets.only(bottom: 4),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(
                          reasons.isNotEmpty
                              ? Icons.error_outline
                              : Icons.check_circle_outline,
                          size: 16,
                          color: style.color,
                        ),
                        const SizedBox(width: 8),
                        Expanded(child: Text(r, style: AppTextStyles.caption)),
                      ],
                    ),
                  ),
              ],
              Text(
                onDevice
                    ? 'Read on your phone from the label text only.'
                    : 'Photo checked by SpazaSure AI. It can make mistakes.',
                style: AppTextStyles.caption.copyWith(
                  color: AppColors.textHint,
                  fontSize: 11,
                ),
              ),
            ],
          ),
        ),

        // 2. What to do next
        const SizedBox(height: 12),
        if (style.isDanger && onReport != null) ...[
          FilledButton.icon(
            onPressed: onReport,
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.error,
              padding: const EdgeInsets.symmetric(vertical: 14),
            ),
            icon: const Icon(Icons.flag_rounded),
            label: const Text('Report suspicious product'),
          ),
          const SizedBox(height: 8),
        ],
        if (onVerifyBarcode != null) ...[
          (style.isDanger
              ? OutlinedButton.icon(
                  onPressed: onVerifyBarcode,
                  icon: const Icon(Icons.verified_user_rounded),
                  label: const Text('Check this barcode on SpazaSure'),
                )
              : FilledButton.icon(
                  onPressed: onVerifyBarcode,
                  style: FilledButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 14),
                  ),
                  icon: const Icon(Icons.verified_user_rounded),
                  label: const Text('Check this barcode on SpazaSure'),
                )),
          const SizedBox(height: 8),
        ],
        if (onRetake != null && style.step == 0)
          OutlinedButton.icon(
            onPressed: onRetake,
            icon: const Icon(Icons.photo_camera_outlined),
            label: const Text('Take a clearer photo'),
          ),

        // 3. What was read, in short
        const SizedBox(height: 4),
        _card(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                name.isEmpty ? 'Product' : name,
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w800),
              ),
              if (_s(a['variant']) != null || _s(a['packSize']) != null)
                Text(
                  [
                    _s(a['variant']),
                    _s(a['packSize']),
                  ].whereType<String>().join(' · '),
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              const SizedBox(height: 10),
              _row(Icons.factory_outlined, 'Made by', _s(a['manufacturer'])),
              _row(
                Icons.qr_code_rounded,
                'Barcode',
                _s(result['barcode']) ?? _s(a['barcodeDigits']),
              ),
              _row(Icons.event, 'Expiry', _s(a['expiryDate'])),
              _row(Icons.inventory_2_outlined, 'Batch', _s(a['batchNumber'])),
              _row(
                Icons.storefront_outlined,
                'On SpazaSure',
                registry != null
                    ? 'Yes, ${_s(registry['name']) ?? 'registered'}'
                    : 'No',
              ),
            ],
          ),
        ),

        // 4. Everything else, folded away
        const SizedBox(height: 20),
        Text(
          'More details',
          style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 8),
        if (checks.isNotEmpty)
          _details(
            Icons.fact_check_outlined,
            'What we checked',
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [for (final c in checks) _checkRow(c)],
            ),
          ),
        if (redFlags.isNotEmpty)
          _details(
            Icons.flag_outlined,
            'All warning signs (${redFlags.length})',
            _bullets(redFlags, Icons.error_outline, AppColors.error),
          ),
        if (goodSigns.isNotEmpty)
          _details(
            Icons.thumb_up_alt_outlined,
            'Good signs (${goodSigns.length})',
            _bullets(goodSigns, Icons.check_circle_outline, AppColors.success),
          ),
        if (_list(a['tips']).isNotEmpty)
          _details(
            Icons.lightbulb_outline,
            'What to check next',
            _bullets(
              _list(a['tips']),
              Icons.arrow_right_rounded,
              AppColors.primary,
            ),
          ),
        if (registry != null || database != null)
          _details(
            Icons.storefront_outlined,
            registry != null
                ? 'On SpazaSure'
                : 'In ${_s(database!['dataSource']) ?? 'a product database'}',
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: registry != null
                  ? [
                      _row(
                        Icons.label_outline,
                        'Product',
                        _s(registry['name']),
                      ),
                      _row(
                        Icons.storefront_outlined,
                        'Supplier',
                        '${_s(registry['supplierName']) ?? ''}${registry['supplierVerified'] == true ? ' (verified)' : ''}',
                      ),
                      _row(
                        Icons.factory_outlined,
                        'Maker',
                        _s(registry['manufacturer']),
                      ),
                    ]
                  : [
                      _row(
                        Icons.label_outline,
                        'Product',
                        [
                          _s(database!['brand']),
                          _s(database['name']),
                        ].whereType<String>().join(' '),
                      ),
                      _row(
                        Icons.scale_outlined,
                        'Size',
                        _s(database['quantity']),
                      ),
                    ],
            ),
          ),
        if (_s(a['ingredients']) != null || _list(a['allergens']).isNotEmpty)
          _details(
            Icons.restaurant_menu_outlined,
            'Ingredients and allergens',
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (_list(a['allergens']).isNotEmpty)
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      for (final x in _list(a['allergens']))
                        _pill(x, AppColors.error),
                    ],
                  ),
                if (_s(a['ingredients']) != null) ...[
                  const SizedBox(height: 8),
                  Text(_s(a['ingredients'])!, style: AppTextStyles.caption),
                ],
              ],
            ),
          ),
        if (_list(a['certifications']).isNotEmpty ||
            _list(a['claims']).isNotEmpty)
          _details(
            Icons.workspace_premium_outlined,
            'Marks and claims',
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final x in _list(a['certifications']))
                  _pill(x, AppColors.secondary),
                for (final x in _list(a['claims'])) _pill(x, AppColors.primary),
              ],
            ),
          ),
        if (_list(a['warnings']).isNotEmpty ||
            _s(a['usage']) != null ||
            _hasMakerDetails(a))
          _details(
            Icons.menu_book_outlined,
            'Everything on the label',
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _row(
                  Icons.place_outlined,
                  'Address',
                  _s(a['manufacturerAddress']),
                ),
                _row(
                  Icons.call_outlined,
                  'Contact',
                  _list(a['contacts']).join(', '),
                ),
                _row(Icons.public, 'Origin', _s(a['countryOfOrigin'])),
                _row(
                  Icons.precision_manufacturing_outlined,
                  'Made on',
                  _s(a['manufactureDate']),
                ),
                _row(
                  Icons.translate,
                  'Languages',
                  _list(a['languages']).join(', '),
                ),
                for (final w in _list(a['warnings']))
                  _row(Icons.warning_amber_rounded, 'Warning', w),
                _row(Icons.menu_book_outlined, 'Use', _s(a['usage'])),
              ],
            ),
          ),

        const SizedBox(height: 16),
        if (!style.isDanger && onReport != null)
          OutlinedButton.icon(
            onPressed: onReport,
            icon: const Icon(Icons.flag_outlined),
            label: const Text('Report a problem with this product'),
          ),
        if (onRetake != null && style.step != 0)
          TextButton.icon(
            onPressed: onRetake,
            icon: const Icon(Icons.photo_camera_outlined),
            label: const Text('Photograph another side'),
          ),
      ],
    );
  }

  static bool _hasMakerDetails(Map<String, dynamic> a) =>
      _s(a['manufacturerAddress']) != null ||
      _list(a['contacts']).isNotEmpty ||
      _s(a['countryOfOrigin']) != null ||
      _s(a['manufactureDate']) != null;

  /// A folded section under "More details".
  Widget _details(IconData icon, String title, Widget child) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
      ),
      child: ExpansionTile(
        shape: const Border(),
        collapsedShape: const Border(),
        leading: Icon(icon, color: AppColors.primary, size: 22),
        title: Text(
          title,
          style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
        ),
        childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 14),
        expandedCrossAxisAlignment: CrossAxisAlignment.stretch,
        children: [child],
      ),
    ),
  );

  Widget _bullets(List<String> items, IconData icon, Color color) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
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
    ],
  );

  Widget _card({required Widget child}) => Container(
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(16),
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
    if (value == null || value.trim().isEmpty) return const SizedBox.shrink();
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
      'warn' => (Icons.error_outline, RiskStyle.amber),
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
                  _s(c['label']) ?? '',
                  style: AppTextStyles.caption.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                Text(
                  _s(c['detail']) ?? '',
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
