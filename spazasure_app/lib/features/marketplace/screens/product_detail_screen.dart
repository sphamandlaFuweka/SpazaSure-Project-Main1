import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/custom_button.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/providers/cart_provider.dart';

class ProductDetailScreen extends StatefulWidget {
  const ProductDetailScreen({super.key});

  @override
  State<ProductDetailScreen> createState() => _ProductDetailScreenState();
}

class _ProductDetailScreenState extends State<ProductDetailScreen> {
  int _quantity = 1;
  int _currentImageIndex = 0;

  Future<void> _editQuantity(Product product) async {
    final controller = TextEditingController(text: '$_quantity');
    final formKey = GlobalKey<FormState>();
    final quantity = await showDialog<int>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Choose quantity'),
        content: Form(
          key: formKey,
          child: TextFormField(
            controller: controller,
            autofocus: true,
            keyboardType: TextInputType.number,
            decoration: InputDecoration(
              labelText: 'Units',
              helperText: '${product.stockQuantity} available',
            ),
            validator: (value) {
              final selected = int.tryParse((value ?? '').trim());
              if (selected == null || selected < 1) {
                return 'Enter at least 1 unit';
              }
              if (selected > product.stockQuantity) {
                return 'Only ${product.stockQuantity} available';
              }
              return null;
            },
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              if (formKey.currentState!.validate()) {
                Navigator.pop(ctx, int.parse(controller.text.trim()));
              }
            },
            child: const Text('Apply'),
          ),
        ],
      ),
    );
    controller.dispose();
    if (quantity != null && mounted) setState(() => _quantity = quantity);
  }

  Widget _buildProductImage(String url) {
    if (url.isEmpty) return _imagePlaceholder();
    // Handle base64 data URIs
    if (url.startsWith('data:image')) {
      try {
        final dataIndex = url.indexOf(',');
        if (dataIndex == -1) return _imagePlaceholder();
        final base64Str = url.substring(dataIndex + 1);
        final bytes = base64Decode(base64Str);
        return Image.memory(
          bytes,
          fit: BoxFit.cover,
          width: double.infinity,
          height: double.infinity,
          errorBuilder: (_, __, ___) => _imagePlaceholder(),
        );
      } catch (_) {
        return _imagePlaceholder();
      }
    }
    // Handle HTTP/HTTPS URLs
    if (url.startsWith('http')) {
      return Image.network(
        url,
        fit: BoxFit.cover,
        width: double.infinity,
        height: double.infinity,
        errorBuilder: (_, __, ___) => _imagePlaceholder(),
      );
    }
    return _imagePlaceholder();
  }

  Widget _imagePlaceholder() {
    return Center(
      child: Icon(
        Icons.inventory_2_outlined,
        size: 100,
        color: AppColors.primary.withValues(alpha: 0.3),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final product = ModalRoute.of(context)!.settings.arguments as Product;
    return Scaffold(
      backgroundColor: AppColors.surface,
      body: CustomScrollView(
        slivers: [
          // Image header
          SliverAppBar(
            expandedHeight: 280,
            pinned: true,
            backgroundColor: AppColors.surface,
            flexibleSpace: FlexibleSpaceBar(
              background: Container(
                color: AppColors.primary.withValues(alpha: 0.08),
                child: product.images.isNotEmpty
                    ? Stack(
                        children: [
                          PageView.builder(
                            itemCount: product.images.length,
                            onPageChanged: (i) =>
                                setState(() => _currentImageIndex = i),
                            itemBuilder: (ctx, i) =>
                                _buildProductImage(product.images[i]),
                          ),
                          // Image indicators
                          if (product.images.length > 1)
                            Positioned(
                              bottom: 12,
                              left: 0,
                              right: 0,
                              child: Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: List.generate(
                                  product.images.length,
                                  (i) => Container(
                                    width: _currentImageIndex == i ? 20 : 8,
                                    height: 8,
                                    margin: const EdgeInsets.symmetric(
                                      horizontal: 3,
                                    ),
                                    decoration: BoxDecoration(
                                      color: _currentImageIndex == i
                                          ? AppColors.primary
                                          : Colors.white54,
                                      borderRadius: BorderRadius.circular(4),
                                    ),
                                  ),
                                ),
                              ),
                            ),
                        ],
                      )
                    : (product.imageUrl.isNotEmpty
                          ? _buildProductImage(product.imageUrl)
                          : _imagePlaceholder()),
              ),
            ),
          ),
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Supplier badge
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 4,
                        ),
                        decoration: BoxDecoration(
                          color: AppColors.primary.withValues(alpha: 0.1),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(
                              Icons.verified,
                              size: 14,
                              color: AppColors.primary,
                            ),
                            const SizedBox(width: 4),
                            Text(
                              product.supplierName,
                              style: AppTextStyles.caption.copyWith(
                                color: AppColors.primary,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const Spacer(),
                      Row(
                        children: [
                          const Icon(
                            Icons.star,
                            size: 18,
                            color: AppColors.warning,
                          ),
                          const SizedBox(width: 4),
                          Text(
                            product.reviewCount > 0
                                ? product.rating.toStringAsFixed(1)
                                : 'New',
                            style: AppTextStyles.subtitle,
                          ),
                          if (product.reviewCount > 0)
                            Text(
                              ' (${product.reviewCount} review${product.reviewCount == 1 ? '' : 's'})',
                              style: AppTextStyles.bodySmall,
                            ),
                        ],
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  // Name
                  Text(product.name, style: AppTextStyles.h2),
                  const SizedBox(height: 8),
                  Text('SKU: ${product.sku}', style: AppTextStyles.caption),
                  const SizedBox(height: 16),
                  // Price
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(
                        'R${(product.discountPrice ?? product.price).toStringAsFixed(2)}',
                        style: AppTextStyles.price.copyWith(fontSize: 28),
                      ),
                      if (product.discountPrice != null) ...[
                        const SizedBox(width: 8),
                        Text(
                          'R${product.price.toStringAsFixed(2)}',
                          style: AppTextStyles.body.copyWith(
                            decoration: TextDecoration.lineThrough,
                            color: AppColors.textHint,
                          ),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 8),
                  // Stock
                  Row(
                    children: [
                      Container(
                        width: 8,
                        height: 8,
                        decoration: BoxDecoration(
                          color: product.stockQuantity > 20
                              ? AppColors.success
                              : product.stockQuantity > 0
                              ? AppColors.warning
                              : AppColors.error,
                          shape: BoxShape.circle,
                        ),
                      ),
                      const SizedBox(width: 6),
                      Text(
                        product.stockQuantity > 20
                            ? 'In Stock (${product.stockQuantity} available)'
                            : product.stockQuantity > 0
                            ? 'Low Stock (${product.stockQuantity} left)'
                            : 'Out of Stock',
                        style: AppTextStyles.bodySmall.copyWith(
                          color: product.stockQuantity > 20
                              ? AppColors.success
                              : product.stockQuantity > 0
                              ? AppColors.warning
                              : AppColors.error,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  const Divider(),
                  const SizedBox(height: 16),
                  // Description
                  Text('Description', style: AppTextStyles.h3),
                  const SizedBox(height: 8),
                  Text(
                    product.description,
                    style: AppTextStyles.body.copyWith(
                      color: AppColors.textSecondary,
                      height: 1.5,
                    ),
                  ),
                  const SizedBox(height: 24),
                  // Quantity selector
                  Text('Quantity', style: AppTextStyles.h3),
                  const SizedBox(height: 12),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 4,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.background,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        _quantityButton(
                          Icons.remove,
                          () => setState(() => _quantity--),
                          enabled: _quantity > 1,
                        ),
                        InkWell(
                          onTap: product.stockQuantity > 0
                              ? () => _editQuantity(product)
                              : null,
                          borderRadius: BorderRadius.circular(8),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 20),
                            child: Text(
                              product.stockQuantity > 0 ? '$_quantity' : '—',
                              style: AppTextStyles.h3,
                            ),
                          ),
                        ),
                        _quantityButton(
                          Icons.add,
                          () => setState(() => _quantity++),
                          enabled: _quantity < product.stockQuantity,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 100),
                ],
              ),
            ),
          ),
        ],
      ),
      // Bottom bar
      bottomNavigationBar: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.surface,
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.08),
              blurRadius: 10,
              offset: const Offset(0, -4),
            ),
          ],
        ),
        child: SafeArea(
          child: Row(
            children: [
              Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Total', style: AppTextStyles.bodySmall),
                  Text(
                    'R${((product.discountPrice ?? product.price) * _quantity).toStringAsFixed(2)}',
                    style: AppTextStyles.price,
                  ),
                ],
              ),
              const SizedBox(width: 20),
              Expanded(
                child: CustomButton(
                  text: product.stockQuantity > 0
                      ? 'Add to Cart'
                      : 'Out of Stock',
                  icon: Icons.shopping_cart_outlined,
                  onPressed: product.stockQuantity > 0
                      ? () {
                          final added = context.read<CartProvider>().add(
                            product,
                            _quantity,
                          );
                          if (!added) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(
                                content: Text(
                                  'There is not enough stock for that quantity.',
                                ),
                              ),
                            );
                            return;
                          }
                          final messenger = ScaffoldMessenger.of(context);
                          final navigator = Navigator.of(context);
                          final quantity = _quantity;
                          final productName = product.name;
                          navigator.pop(true);
                          messenger.showSnackBar(
                            SnackBar(
                              content: Text(
                                '$quantity × $productName added to cart',
                              ),
                              backgroundColor: AppColors.primary,
                              behavior: SnackBarBehavior.floating,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(10),
                              ),
                              action: SnackBarAction(
                                label: 'VIEW CART',
                                textColor: Colors.white,
                                onPressed: () => navigator.pushNamed('/cart'),
                              ),
                            ),
                          );
                        }
                      : null,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _quantityButton(
    IconData icon,
    VoidCallback onTap, {
    bool enabled = true,
  }) {
    return GestureDetector(
      onTap: enabled ? onTap : null,
      child: Container(
        width: 40,
        height: 40,
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(10),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.05),
              blurRadius: 4,
            ),
          ],
        ),
        child: Icon(
          icon,
          color: enabled ? AppColors.primary : AppColors.disabled,
        ),
      ),
    );
  }
}
