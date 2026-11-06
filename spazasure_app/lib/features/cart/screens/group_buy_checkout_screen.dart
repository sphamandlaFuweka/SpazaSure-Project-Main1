import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/services/group_buy_service.dart';
import 'package:spazasure_app/services/profile_service.dart';

/// Checkout for the group buy in the cart: where it's delivered, what delivery costs, and how
/// the shop pays. The shop only joins the deal once it pays. Closes with true when it's done.
class GroupBuyCheckoutScreen extends StatefulWidget {
  final GroupBuyCartEntry entry;

  const GroupBuyCheckoutScreen({super.key, required this.entry});

  @override
  State<GroupBuyCheckoutScreen> createState() => _GroupBuyCheckoutScreenState();
}

class _GroupBuyCheckoutScreenState extends State<GroupBuyCheckoutScreen> {
  late GroupBuyQuote _quote = widget.entry.quote;
  ShopProfile? _profile;
  String? _error;
  bool _quoting = true;
  bool _paying = false;

  @override
  void initState() {
    super.initState();
    _refreshQuote();
    ProfileService.getProfile()
        .then((profile) {
          if (mounted) setState(() => _profile = profile);
        })
        .catchError((_) {});
  }

  /// Other shops may have joined since this was added to the cart, so price it again.
  Future<void> _refreshQuote() async {
    try {
      final quote = await GroupBuyService.quote(
        widget.entry.group.id,
        widget.entry.items,
      );
      if (mounted) setState(() => _quote = quote);
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _quoting = false);
    }
  }

  void _showMessage(String message, Color color) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: color,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _finish(String message, Color color) {
    _showMessage(message, color);
    context.read<CartProvider>().clearGroupBuy();
    Navigator.pop(context, true);
  }

  Future<void> _pay(String method) async {
    final group = widget.entry.group;
    setState(() {
      _paying = true;
      _error = null;
    });
    try {
      final result = await GroupBuyService.join(
        group.id,
        widget.entry.items,
        method,
      );
      if (!mounted) return;
      if (result.checkoutUrl == null) {
        _finish(
          result.message ?? 'You joined the group buy!',
          AppColors.success,
        );
        return;
      }
      await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => OnboardingFeeCheckoutScreen(
            checkoutUrl: result.checkoutUrl!,
            title: 'Pay for group buy',
          ),
        ),
      );
      if (!mounted) return;
      // Paystack tells SpazaSure a moment after the payment, so check a few times
      var status = 'pending';
      for (var i = 0; i < 5 && status == 'pending'; i++) {
        status = await GroupBuyService.confirmPayment(group.id);
        if (status == 'pending') {
          await Future.delayed(const Duration(seconds: 2));
        }
      }
      if (!mounted) return;
      switch (status) {
        case 'joined':
          _finish(
            'Payment received. You joined the group buy!',
            AppColors.success,
          );
        case 'refunded':
          _finish(
            'The group buy filled up while you paid, so the money is in your wallet.',
            AppColors.warning,
          );
        default:
          setState(() => _paying = false);
          _showMessage(
            'Payment not confirmed yet. If you paid, you will be added shortly; '
            'otherwise tap Pay to try again.',
            AppColors.warning,
          );
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _paying = false;
          _error = error.toString();
        });
      }
    }
  }

  static String _rand(double amount) => 'R${amount.toStringAsFixed(2)}';

  @override
  Widget build(BuildContext context) {
    final group = widget.entry.group;
    final quote = _quote;
    final canPay = !_quoting && !_paying && _error == null;
    final profile = _profile;
    final address = profile == null
        ? null
        : [
            profile.address,
            profile.city,
            profile.province,
          ].where((part) => part.isNotEmpty).join(', ');

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(title: const Text('Group buy checkout')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          _section('Delivery to', [
            Text(
              profile?.shopName ?? 'Your shop',
              style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 2),
            Text(
              address == null || address.isEmpty
                  ? 'The address saved on your shop profile'
                  : address,
              style: AppTextStyles.bodySmall,
            ),
            const SizedBox(height: 6),
            Text(
              'To deliver somewhere else, change the address in your profile.',
              style: AppTextStyles.caption,
            ),
          ]),
          const SizedBox(height: 12),
          _section(group.title, [
            for (final line in quote.lines)
              _row(
                '${line.quantity} x ${line.productName}',
                _rand(line.lineTotal),
              ),
            const Divider(height: 20),
            _row('Items', _rand(quote.itemsTotal)),
            _row(
              'Delivery',
              _quoting ? 'Working out…' : _rand(quote.deliveryFee),
            ),
            const Divider(height: 20),
            _row('Total', _rand(quote.total), bold: true),
          ]),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(
              _error!,
              style: AppTextStyles.bodySmall.copyWith(color: AppColors.error),
            ),
          ],
          const SizedBox(height: 16),
          ElevatedButton.icon(
            onPressed: canPay && quote.walletBalance >= quote.total
                ? () => _pay('wallet')
                : null,
            icon: const Icon(Icons.account_balance_wallet_outlined),
            label: Text('Pay from wallet (${_rand(quote.walletBalance)})'),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: canPay ? () => _pay('paystack') : null,
            icon: const Icon(Icons.credit_card),
            label: Text('Pay ${_rand(quote.total)} by card'),
          ),
          const SizedBox(height: 8),
          Text(
            'Payment is final. If a product doesn\'t reach its target, its money goes back to your wallet.',
            style: AppTextStyles.caption,
            textAlign: TextAlign.center,
          ),
        ],
      ),
    );
  }

  Widget _section(String title, List<Widget> children) => Container(
    padding: const EdgeInsets.all(16),
    decoration: BoxDecoration(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(16),
      border: Border.all(color: AppColors.divider),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: AppTextStyles.subtitle),
        const SizedBox(height: 10),
        ...children,
      ],
    ),
  );

  Widget _row(String label, String value, {bool bold = false}) {
    final style = bold
        ? AppTextStyles.body.copyWith(fontWeight: FontWeight.w800)
        : AppTextStyles.bodySmall;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(child: Text(label, style: style)),
          const SizedBox(width: 12),
          Text(value, style: style),
        ],
      ),
    );
  }
}
