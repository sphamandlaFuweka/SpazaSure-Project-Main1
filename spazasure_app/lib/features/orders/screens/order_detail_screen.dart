import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:timeline_tile/timeline_tile.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/status_badge.dart';
import 'package:spazasure_app/core/widgets/custom_button.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/services/order_service.dart';
import 'package:spazasure_app/services/api_service.dart';

class OrderDetailScreen extends StatelessWidget {
  const OrderDetailScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final order = ModalRoute.of(context)!.settings.arguments as Order;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(order.orderNumber),
        backgroundColor: AppColors.surface,
        actions: [
          IconButton(
            icon: const Icon(Icons.receipt_long_outlined),
            onPressed: () => Navigator.pushNamed(context, '/receipt', arguments: order),
          ),
        ],
      ),
      body: SingleChildScrollView(
        child: Column(
          children: [
            // Status card
            Container(
              margin: const EdgeInsets.all(16),
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(14),
              ),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Order Status', style: AppTextStyles.subtitle),
                      StatusBadge(status: order.status),
                    ],
                  ),
                  const SizedBox(height: 16),
                  _buildTimeline(order.status),
                ],
              ),
            ),
            // Supplier info
            _buildSection(
              'Supplier',
              Row(
                children: [
                  Container(
                    width: 48,
                    height: 48,
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: const Icon(Icons.business, color: AppColors.primary),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(order.supplierName, style: AppTextStyles.subtitle),
                        Text('Verified Supplier', style: AppTextStyles.caption),
                      ],
                    ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.phone_outlined, color: AppColors.primary),
                    onPressed: () {},
                  ),
                ],
              ),
            ),
            // Items
            _buildSection(
              'Items (${order.items.length})',
              Column(
                children: order.items.map((item) => Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Row(
                        children: [
                          Container(
                            width: 48,
                            height: 48,
                            decoration: BoxDecoration(
                              color: AppColors.primary.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(8),
                            ),
                            child: Icon(Icons.inventory_2_outlined, color: AppColors.primary.withValues(alpha: 0.4), size: 24),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(item.productName, style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600), maxLines: 1, overflow: TextOverflow.ellipsis),
                                Text('Qty: ${item.quantity}', style: AppTextStyles.caption),
                              ],
                            ),
                          ),
                          Text('R${item.total.toStringAsFixed(2)}', style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600)),
                        ],
                      ),
                    )).toList(),
              ),
            ),
            // Payment summary
            _buildSection(
              'Payment Summary',
              Column(
                children: [
                  _summaryRow('Subtotal', 'R${order.subtotal.toStringAsFixed(2)}'),
                  _summaryRow('Delivery (${order.deliveryOption})', 'R${order.deliveryFee.toStringAsFixed(2)}'),
                  _summaryRow('Platform Fee', 'R${order.platformFee.toStringAsFixed(2)}'),
                  const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Divider()),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Total', style: AppTextStyles.h3),
                      Text('R${order.total.toStringAsFixed(2)}', style: AppTextStyles.price),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Icon(
                        order.paymentStatus == 'released' ? Icons.check_circle : Icons.schedule,
                        size: 16,
                        color: order.paymentStatus == 'released' ? AppColors.success : AppColors.warning,
                      ),
                      const SizedBox(width: 6),
                      Text(
                        order.paymentStatus == 'released' ? 'Payment completed' : 'Payment held in escrow',
                        style: AppTextStyles.caption.copyWith(
                          color: order.paymentStatus == 'released' ? AppColors.success : AppColors.warning,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            // Delivery info
            if (order.status == 'dispatched')
              _buildSection(
                'Delivery',
                Column(
                  children: [
                    Row(
                      children: [
                        Container(
                          width: 48,
                          height: 48,
                          decoration: BoxDecoration(
                            color: AppColors.info.withValues(alpha: 0.1),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: const Icon(Icons.local_shipping, color: AppColors.info),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Driver: John M.', style: AppTextStyles.subtitle),
                              if (order.estimatedDelivery != null)
                                Text('ETA: ${DateFormat('HH:mm').format(order.estimatedDelivery!)}', style: AppTextStyles.bodySmall.copyWith(color: AppColors.info)),
                            ],
                          ),
                        ),
                        IconButton(
                          icon: const Icon(Icons.phone_outlined, color: AppColors.info),
                          onPressed: () {},
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    CustomButton(
                      text: 'Track on Map',
                      icon: Icons.map_outlined,
                      backgroundColor: AppColors.info,
                      onPressed: () => Navigator.pushNamed(context, '/delivery-tracking', arguments: order),
                    ),
                  ],
                ),
              ),
            const SizedBox(height: 100),
          ],
        ),
      ),
      bottomNavigationBar: order.status == 'dispatched'
          ? Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: AppColors.surface,
                boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.08), blurRadius: 10, offset: const Offset(0, -4))],
              ),
              child: SafeArea(
                child: CustomButton(
                  text: 'Confirm Delivery',
                  icon: Icons.check_circle_outline,
                  onPressed: () => _showConfirmDelivery(context, order),
                ),
              ),
            )
          : null,
    );
  }

  Widget _buildTimeline(String currentStatus) {
    final steps = ['pending', 'confirmed', 'processing', 'dispatched', 'delivered'];
    final labels = ['Pending', 'Confirmed', 'Processing', 'Dispatched', 'Delivered'];
    final currentIndex = steps.indexOf(currentStatus);

    return SizedBox(
      height: 80,
      child: Row(
        children: List.generate(steps.length, (i) {
          final isCompleted = i <= currentIndex;
          final isCurrent = i == currentIndex;
          return Expanded(
            child: TimelineTile(
              axis: TimelineAxis.horizontal,
              alignment: TimelineAlign.center,
              isFirst: i == 0,
              isLast: i == steps.length - 1,
              indicatorStyle: IndicatorStyle(
                width: 24,
                height: 24,
                indicator: Container(
                  decoration: BoxDecoration(
                    color: isCompleted ? AppColors.primary : AppColors.divider,
                    shape: BoxShape.circle,
                    border: isCurrent ? Border.all(color: AppColors.primary, width: 3) : null,
                  ),
                  child: isCompleted && !isCurrent
                      ? const Icon(Icons.check, size: 14, color: Colors.white)
                      : null,
                ),
              ),
              beforeLineStyle: LineStyle(color: i <= currentIndex ? AppColors.primary : AppColors.divider, thickness: 2),
              afterLineStyle: LineStyle(color: i < currentIndex ? AppColors.primary : AppColors.divider, thickness: 2),
              endChild: Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  labels[i],
                  style: AppTextStyles.caption.copyWith(
                    color: isCompleted ? AppColors.primary : AppColors.textHint,
                    fontWeight: isCurrent ? FontWeight.w600 : FontWeight.w400,
                  ),
                  textAlign: TextAlign.center,
                ),
              ),
            ),
          );
        }),
      ),
    );
  }

  Widget _buildSection(String title, Widget child) {
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
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

  Widget _summaryRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: AppTextStyles.body.copyWith(color: AppColors.textSecondary)),
          Text(value, style: AppTextStyles.body),
        ],
      ),
    );
  }

  void _showConfirmDelivery(BuildContext context, Order order) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: Text('Confirm Delivery', style: AppTextStyles.h3),
        content: Text('Have you received all items in good condition?', style: AppTextStyles.body),
        actions: [
          TextButton(
            onPressed: () {
              Navigator.pop(ctx);
              _showReportIssue(context, order);
            },
            child: Text('Report Issue', style: AppTextStyles.body.copyWith(color: AppColors.error)),
          ),
          ElevatedButton(
            onPressed: () async {
              Navigator.pop(ctx);
              try {
                await OrderService.confirmDelivery(order.id);
                if (!context.mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('Delivery confirmed! Payment released to supplier.'),
                    backgroundColor: AppColors.success,
                    behavior: SnackBarBehavior.floating,
                  ),
                );
                Navigator.pushReplacementNamed(context, '/rate-delivery',
                    arguments: order);
              } catch (e) {
                if (!context.mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text(e.toString()), backgroundColor: AppColors.error),
                );
              }
            },
            child: const Text('Confirm'),
          ),
        ],
      ),
    );
  }

  void _showReportIssue(BuildContext context, Order order) {
    final issueTypes = ['Damaged Items', 'Missing Items', 'Wrong Items', 'Late Delivery', 'Other'];
    String selectedType = issueTypes[0];
    final descController = TextEditingController();

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          title: Row(
            children: [
              const Icon(Icons.report_problem_outlined, color: AppColors.error, size: 24),
              const SizedBox(width: 8),
              Text('Report Issue', style: AppTextStyles.h3),
            ],
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('What went wrong?', style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600)),
              const SizedBox(height: 12),
              ...issueTypes.map((type) => RadioListTile<String>(
                title: Text(type, style: AppTextStyles.bodySmall),
                value: type,
                groupValue: selectedType,
                onChanged: (v) => setDialogState(() => selectedType = v!),
                dense: true,
                activeColor: AppColors.primary,
              )),
              const SizedBox(height: 12),
              TextField(
                controller: descController,
                maxLines: 3,
                decoration: InputDecoration(
                  hintText: 'Describe the issue...',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
                  contentPadding: const EdgeInsets.all(12),
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(backgroundColor: AppColors.error),
              onPressed: () async {
                Navigator.pop(ctx);
                try {
                  await ApiService.post('/shop/orders/${order.id}/report-issue', {
                    'issueType': selectedType,
                    'description': descController.text.trim().isNotEmpty
                        ? descController.text.trim()
                        : selectedType,
                  });
                  if (!context.mounted) return;
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('Issue reported. We will investigate and contact you.'),
                      backgroundColor: AppColors.info,
                      behavior: SnackBarBehavior.floating,
                    ),
                  );
                } catch (e) {
                  if (!context.mounted) return;
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(content: Text('Failed: $e'), backgroundColor: AppColors.error),
                  );
                }
              },
              child: const Text('Submit Report', style: TextStyle(color: Colors.white)),
            ),
          ],
        ),
      ),
    );
  }
}

