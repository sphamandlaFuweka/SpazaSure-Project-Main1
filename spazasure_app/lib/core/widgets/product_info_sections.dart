import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/models/product_info.dart';

/// Retail-style product information: expandable sections for details and specifications,
/// ingredients and allergens, nutrition, usage, storage, warnings and how to spot a fake.
/// Sections with nothing in them are left out.
class ProductInfoSections extends StatelessWidget {
  final ProductInfo info;

  /// Allergens the signed-in customer is allergic to, highlighted in red.
  final List<String> highlightAllergens;

  /// The customer scan result already shows ingredients and allergens in its own card.
  final bool showIngredients;
  final bool showAuthenticity;

  /// Open the "How to spot a fake" section straight away, e.g. when a scan looks suspicious.
  final bool expandAuthenticity;

  const ProductInfoSections({
    super.key,
    required this.info,
    this.highlightAllergens = const [],
    this.showIngredients = true,
    this.showAuthenticity = true,
    this.expandAuthenticity = false,
  });

  @override
  Widget build(BuildContext context) {
    final facts = info.facts;
    final sections = <Widget>[
      if (showAuthenticity && info.authenticityTips.isNotEmpty)
        _section(
          icon: Icons.verified_user_outlined,
          title: 'How to spot a fake',
          color: AppColors.secondary,
          initiallyExpanded: expandAuthenticity,
          child: AuthenticityChecklist(tips: info.authenticityTips),
        ),
      if (facts.isNotEmpty)
        _section(
          icon: Icons.list_alt_rounded,
          title: 'Product details',
          child: Column(children: [for (final f in facts) _factRow(f.label, f.value)]),
        ),
      if (showIngredients && (info.ingredients != null || info.allergens.isNotEmpty))
        _section(
          icon: Icons.eco_outlined,
          title: 'Ingredients and allergens',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (info.ingredients != null)
                Text(info.ingredients!, style: AppTextStyles.bodySmall.copyWith(height: 1.5)),
              if (info.allergens.isNotEmpty) ...[
                const SizedBox(height: 10),
                Text('Contains', style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w700)),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    for (final a in info.allergens)
                      _chip(a, highlightAllergens.any((h) => h.toLowerCase() == a.toLowerCase())),
                  ],
                ),
              ],
            ],
          ),
        ),
      if (info.nutrition.isNotEmpty)
        _section(
          icon: Icons.restaurant_menu_rounded,
          title: 'Nutritional information',
          child: _nutritionTable(),
        ),
      if (info.usageInstructions != null)
        _section(
          icon: Icons.soup_kitchen_outlined,
          title: 'How to use and serve',
          child: _paragraph(info.usageInstructions!),
        ),
      if (info.storageInstructions != null)
        _section(
          icon: Icons.inventory_2_outlined,
          title: 'Storage',
          child: _paragraph(info.storageInstructions!),
        ),
      if (info.warnings != null)
        _section(
          icon: Icons.warning_amber_rounded,
          title: 'Warnings',
          color: AppColors.error,
          child: _paragraph(info.warnings!),
        ),
    ];
    if (sections.isEmpty) return const SizedBox.shrink();

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.divider),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          for (var i = 0; i < sections.length; i++) ...[
            if (i > 0) const Divider(height: 1, color: AppColors.divider),
            sections[i],
          ],
        ],
      ),
    );
  }

  Widget _section({
    required IconData icon,
    required String title,
    required Widget child,
    Color color = AppColors.primary,
    bool initiallyExpanded = false,
  }) {
    return Theme(
      // ExpansionTile draws its own dividers; the container already separates sections.
      data: ThemeData(dividerColor: Colors.transparent),
      child: ExpansionTile(
        initiallyExpanded: initiallyExpanded,
        leading: Icon(icon, color: color, size: 22),
        title: Text(title, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700)),
        iconColor: AppColors.textSecondary,
        collapsedIconColor: AppColors.textSecondary,
        childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        expandedCrossAxisAlignment: CrossAxisAlignment.start,
        expandedAlignment: Alignment.topLeft,
        children: [child],
      ),
    );
  }

  Widget _factRow(String label, String value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(flex: 2, child: Text(label, style: AppTextStyles.bodySmall.copyWith(color: AppColors.textSecondary))),
        const SizedBox(width: 12),
        Expanded(flex: 3, child: Text(value, style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w700))),
      ],
    ),
  );

  Widget _paragraph(String text) => Text(
    text,
    style: AppTextStyles.bodySmall.copyWith(color: AppColors.textSecondary, height: 1.5),
  );

  Widget _chip(String label, bool highlighted) {
    final color = highlighted ? AppColors.error : AppColors.warning;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: AppTextStyles.caption.copyWith(color: highlighted ? AppColors.error : AppColors.textPrimary, fontWeight: FontWeight.w700),
      ),
    );
  }

  Widget _nutritionTable() {
    final header = AppTextStyles.caption.copyWith(fontWeight: FontWeight.w800, color: AppColors.textSecondary);
    final cell = AppTextStyles.bodySmall;
    final hasServing = info.nutrition.any((n) => n.perServing != null);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (info.servingSize != null || info.servingsPerPack != null)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Text(
              [
                if (info.servingSize != null) 'Serving size: ${info.servingSize}',
                if (info.servingsPerPack != null) 'Servings per pack: ${info.servingsPerPack}',
              ].join('  ·  '),
              style: AppTextStyles.caption,
            ),
          ),
        Table(
          columnWidths: {
            0: const FlexColumnWidth(3),
            1: const FlexColumnWidth(2),
            if (hasServing) 2: const FlexColumnWidth(2),
          },
          border: const TableBorder(horizontalInside: BorderSide(color: AppColors.divider)),
          children: [
            TableRow(children: [
              _cell(Text('Typical values', style: header)),
              _cell(Text('Per 100 g/ml', style: header)),
              if (hasServing) _cell(Text('Per serving', style: header)),
            ]),
            for (final n in info.nutrition)
              TableRow(children: [
                _cell(Text(n.nutrient, style: cell)),
                _cell(Text(n.per100 ?? '-', style: cell.copyWith(fontWeight: FontWeight.w700))),
                if (hasServing) _cell(Text(n.perServing ?? '-', style: cell.copyWith(fontWeight: FontWeight.w700))),
              ]),
          ],
        ),
      ],
    );
  }

  Widget _cell(Widget child) => Padding(padding: const EdgeInsets.symmetric(vertical: 7), child: child);
}

/// The supplier's numbered checks for telling the genuine product from a counterfeit.
class AuthenticityChecklist extends StatelessWidget {
  final List<String> tips;
  const AuthenticityChecklist({super.key, required this.tips});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Checks from the supplier. Compare the pack in your hand with these and with the product photos.',
          style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
        ),
        const SizedBox(height: 10),
        for (var i = 0; i < tips.length; i++)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 22,
                  height: 22,
                  alignment: Alignment.center,
                  decoration: const BoxDecoration(color: AppColors.secondaryTint, shape: BoxShape.circle),
                  child: Text(
                    '${i + 1}',
                    style: AppTextStyles.caption.copyWith(color: AppColors.secondary, fontWeight: FontWeight.w800),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(child: Text(tips[i], style: AppTextStyles.bodySmall.copyWith(height: 1.4))),
              ],
            ),
          ),
      ],
    );
  }
}
