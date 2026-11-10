import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';
import 'package:spazasure_app/features/cart/screens/cart_screen.dart';
import 'package:spazasure_app/features/customer/screens/bulk_address_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_subscription_screen.dart';
import 'package:spazasure_app/features/marketplace/screens/marketplace_screen.dart';
import 'package:spazasure_app/features/orders/screens/orders_screen.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/providers/product_provider.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/customer_bulk_service.dart';

/// Bulk buying for shoppers on the Bulk Buyer plan: the supplier marketplace at bulk prices,
/// orders from R1,000 per supplier, paid by card, delivered or collected.
class BulkShopScreen extends StatefulWidget {
  const BulkShopScreen({super.key});

  @override
  State<BulkShopScreen> createState() => _BulkShopScreenState();
}

class _BulkShopScreenState extends State<BulkShopScreen> {
  BulkAccount? _account;
  Object? _error;
  bool _loading = true;
  bool _catalogueLoaded = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final account = await CustomerBulkService.getAccount();
      if (!mounted) return;
      setState(() => _account = account);
      if (account.isBulkActive && account.address != null) await _loadCatalogue();
    } catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  /// Categories and suppliers depend on the address (who delivers there), so reload them.
  Future<void> _loadCatalogue() async {
    await context.read<ProductProvider>().refreshAll();
    if (mounted) setState(() => _catalogueLoaded = true);
  }

  Future<void> _openMembership() async {
    await Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => const CustomerSubscriptionScreen()),
    );
    if (mounted) _load();
  }

  Future<void> _editAddress() async {
    final saved = await Navigator.push<BulkAddress>(
      context,
      MaterialPageRoute(
        builder: (_) => BulkAddressScreen(current: _account?.address),
      ),
    );
    if (saved == null || !mounted) return;
    // Suppliers and prices may differ at the new address; start the cart afresh
    context.read<CartProvider>().clear();
    setState(() => _catalogueLoaded = false);
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    final account = _account;
    if (_loading && account == null) {
      return _plain(const Center(child: CircularProgressIndicator()));
    }
    if (account == null) {
      return _plain(
        Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  _error is ApiException
                      ? (_error as ApiException).message
                      : 'Could not load bulk buying. Check your connection.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 12),
                OutlinedButton(onPressed: _load, child: const Text('Retry')),
              ],
            ),
          ),
        ),
      );
    }
    if (!account.isBulkActive) return _plain(_upsell(account));
    if (account.address == null) return _plain(_askAddress(account));
    if (!_catalogueLoaded) {
      return _plain(const Center(child: CircularProgressIndicator()));
    }

    final cartCount = context.watch<CartProvider>().badgeCount;
    return MarketplaceScreen(
      title: 'Bulk buying',
      header: _addressHeader(account),
      extraActions: [
        IconButton(
          tooltip: 'My bulk orders',
          icon: const Icon(Icons.receipt_long_outlined),
          onPressed: () => Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => const OrdersScreen()),
          ),
        ),
        IconButton(
          tooltip: 'Cart',
          icon: Badge(
            isLabelVisible: cartCount > 0,
            label: Text('$cartCount'),
            child: const Icon(Icons.shopping_cart_outlined),
          ),
          onPressed: () => Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => const CartScreen()),
          ),
        ),
      ],
    );
  }

  Widget _plain(Widget body) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: BrandAppBar(title: const Text('Bulk buying')),
    body: body,
  );

  Widget _addressHeader(BulkAccount account) => Material(
    color: AppColors.primary.withValues(alpha: 0.06),
    child: InkWell(
      onTap: _editAddress,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 10, 8, 10),
        child: Row(
          children: [
            const Icon(Icons.location_on_outlined, size: 20, color: AppColors.primary),
            const SizedBox(width: 8),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    account.address!.fullLine,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600),
                  ),
                  Text(
                    'Minimum R${account.minOrderAmount.toStringAsFixed(0)} per supplier · delivery or collection',
                    style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
                  ),
                ],
              ),
            ),
            const Icon(Icons.edit_outlined, size: 18, color: AppColors.textSecondary),
          ],
        ),
      ),
    ),
  );

  Widget _upsell(BulkAccount account) => ListView(
    padding: const EdgeInsets.all(20),
    children: [
      const Icon(Icons.inventory_2_outlined, size: 56, color: AppColors.primary),
      const SizedBox(height: 12),
      Text('Buy in bulk, straight from suppliers', style: AppTextStyles.h2, textAlign: TextAlign.center),
      const SizedBox(height: 8),
      Text(
        'With the Bulk Buyer plan you shop the same verified suppliers that spaza shops buy from.',
        style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
        textAlign: TextAlign.center,
      ),
      const SizedBox(height: 20),
      for (final line in [
        'R${account.monthlyPrice.toStringAsFixed(0)} a month, cancel any time',
        'Orders from R${account.minOrderAmount.toStringAsFixed(0)} per supplier',
        'Delivered to your door, or collect from the supplier',
        'Pay safely by card with Paystack',
        'Includes every SpazaSure Plus feature',
      ])
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 5),
          child: Row(
            children: [
              const Icon(Icons.check_circle_outline_rounded, size: 20, color: AppColors.primary),
              const SizedBox(width: 10),
              Expanded(child: Text(line, style: AppTextStyles.body)),
            ],
          ),
        ),
      const SizedBox(height: 24),
      FilledButton(
        onPressed: _openMembership,
        child: Text('Get Bulk Buyer for R${account.monthlyPrice.toStringAsFixed(0)}/month'),
      ),
    ],
  );

  Widget _askAddress(BulkAccount account) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.home_work_outlined, size: 56, color: AppColors.primary),
          const SizedBox(height: 12),
          Text('Where do your orders go?', style: AppTextStyles.h3, textAlign: TextAlign.center),
          const SizedBox(height: 8),
          Text(
            'Add your address so we can show the suppliers that deliver to you.',
            style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 20),
          FilledButton(onPressed: _editAddress, child: const Text('Add my address')),
        ],
      ),
    ),
  );
}
