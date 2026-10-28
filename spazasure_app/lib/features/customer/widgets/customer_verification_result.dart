import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:spazasure_app/core/widgets/product_info_sections.dart';
import 'package:spazasure_app/models/product_info.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/screens/compare_pack_screen.dart';
import 'package:spazasure_app/features/customer/widgets/product_gallery.dart';
import 'package:spazasure_app/features/customer/widgets/product_insights_card.dart';
import 'package:spazasure_app/features/customer/widgets/risk_style.dart';
import 'package:spazasure_app/features/feedback/send_feedback_screen.dart';
import 'package:spazasure_app/services/feedback_service.dart';

/// Asks for the hidden scratch-off PIN printed under the panel on the pack.
class _PinCard extends StatefulWidget {
  final bool invalid;
  final ValueChanged<String> onSubmit;

  const _PinCard({required this.invalid, required this.onSubmit});

  @override
  State<_PinCard> createState() => _PinCardState();
}

class _PinCardState extends State<_PinCard> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final color = widget.invalid ? AppColors.error : AppColors.primary;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            widget.invalid
                ? 'That PIN did not match. Check it and try again.'
                : 'Scratch the silver panel and enter the PIN',
            style: AppTextStyles.body.copyWith(
              fontWeight: FontWeight.w700,
              color: color,
            ),
          ),
          const SizedBox(height: 10),
          TextField(
            controller: _controller,
            textCapitalization: TextCapitalization.characters,
            decoration: const InputDecoration(
              hintText: 'XXXX-XXXX-XXXX',
              prefixIcon: Icon(Icons.pin_outlined),
            ),
            onSubmitted: (v) => widget.onSubmit(v.trim()),
          ),
          const SizedBox(height: 10),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: () => widget.onSubmit(_controller.text.trim()),
              child: const Text('Check PIN'),
            ),
          ),
        ],
      ),
    );
  }
}

/// The full product report after a scan, for customers and shops alike. Shops also see the
/// marketplace listing (when a supplier sells it) and never redeem the customer's PIN.
class CustomerVerificationResult extends StatelessWidget {
  final Map<String, dynamic> product;
  final String fallbackCode;
  final DateTime? enteredExpiry;
  final String? rewardMessage;
  final bool rewardDuplicate;
  final VoidCallback onReport;
  final VoidCallback? onCheckExpiry;
  final ValueChanged<String>? onSubmitPin;

  /// Opens a report with a photo of the customer's pack and what didn't match.
  final void Function(PlatformFile photo, String details)? onReportWithPhoto;

  /// Shops only: opens the marketplace product (by id) to order it.
  final ValueChanged<String>? onOrder;

