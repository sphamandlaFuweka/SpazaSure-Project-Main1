import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';
import 'package:spazasure_app/services/customer_subscription_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';
import 'package:spazasure_app/features/customer/screens/bulk_shop_screen.dart';

class CustomerSubscriptionScreen extends StatefulWidget {
  const CustomerSubscriptionScreen({super.key});

  @override
  State<CustomerSubscriptionScreen> createState() =>
      _CustomerSubscriptionScreenState();
}

class _CustomerSubscriptionScreenState
    extends State<CustomerSubscriptionScreen> {
  CustomerSubscriptionOverview? _overview;
  Object? _loadError;
  bool _loading = true;
  bool _working = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      var overview = await CustomerSubscriptionService.getOverview();
      // A payment left pending may already have gone through on Paystack
      if (overview.status == 'pending') {
        overview = await CustomerSubscriptionService.verifyPayment();
      }
      if (mounted) setState(() => _overview = overview);
    } catch (error) {
      if (mounted) setState(() => _loadError = error);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  static String _planName(String key) =>
      key == 'bulk' ? 'Bulk Buyer' : key == 'plus' ? 'SpazaSure Plus' : 'SpazaSure Free';

  Future<void> _checkout(String planKey) async {
    setState(() => _working = true);
    try {
      final url = await CustomerSubscriptionService.createCheckout(planKey);
      if (!mounted) return;
      if (url != null) {
        await Navigator.push<bool>(
          context,
          MaterialPageRoute(
            builder: (_) => OnboardingFeeCheckoutScreen(
              checkoutUrl: url,
              title: 'Subscribe to ${_planName(planKey)}',
            ),
          ),
        );
      }
      // Checked even when the page was closed by hand, since the payment may still have gone through
      final overview = await CustomerSubscriptionService.verifyPayment();
      if (!mounted) return;
      setState(() => _overview = overview);
      if (planKey == 'bulk' && overview.isBulkActive) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Welcome to Bulk Buyer! The bulk marketplace is unlocked.'),
            backgroundColor: AppColors.success,
          ),
        );
        Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => const BulkShopScreen()),
        );
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            overview.isPlusActive
                ? 'Welcome to SpazaSure Plus! Your Plus features are unlocked.'
                : overview.status == 'pending'
                ? 'Payment not confirmed yet. If you paid, it will show here shortly.'
                : 'Your membership has been updated.',
          ),
          backgroundColor: overview.isPlusActive
              ? AppColors.success
              : AppColors.textPrimary,
        ),
      );
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not start checkout: $error'),
            backgroundColor: AppColors.error,
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _working = false);
    }
  }

  Future<void> _cancel() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('Cancel ${_planName(_overview?.currentPlan ?? 'plus')}?'),
        content: const Text(
          'Cancelling ends your membership benefits immediately and stops future renewals. '
          'Orders you already placed are not affected.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Keep subscription'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Cancel subscription'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _working = true);
    try {
      await CustomerSubscriptionService.cancelSubscription();
      await _load();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Your subscription has been cancelled immediately.'),
            backgroundColor: AppColors.success,
          ),
        );
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not cancel subscription: $error'),
            backgroundColor: AppColors.error,
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _working = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: BrandAppBar(title: const Text('Membership')),
    body: _loading && _overview == null
        ? const Center(child: CircularProgressIndicator())
        : _loadError != null && _overview == null
        ? Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('Could not load plans: $_loadError'),
                  const SizedBox(height: 12),
                  OutlinedButton(onPressed: _load, child: const Text('Retry')),
                ],
              ),
            ),
          )
        : RefreshIndicator(
            onRefresh: _load,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                if (_overview case final overview?)
                  Card(
                    child: ListTile(
                      leading: Icon(
                        overview.isPlusActive
                            ? Icons.workspace_premium_rounded
                            : Icons.verified_user_outlined,
                        color: AppColors.primary,
                      ),
                      title: Text(
                        _planName(overview.currentPlan),
                        style: AppTextStyles.subtitle,
                      ),
                      trailing: overview.isBulkActive
                          ? TextButton(
                              onPressed: () => Navigator.push(
                                context,
                                MaterialPageRoute(
                                  builder: (_) => const BulkShopScreen(),
                                ),
                              ),
                              child: const Text('Shop bulk'),
                            )
                          : null,
                      subtitle: Text(
                        _statusLabel(overview),
                        style: AppTextStyles.bodySmall,
                      ),
                    ),
                  ),
                const SizedBox(height: 8),
                for (final plan in _overview?.plans ?? const [])
                  _planCard(plan, _overview!),
                if (_loadError != null) ...[
                  const SizedBox(height: 8),
                  Text(
                    'Could not refresh payment status: $_loadError',
                    style: AppTextStyles.bodySmall.copyWith(
                      color: AppColors.error,
                    ),
                  ),
                ],
              ],
            ),
          ),
  );

  Widget _planCard(
    CustomerSubscriptionPlan plan,
    CustomerSubscriptionOverview overview,
  ) {
    final isCurrent = overview.currentPlan == plan.key;
    final isPaid = plan.key == 'plus' || plan.key == 'bulk';
    final isPlus = plan.key == 'plus';
    final isBulk = plan.key == 'bulk';
    final isFamily = plan.key == 'family';
    // Bulk Buyer includes Plus, so Plus can't be bought on top of it
    final includedInBulk = isPlus && overview.isBulkActive;
    final canCancel =
        isPaid &&
        isCurrent &&
        overview.autoRenews &&
        !overview.cancelAtPeriodEnd &&
        const {'active', 'past_due', 'unpaid'}.contains(overview.status);
    final activeOnceOff = isCurrent &&
        isPaid &&
        overview.isPlusActive &&
        !overview.autoRenews;
    final upgrade = isBulk && overview.currentPlan == 'plus' && overview.isPlusActive;

    return Card(
      margin: const EdgeInsets.symmetric(vertical: 8),
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(child: Text(plan.name, style: AppTextStyles.h3)),
                if (isPlus && !includedInBulk && !isCurrent)
                  const Chip(
                    label: Text('Recommended'),
                    visualDensity: VisualDensity.compact,
                  ),
                if (isCurrent)
                  const Chip(
                    label: Text('Current'),
                    visualDensity: VisualDensity.compact,
                  ),
                if (includedInBulk)
                  const Chip(
                    label: Text('Included'),
                    visualDensity: VisualDensity.compact,
                  ),
                if (isFamily && !plan.isAvailable)
                  const Chip(
                    label: Text('Coming soon'),
                    visualDensity: VisualDensity.compact,
                  ),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              plan.monthlyPrice == 0
                  ? 'Free forever'
                  : 'R${plan.monthlyPrice.toStringAsFixed(2)} / month',
              style: AppTextStyles.price,
            ),
            const SizedBox(height: 8),
            Text(plan.description, style: AppTextStyles.body),
            const SizedBox(height: 12),
            for (final feature in plan.features)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 3),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(
                      Icons.check_circle_outline_rounded,
                      size: 18,
                      color: AppColors.primary,
                    ),
                    const SizedBox(width: 8),
                    Expanded(child: Text(feature)),
                  ],
                ),
              ),
            const SizedBox(height: 8),
            if (includedInBulk)
              Text(
                'Your Bulk Buyer plan includes every Plus feature.',
                style: AppTextStyles.bodySmall,
              )
            else if (canCancel)
              SizedBox(
                width: double.infinity,
                child: OutlinedButton(
                  onPressed: _working ? null : _cancel,
                  child: const Text('Cancel subscription'),
                ),
              )
            else if (isPaid && isCurrent && overview.cancelAtPeriodEnd)
              Text(
                'Ends ${_dateLabel(overview.currentPeriodEnd)}',
                style: AppTextStyles.bodySmall,
              )
            else if (activeOnceOff)
              Text(
                'Paid until ${_dateLabel(overview.currentPeriodEnd)}. '
                'It does not renew by itself, so pay again here when it ends.',
                style: AppTextStyles.bodySmall,
              )
            else if (isPaid && isCurrent && overview.status == 'pending')
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _working ? null : () => _checkout(plan.key),
                  child: Text(
                    _working ? 'Please wait...' : 'Continue checkout',
                  ),
                ),
              )
            else if (isPaid && (!isCurrent || overview.status == 'expired'))
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _working ? null : () => _checkout(plan.key),
                  child: Text(
                    _working
                        ? 'Please wait...'
                        : upgrade
                        ? 'Upgrade to Bulk Buyer'
                        : isCurrent && overview.status == 'expired'
                        ? 'Renew ${_planName(plan.key)}'
                        : 'Choose ${_planName(plan.key)}',
                  ),
                ),
              ),
            if (upgrade)
              Padding(
                padding: const EdgeInsets.only(top: 6),
                child: Text(
                  'Your Plus subscription stops once Bulk Buyer is paid, so you are never billed for both.',
                  style: AppTextStyles.bodySmall,
                ),
              ),
          ],
        ),
      ),
    );
  }

  String _statusLabel(CustomerSubscriptionOverview overview) {
    if (overview.isPlusActive &&
        (overview.cancelAtPeriodEnd || !overview.autoRenews)) {
      return 'Active until ${_dateLabel(overview.currentPeriodEnd)}';
    }
    if (overview.isPlusActive) return 'Active';
    if (overview.status == 'pending') return 'Payment pending';
    if (overview.status == 'past_due' || overview.status == 'unpaid') {
      return 'Payment needs attention';
    }
    if (overview.status == 'expired') {
      return '${_planName(overview.currentPlan)} has ended. Renew to unlock it again';
    }
    return 'Free plan';
  }

  String _dateLabel(DateTime? date) {
    if (date == null) return 'the end of this billing period';
    return '${date.day}/${date.month}/${date.year}';
  }
}
