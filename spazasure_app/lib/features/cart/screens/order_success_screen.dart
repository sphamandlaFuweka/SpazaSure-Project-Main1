import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/order_service.dart';

/// What was just ordered and how it was paid, for the thank-you screen.
class OrderSuccess {
  final String orderNumber;
  final String paymentMethod;
  final double total;
  /// Card payments: whether Paystack has already confirmed it (otherwise the webhook will shortly).
  final bool confirmed;

  const OrderSuccess({
    required this.orderNumber,
    required this.paymentMethod,
    required this.total,
    this.confirmed = false,
  });
}

/// "Thank you for your order", with the order number and what happens next.
class OrderSuccessScreen extends StatelessWidget {
  final OrderSuccess success;

  const OrderSuccessScreen({super.key, required this.success});

  String get _message => switch (success.paymentMethod) {
        OrderService.cashOnDelivery =>
          'Your supplier has been notified. Have R${success.total.toStringAsFixed(2)} in cash ready when your order arrives, then confirm delivery in Orders.',
        OrderService.wallet =>
          'Paid from your SpazaSure wallet. Your supplier has been notified and is preparing your order.',
        _ => success.confirmed
            ? 'Payment confirmed. Your supplier has been notified and will start preparing your order.'
            : 'We received your payment and will let your supplier know as soon as Paystack confirms it.',
      };

  void _goHome(BuildContext context) => Navigator.of(context).pushNamedAndRemoveUntil('/home', (_) => false);

  void _trackOrder(BuildContext context) {
    final navigator = Navigator.of(context);
    navigator.pushNamedAndRemoveUntil('/home', (_) => false);
    navigator.pushNamed('/orders');
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _goHome(context);
      },
      child: Scaffold(
        backgroundColor: AppColors.surface,
        body: SafeArea(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 28),
            child: Column(
              children: [
                const Spacer(flex: 2),
                Container(
                  width: 112,
                  height: 112,
                  decoration: BoxDecoration(
                    color: AppColors.primary,
                    shape: BoxShape.circle,
                    boxShadow: [
                      BoxShadow(color: AppColors.primary.withValues(alpha: 0.3), blurRadius: 30, offset: const Offset(0, 12)),
                    ],
                  ),
                  child: const Icon(Icons.check_rounded, color: Colors.white, size: 64),
                ).animate().scale(duration: 450.ms, curve: Curves.easeOutBack),
                const SizedBox(height: 32),
                Text(
                  success.paymentMethod == OrderService.cashOnDelivery ? 'Order placed!' : 'Thank you for your order',
                  style: AppTextStyles.h2.copyWith(fontWeight: FontWeight.w800),
                  textAlign: TextAlign.center,
                ).animate().fadeIn(delay: 150.ms),
                const SizedBox(height: 12),
                if (success.orderNumber.isNotEmpty)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Text(
                      success.orderNumber,
                      style: AppTextStyles.body.copyWith(color: AppColors.primary, fontWeight: FontWeight.w700),
                    ),
                  ).animate().fadeIn(delay: 220.ms),
                const SizedBox(height: 16),
                Text(
                  _message,
                  style: AppTextStyles.body.copyWith(color: AppColors.textSecondary, height: 1.5),
                  textAlign: TextAlign.center,
                ).animate().fadeIn(delay: 280.ms),
                const Spacer(flex: 3),
                SizedBox(
                  width: double.infinity,
                  height: 56,
                  child: FilledButton(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
                    ),
                    onPressed: () => _trackOrder(context),
                    child: const Text('Track your order', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
                  ),
                ),
                const SizedBox(height: 8),
                TextButton(
                  onPressed: () => _goHome(context),
                  child: const Text('Back to home'),
                ),
                const SizedBox(height: 16),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
