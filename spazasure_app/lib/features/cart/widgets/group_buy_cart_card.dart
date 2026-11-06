import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/cart/screens/group_buy_checkout_screen.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/services/group_buy_service.dart';

/// The group buy waiting in the cart. It's paid on its own: group prices, delivery and the
/// payment choices differ from a normal order.
class GroupBuyCartCard extends StatelessWidget {
  final GroupBuyCartEntry entry;

  const GroupBuyCartCard({super.key, required this.entry});

  Future<void> _checkout(BuildContext context) async {
    final paid = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => GroupBuyCheckoutScreen(entry: entry)),
    );
    // Opened from the Group Buy screen: go back there. The Cart tab has nothing to go back to.
    if (paid == true && context.mounted && Navigator.canPop(context)) {
      Navigator.pop(context);
    }
  }

  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.fromLTRB(16, 8, 16, 8),
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(16),
      border: Border.all(color: AppColors.primary.withValues(alpha: 0.25)),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Icon(
              Icons.groups_rounded,
              size: 20,
              color: AppColors.primary,
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(entry.group.title, style: AppTextStyles.subtitle),
            ),
            IconButton(
              tooltip: 'Remove group buy',
              visualDensity: VisualDensity.compact,
              onPressed: () => context.read<CartProvider>().clearGroupBuy(),
              icon: const Icon(Icons.delete_outline),
            ),
          ],
        ),
        for (final line in entry.quote.lines)
          Padding(
            padding: const EdgeInsets.only(top: 2),
            child: Text(
              '${line.quantity} x ${line.productName}',
              style: AppTextStyles.bodySmall,
            ),
          ),
        const SizedBox(height: 8),
        Text(
          'Items R${entry.quote.itemsTotal.toStringAsFixed(2)}. '
          'You join the deal when you pay.',
          style: AppTextStyles.caption,
        ),
        const SizedBox(height: 10),
        SizedBox(
          width: double.infinity,
          child: ElevatedButton(
            onPressed: () => _checkout(context),
            child: const Text('Add delivery and pay'),
          ),
        ),
      ],
    ),
  );
}
