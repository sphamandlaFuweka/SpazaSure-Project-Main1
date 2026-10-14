import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:intl/intl.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/models/models.dart';

/// Delivery status for an order. Shows the shop's real delivery pin and the
/// order's real progress — there is no live driver GPS feed, so none is faked.
class DeliveryTrackingScreen extends StatelessWidget {
  const DeliveryTrackingScreen({super.key});

  static const _steps = [
    ('pending', 'Order placed', Icons.receipt_long_rounded),
    ('processing', 'Being prepared by the supplier', Icons.inventory_2_rounded),
    ('dispatched', 'On the way to your shop', Icons.local_shipping_rounded),
    ('delivered', 'Delivered', Icons.store_rounded),
  ];

  int _stepIndex(String status) {
    if (status == 'confirmed') return 0;
    final i = _steps.indexWhere((s) => s.$1 == status);
    return i < 0 ? 0 : i;
  }

  @override
  Widget build(BuildContext context) {
    final order = ModalRoute.of(context)!.settings.arguments as Order;
    final shopPoint =
        order.deliveryLatitude != null && order.deliveryLongitude != null
        ? LatLng(order.deliveryLatitude!, order.deliveryLongitude!)
        : null;
    final current = _stepIndex(order.status);
    final cancelled = order.status == 'cancelled';
    final supplier = order.supplierName.isEmpty
        ? 'The supplier'
        : order.supplierName;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Stack(
        children: [
          // Map (or a friendly placeholder when the order has no delivery pin)
          Positioned.fill(
            child: shopPoint == null
                ? Container(
                    color: const Color(0xFFE8ECF8),
                    alignment: const Alignment(0, -0.45),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.map_outlined,
                          size: 56,
                          color: AppColors.primary.withValues(alpha: 0.35),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'No map pin for this delivery',
                          style: AppTextStyles.bodySmall.copyWith(
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  )
                : FlutterMap(
                    options: MapOptions(
                      initialCenter: shopPoint,
                      initialZoom: 15,
                    ),
                    children: [
                      TileLayer(
                        urlTemplate:
                            'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                        userAgentPackageName: 'com.spazasure.app',
                      ),
                      MarkerLayer(
                        markers: [
                          Marker(
                            point: shopPoint,
                            width: 48,
                            height: 48,
                            child: Container(
                              decoration: BoxDecoration(
                                color: AppColors.secondary,
                                shape: BoxShape.circle,
                                border: Border.all(
                                  color: Colors.white,
                                  width: 3,
                                ),
                                boxShadow: [
                                  BoxShadow(
                                    color: AppColors.secondary.withValues(
                                      alpha: 0.4,
                                    ),
                                    blurRadius: 8,
                                  ),
                                ],
                              ),
                              child: const Icon(
                                Icons.store,
                                color: Colors.white,
                                size: 22,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
          ),

          // Back button
          Positioned(
            top: MediaQuery.of(context).padding.top + 12,
            left: 16,
            child: Material(
              color: Colors.white,
              borderRadius: BorderRadius.circular(14),
              elevation: 2,
              child: InkWell(
                borderRadius: BorderRadius.circular(14),
                onTap: () => Navigator.pop(context),
                child: const SizedBox(
                  width: 44,
                  height: 44,
                  child: Icon(
                    Icons.arrow_back_rounded,
                    size: 20,
                    semanticLabel: 'Back',
                  ),
                ),
              ),
            ),
          ),

          // Order number badge
          Positioned(
            top: MediaQuery.of(context).padding.top + 12,
            right: 16,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.1),
                    blurRadius: 8,
                  ),
                ],
              ),
              child: Text(
                order.orderNumber,
                style: AppTextStyles.bodySmall.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ),

          // Bottom info panel
          Positioned(
            bottom: 0,
            left: 0,
            right: 0,
            child: Container(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 20),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: const BorderRadius.vertical(
                  top: Radius.circular(28),
                ),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.12),
                    blurRadius: 16,
                    offset: const Offset(0, -4),
                  ),
                ],
              ),
              child: SafeArea(
                top: false,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Center(
                      child: Container(
                        width: 40,
                        height: 4,
                        decoration: BoxDecoration(
                          color: AppColors.divider,
                          borderRadius: BorderRadius.circular(2),
                        ),
                      ),
                    ),
                    const SizedBox(height: 16),

                    // Headline: real ETA if the supplier set one, otherwise status
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 12,
                      ),
                      decoration: BoxDecoration(
                        color: (cancelled ? AppColors.error : AppColors.info)
                            .withValues(alpha: 0.08),
                        borderRadius: BorderRadius.circular(14),
                        border: Border.all(
                          color: (cancelled ? AppColors.error : AppColors.info)
                              .withValues(alpha: 0.2),
                        ),
                      ),
                      child: Row(
                        children: [
                          Icon(
                            cancelled
                                ? Icons.cancel_outlined
                                : Icons.access_time_rounded,
                            color: cancelled ? AppColors.error : AppColors.info,
                            size: 20,
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              cancelled
                                  ? 'This order was cancelled'
                                  : order.status == 'delivered'
                                  ? 'Delivered to your shop'
                                  : order.estimatedDelivery != null
                                  ? 'Expected by ${DateFormat('EEE d MMM, HH:mm').format(order.estimatedDelivery!)}'
                                  : order.status == 'dispatched'
                                  ? '$supplier has dispatched your order'
                                  : '$supplier is preparing your order',
                              style: AppTextStyles.subtitle.copyWith(
                                color: cancelled
                                    ? AppColors.error
                                    : AppColors.info,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 16),

                    // Real status timeline
                    if (!cancelled)
                      for (var i = 0; i < _steps.length; i++) ...[
                        _buildStatusRow(
                          _steps[i].$3,
                          _steps[i].$2,
                          i <= current,
                        ),
                        if (i < _steps.length - 1) const SizedBox(height: 8),
                      ],

                    if (order.deliveryAddress.isNotEmpty ||
                        order.deliveryDistanceKm != null) ...[
                      const SizedBox(height: 16),
                      const Divider(height: 1),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          const Icon(
                            Icons.place_outlined,
                            size: 18,
                            color: AppColors.textSecondary,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              [
                                if (order.deliveryAddress.isNotEmpty)
                                  order.deliveryAddress,
                                if (order.deliveryDistanceKm != null)
                                  '${order.deliveryDistanceKm!.toStringAsFixed(1)} km from $supplier',
                              ].join(' • '),
                              style: AppTextStyles.bodySmall,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildStatusRow(IconData icon, String text, bool done) {
    return Row(
      children: [
        Icon(
          icon,
          size: 18,
          color: done ? AppColors.success : AppColors.textHint,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            text,
            style: AppTextStyles.bodySmall.copyWith(
              fontWeight: done ? FontWeight.w600 : FontWeight.w400,
              color: done ? AppColors.textPrimary : AppColors.textHint,
            ),
          ),
        ),
        if (done)
          const Icon(Icons.check_rounded, size: 16, color: AppColors.success),
      ],
    );
  }
}
