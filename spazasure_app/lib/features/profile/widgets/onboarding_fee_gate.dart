import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/brand_pattern.dart';
import 'package:spazasure_app/core/widgets/motion.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';
import 'package:spazasure_app/services/profile_service.dart';

/// Group buying is for onboarded shops: before a shop joins its first group buy
/// it pays the once-off onboarding fee (R150 unless an admin changes it).
class OnboardingFeeGate {
  /// True when the shop can go on: the fee is paid, there is none, or the
  /// status couldn't be checked (the server still refuses an unpaid join).
  static Future<bool> ensurePaid(BuildContext context) async {
    OnboardingPaymentStatus status;
    try {
      status = await ProfileService.getOnboardingPaymentStatus();
    } catch (_) {
      return true;
    }
    if (status.paid || status.amount <= 0) return true;
    if (!context.mounted) return false;

    final pay = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _FeeSheet(amount: status.amount),
    );
    if (pay != true || !context.mounted) return false;
    return _pay(context);
  }

  static Future<bool> _pay(BuildContext context) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      final url = await ProfileService.initiatePaystackOnboardingPayment();
      if (!context.mounted) return false;
      if (url == null || url.isEmpty) {
        // Nothing to pay after all (already paid, or the fee was removed)
        return true;
      }
      await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => OnboardingFeeCheckoutScreen(
            checkoutUrl: url,
            title: 'Pay onboarding fee',
          ),
        ),
      );
      // Paystack tells SpazaSure a moment after the payment, so check a few times
      for (var i = 0; i < 5; i++) {
        final status = await ProfileService.getOnboardingPaymentStatus();
        if (status.paid) {
          messenger.showSnackBar(
            const SnackBar(
              content: Text('Onboarding fee paid. You can buy in groups now!'),
              backgroundColor: AppColors.success,
            ),
          );
          return true;
        }
        await Future.delayed(const Duration(seconds: 2));
      }
      messenger.showSnackBar(
        const SnackBar(
          content: Text(
            'We are still confirming your payment. Try joining again in a minute.',
          ),
          backgroundColor: AppColors.warning,
        ),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(
          content: Text(error.toString()),
          backgroundColor: AppColors.error,
        ),
      );
    }
    return false;
  }
}

class _FeeSheet extends StatelessWidget {
  final double amount;
  const _FeeSheet({required this.amount});

  String get _rand =>
      'R${amount.toStringAsFixed(amount == amount.roundToDouble() ? 0 : 2)}';

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.all(12),
      padding: EdgeInsets.fromLTRB(
        22,
        0,
        22,
        20 + MediaQuery.of(context).padding.bottom,
      ),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(32),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // A brand card peeking over the top, like the welcome pages
          Center(
            child: Transform.translate(
              offset: const Offset(0, -28),
              child: Container(
                width: 96,
                height: 96,
                clipBehavior: Clip.antiAlias,
                decoration: BoxDecoration(
                  color: AppColors.secondary,
                  borderRadius: BorderRadius.circular(26),
                  border: Border.all(color: Colors.white, width: 4),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.18),
                      blurRadius: 18,
                      offset: const Offset(0, 8),
                    ),
                  ],
                ),
                child: Stack(
                  children: [
                    const Positioned.fill(
                      child: CustomPaint(
                        painter: BrandPattern([
                          AppColors.accent,
                          AppColors.primaryDark,
                          Colors.white,
                        ]),
                      ),
                    ),
                    Center(
                      child: Container(
                        width: 48,
                        height: 48,
                        decoration: const BoxDecoration(
                          color: Colors.white,
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(
                          Icons.groups_rounded,
                          color: AppColors.secondary,
                          size: 28,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          Text(
            'Start buying in groups',
            style: GoogleFonts.nunito(
              fontSize: 23,
              fontWeight: FontWeight.w900,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Group buys are for onboarded SpazaSure shops. Pay the once-off $_rand onboarding fee to join this group buy and every one after it.',
            style: GoogleFonts.nunito(
              fontSize: 14,
              height: 1.5,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 14),
          for (final point in const [
            'Bulk prices when shops order together',
            'Paid once, never again',
            'Secure payment with Paystack',
          ])
            Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: Row(
                children: [
                  const Icon(
                    Icons.check_circle_rounded,
                    color: AppColors.secondary,
                    size: 18,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      point,
                      style: GoogleFonts.nunito(
                        fontSize: 13.5,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 16),
          Pressable(
            onTap: () => Navigator.pop(context, true),
            child: Container(
              height: 56,
              width: double.infinity,
              decoration: BoxDecoration(
                color: AppColors.textPrimary,
                borderRadius: BorderRadius.circular(28),
              ),
              child: Center(
                child: Text(
                  'Pay $_rand onboarding fee',
                  style: GoogleFonts.nunito(
                    color: Colors.white,
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ),
          ),
          const SizedBox(height: 6),
          Center(
            child: TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Not now'),
            ),
          ),
        ],
      ),
    );
  }
}
