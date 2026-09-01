import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/models/models.dart';

class DeliveryTrackingScreen extends StatefulWidget {
  const DeliveryTrackingScreen({super.key});

  @override
  State<DeliveryTrackingScreen> createState() => _DeliveryTrackingScreenState();
}

class _DeliveryTrackingScreenState extends State<DeliveryTrackingScreen> {
  // Simulated positions (Johannesburg area)
  // In production, these would come from real-time GPS data
  final LatLng _driverPosition = const LatLng(-26.2041, 28.0473); // Driver
  final LatLng _shopPosition = const LatLng(-26.2121, 28.0539);   // Shop destination
  final LatLng _supplierPosition = const LatLng(-26.1952, 28.0348); // Supplier origin

  @override
  Widget build(BuildContext context) {
    final order = ModalRoute.of(context)!.settings.arguments as Order;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Stack(
        children: [
          // Map
          FlutterMap(
            options: MapOptions(
              initialCenter: _driverPosition,
              initialZoom: 14.0,
            ),
            children: [
              // OpenStreetMap tiles (free, no API key)
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'com.spazasure.app',
              ),
              // Route line
              PolylineLayer(
                polylines: [
                  Polyline(
                    points: [_supplierPosition, _driverPosition, _shopPosition],
                    color: AppColors.primary,
                    strokeWidth: 4,
                  ),
                ],
              ),
              // Markers
              MarkerLayer(
                markers: [
                  // Supplier (origin)
                  Marker(
                    point: _supplierPosition,
                    width: 44,
                    height: 44,
                    child: Container(
                      decoration: BoxDecoration(
                        color: AppColors.info,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.white, width: 3),
                        boxShadow: [BoxShadow(color: AppColors.info.withOpacity(0.4), blurRadius: 8)],
                      ),
                      child: const Icon(Icons.business, color: Colors.white, size: 20),
                    ),
                  ),
                  // Driver (moving)
                  Marker(
                    point: _driverPosition,
                    width: 52,
                    height: 52,
                    child: Container(
                      decoration: BoxDecoration(
                        color: AppColors.primary,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.white, width: 3),
                        boxShadow: [BoxShadow(color: AppColors.primary.withOpacity(0.5), blurRadius: 12)],
                      ),
                      child: const Icon(Icons.local_shipping, color: Colors.white, size: 22),
                    ),
                  ),
                  // Shop (destination)
                  Marker(
                    point: _shopPosition,
                    width: 44,
                    height: 44,
                    child: Container(
                      decoration: BoxDecoration(
                        color: AppColors.secondary,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.white, width: 3),
                        boxShadow: [BoxShadow(color: AppColors.secondary.withOpacity(0.4), blurRadius: 8)],
                      ),
                      child: const Icon(Icons.store, color: Colors.white, size: 20),
                    ),
                  ),
                ],
              ),
            ],
          ),

          // Back button
          Positioned(
            top: MediaQuery.of(context).padding.top + 12,
            left: 16,
            child: GestureDetector(
              onTap: () => Navigator.pop(context),
              child: Container(
                width: 44, height: 44,
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(14),
                  boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.1), blurRadius: 8)],
                ),
                child: const Icon(Icons.arrow_back_rounded, size: 20),
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
                boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.1), blurRadius: 8)],
              ),
              child: Text(order.orderNumber, style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w700)),
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
                borderRadius: const BorderRadius.vertical(top: Radius.circular(28)),
                boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.12), blurRadius: 16, offset: const Offset(0, -4))],
              ),
              child: SafeArea(
                top: false,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    // Handle
                    Container(
                      width: 40, height: 4,
                      decoration: BoxDecoration(color: AppColors.divider, borderRadius: BorderRadius.circular(2)),
                    ),
                    const SizedBox(height: 16),

                    // ETA
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                      decoration: BoxDecoration(
                        color: AppColors.info.withOpacity(0.08),
                        borderRadius: BorderRadius.circular(14),
                        border: Border.all(color: AppColors.info.withOpacity(0.2)),
                      ),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.access_time_rounded, color: AppColors.info, size: 20),
                          const SizedBox(width: 8),
                          Text('Estimated arrival in ', style: AppTextStyles.body),
                          Text('25 min', style: AppTextStyles.subtitle.copyWith(color: AppColors.info)),
                        ],
                      ),
                    ),
                    const SizedBox(height: 16),

                    // Driver info
                    Row(
                      children: [
                        Container(
                          width: 52, height: 52,
                          decoration: BoxDecoration(
                            color: AppColors.primary.withOpacity(0.1),
                            borderRadius: BorderRadius.circular(16),
                          ),
                          child: const Icon(Icons.person_rounded, color: AppColors.primary, size: 28),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Driver Assigned', style: AppTextStyles.subtitle),
                              Row(
                                children: [
                                  const Icon(Icons.star_rounded, size: 14, color: AppColors.warning),
                                  const SizedBox(width: 3),
                                  Text('4.8', style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600)),
                                  Text(' • On the way to you', style: AppTextStyles.caption),
                                ],
                              ),
                            ],
                          ),
                        ),
                        Container(
                          width: 42, height: 42,
                          decoration: BoxDecoration(
                            color: AppColors.success.withOpacity(0.1),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: const Icon(Icons.phone_rounded, color: AppColors.success, size: 20),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),

                    // Status timeline
                    _buildStatusRow(Icons.check_circle, 'Order picked up from supplier', true),
                    const SizedBox(height: 8),
                    _buildStatusRow(Icons.local_shipping, 'In transit to your shop', true),
                    const SizedBox(height: 8),
                    _buildStatusRow(Icons.store, 'Arriving at ${order.supplierName.isNotEmpty ? "your shop" : "destination"}', false),

                    const SizedBox(height: 16),

                    // Legend
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        _legendDot(AppColors.info, 'Supplier'),
                        const SizedBox(width: 16),
                        _legendDot(AppColors.primary, 'Driver'),
                        const SizedBox(width: 16),
                        _legendDot(AppColors.secondary, 'Your Shop'),
                      ],
                    ),
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
        Icon(icon, size: 18, color: done ? AppColors.success : AppColors.textHint),
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
          Text('✓', style: AppTextStyles.bodySmall.copyWith(color: AppColors.success)),
      ],
    );
  }

  Widget _legendDot(Color color, String label) {
    return Row(
      children: [
        Container(width: 10, height: 10, decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
        const SizedBox(width: 4),
        Text(label, style: AppTextStyles.caption),
      ],
    );
  }
}