  const CustomerVerificationResult({
    super.key,
    required this.product,
    required this.fallbackCode,
    required this.onReport,
    this.enteredExpiry,
    this.rewardMessage,
    this.rewardDuplicate = false,
    this.onCheckExpiry,
    this.onSubmitPin,
    this.onReportWithPhoto,
    this.onOrder,
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
    final style = RiskStyle.of(level);
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
    final photos = GalleryPhoto.fromProduct(p);
    final name = _text(p['name']) ?? 'Unknown product';
    final ingredients = _text(p['ingredients']);
    final authenticityTips = _list(p['authenticityTips']);
    final info = ProductInfo.fromJson(
      p['info'] is Map ? Map<String, dynamic>.from(p['info'] as Map) : null,
    );
    final danger = style.isDanger || recalled;
    final needsExpiry = onCheckExpiry != null && expiryText == null;
    final insights = p['insights'] is Map
        ? Map<String, dynamic>.from(p['insights'] as Map)
        : null;
    final barcodeInfo = p['barcodeInfo'] is Map
        ? Map<String, dynamic>.from(p['barcodeInfo'] as Map)
        : null;
    final outsideRegistry =
        source == 'open_food_facts' || source == 'not_registered';
    final message = outsideRegistry ? _text(p['message']) : null;
    final marketplace = p['marketplace'] is Map
        ? Map<String, dynamic>.from(p['marketplace'] as Map)
        : null;
    // The main reasons for the verdict: warning signs, or for a safe product what passed
    final reasons = [
      // An outage makes "not found" unreliable, so say so up front
      if (p['lookupUnavailable'] == true && message != null) message,
      ...(indicators.isNotEmpty
          ? indicators
          : [
              for (final c in checks)
                if (c['status'] == 'pass')
                  _text(c['detail']) ?? _text(c['label']) ?? '',
            ].where((r) => r.isNotEmpty)),
    ];

    final reportButton = FilledButton.icon(
      onPressed: onReport,
      style: FilledButton.styleFrom(
        backgroundColor: danger ? AppColors.error : null,
        padding: const EdgeInsets.symmetric(vertical: 14),
      ),
      icon: const Icon(Icons.flag_rounded),
      label: Text(danger ? 'Report suspicious product' : 'Report this product'),
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (rewardMessage != null) _rewardBanner(),

        // 1. The answer: one card, coloured like a traffic light.
        _verdictCard(
          style,
          recalled ? 'This product has an active recall' : headline,
          score,
          reasons,
        ),

        // Things the person must act on right now.
        if (onSubmitPin != null &&
            (p['requiresPin'] == true || p['pinStatus'] == 'invalid')) ...[
          const SizedBox(height: 12),
          _PinCard(
            invalid: p['pinStatus'] == 'invalid',
            onSubmit: onSubmitPin!,
          ),
        ],
        if (onSubmitPin == null && p['requiresPin'] == true) ...[
          const SizedBox(height: 12),
          _card(
            tint: AppColors.secondary,
            child: Text(
              "This item has a scratch-off PIN for the customer who buys it. Don't scratch it: "
              'the customer uses it to confirm the item is genuine.',
              style: AppTextStyles.bodySmall,
            ),
          ),
        ],
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

        // 2. What to do next.
        const SizedBox(height: 12),
        if (danger || notRecognised) reportButton,
        if (needsExpiry) ...[
          if (danger || notRecognised) const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: onCheckExpiry,
            icon: const Icon(Icons.event, size: 18),
            label: const Text('Add the expiry date for a better check'),
          ),
        ],

        // 3. What was scanned, in short.
        const SizedBox(height: 12),
        _card(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (photos.isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(right: 12),
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(12),
                        child: SizedBox(
                          width: 64,
                          height: 64,
                          child: ProductImage(photos.first.url),
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
              _row(Icons.event, 'Expiry', expiryText),
              _row(
                Icons.inventory_2_outlined,
                'Batch',
                _text(p['batchNumber']),
              ),
              _row(
                Icons.storefront_outlined,
                'On SpazaSure',
                source == 'open_food_facts' && _text(p['dataSource']) != null
                    ? 'No (found in ${_text(p['dataSource'])})'
                    : _registryLabel(source),
              ),
              // A shop scanned something no SpazaSure supplier sells
              if (p['notInMarketplace'] == true)
                _row(
                  Icons.shopping_cart_outlined,
                  'Marketplace',
                  'Not sold on SpazaSure',
                ),
            ],
          ),
        ),
        if (marketplace != null) ...[
          const SizedBox(height: 12),
          _marketplaceCard(marketplace, danger),
        ],

        // 4. Everything else, folded away until asked for.
        const SizedBox(height: 20),
        Text(
          'More details',
          style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 8),
        if (checks.isNotEmpty || message != null)
          _details(
            Icons.fact_check_outlined,
            'What we checked',
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (final c in checks) _checkRow(c),
                if (message != null && p['lookupUnavailable'] != true)
                  Text(
                    message,
                    style: AppTextStyles.caption.copyWith(
                      color: p['lookupUnavailable'] == true
                          ? RiskStyle.amber
                          : AppColors.textSecondary,
                    ),
                  ),
              ],
            ),
          ),
        if (authenticityTips.isNotEmpty)
          _details(
            Icons.verified_user_outlined,
            'How to spot a fake',
            AuthenticityChecklist(tips: authenticityTips),
          ),
        if (tips.isNotEmpty)
          _details(
            Icons.lightbulb_outline,
            'How to check it yourself',
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (final t in tips)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Text('• $t', style: AppTextStyles.bodySmall),
                  ),
              ],
            ),
          ),
        if (photos.isNotEmpty ||
            (onReportWithPhoto != null && _text(p['name']) != null))
          _details(
            Icons.photo_library_outlined,
            'Compare with photos',
            ProductGalleryCard(
              photos: photos,
              onCompare: onReportWithPhoto == null
                  ? null
                  : () => _openCompare(context, photos),
            ),
          ),
        if (barcodeInfo != null)
          _details(
            Icons.qr_code_rounded,
            'About the barcode',
            BarcodeInfoCard(info: barcodeInfo),
          ),
        if (allergens.isNotEmpty || ingredients != null)
          _details(
            Icons.restaurant_menu_outlined,
            'Ingredients and allergens',
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (allergens.isNotEmpty)
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      for (final a in allergens)
                        _pill(
                          a,
                          matched.contains(a)
                              ? AppColors.error
                              : RiskStyle.amber,
                        ),
                    ],
                  ),
                if (ingredients != null) ...[
                  const SizedBox(height: 8),
                  Text(ingredients, style: AppTextStyles.caption),
                ],
              ],
            ),
          ),
        if (insights != null || !info.isEmpty || _hasExtraFacts(p))
          _details(
            Icons.info_outline,
            'Product information',
            Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (_text(p['description']) != null &&
                    _text(p['description']) != name) ...[
                  Text(_text(p['description'])!, style: AppTextStyles.caption),
                  const SizedBox(height: 8),
                ],
                _row(Icons.category_outlined, 'Category', _text(p['category'])),
                _row(
                  Icons.scale_outlined,
                  'Size',
                  _text(p['quantity']) ?? _text(p['unit']),
                ),
                _row(Icons.public, 'Made in', _text(p['origin'])),
                _row(
                  Icons.travel_explore,
                  'Sold in',
                  _text(p['countriesSold']),
                ),
                _row(Icons.eco_outlined, 'Labels', _text(p['labels'])),
                _row(
                  Icons.favorite_border,
                  'Nutri-Score',
                  _text(p['nutriScore'])?.toUpperCase(),
                ),
                if (insights != null) ...[
                  const SizedBox(height: 8),
                  ProductInsightsCard(insights: insights),
                ],
                if (!info.isEmpty) ...[
                  const SizedBox(height: 8),
                  ProductInfoSections(
                    info: info,
                    highlightAllergens: matched,
                    // Ingredients and the fake checklist have their own sections.
                    showIngredients: false,
                    showAuthenticity: false,
                  ),
                ],
              ],
            ),
          ),
        if (_text(p['supplierName']) != null)
          _details(
            Icons.business_outlined,
            'Supplier',
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _pill(
                  p['supplierVerified'] == true
                      ? 'Verified supplier'
                      : 'Not verified yet',
                  p['supplierVerified'] == true
                      ? AppColors.success
                      : RiskStyle.amber,
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

        const SizedBox(height: 16),
        if (!(danger || notRecognised))
          OutlinedButton.icon(
            onPressed: onReport,
            icon: const Icon(Icons.flag_outlined),
            label: const Text('Report a problem with this product'),
          ),
        TextButton.icon(
          onPressed: () => Navigator.push(
            context,
            MaterialPageRoute(
              settings: const RouteSettings(name: '/send-feedback'),
              builder: (_) => SendFeedbackScreen(
                service: FeedbackService.customer,
                fromScreen: 'Scan result: ${_text(p['code']) ?? fallbackCode}',
              ),
            ),
          ),
          icon: const Icon(Icons.rate_review_outlined, size: 18),
          label: const Text('Something wrong with this result? Tell us'),
        ),
        Text(
          _text(p['disclaimer']) ??
              'This check is a guide only. It does not guarantee authenticity or product safety.',
          textAlign: TextAlign.center,
          style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
        ),
      ],
    );
  }

  static bool _hasExtraFacts(Map<String, dynamic> p) => [
    'description',
    'category',
    'quantity',
    'origin',
    'countriesSold',
    'labels',
    'nutriScore',
  ].any((k) => _text(p[k]) != null);

  /// The verdict: what the risk is, what to do, and the main reasons why.
  Widget _verdictCard(
    RiskStyle style,
    String headline,
    int? score,
    List<String> reasons,
  ) {
    return Container(
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
          if (reasons.isNotEmpty) ...[
            const SizedBox(height: 10),
            for (final r in reasons.take(3))
              Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(
                      style.isDanger || style.step == 2
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
          if (score != null)
            Text(
              'Risk score $score out of 100',
              style: AppTextStyles.caption.copyWith(
                color: AppColors.textHint,
                fontSize: 11,
              ),
            ),
        ],
      ),
    );
  }

  /// A folded section under "More details".
  Widget _details(IconData icon, String title, Widget child) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
      ),
      child: ExpansionTile(
        // No divider lines when a section opens
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

  void _openCompare(BuildContext context, List<GalleryPhoto> photos) {
    final p = product;
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (compareContext) => ComparePackScreen(
          photos: photos,
          name: _text(p['name']),
          brand: _text(p['brand']),
          quantity: _text(p['quantity']),
          barcode: _text(p['code']) ?? fallbackCode,
          onReport: (photo, details) {
            Navigator.pop(compareContext);
            onReportWithPhoto!(photo, details);
          },
        ),
      ),
    );
  }

  /// When the scan looks risky the order button steps back, so it doesn't contradict the verdict.
  Widget _marketplaceCard(Map<String, dynamic> m, bool danger) {
    final verified = m['supplierVerified'] == true;
    final price = (m['price'] as num?)?.toDouble();
    final unit = _text(m['unit']);
    final firstScan = m['firstScan'] == true;
    final repeatScan = m['repeatScan'] == true;
    final id = _text(m['id']);
    return _card(
      tint: AppColors.success,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.storefront_rounded, color: AppColors.success),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Sold on the SpazaSure marketplace',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          if (price != null)
            _row(
              Icons.sell_outlined,
              'Price',
              'R ${price.toStringAsFixed(2)}${unit == null ? '' : ' per $unit'}',
            ),
          _row(
            Icons.inventory_2_outlined,
            'In stock',
            '${m['stockQty'] ?? 0}${unit == null ? '' : ' $unit'}',
          ),
          if ((m['minOrderQty'] as num?) != null &&
              (m['minOrderQty'] as num) > 1)
            _row(
              Icons.shopping_basket_outlined,
              'Minimum order',
              '${m['minOrderQty']}',
            ),
          _row(
            Icons.business,
            'Supplier',
            [
              _text(m['supplierName']),
              verified ? '(verified)' : '(not verified yet)',
            ].whereType<String>().join(' '),
          ),
          if (firstScan)
            _row(Icons.qr_code_2, 'SpazaSure QR', 'First scan of this code'),
          if (repeatScan)
            _row(
              Icons.history,
              'SpazaSure QR',
              'Scanned before${_text(m['scannedAt']) == null ? '' : ' on ${_text(m['scannedAt'])!.substring(0, 10)}'}. '
                  'If this is a new delivery, the code may have been copied.',
            ),
          if (onOrder != null && id != null && m['isAvailable'] != false) ...[
            const SizedBox(height: 6),
            if (danger)
              Padding(
                padding: const EdgeInsets.only(bottom: 6),
                child: Text(
                  'Check the pack in your hand matches this listing before you order.',
                  style: AppTextStyles.caption.copyWith(
                    color: RiskStyle.orange,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            SizedBox(
              width: double.infinity,
              child: danger
                  ? OutlinedButton.icon(
                      onPressed: () => onOrder!(id),
                      icon: const Icon(Icons.add_shopping_cart_rounded),
                      label: Text(
                        'Order from ${_text(m['supplierName']) ?? 'this supplier'}',
                      ),
                    )
                  : FilledButton.icon(
                      onPressed: () => onOrder!(id),
                      icon: const Icon(Icons.add_shopping_cart_rounded),
                      label: Text(
                        'Order from ${_text(m['supplierName']) ?? 'this supplier'}',
                      ),
                    ),
            ),
          ],
        ],
      ),
    );
  }

  static String _fallbackLevel(bool recalled, bool notRecognised) =>
      recalled ? 'high' : (notRecognised ? 'review' : 'low');

  static String? _registryLabel(String source) => switch (source) {
    'spazasure_qr' => 'Yes, batch-tracked QR code',
    'spazasure_unit' => 'Yes, single-pack code',
    'spazasure_barcode' => 'Yes, registered product',
    'open_food_facts' => 'No (global database only)',
    _ => 'No',
  };

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
