import 'dart:async';

import 'package:spazasure_app/core/widgets/brand_map.dart';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:intl/intl.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/delivery_service.dart';
import 'package:url_launcher/url_launcher.dart';

/// Delivery status for an order: the branch it ships from, the shop's delivery pin, the driver
/// SpazaSure allocated and, while the order is on the road, the driver's live position (shared
/// from their phone and refreshed every 15 seconds).
class DeliveryTrackingScreen extends StatefulWidget {
  const DeliveryTrackingScreen({super.key});

  @override
  State<DeliveryTrackingScreen> createState() => _DeliveryTrackingScreenState();
}

class _DeliveryTrackingScreenState extends State<DeliveryTrackingScreen> {
  static const _steps = [
    ('pending', 'Order placed', Icons.receipt_long_rounded),
    ('processing', 'Being prepared by the supplier', Icons.inventory_2_rounded),
    ('ready', 'Packed: SpazaSure is collecting it', Icons.inventory_rounded),
    (
      'dispatched',
      'SpazaSure is delivering it to your shop',
      Icons.local_shipping_rounded,
    ),
    ('delivered', 'Delivered', Icons.store_rounded),
  ];

  final _map = MapController();
  Timer? _timer;
  DeliveryTracking? _tracking;
  Order? _order;
  bool _followDriver = true;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_order != null) return;
    _order = ModalRoute.of(context)!.settings.arguments as Order;
    _refresh();
    _timer = Timer.periodic(const Duration(seconds: 15), (_) => _refresh());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _refresh() async {
    try {
      final tracking = await DeliveryService.tracking(_order!.id);
      if (!mounted) return;
      setState(() => _tracking = tracking);
      if (_followDriver && tracking.hasLiveLocation) {
        // After the frame, so the map exists (it isn't shown until there is a point to centre on)
        WidgetsBinding.instance.addPostFrameCallback((_) {
          try {
            _map.move(
              LatLng(tracking.driverLatitude!, tracking.driverLongitude!),
              _map.camera.zoom,
            );
          } catch (_) {}
        });
      }
      if (tracking.status == 'delivered' || tracking.status == 'cancelled') {
        _timer?.cancel();
      }
    } catch (_) {
      // Keep showing the order's own details; the next refresh tries again
    }
  }

  int _stepIndex(String status) {
    if (status == 'confirmed') return 0;
    final i = _steps.indexWhere((s) => s.$1 == status);
    return i < 0 ? 0 : i;
  }

  LatLng? _point(double? lat, double? lng) =>
      lat != null && lng != null ? LatLng(lat, lng) : null;

  @override
  Widget build(BuildContext context) {
    final order = _order!;
    final tracking = _tracking;
    final status = tracking?.status.isNotEmpty == true
        ? tracking!.status
        : order.status;
    final shopPoint =
        _point(order.deliveryLatitude, order.deliveryLongitude) ??
        _point(
          tracking?.destination?.latitude,
          tracking?.destination?.longitude,
        );
    final pickupPoint = _point(
      tracking?.pickup?.latitude,
      tracking?.pickup?.longitude,
    );
    final driverPoint = tracking?.hasLiveLocation == true
        ? LatLng(tracking!.driverLatitude!, tracking.driverLongitude!)
        : null;
    final current = _stepIndex(status);
    final cancelled = status == 'cancelled';
    final supplier = order.supplierName.isEmpty
        ? 'The supplier'
        : order.supplierName;
    final center = driverPoint ?? shopPoint ?? pickupPoint;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Stack(
        children: [
          Positioned.fill(
            child: center == null
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
                    mapController: _map,
                    options: MapOptions(
                      initialCenter: center,
                      initialZoom: 13,
                      minZoom: BrandMap.minZoom,
                      cameraConstraint: BrandMap.cameraConstraint,
                      backgroundColor: BrandMap.background,
                      // The shop moved the map: stop snapping back to the driver
                      onPositionChanged: (_, hasGesture) {
                        if (hasGesture && _followDriver) {
                          setState(() => _followDriver = false);
                        }
                      },
                    ),
                    children: [
                      BrandMap.tiles(),
                      if (driverPoint != null && shopPoint != null)
                        PolylineLayer(
                          polylines: [
                            Polyline(
                              points: [driverPoint, shopPoint],
                              strokeWidth: 3,
                              color: AppColors.primary.withValues(alpha: 0.5),
                            ),
                          ],
                        ),
                      MarkerLayer(
                        markers: [
                          if (pickupPoint != null &&
                              driverPoint == null &&
                              status != 'delivered')
                            _marker(
                              pickupPoint,
                              Icons.warehouse_rounded,
                              AppColors.accentDark,
                            ),
                          if (shopPoint != null)
                            _marker(
                              shopPoint,
                              Icons.store,
                              AppColors.secondary,
                            ),
                          if (driverPoint != null)
                            _marker(
                              driverPoint,
                              Icons.local_shipping_rounded,
                              AppColors.primary,
                              size: 52,
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

          // Order number, and a button to find the driver again after moving the map
          Positioned(
            top: MediaQuery.of(context).padding.top + 12,
            right: 16,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 8,
                  ),
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
                if (driverPoint != null && !_followDriver) ...[
                  const SizedBox(height: 8),
                  FilledButton.icon(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.primary,
                    ),
                    onPressed: () {
                      setState(() => _followDriver = true);
                      _map.move(driverPoint, _map.camera.zoom);
                    },
                    icon: const Icon(Icons.my_location_rounded, size: 16),
                    label: const Text('Follow driver'),
                  ),
                ],
              ],
            ),
          ),

          // Bottom info panel
          Positioned(
            bottom: 0,
            left: 0,
            right: 0,
            child: Container(
              constraints: BoxConstraints(
                maxHeight: MediaQuery.of(context).size.height * 0.6,
              ),
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
                child: SingleChildScrollView(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 20),
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
                      _headline(order, status, cancelled, supplier, tracking),
                      if (tracking?.driver != null &&
                          !cancelled &&
                          status != 'delivered') ...[
                        const SizedBox(height: 12),
                        _driverCard(tracking!.driver!, tracking),
                      ],
                      const SizedBox(height: 16),
                      if (!cancelled)
                        for (var i = 0; i < _steps.length; i++) ...[
                          _buildStatusRow(
                            _steps[i].$3,
                            _steps[i].$2,
                            i <= current,
                          ),
                          if (i < _steps.length - 1) const SizedBox(height: 8),
                        ],
                      if (tracking?.pickup != null ||
                          order.deliveryAddress.isNotEmpty) ...[
                        const SizedBox(height: 16),
                        const Divider(height: 1),
                        const SizedBox(height: 12),
                        if (tracking?.pickup != null &&
                            tracking!.pickup!.name.isNotEmpty)
                          _placeRow(
                            Icons.warehouse_outlined,
                            'From ${tracking.pickup!.name}',
                          ),
                        if (order.deliveryAddress.isNotEmpty ||
                            order.deliveryDistanceKm != null)
                          _placeRow(
                            Icons.place_outlined,
                            [
                              if (order.deliveryAddress.isNotEmpty)
                                order.deliveryAddress,
                              if (order.deliveryDistanceKm != null)
                                '${order.deliveryDistanceKm!.toStringAsFixed(1)} km',
                            ].join(' • '),
                          ),
                      ],
                    ],
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Marker _marker(
    LatLng point,
    IconData icon,
    Color color, {
    double size = 44,
  }) => Marker(
    point: point,
    width: size,
    height: size,
    child: Container(
      decoration: BoxDecoration(
        color: color,
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white, width: 3),
        boxShadow: [
          BoxShadow(color: color.withValues(alpha: 0.4), blurRadius: 8),
        ],
      ),
      child: Icon(icon, color: Colors.white, size: size * 0.45),
    ),
  );

  Widget _headline(
    Order order,
    String status,
    bool cancelled,
    String supplier,
    DeliveryTracking? tracking,
  ) {
    final color = cancelled ? AppColors.error : AppColors.info;
    final live = tracking?.hasLiveLocation == true;
    final text = cancelled
        ? 'This order was cancelled'
        : status == 'delivered'
        ? 'Delivered to your shop'
        : status == 'dispatched' && live
        ? '${tracking!.driver?.fullName.split(' ').first ?? 'Your driver'} is on the way'
        : order.estimatedDelivery != null
        ? 'Expected by ${DateFormat('EEE d MMM, HH:mm').format(order.estimatedDelivery!)}'
        : status == 'dispatched'
        ? 'SpazaSure is on the way with your order'
        : status == 'ready'
        ? 'Packed by $supplier: SpazaSure is collecting it'
        : '$supplier is preparing your order';
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: color.withValues(alpha: 0.2)),
      ),
      child: Row(
        children: [
          Icon(
            cancelled
                ? Icons.cancel_outlined
                : live
                ? Icons.navigation_rounded
                : Icons.access_time_rounded,
            color: color,
            size: 20,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: AppTextStyles.subtitle.copyWith(color: color),
            ),
          ),
          if (live && tracking!.driverUpdatedAt != null)
            Text(
              'Live · ${DateFormat('HH:mm').format(tracking.driverUpdatedAt!)}',
              style: AppTextStyles.caption.copyWith(
                color: AppColors.success,
                fontWeight: FontWeight.w700,
              ),
            ),
        ],
      ),
    );
  }

  Widget _driverCard(
    TrackingDriver driver,
    DeliveryTracking tracking,
  ) => Container(
    padding: const EdgeInsets.all(12),
    decoration: BoxDecoration(
      color: AppColors.background,
      borderRadius: BorderRadius.circular(14),
    ),
    child: Row(
      children: [
        const CircleAvatar(
          radius: 20,
          backgroundColor: AppColors.primaryTint,
          child: Icon(Icons.person_rounded, color: AppColors.primary),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                driver.fullName,
                style: AppTextStyles.subtitle.copyWith(fontSize: 14),
              ),
              Text(
                '${driver.vehicleType} · ${driver.vehicleRegistration}',
                style: AppTextStyles.caption,
              ),
              GestureDetector(
                // Opens SpazaSure's public check, so the shop knows the driver is genuine
                onTap: () => launchUrl(
                  Uri.parse(
                    '${ApiService.baseUrl.replaceFirst(RegExp(r'/api/?$'), '')}/verify/driver/${driver.badgeCode}',
                  ),
                  mode: LaunchMode.externalApplication,
                ),
                child: Text(
                  'Badge ${driver.badgeCode} · check it',
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
        ),
        if (driver.phone.isNotEmpty)
          IconButton.filled(
            style: IconButton.styleFrom(backgroundColor: AppColors.secondary),
            onPressed: () => launchUrl(Uri(scheme: 'tel', path: driver.phone)),
            icon: const Icon(Icons.call_rounded, color: Colors.white),
            tooltip: 'Call driver',
          ),
      ],
    ),
  );

  Widget _placeRow(IconData icon, String text) => Padding(
    padding: const EdgeInsets.only(bottom: 6),
    child: Row(
      children: [
        Icon(icon, size: 18, color: AppColors.textSecondary),
        const SizedBox(width: 8),
        Expanded(child: Text(text, style: AppTextStyles.bodySmall)),
      ],
    ),
  );

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
