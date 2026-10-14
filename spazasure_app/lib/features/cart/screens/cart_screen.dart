import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/custom_button.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/services/order_service.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/platform_settings_service.dart';
import 'package:spazasure_app/services/profile_service.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';

class CartScreen extends StatefulWidget {
  const CartScreen({super.key});

  @override
  State<CartScreen> createState() => _CartScreenState();
}

class _CartScreenState extends State<CartScreen> {
  String _deliveryOption = 'standard';
  String _paymentMethod = OrderService.paystack;
  bool _placing = false;
  String? _pendingCheckoutUrl;
  String? _pendingOrderNumber;
  /// Shop wallet balance; null until loaded (or if it couldn't be loaded)
  double? _walletBalance;

  @override
  void initState() {
    super.initState();
    // Fees and cash-on-delivery rules come from admin settings
    PlatformSettingsService.fetch().then((settings) {
      if (mounted) context.read<CartProvider>().applySettings(settings);
    });
    _loadWalletBalance();
  }

  Future<void> _loadWalletBalance() async {
    try {
      final res = await ApiService.get('/shop/wallet');
      final balance = ((res['data'] as Map<String, dynamic>)['balance'] as num?)?.toDouble();
      if (mounted) setState(() => _walletBalance = balance);
    } catch (_) {
      // Paying from the wallet just isn't offered if the balance can't be loaded
    }
  }

  /// The wallet can pay only when its balance covers the whole order
  bool _walletAllowed(CartProvider cart) =>
      _walletBalance != null && _walletBalance! >= cart.total(_deliveryOption);

  /// Cash on delivery only when the admin allows it and the total is within the limit
  bool _codAllowed(CartProvider cart) =>
      cart.settings.cashOnDeliveryAllowedFor(cart.total(_deliveryOption));

  String _effectiveMethod(CartProvider cart) =>
      (_paymentMethod == OrderService.cashOnDelivery && !_codAllowed(cart)) ||
          (_paymentMethod == OrderService.wallet && !_walletAllowed(cart))
      ? OrderService.paystack
      : _paymentMethod;

