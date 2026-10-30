import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/services/order_service.dart';

/// For a card order whose payment failed or was abandoned: pay again, or cancel it.
/// Closes the order screen with `true` when something changed, so the list reloads.
class UnpaidOrderActions extends StatefulWidget {
  final Order order;

  const UnpaidOrderActions({super.key, required this.order});

  static bool appliesTo(Order order) =>
      order.paymentMethod == OrderService.paystack &&
      order.paymentStatus != 'paid' &&
      order.paymentStatus != 'released' &&
      order.status == 'pending';

  @override
  State<UnpaidOrderActions> createState() => _UnpaidOrderActionsState();
}

class _UnpaidOrderActionsState extends State<UnpaidOrderActions> {
  bool _busy = false;

  void _done(String message, Color color) {
    final messenger = ScaffoldMessenger.of(context);
    Navigator.pop(context, true);
    messenger.showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: color,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _error(Object error) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(error.toString()),
        backgroundColor: AppColors.error,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  Future<void> _pay() async {
    setState(() => _busy = true);
    try {
      final checkout = await OrderService.payAgain(widget.order.id);
      if (!mounted) return;
      if (checkout.checkoutUrl == null) {
        _done('This order is already paid.', AppColors.success);
        return;
      }
      await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => OnboardingFeeCheckoutScreen(
            checkoutUrl: checkout.checkoutUrl!,
            title: 'Pay for order',
          ),
        ),
      );
      // Paystack tells SpazaSure a moment after the payment, so check a few times
      var paid = false;
      for (var i = 0; i < 4 && !paid; i++) {
        paid = await OrderService.confirmPayment(checkout.orderNumber);
        if (!paid) await Future.delayed(const Duration(seconds: 2));
      }
      if (!mounted) return;
      if (paid) {
        _done(
          'Payment received. Your supplier has been notified.',
          AppColors.success,
        );
      } else {
        setState(() => _busy = false);
        _error('Payment not completed. You can try again or cancel the order.');
      }
    } catch (error) {
      if (!mounted) return;
      setState(() => _busy = false);
      _error(error);
    }
  }

  Future<void> _cancel() async {
    final sure = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Cancel this order?'),
        content: const Text(
          'Nothing was paid, so nothing is charged. You can order again any time.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Keep order'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            style: TextButton.styleFrom(foregroundColor: AppColors.error),
            child: const Text('Cancel order'),
          ),
        ],
      ),
    );
    if (sure != true || !mounted) return;
    setState(() => _busy = true);
    try {
      await OrderService.cancelUnpaid(widget.order.id);
      if (mounted) _done('Order cancelled.', AppColors.info);
    } catch (error) {
      if (!mounted) return;
      setState(() => _busy = false);
      _error(error);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 16),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.warning.withValues(alpha: 0.4)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Payment not completed',
            style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 4),
          Text(
            'The supplier only starts on this order once it is paid.',
            style: AppTextStyles.caption,
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton.icon(
              onPressed: _busy ? null : _pay,
              icon: _busy
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.credit_card),
              label: Text(
                'Continue to payment (R${widget.order.total.toStringAsFixed(2)})',
              ),
            ),
          ),
          const SizedBox(height: 6),
          SizedBox(
            width: double.infinity,
            child: TextButton(
              onPressed: _busy ? null : _cancel,
              style: TextButton.styleFrom(foregroundColor: AppColors.error),
              child: const Text('Cancel order'),
            ),
          ),
        ],
      ),
    );
  }
}
