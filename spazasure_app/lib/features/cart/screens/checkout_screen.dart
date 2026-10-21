import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/cart/screens/order_success_screen.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/order_service.dart';
import 'package:spazasure_app/services/profile_service.dart';

/// Checkout: where the order goes, how the shop pays, what it costs, and one button to pay.
class CheckoutScreen extends StatefulWidget {
  const CheckoutScreen({super.key});

  @override
  State<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends State<CheckoutScreen> {
  String _deliveryOption = 'standard';
  String _paymentMethod = OrderService.paystack;
  bool _placing = false;
  String? _pendingCheckoutUrl;
  String? _pendingOrderNumber;
  /// Shop wallet balance; null until loaded (or if it couldn't be loaded)
  double? _walletBalance;
  ShopProfile? _profile;

  @override
  void initState() {
    super.initState();
    _loadWalletBalance();
    ProfileService.getProfile().then((p) {
      if (mounted) setState(() => _profile = p);
    }).catchError((_) {});
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

  void _choosePayment(String method) => setState(() {
        _paymentMethod = method;
        if (method != OrderService.paystack) {
          // A Paystack checkout started earlier is abandoned when paying another way
          _pendingCheckoutUrl = null;
          _pendingOrderNumber = null;
        }
      });

  @override
  Widget build(BuildContext context) {
    final cart = context.watch<CartProvider>();
    final total = cart.total(_deliveryOption);
    final method = _effectiveMethod(cart);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Checkout'),
        centerTitle: true,
        backgroundColor: AppColors.background,
        elevation: 0,
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
        children: [
          _sectionTitle('Deliver to'),
          _addressCard(),
          const SizedBox(height: 12),
          _deliveryChoices(cart),
          const SizedBox(height: 22),
          _sectionTitle('Payment method'),
          _card(
            child: Column(
              children: [
                _paymentRow(
                  method: OrderService.paystack,
                  selected: method == OrderService.paystack,
                  icon: Icons.credit_card_rounded,
                  iconColor: const Color(0xFF0BA4DB),
                  title: 'Card or EFT',
                  subtitle: 'Visa, Mastercard, instant EFT · secured by Paystack',
                ),
                if (_walletBalance != null) ...[
                  _divider(),
                  _paymentRow(
                    method: OrderService.wallet,
                    selected: method == OrderService.wallet,
                    icon: Icons.account_balance_wallet_rounded,
                    iconColor: AppColors.secondary,
                    title: 'SpazaSure wallet',
                    subtitle: _walletAllowed(cart)
                        ? 'Balance R${_walletBalance!.toStringAsFixed(2)} · paid instantly'
                        : 'Balance R${_walletBalance!.toStringAsFixed(2)} · top up to use it',
                    enabled: _walletAllowed(cart),
                  ),
                ],
                _divider(),
                _paymentRow(
                  method: OrderService.cashOnDelivery,
                  selected: method == OrderService.cashOnDelivery,
                  icon: Icons.payments_rounded,
                  iconColor: AppColors.accent,
                  title: 'Cash on delivery',
                  subtitle: _codAllowed(cart)
                      ? 'Pay in cash when your order arrives'
                      : !cart.settings.cashOnDeliveryEnabled
                          ? 'Not available right now'
                          : 'For orders up to R${cart.settings.cashOnDeliveryMaxOrderTotal.toStringAsFixed(0)}',
                  enabled: _codAllowed(cart),
                ),
              ],
            ),
          ),
          const SizedBox(height: 22),
          _sectionTitle('Amount'),
          _card(
            child: Column(
              children: [
                _amountRow('Items (${cart.count})', cart.subtotal),
                _amountRow('Delivery', cart.deliveryFee(_deliveryOption)),
                _amountRow(
                  cart.settings.shopServiceFeePercent > 0
                      ? 'Service fee (${cart.settings.shopServiceFeePercent.toStringAsFixed(1)}%)'
                      : 'Platform fee',
                  cart.platformFee(),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  child: Divider(color: AppColors.divider.withValues(alpha: 0.9), height: 1),
                ),
                Row(
                  children: [
                    Text('Total', style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w800)),
                    const Spacer(),
                    Text(
                      'R${total.toStringAsFixed(2)}',
                      style: AppTextStyles.h3.copyWith(color: AppColors.primary, fontWeight: FontWeight.w800),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              SizedBox(
                width: double.infinity,
                height: 56,
                child: FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                  ),
                  onPressed: _placing || cart.isEmpty ? null : () => _placeOrder(cart),
                  child: _placing
                      ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
                      : Text(
                          _payLabel(method, total),
                          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                        ),
                ),
              ),
              if (method == OrderService.paystack) ...[
                const SizedBox(height: 8),
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.lock_outline, size: 13, color: AppColors.textSecondary),
                    const SizedBox(width: 4),
                    Text('Payments are secured by Paystack',
                        style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary)),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  String _payLabel(String method, double total) {
    final amount = 'R${total.toStringAsFixed(2)}';
    if (method == OrderService.cashOnDelivery) return 'Place order · pay $amount on delivery';
    if (method == OrderService.wallet) return 'Pay $amount from wallet';
    return _pendingCheckoutUrl != null ? 'Continue to payment' : 'Pay $amount';
  }

  // ── Building blocks ──────────────────────────────────────────────────────

  Widget _sectionTitle(String text) => Padding(
        padding: const EdgeInsets.only(left: 4, bottom: 10),
        child: Text(text, style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w800)),
      );

  Widget _card({required Widget child, EdgeInsets padding = const EdgeInsets.all(16)}) => Container(
        padding: padding,
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(20),
          boxShadow: [BoxShadow(color: AppColors.primary.withValues(alpha: 0.06), blurRadius: 18, offset: const Offset(0, 6))],
        ),
        child: child,
      );

  Widget _divider() => Divider(height: 1, color: AppColors.divider.withValues(alpha: 0.8));

  Widget _addressCard() {
    final pickup = _deliveryOption == 'pickup';
    final profile = _profile;
    return _card(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _radioDot(true),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  pickup ? 'Collect from suppliers' : (profile?.shopName.isNotEmpty == true ? profile!.shopName : 'Shop address'),
                  style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: 4),
                if (pickup)
                  Text('You collect each supplier\'s order yourself. No delivery fee.',
                      style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary))
                else if (profile == null)
                  Text('Loading your address…', style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary))
                else ...[
                  if (profile.phone.isNotEmpty)
                    Text(profile.phone, style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary)),
                  Text(
                    profile.address.isEmpty ? 'No address yet: add it in your Profile' : profile.address,
                    style: AppTextStyles.caption.copyWith(
                      color: profile.address.isEmpty ? AppColors.error : AppColors.textSecondary,
                    ),
                  ),
                ],
              ],
            ),
          ),
          if (!pickup)
            IconButton(
              tooltip: 'Change address',
              icon: const Icon(Icons.edit_outlined, size: 20, color: AppColors.textSecondary),
              onPressed: () => ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Change your delivery address in Profile → Shop details.')),
              ),
            ),
        ],
      ),
    );
  }

  Widget _deliveryChoices(CartProvider cart) {
    final options = [
      ('standard', Icons.local_shipping_outlined, 'Standard', '2–3 days'),
      ('express', Icons.bolt_rounded, 'Express', 'Same day'),
      ('pickup', Icons.storefront_outlined, 'Pickup', 'Collect'),
    ];
    return Row(
      children: [
        for (final (id, icon, title, detail) in options) ...[
          Expanded(
            child: GestureDetector(
              onTap: () => setState(() => _deliveryOption = id),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 6),
                decoration: BoxDecoration(
                  color: _deliveryOption == id ? AppColors.primary : AppColors.surface,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: _deliveryOption == id ? AppColors.primary : AppColors.divider),
                ),
                child: Column(
                  children: [
                    Icon(icon, size: 20, color: _deliveryOption == id ? Colors.white : AppColors.primary),
                    const SizedBox(height: 4),
                    Text(title,
                        style: AppTextStyles.caption.copyWith(
                          fontWeight: FontWeight.w700,
                          color: _deliveryOption == id ? Colors.white : AppColors.textPrimary,
                        )),
                    Text(
                      cart.deliveryFee(id) == 0 ? detail : 'R${cart.deliveryFee(id).toStringAsFixed(0)}',
                      style: AppTextStyles.caption.copyWith(
                        fontSize: 11,
                        color: _deliveryOption == id ? Colors.white70 : AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          if (id != 'pickup') const SizedBox(width: 10),
        ],
      ],
    );
  }

  Widget _paymentRow({
    required String method,
    required bool selected,
    required IconData icon,
    required Color iconColor,
    required String title,
    required String subtitle,
    bool enabled = true,
  }) {
    return Opacity(
      opacity: enabled ? 1 : 0.45,
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: enabled ? () => _choosePayment(method) : null,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: Row(
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: iconColor.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(icon, color: iconColor, size: 22),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700)),
                    const SizedBox(height: 2),
                    Text(subtitle, style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary)),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              _radioDot(selected),
            ],
          ),
        ),
      ),
    );
  }

  Widget _radioDot(bool selected) => AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        width: 22,
        height: 22,
        margin: const EdgeInsets.only(top: 2),
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          border: Border.all(color: selected ? AppColors.primary : AppColors.divider, width: selected ? 6.5 : 2),
        ),
      );

  Widget _amountRow(String label, double amount) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          children: [
            Expanded(child: Text(label, style: AppTextStyles.body.copyWith(color: AppColors.textSecondary))),
            Text(
              amount == 0 ? 'Free' : 'R${amount.toStringAsFixed(2)}',
              style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
            ),
          ],
        ),
      );

  // ── Paying ───────────────────────────────────────────────────────────────

  Future<void> _placeOrder(CartProvider cart) async {
    if (cart.isEmpty) return;
    setState(() => _placing = true);
    try {
      final profile = _profile ?? await ProfileService.getProfile();
      final deliveryAddress = profile.address.trim();
      if (_deliveryOption != 'pickup' && deliveryAddress.isEmpty) {
        throw StateError('Add a delivery address to your shop profile before ordering.');
      }

      final method = _effectiveMethod(cart);
      final total = cart.total(_deliveryOption);
      if (method == OrderService.wallet || method == OrderService.cashOnDelivery) {
        final placed = await OrderService.placeOrder(
          items: cart.items.toList(),
          deliveryOption: _deliveryOption,
          deliveryAddress: deliveryAddress,
          paymentMethod: method,
        );
        cart.clear();
        if (!mounted) return;
        _showSuccess(OrderSuccess(
          orderNumber: placed.orderNumber,
          paymentMethod: method,
          total: placed.totalAmount > 0 ? placed.totalAmount : total,
        ));
        return;
      }

      final checkout = _pendingCheckoutUrl == null
          ? await OrderService.placeOrder(
              items: cart.items.toList(),
              deliveryOption: _deliveryOption,
              deliveryAddress: deliveryAddress,
            )
          : OrderCheckout(orderNumber: _pendingOrderNumber ?? '', checkoutUrl: _pendingCheckoutUrl!);
      _pendingCheckoutUrl = checkout.checkoutUrl;
      _pendingOrderNumber = checkout.orderNumber;
      if (!mounted) return;
      final paid = await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => OnboardingFeeCheckoutScreen(checkoutUrl: checkout.checkoutUrl!, title: 'Pay for your order'),
        ),
      );
      // Confirm with Paystack straight away, also when the page was closed before it redirected back.
      final confirmed = await OrderService.confirmPayment(checkout.orderNumber);
      if (paid != true && !confirmed) return;
      cart.clear();
      _pendingCheckoutUrl = null;
      _pendingOrderNumber = null;
      if (!mounted) return;
      _showSuccess(OrderSuccess(
        orderNumber: checkout.orderNumber,
        paymentMethod: OrderService.paystack,
        total: total,
        confirmed: confirmed,
      ));
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e is ApiException
              ? e.message
              : e.toString().replaceFirst(RegExp(r'^(Exception|Bad state): '), '')),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          duration: const Duration(seconds: 5),
        ),
      );
    } finally {
      if (mounted) setState(() => _placing = false);
    }
  }

  void _showSuccess(OrderSuccess success) {
    Navigator.pushReplacement(
      context,
      MaterialPageRoute(builder: (_) => OrderSuccessScreen(success: success)),
    );
  }
}
