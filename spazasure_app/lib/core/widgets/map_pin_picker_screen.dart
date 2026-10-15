import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/address_service.dart';
import 'package:spazasure_app/services/device_context.dart';

/// Lets the user place an address exactly on the map when search can't find it
/// (common for township stands and new developments). Returns an [AddressSuggestion]
/// with the pin's exact coordinates.
class MapPinPickerScreen extends StatefulWidget {
  const MapPinPickerScreen({super.key, this.initial, this.typedAddress});

  /// Where to start (e.g. the closest suggestion); falls back to the user's location.
  final LatLng? initial;

  /// What the user typed, kept as the street line if the map can't name the spot.
  final String? typedAddress;

  static Future<AddressSuggestion?> open(
    BuildContext context, {
    LatLng? initial,
    String? typedAddress,
  }) => Navigator.push<AddressSuggestion>(
    context,
    MaterialPageRoute(
      builder: (_) =>
          MapPinPickerScreen(initial: initial, typedAddress: typedAddress),
    ),
  );

  @override
  State<MapPinPickerScreen> createState() => _MapPinPickerScreenState();
}

class _MapPinPickerScreenState extends State<MapPinPickerScreen> {
  /// Johannesburg, if we know nothing better
  static const _fallback = LatLng(-26.2041, 28.0473);

  final _map = MapController();
  late LatLng _center = widget.initial ?? _fallback;
  AddressSuggestion? _place;
  bool _looking = false;
  bool _moving = false;
  Timer? _debounce;

  @override
  void initState() {
    super.initState();
    if (widget.initial == null) {
      // Start where the user is standing, if location is allowed
      DeviceContext.location().then((fix) {
        if (fix == null || !mounted) return;
        final here = LatLng(fix.latitude, fix.longitude);
        _map.move(here, 17);
        _lookUp(here);
      });
    }
    WidgetsBinding.instance.addPostFrameCallback((_) => _lookUp(_center));
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  void _onMoved(MapCamera camera, bool hasGesture) {
    _center = camera.center;
    if (!_moving) setState(() => _moving = true);
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 700), () {
      if (!mounted) return;
      setState(() => _moving = false);
      _lookUp(_center);
    });
  }

  Future<void> _lookUp(LatLng point) async {
    setState(() => _looking = true);
    try {
      final place = await AddressService.reverse(
        point.latitude,
        point.longitude,
      );
      if (!mounted || point != _center) return;
      setState(() => _place = place);
    } catch (_) {
      if (mounted) setState(() => _place = null);
    } finally {
      if (mounted) setState(() => _looking = false);
    }
  }

  void _confirm() {
    final typed = widget.typedAddress?.trim() ?? '';
    final place = _place;
    // Keep the exact pin, and the street the user typed if the map only knows the area
    final street = typed.isNotEmpty && (place == null || place.street.isEmpty)
        ? typed
        : place?.street ?? typed;
    final result = AddressSuggestion(
      label: [
        street,
        place?.suburb,
        place?.city,
        place?.province,
        place?.postalCode,
      ].whereType<String>().where((p) => p.isNotEmpty).join(', '),
      street: street,
      suburb: place?.suburb ?? '',
      city: place?.city ?? '',
      province: place?.province ?? '',
      postalCode: place?.postalCode ?? '',
      latitude: _center.latitude,
      longitude: _center.longitude,
    );
    Navigator.pop(context, result);
  }

  @override
  Widget build(BuildContext context) {
    final place = _place;
    return Scaffold(
      appBar: AppBar(title: const Text('Pin the exact location')),
      body: Stack(
        children: [
          FlutterMap(
            mapController: _map,
            options: MapOptions(
              initialCenter: _center,
              initialZoom: widget.initial == null ? 12 : 17,
              minZoom: 5,
              maxZoom: 19,
              onPositionChanged: _onMoved,
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'co.spazasure.app',
              ),
            ],
          ),
          // The pin stays in the middle; the map moves underneath it
          IgnorePointer(
            child: Center(
              child: Padding(
                padding: const EdgeInsets.only(bottom: 44),
                child: AnimatedScale(
                  scale: _moving ? 1.15 : 1,
                  duration: const Duration(milliseconds: 150),
                  child: const Icon(
                    Icons.location_on,
                    size: 48,
                    color: AppColors.error,
                  ),
                ),
              ),
            ),
          ),
          Positioned(
            top: 12,
            left: 12,
            right: 12,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(14),
                boxShadow: const [
                  BoxShadow(color: Colors.black12, blurRadius: 10),
                ],
              ),
              child: Row(
                children: [
                  const Icon(
                    Icons.pan_tool_alt_outlined,
                    size: 18,
                    color: AppColors.primary,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Move the map so the pin sits on the entrance of the place.',
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          Positioned(
            right: 12,
            bottom: 170,
            child: FloatingActionButton.small(
              heroTag: 'my-location',
              tooltip: 'Go to my location',
              backgroundColor: AppColors.surface,
              foregroundColor: AppColors.primary,
              onPressed: () async {
                final fix = await DeviceContext.location();
                if (fix == null) {
                  if (!context.mounted) return;
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text(
                        'Turn on location and allow access to use this.',
                      ),
                    ),
                  );
                  return;
                }
                _map.move(LatLng(fix.latitude, fix.longitude), 17);
              },
              child: const Icon(Icons.my_location_rounded),
            ),
          ),
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: SafeArea(
              top: false,
              child: Container(
                margin: const EdgeInsets.all(12),
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(20),
                  boxShadow: const [
                    BoxShadow(color: Colors.black26, blurRadius: 16),
                  ],
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const Icon(
                          Icons.place_rounded,
                          color: AppColors.primary,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            _moving || _looking
                                ? 'Finding this place...'
                                : place?.label.isNotEmpty == true
                                ? place!.label
                                : 'Location selected',
                            style: AppTextStyles.body.copyWith(
                              fontWeight: FontWeight.w700,
                            ),
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    SizedBox(
                      width: double.infinity,
                      height: 50,
                      child: ElevatedButton.icon(
                        onPressed: _moving ? null : _confirm,
                        icon: const Icon(Icons.check_rounded),
                        label: const Text('Use this location'),
                      ),
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
}
