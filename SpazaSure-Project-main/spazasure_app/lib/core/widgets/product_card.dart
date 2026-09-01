import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';

class ProductCard extends StatelessWidget {
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
          size: 48,
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
          height: 120,
          child: Image.memory(bytes, fit: BoxFit.cover,
            errorBuilder: (_, __, ___) => _imagePlaceholder()),
        );
      } catch (_) {
        return _imagePlaceholder();
      }
    }
    // Handle HTTP/HTTPS URLs
    if (url.startsWith('http')) {
      return SizedBox(
        width: double.infinity,
        height: 120,
        child: Image.network(url, fit: BoxFit.cover,
          errorBuilder: (_, __, ___) => _imagePlaceholder()),
      );
    }
    return _imagePlaceholder();
  }

  Widget _imagePlaceholder() {
    return Center(
      child: Icon(
        Icons.inventory_2_outlined,
        size: 48,
        color: AppColors.primary.withValues(alpha: 0.3),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(16),
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
            Container(
              height: 120,
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.08),
                borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
              ),
              child: Stack(
                children: [
                  // Show actual image or fallback to icon
                  ClipRRect(
                    borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
                    child: _buildProductImage(),
                  ),
                  if (product.discountPrice != null)
                    Positioned(
                      top: 8,
                      left: 8,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: AppColors.error,
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          '${(((product.price - product.discountPrice!) / product.price) * 100).round()}% OFF',
                          style: AppTextStyles.caption.copyWith(color: Colors.white, fontWeight: FontWeight.w600),
                        ),
                      ),
                    ),
                  // Supplier tier badge (top-right)
                  Positioned(
                    top: 8,
                    right: 8,
                    child: _SupplierTierBadge(supplierName: product.supplierName),
                  ),
                  // Nearby badge
                  if (product.isNearby)
                    Positioned(
                      bottom: 8,
                      left: 8,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: AppColors.primary,
                          borderRadius: BorderRadius.circular(8),
                          boxShadow: [BoxShadow(color: AppColors.primary.withValues(alpha: 0.4), blurRadius: 4)],
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.near_me, size: 10, color: Colors.white),
                            const SizedBox(width: 3),
                            Text(
                              'Nearby',
                              style: AppTextStyles.caption.copyWith(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 10),
                            ),
                          ],
                        ),
                      ),
                    ),
                  if (!product.isAvailable)
                    Container(
                      decoration: BoxDecoration(
                        color: Colors.black45,
                        borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
                      ),
                      child: Center(
                        child: Text('Out of Stock', style: AppTextStyles.subtitle.copyWith(color: Colors.white)),
                      ),
                    ),
                ],
              ),
            ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      product.name,
                      style: AppTextStyles.bodySmall.copyWith(
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 2),
                    Text(
                      product.supplierName,
                      style: AppTextStyles.caption,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    if (product.supplierCity != null && product.supplierCity!.isNotEmpty)
                      Row(
                        children: [
                          Icon(Icons.location_on, size: 10, color: product.isNearby ? AppColors.primary : AppColors.textHint),
                          const SizedBox(width: 2),
                          Expanded(
                            child: Text(
                              product.supplierCity!,
                              style: AppTextStyles.caption.copyWith(
                                fontSize: 10,
                                color: product.isNearby ? AppColors.primary : AppColors.textHint,
                                fontWeight: product.isNearby ? FontWeight.w600 : FontWeight.w400,
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ],
                      ),
                    const Spacer(),
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
                                  ),
                                ),
                                Text('R${product.discountPrice!.toStringAsFixed(2)}', style: AppTextStyles.priceSmall),
                              ] else
                                Text('R${product.price.toStringAsFixed(2)}', style: AppTextStyles.priceSmall),
                            ],
                          ),
                        ),
                        if (product.isAvailable)
                          GestureDetector(
                            onTap: onAddToCart,
                            child: Container(
                              padding: const EdgeInsets.all(6),
                              decoration: BoxDecoration(
                                color: AppColors.primary,
                                borderRadius: BorderRadius.circular(8),
                              ),
                              child: const Icon(Icons.add, color: Colors.white, size: 18),
                            ),
                          ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}


// ─── Supplier Tier Badge ──────────────────────────────────────────────────────
// Derives a mock tier from the supplier name hash so it's deterministic.
class _SupplierTierBadge extends StatelessWidget {
  final String supplierName;
  const _SupplierTierBadge({required this.supplierName});

  static const _tiers = ['gold', 'silver', 'bronze', 'basic'];

  String get _tier => _tiers[supplierName.length % _tiers.length];

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
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
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