  @override
  Widget build(BuildContext context) {
    final cart = context.watch<CartProvider>();

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Your cart'),
            Text(
              '${cart.count} ${cart.count == 1 ? 'item' : 'items'}',
              style: AppTextStyles.caption.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
          ],
        ),
        backgroundColor: AppColors.surface,
        actions: [
          if (!cart.isEmpty)
            IconButton(
              tooltip: 'Clear cart',
              onPressed: () => cart.clear(),
              icon: const Icon(Icons.delete_outline),
              color: AppColors.textSecondary,
            ),
        ],
      ),
      body: cart.isEmpty
          ? _buildEmptyCart()
          : SingleChildScrollView(
              padding: const EdgeInsets.only(top: 8),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _buildBasketHeading(cart),
                  ...cart.items.map((item) => _buildCartItem(cart, item)),
                  const SizedBox(height: 12),
                  _buildSection('Delivery option', _buildDeliveryOptions(cart)),
                  const SizedBox(height: 12),
                  _buildSection('How will you pay?', _buildPaymentOptions(cart)),
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
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Total',
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textSecondary,
                            ),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            'R${cart.total(_deliveryOption).toStringAsFixed(2)}',
                            style: AppTextStyles.subtitle.copyWith(
                              color: AppColors.textPrimary,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      flex: 2,
                      child: CustomButton(
                        text: _effectiveMethod(cart) == OrderService.cashOnDelivery
                            ? (_placing ? 'Placing order...' : 'Place order · pay on delivery')
                            : _effectiveMethod(cart) == OrderService.wallet
                            ? (_placing ? 'Paying...' : 'Pay from wallet')
                            : _placing
                            ? 'Opening checkout...'
                            : _pendingCheckoutUrl != null
                            ? 'Continue to payment'
                            : 'Pay securely with Paystack',
                        onPressed: _placing ? () {} : () => _placeOrder(cart),
                      ),
                    ),
                  ],
                ),
              ),
            ),
    );
  }

  Widget _buildEmptyCart() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 112,
            height: 112,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.07),
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.shopping_bag_outlined,
              size: 48,
              color: AppColors.primary,
            ),
          ),
          const SizedBox(height: 24),
          Text('Your cart is empty', style: AppTextStyles.h3),
          const SizedBox(height: 10),
          Text(
            'Find products in the marketplace and add them here.',
            style: AppTextStyles.bodySmall.copyWith(
              color: AppColors.textSecondary,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 28),
          CustomButton(
            text: 'Browse marketplace',
            width: 230,
            onPressed: () => Navigator.pushNamed(context, '/marketplace'),
          ),
        ],
      ),
    );
  }

  Widget _buildBasketHeading(CartProvider cart) {
    final supplierCount = cart.items
        .map((item) => item.product.supplierId)
        .toSet()
        .length;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Review your order', style: AppTextStyles.h3),
                const SizedBox(height: 4),
                Text(
                  'Items from $supplierCount ${supplierCount == 1 ? 'supplier' : 'suppliers'}',
                  style: AppTextStyles.bodySmall.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: AppColors.divider),
            ),
            child: Text(
              '${cart.count} ${cart.count == 1 ? 'item' : 'items'}',
              style: AppTextStyles.caption.copyWith(
                color: AppColors.primary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCartItem(CartProvider cart, item) {
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 6, 16, 4),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.divider.withValues(alpha: 0.7)),
        boxShadow: [
          BoxShadow(
            color: AppColors.textPrimary.withValues(alpha: 0.035),
            blurRadius: 14,
            offset: const Offset(0, 5),
          ),
        ],
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 76,
            height: 76,
            clipBehavior: Clip.antiAlias,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(12),
            ),
            child: item.product.imageUrl.trim().isEmpty
                ? _buildProductPlaceholder()
                : Image.network(
                    item.product.imageUrl,
                    fit: BoxFit.cover,
                    errorBuilder: (context, error, stackTrace) =>
                        _buildProductPlaceholder(),
                  ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.product.name,
                  style: AppTextStyles.bodySmall.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(
                  item.product.supplierName,
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 10),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'R${item.total.toStringAsFixed(2)}',
                      style: AppTextStyles.priceSmall,
                    ),
                    _buildQuantityControl(cart, item),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: 2),
          IconButton(
            tooltip: 'Remove ${item.product.name}',
            visualDensity: VisualDensity.compact,
            constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
            padding: EdgeInsets.zero,
            onPressed: () => cart.remove(item.product.id),
            icon: const Icon(Icons.close, size: 18),
            color: AppColors.textHint,
          ),
        ],
      ),
    );
  }

  Widget _buildProductPlaceholder() {
    return const Center(
      child: Icon(
        Icons.inventory_2_outlined,
        color: AppColors.primary,
        size: 30,
      ),
    );
  }

  Widget _buildQuantityControl(CartProvider cart, item) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: AppColors.divider),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _miniButton(
            Icons.remove,
            () => cart.updateQuantity(item.product.id, item.quantity - 1),
            enabled: item.quantity > 1,
          ),
          InkWell(
            onTap: () => _editQuantity(cart, item),
            borderRadius: BorderRadius.circular(8),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 10),
              child: Text(
                '${item.quantity}',
                style: AppTextStyles.body.copyWith(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
          ),
          _miniButton(
            Icons.add,
            () => cart.updateQuantity(item.product.id, item.quantity + 1),
            enabled: item.quantity < item.product.stockQuantity,
          ),
        ],
      ),
    );
  }

  Future<void> _editQuantity(CartProvider cart, CartItem item) async {
    final controller = TextEditingController(text: '${item.quantity}');
    final formKey = GlobalKey<FormState>();
    final quantity = await showDialog<int>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(item.product.name, maxLines: 1, overflow: TextOverflow.ellipsis),
        content: Form(
          key: formKey,
          child: TextFormField(
            controller: controller,
            autofocus: true,
            keyboardType: TextInputType.number,
            decoration: InputDecoration(
              labelText: 'Quantity',
              helperText: '${item.product.stockQuantity} units available',
            ),
            validator: (value) {
              final selected = int.tryParse((value ?? '').trim());
              if (selected == null || selected < 1) {
                return 'Enter at least 1 unit';
              }
              if (selected > item.product.stockQuantity) {
                return 'Only ${item.product.stockQuantity} available';
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
    if (quantity != null) cart.updateQuantity(item.product.id, quantity);
  }

  Widget _miniButton(
    IconData icon,
    VoidCallback onTap, {
    bool enabled = true,
  }) {
    return IconButton(
      onPressed: enabled ? onTap : null,
      visualDensity: VisualDensity.compact,
      constraints: const BoxConstraints(minWidth: 34, minHeight: 36),
      padding: EdgeInsets.zero,
      icon: Icon(
        icon,
        size: 17,
        color: enabled ? AppColors.primary : AppColors.disabled,
      ),
    );
  }

  Widget _buildSection(String title, Widget child) {
    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 16),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.divider.withValues(alpha: 0.7)),
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

  Widget _buildDeliveryOptions(CartProvider cart) {
    final options = [
      {
        'id': 'standard',
        'title': 'Standard delivery',
        'detail': '2–3 business days · R${cart.settings.standardDeliveryFee.toStringAsFixed(0)} per supplier',
        'icon': Icons.local_shipping_outlined,
      },
      {
        'id': 'express',
        'title': 'Express delivery',
        'detail': 'Same day · R${cart.settings.expressDeliveryFee.toStringAsFixed(0)} per supplier',
        'icon': Icons.bolt,
      },
      {
        'id': 'pickup',
        'title': 'Pickup',
        'detail': 'Collect from your suppliers',
        'icon': Icons.store_outlined,
      },
    ];
    return Column(
      children: options.map((opt) {
        final isSelected = _deliveryOption == opt['id'];
        final optionId = opt['id'] as String;
        final fee = cart.deliveryFee(optionId);
        return InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: () => setState(() => _deliveryOption = optionId),
          child: Container(
            margin: const EdgeInsets.only(bottom: 10),
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
            decoration: BoxDecoration(
              color: isSelected
                  ? AppColors.primary.withValues(alpha: 0.05)
                  : AppColors.surface,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(
                color: isSelected ? AppColors.primary : AppColors.divider,
              ),
            ),
            child: Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: isSelected
                        ? AppColors.primary.withValues(alpha: 0.1)
                        : AppColors.background,
                    borderRadius: BorderRadius.circular(11),
                  ),
                  child: Icon(
                    opt['icon'] as IconData,
                    color: isSelected
                        ? AppColors.primary
                        : AppColors.textSecondary,
                    size: 20,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        opt['title'] as String,
                        style: AppTextStyles.body.copyWith(
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        opt['detail'] as String,
                        style: AppTextStyles.caption.copyWith(
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      fee == 0 ? 'Free' : 'R${fee.toStringAsFixed(2)}',
                      style: AppTextStyles.body.copyWith(
                        fontWeight: FontWeight.w700,
                        color: isSelected
                            ? AppColors.primary
                            : AppColors.textPrimary,
                      ),
                    ),
                    Text(
                      'for order',
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
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
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.divider.withValues(alpha: 0.7)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Order summary', style: AppTextStyles.subtitle),
          const SizedBox(height: 12),
          _summaryRow('Subtotal', 'R${sub.toStringAsFixed(2)}'),
          _summaryRow('Delivery Fee', 'R${del.toStringAsFixed(2)}'),
          _summaryRow(
            cart.settings.shopServiceFeePercent > 0
                ? 'Platform fee (incl. ${cart.settings.shopServiceFeePercent.toStringAsFixed(1)}% service fee)'
                : 'Platform Fee (supplier plan rates)',
            'R${fee.toStringAsFixed(2)}',
          ),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Divider(),
          ),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Total',
                style: AppTextStyles.subtitle.copyWith(
                  color: AppColors.textPrimary,
                ),
              ),
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

  Widget _buildPaymentOptions(CartProvider cart) {
    final codAllowed = _codAllowed(cart);
    final method = _effectiveMethod(cart);
    final settings = cart.settings;
    final codReason = !settings.cashOnDeliveryEnabled
        ? 'Not available right now'
        : 'Available for orders up to R${settings.cashOnDeliveryMaxOrderTotal.toStringAsFixed(0)}';

    return Column(
      children: [
        _paymentTile(
          selected: method == OrderService.paystack,
          icon: Icons.lock_outline,
          title: 'Pay now with Paystack',
          detail: 'Card, EFT or instant payment. Your order goes to the supplier once paid.',
          onTap: () => setState(() => _paymentMethod = OrderService.paystack),
        ),
        if (_walletBalance != null)
          _paymentTile(
            selected: method == OrderService.wallet,
            icon: Icons.account_balance_wallet_outlined,
            title: 'Pay from my wallet',
            detail: _walletAllowed(cart)
                ? 'Balance R${_walletBalance!.toStringAsFixed(2)}. Paid instantly, and your order goes straight to the supplier.'
                : 'Balance R${_walletBalance!.toStringAsFixed(2)}. Top up your wallet by card or direct deposit to use it.',
            enabled: _walletAllowed(cart),
            onTap: () => setState(() {
              _paymentMethod = OrderService.wallet;
              // A Paystack checkout started earlier is abandoned when switching to the wallet
              _pendingCheckoutUrl = null;
              _pendingOrderNumber = null;
            }),
          ),
        _paymentTile(
          selected: method == OrderService.cashOnDelivery,
          icon: Icons.payments_outlined,
          title: 'Cash on delivery',
          detail: codAllowed
              ? 'Pay in cash when your order arrives. SpazaSure collects the payment, and the order is complete once you confirm delivery.'
              : codReason,
          enabled: codAllowed,
          onTap: () => setState(() {
            _paymentMethod = OrderService.cashOnDelivery;
            // A Paystack checkout started earlier is abandoned when switching to cash
            _pendingCheckoutUrl = null;
            _pendingOrderNumber = null;
          }),
        ),
      ],
    );
  }

  Widget _paymentTile({
    required bool selected,
    required IconData icon,
    required String title,
    required String detail,
    required VoidCallback onTap,
    bool enabled = true,
  }) {
    return Opacity(
      opacity: enabled ? 1 : 0.5,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: enabled ? onTap : null,
        child: Container(
          margin: const EdgeInsets.only(bottom: 10),
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
          decoration: BoxDecoration(
            color: selected ? AppColors.primary.withValues(alpha: 0.05) : AppColors.surface,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: selected ? AppColors.primary : AppColors.divider),
          ),
          child: Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: selected ? AppColors.primary.withValues(alpha: 0.1) : AppColors.background,
                  borderRadius: BorderRadius.circular(11),
                ),
                child: Icon(icon, color: selected ? AppColors.primary : AppColors.textSecondary, size: 20),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700)),
                    const SizedBox(height: 3),
                    Text(detail, style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary)),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Icon(
                selected ? Icons.radio_button_checked : Icons.radio_button_off,
                color: selected ? AppColors.primary : AppColors.divider,
              ),
            ],
          ),
        ),
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

      final method = _effectiveMethod(cart);
      if (method == OrderService.wallet) {
        final placed = await OrderService.placeOrder(
          items: cart.items.toList(),
          deliveryOption: _deliveryOption,
          deliveryAddress: deliveryAddress,
          paymentMethod: OrderService.wallet,
        );
        cart.clear();
        _loadWalletBalance();
        if (!mounted) return;
        _showSuccess(placed.orderNumber, walletPaid: true);
        return;
      }
      if (method == OrderService.cashOnDelivery) {
        final total = cart.total(_deliveryOption);
        final placed = await OrderService.placeOrder(
          items: cart.items.toList(),
          deliveryOption: _deliveryOption,
          deliveryAddress: deliveryAddress,
          paymentMethod: OrderService.cashOnDelivery,
        );
        cart.clear();
        if (!mounted) return;
        _showSuccess(
          placed.orderNumber,
          cashOnDelivery: true,
          cashTotal: placed.totalAmount > 0 ? placed.totalAmount : total,
        );
        return;
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
            checkoutUrl: checkout.checkoutUrl!,
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
          content: Text(e.toString().replaceFirst(RegExp(r'^(Exception|Bad state): '), '')),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          duration: const Duration(seconds: 5),
        ),
      );
    } finally {
      if (mounted) setState(() => _placing = false);
    }
  }

  void _showSuccess(
    String orderNumber, {
    bool cashOnDelivery = false,
    bool walletPaid = false,
    double cashTotal = 0,
  }) {
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
                color: Color(0xFFE6F5EE),
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.check_circle,
                color: AppColors.success,
                size: 56,
              ),
            ),
            const SizedBox(height: 20),
            Text(
              cashOnDelivery ? 'Order placed' : walletPaid ? 'Order paid' : 'Payment Submitted',
              style: AppTextStyles.h2,
            ),
            const SizedBox(height: 8),
            Text(
              orderNumber,
              style: AppTextStyles.subtitle.copyWith(color: AppColors.primary),
            ),
            const SizedBox(height: 8),
            Text(
              cashOnDelivery
                  ? 'Your supplier has been notified. Have R${cashTotal.toStringAsFixed(2)} in cash ready when your order arrives, then confirm delivery in Orders to complete it.'
                  : walletPaid
                  ? 'Paid from your SpazaSure wallet. Your supplier has been notified and will start preparing your order.'
                  : 'Paystack received your payment. We will confirm it and notify the supplier shortly.',
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
