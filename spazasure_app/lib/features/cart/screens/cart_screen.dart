import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/custom_button.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/services/order_service.dart';
import 'package:spazasure_app/services/profile_service.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';

class CartScreen extends StatefulWidget {
  const CartScreen({super.key});

  @override
  State<CartScreen> createState() => _CartScreenState();
}

class _CartScreenState extends State<CartScreen> {
  String _deliveryOption = 'standard';
  bool _placing = false;
  String? _pendingCheckoutUrl;
  String? _pendingOrderNumber;

  @override
  Widget build(BuildContext context) {
    final cart = context.watch<CartProvider>();

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Cart (${cart.count})'),
        backgroundColor: AppColors.surface,
        actions: [
          if (!cart.isEmpty)
            TextButton(
              onPressed: () => cart.clear(),
              child: Text(
                'Clear',
                style: AppTextStyles.body.copyWith(color: AppColors.error),
              ),
            ),
        ],
      ),
      body: cart.isEmpty
          ? _buildEmptyCart()
          : SingleChildScrollView(
              child: Column(
                children: [
                  ...cart.items.map((item) => _buildCartItem(cart, item)),
                  const SizedBox(height: 12),
                  _buildSection('Delivery Option', _buildDeliveryOptions()),
                  const SizedBox(height: 12),
                  _buildSection(
                    'Secure payment',
                    Text(
                      'Pay the full order total securely through Stripe. Supplier-specific platform fees are included in the total.',
                      style: AppTextStyles.body.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  _buildOrderSummary(cart),
                  const SizedBox(height: 100),
                ],
              ),
            ),
      bottomNavigationBar: cart.isEmpty
          ? null
          : Container(
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
                child: CustomButton(
                  text: _placing
                      ? 'Opening secure checkout...'
                      : _pendingCheckoutUrl != null
                      ? 'Continue to Payment'
                      : 'Pay with Stripe  •  R${cart.total(_deliveryOption).toStringAsFixed(2)}',
                  onPressed: _placing ? () {} : () => _placeOrder(cart),
                ),
              ),
            ),
    );
  }

  Widget _buildEmptyCart() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Icons.shopping_cart_outlined,
            size: 80,
            color: AppColors.textHint,
          ),
          const SizedBox(height: 16),
          Text('Your cart is empty', style: AppTextStyles.h3),
          const SizedBox(height: 8),
          Text(
            'Browse the marketplace to add products',
            style: AppTextStyles.bodySmall,
          ),
          const SizedBox(height: 24),
          CustomButton(
            text: 'Browse Products',
            width: 200,
            onPressed: () => Navigator.pushNamed(context, '/marketplace'),
          ),
        ],
      ),
    );
  }

  Widget _buildCartItem(CartProvider cart, item) {
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Container(
            width: 64,
            height: 64,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(
              Icons.inventory_2_outlined,
              color: AppColors.primary.withValues(alpha: 0.4),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.product.name,
                  style: AppTextStyles.bodySmall.copyWith(
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(item.product.supplierName, style: AppTextStyles.caption),
                const SizedBox(height: 6),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'R${item.total.toStringAsFixed(2)}',
                      style: AppTextStyles.priceSmall,
                    ),
                    Container(
                      decoration: BoxDecoration(
                        color: AppColors.background,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Row(
                        children: [
                          _miniButton(
                            Icons.remove,
                            () => cart.updateQuantity(
                              item.product.id,
                              item.quantity - 1,
                            ),
                          ),
                          Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 12),
                            child: Text(
                              '${item.quantity}',
                              style: AppTextStyles.subtitle,
                            ),
                          ),
                          _miniButton(
                            Icons.add,
                            () => cart.updateQuantity(
                              item.product.id,
                              item.quantity + 1,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: 4),
          GestureDetector(
            onTap: () => cart.remove(item.product.id),
            child: const Icon(Icons.close, size: 18, color: AppColors.textHint),
          ),
        ],
      ),
    );
  }

  Widget _miniButton(IconData icon, VoidCallback onTap) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 30,
        height: 30,
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(6),
        ),
        child: Icon(icon, size: 16, color: AppColors.primary),
      ),
    );
  }

  Widget _buildSection(String title, Widget child) {
    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 16),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: AppTextStyles.subtitle),
          const SizedBox(height: 12),
          child,
        ],
      ),
    );
  }

  Widget _buildDeliveryOptions() {
    final options = [
      {
        'id': 'standard',
        'title': 'Standard (2-3 days)',
        'price': 'R150.00',
        'icon': Icons.local_shipping_outlined,
      },
      {
        'id': 'express',
        'title': 'Express (Same day)',
        'price': 'R250.00',
        'icon': Icons.bolt,
      },
      {
        'id': 'pickup',
        'title': 'Pickup',
        'price': 'Free',
        'icon': Icons.store_outlined,
      },
    ];
    return Column(
      children: options.map((opt) {
        final isSelected = _deliveryOption == opt['id'];
        return GestureDetector(
          onTap: () => setState(() => _deliveryOption = opt['id'] as String),
          child: Container(
            margin: const EdgeInsets.only(bottom: 8),
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: isSelected
                  ? AppColors.primary.withValues(alpha: 0.05)
                  : AppColors.background,
              borderRadius: BorderRadius.circular(10),
              border: Border.all(
                color: isSelected ? AppColors.primary : AppColors.divider,
              ),
            ),
            child: Row(
              children: [
                Icon(
                  opt['icon'] as IconData,
                  color: isSelected
                      ? AppColors.primary
                      : AppColors.textSecondary,
                  size: 22,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    opt['title'] as String,
                    style: AppTextStyles.body,
                  ),
                ),
                Text(
                  opt['price'] as String,
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w600,
                    color: isSelected
                        ? AppColors.primary
                        : AppColors.textPrimary,
                  ),
                ),
                const SizedBox(width: 8),
                Icon(
                  isSelected
                      ? Icons.radio_button_checked
                      : Icons.radio_button_off,
                  color: isSelected ? AppColors.primary : AppColors.textHint,
                  size: 20,
                ),
              ],
            ),
          ),
        );
      }).toList(),
    );
  }

  Widget _buildOrderSummary(CartProvider cart) {
    final sub = cart.subtotal;
    final del = cart.deliveryFee(_deliveryOption);
    final fee = cart.platformFee();
    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 16),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Order Summary', style: AppTextStyles.subtitle),
          const SizedBox(height: 12),
          _summaryRow('Subtotal', 'R${sub.toStringAsFixed(2)}'),
          _summaryRow('Delivery Fee', 'R${del.toStringAsFixed(2)}'),
          _summaryRow(
            'Platform Fee (supplier plan rates)',
            'R${fee.toStringAsFixed(2)}',
          ),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Divider(),
          ),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Total', style: AppTextStyles.h3),
              Text(
                'R${cart.total(_deliveryOption).toStringAsFixed(2)}',
                style: AppTextStyles.price,
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _summaryRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
          ),
          Text(value, style: AppTextStyles.body),
        ],
      ),
    );
  }

  Future<void> _placeOrder(CartProvider cart) async {
    if (cart.isEmpty) return;
    setState(() => _placing = true);
    try {
      final profile = await ProfileService.getProfile();
      final deliveryAddress = profile.address.trim();
      if (_deliveryOption != 'pickup' && deliveryAddress.isEmpty) {
        throw StateError(
          'Add a delivery address to your shop profile before ordering.',
        );
      }

      final checkout = _pendingCheckoutUrl == null
          ? await OrderService.placeOrder(
              items: cart.items.toList(),
              deliveryOption: _deliveryOption,
              deliveryAddress: deliveryAddress,
            )
          : OrderCheckout(
              orderNumber: _pendingOrderNumber ?? '',
              checkoutUrl: _pendingCheckoutUrl!,
            );
      _pendingCheckoutUrl = checkout.checkoutUrl;
      _pendingOrderNumber = checkout.orderNumber;
      if (!mounted) return;
      final paid = await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => OnboardingFeeCheckoutScreen(
            stripeUrl: checkout.checkoutUrl,
            title: 'Pay for your order',
          ),
        ),
      );
      if (paid != true) return;
      cart.clear();
      _pendingCheckoutUrl = null;
      _pendingOrderNumber = null;
      if (!mounted) return;
      _showSuccess(checkout.orderNumber);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Failed: $e'),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          duration: const Duration(seconds: 5),
        ),
      );
    } finally {
      if (mounted) setState(() => _placing = false);
    }
  }

  void _showSuccess(String orderNumber) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 80,
              height: 80,
              decoration: const BoxDecoration(
                color: Color(0xFFE8F5E9),
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.check_circle,
                color: AppColors.success,
                size: 56,
              ),
            ),
            const SizedBox(height: 20),
            Text('Payment Submitted', style: AppTextStyles.h2),
            const SizedBox(height: 8),
            Text(
              orderNumber,
              style: AppTextStyles.subtitle.copyWith(color: AppColors.primary),
            ),
            const SizedBox(height: 8),
            Text(
              'Stripe received your payment. We will confirm it and notify the supplier shortly.',
              style: AppTextStyles.body.copyWith(
                color: AppColors.textSecondary,
              ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
            CustomButton(
              text: 'View Orders',
              onPressed: () {
                Navigator.pop(ctx);
                Navigator.pushNamedAndRemoveUntil(
                  context,
                  '/home',
                  (r) => false,
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}
