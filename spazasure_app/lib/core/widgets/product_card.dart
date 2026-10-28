import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/motion.dart';

/// Sized for a three-column catalogue grid of [gridHeight]. The photo takes
/// whatever height the text leaves, so cards are filled to the bottom.
class ProductCard extends StatelessWidget {
  static const double gridHeight = 196;

  final Product product;
  final VoidCallback? onTap;
  final VoidCallback? onAddToCart;

  const ProductCard({
    super.key,
    required this.product,
    this.onTap,
    this.onAddToCart,
  });

  Widget _buildProductImage() {
    final url = product.imageUrl;
    if (url.isEmpty) {
      return Center(
        child: Icon(
          Icons.inventory_2_outlined,
          size: 32,
          color: AppColors.primary.withValues(alpha: 0.3),
        ),
      );
    }
    // Handle base64 data URIs
    if (url.startsWith('data:image')) {
      try {
        final dataIndex = url.indexOf(',');
        if (dataIndex == -1) return _imagePlaceholder();
        final base64Str = url.substring(dataIndex + 1);
        final bytes = base64Decode(base64Str);
        return SizedBox(
          width: double.infinity,
          height: double.infinity,
          child: Image.memory(
            bytes,
            fit: BoxFit.cover,
            errorBuilder: (_, __, ___) => _imagePlaceholder(),
          ),
        );
      } catch (_) {
        return _imagePlaceholder();
      }
    }
    // Handle HTTP/HTTPS URLs
    if (url.startsWith('http')) {
      return SizedBox(
        width: double.infinity,
        height: double.infinity,
        child: Image.network(
          url,
          fit: BoxFit.cover,
          errorBuilder: (_, __, ___) => _imagePlaceholder(),
        ),
      );
    }
    return _imagePlaceholder();
  }

