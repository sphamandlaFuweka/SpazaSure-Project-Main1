import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/screens/customer_subscription_screen.dart';
import 'package:spazasure_app/services/customer_subscription_service.dart';

/// Opens a SpazaSure Plus screen for members, and offers Plus to everyone else.
class PlusGate {
  static Future<void> open(
    BuildContext context, {
    required String feature,
    required WidgetBuilder builder,
  }) async {
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    bool active;
    try {
      active = (await CustomerSubscriptionService.getOverview()).isPlusActive;
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(
          content: Text('Could not check your membership: $error'),
          backgroundColor: AppColors.error,
        ),
      );
      return;
    }
    if (!active) {
      if (!context.mounted) return;
      final upgrade = await showModalBottomSheet<bool>(
        context: context,
        showDragHandle: true,
        builder: (sheetContext) => _UpsellSheet(feature: feature),
      );
      if (upgrade != true) return;
      await navigator.push(
        MaterialPageRoute(builder: (_) => const CustomerSubscriptionScreen()),
      );
      // Straight into the feature once they've joined
      try {
        active = (await CustomerSubscriptionService.getOverview()).isPlusActive;
      } catch (_) {
        return;
      }
      if (!active) return;
    }
    await navigator.push(MaterialPageRoute(builder: builder));
  }
}

class _UpsellSheet extends StatelessWidget {
  final String feature;

  const _UpsellSheet({required this.feature});

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.fromLTRB(24, 0, 24, 20),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 64,
            height: 64,
            decoration: const BoxDecoration(
              color: AppColors.primaryTint,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.workspace_premium_rounded,
              color: AppColors.primary,
              size: 34,
            ),
          ),
          const SizedBox(height: 14),
          Text(
            '$feature is a Plus feature',
            textAlign: TextAlign.center,
            style: AppTextStyles.h3,
          ),
          const SizedBox(height: 6),
          Text(
            'Join SpazaSure Plus for R19.99 a month to unlock scan history, '
            'rewards and more.',
            textAlign: TextAlign.center,
            style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 20),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Get SpazaSure Plus'),
            ),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Not now'),
          ),
        ],
      ),
    ),
  );
}