  Widget _imagePlaceholder() {
    return Center(
      child: Icon(
        Icons.inventory_2_outlined,
        size: 32,
        color: AppColors.primary.withValues(alpha: 0.3),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Pressable(
      onTap: onTap,
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(12),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.06),
              blurRadius: 10,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Product Image
            Expanded(
              child: Container(
                width: double.infinity,
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.08),
                  borderRadius: const BorderRadius.vertical(
                    top: Radius.circular(12),
                  ),
                ),
                child: Stack(
                  children: [
                    // Show actual image or fallback to icon
                    ClipRRect(
                      borderRadius: const BorderRadius.vertical(
                        top: Radius.circular(12),
                      ),
                      child: _buildProductImage(),
                    ),
                    if (product.discountPrice != null)
                      Positioned(
                        top: 4,
                        left: 4,
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 5,
                            vertical: 2,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.error,
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            '-${(((product.price - product.discountPrice!) / product.price) * 100).round()}%',
                            style: AppTextStyles.caption.copyWith(
                              color: Colors.white,
                              fontWeight: FontWeight.w700,
                              fontSize: 9,
                            ),
                          ),
                        ),
                      ),
                    // Supplier tier badge (top-right) — only for paid tiers we actually know
                    if (const {
                      'gold',
                      'silver',
                      'bronze',
                    }.contains(product.supplierTier))
                      Positioned(
                        top: 4,
                        right: 4,
                        child: _SupplierTierBadge(tier: product.supplierTier!),
                      ),
                    // Nearby badge
                    if (product.isNearby)
                      Positioned(
                        bottom: 4,
                        left: 4,
                        child: Container(
                          padding: const EdgeInsets.all(4),
                          decoration: BoxDecoration(
                            color: AppColors.primary,
                            borderRadius: BorderRadius.circular(8),
                            boxShadow: [
                              BoxShadow(
                                color: AppColors.primary.withValues(alpha: 0.4),
                                blurRadius: 4,
                              ),
                            ],
                          ),
                          child: const Tooltip(
                            message: 'Nearby',
                            child: Icon(
                              Icons.near_me,
                              size: 10,
                              color: Colors.white,
                            ),
                          ),
                        ),
                      ),
                    if (!product.isAvailable)
                      Container(
                        decoration: BoxDecoration(
                          color: Colors.black45,
                          borderRadius: const BorderRadius.vertical(
                            top: Radius.circular(12),
                          ),
                        ),
                        child: Center(
                          child: Text(
                            'Out of stock',
                            style: AppTextStyles.caption.copyWith(
                              color: Colors.white,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(6, 6, 6, 6),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    product.name,
                    style: AppTextStyles.bodySmall.copyWith(
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary,
                      fontSize: 11.5,
                      height: 1.2,
                    ),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 2),
                  Text(
                    product.supplierName,
                    style: AppTextStyles.caption.copyWith(fontSize: 10),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  if (product.branchStockNote != null)
                    Text(
                      product.branchStockNote!,
                      style: AppTextStyles.caption.copyWith(
                        fontSize: 10,
                        fontWeight: FontWeight.w600,
                        color: product.outOfStockNearYou
                            ? AppColors.error
                            : AppColors.accentDark,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  if (product.supplierCity != null &&
                      product.supplierCity!.isNotEmpty)
                    Row(
                      children: [
                        Icon(
                          Icons.location_on,
                          size: 10,
                          color: product.isNearby
                              ? AppColors.primary
                              : AppColors.textHint,
                        ),
                        const SizedBox(width: 2),
                        Expanded(
                          child: Text(
                            product.supplierCity!,
                            style: AppTextStyles.caption.copyWith(
                              fontSize: 10,
                              color: product.isNearby
                                  ? AppColors.primary
                                  : AppColors.textHint,
                              fontWeight: product.isNearby
                                  ? FontWeight.w600
                                  : FontWeight.w400,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  const SizedBox(height: 4),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Flexible(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (product.discountPrice != null) ...[
                              Text(
                                'R${product.price.toStringAsFixed(2)}',
                                style: AppTextStyles.caption.copyWith(
                                  decoration: TextDecoration.lineThrough,
                                  color: AppColors.textHint,
                                  fontSize: 9,
                                ),
                              ),
                              Text(
                                'R${product.discountPrice!.toStringAsFixed(2)}',
                                style: AppTextStyles.priceSmall.copyWith(
                                  fontSize: 12,
                                ),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ] else
                              Text(
                                'R${product.price.toStringAsFixed(2)}',
                                style: AppTextStyles.priceSmall.copyWith(
                                  fontSize: 12,
                                ),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                              ),
                          ],
                        ),
                      ),
                      if (product.isAvailable)
                        Pressable(
                          onTap: onAddToCart,
                          scale: 0.82,
                          child: Container(
                            padding: const EdgeInsets.all(4),
                            decoration: BoxDecoration(
                              color: AppColors.primary,
                              borderRadius: BorderRadius.circular(6),
                            ),
                            child: const Icon(
                              Icons.add,
                              color: Colors.white,
                              size: 16,
                            ),
                          ),
                        ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─── Supplier Tier Badge ──────────────────────────────────────────────────────
// Shows the supplier's real subscription tier from the marketplace API.
class _SupplierTierBadge extends StatelessWidget {
  final String tier;
  const _SupplierTierBadge({required this.tier});

  String get _tier => tier;

  Color get _color {
    switch (_tier) {
      case 'gold':
        return const Color(0xFFFFC107); // amber
      case 'silver':
        return const Color(0xFF9E9E9E); // grey
      case 'bronze':
        return const Color(0xFF795548); // brown
      default:
        return AppColors.primaryLight; // green for basic
    }
  }

  String get _label {
    switch (_tier) {
      case 'gold':
        return '🥇';
      case 'silver':
        return '🥈';
      case 'bronze':
        return '🥉';
      default:
        return '✓';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: '${_tier[0].toUpperCase()}${_tier.substring(1)} Supplier',
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
        decoration: BoxDecoration(
          color: _color.withValues(alpha: 0.9),
          borderRadius: BorderRadius.circular(6),
          boxShadow: [
            BoxShadow(
              color: _color.withValues(alpha: 0.4),
              blurRadius: 4,
              offset: const Offset(0, 2),
            ),
          ],
        ),
        child: Text(
          _label,
          style: AppTextStyles.caption.copyWith(
            color: Colors.white,
            fontWeight: FontWeight.w700,
            fontSize: 10,
          ),
        ),
      ),
    );
  }
}
