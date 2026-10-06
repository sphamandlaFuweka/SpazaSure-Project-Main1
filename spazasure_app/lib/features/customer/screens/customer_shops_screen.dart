import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/geo/south_africa.dart';
import 'package:spazasure_app/services/customer_shop_service.dart';
import 'customer_shop_detail_screen.dart';

class CustomerShopsScreen extends StatefulWidget {
  const CustomerShopsScreen({super.key});

  @override
  State<CustomerShopsScreen> createState() => _CustomerShopsScreenState();
}

class _CustomerShopsScreenState extends State<CustomerShopsScreen> {
  final _searchController = TextEditingController();
  final _mapController = MapController();
  List<CustomerShop> _shops = [];
  bool _loading = true;
  bool _mapReady = false;
  LatLng? _userPosition;
  String _viewLabel = 'South Africa';
  String? _error;

  List<LatLng> get _pins => _shops
      .where((shop) => shop.latitude != null && shop.longitude != null)
      .map((shop) => LatLng(shop.latitude!, shop.longitude!))
      .toList();

  @override
  void initState() {
    super.initState();
    _load();
    _locateUser();
  }

  @override
  void dispose() {
    _searchController.dispose();
    _mapController.dispose();
    super.dispose();
  }

  Future<void> _locateUser() async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) return;
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        return;
      }
      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.low,
          timeLimit: Duration(seconds: 10),
        ),
      );
      if (!mounted) return;
      setState(
        () => _userPosition = LatLng(position.latitude, position.longitude),
      );
      _frameMap();
    } catch (_) {
      // Without a location the map falls back to the shops, then South Africa.
    }
  }

  Future<void> _recenter() async {
    await _locateUser();
    if (_userPosition == null) _frameMap();
  }

  void _frameMap() {
    if (!_mapReady || _loading || !mounted) return;
    final pins = _pins;
    final user = _userPosition;
    final province = user == null
        ? null
        : SouthAfrica.provinceAt(user.latitude, user.longitude);
    final searching = _searchController.text.trim().isNotEmpty;

    if (searching && pins.isNotEmpty) {
      _fitPins(pins, 'search results');
    } else if (province != null) {
      final box = province.box;
      _fit(
        LatLngBounds(LatLng(box.south, box.west), LatLng(box.north, box.east)),
        province.name,
      );
    } else if (pins.isNotEmpty) {
      _fitPins(pins, 'all shops');
    } else {
      const box = SouthAfrica.bounds;
      _fit(
        LatLngBounds(LatLng(box.south, box.west), LatLng(box.north, box.east)),
        'South Africa',
      );
    }
  }

  void _fitPins(List<LatLng> pins, String label) {
    if (pins.length == 1) {
      _mapController.move(pins.first, 12);
      setState(() => _viewLabel = label);
    } else {
      _fit(LatLngBounds.fromPoints(pins), label, maxZoom: 14);
    }
  }

  void _fit(LatLngBounds bounds, String label, {double? maxZoom}) {
    _mapController.fitCamera(
      CameraFit.bounds(
        bounds: bounds,
        padding: const EdgeInsets.all(24),
        maxZoom: maxZoom,
      ),
    );
    setState(() => _viewLabel = label);
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final shops = await CustomerShopService.list(
        search: _searchController.text,
      );
      if (mounted) setState(() => _shops = shops);
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
      _frameMap();
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: AppBar(title: const Text('Trusted shops')),
    body: RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          TextField(
            controller: _searchController,
            onSubmitted: (_) => _load(),
            decoration: InputDecoration(
              hintText: 'Search by shop or city',
              prefixIcon: const Icon(Icons.search_rounded),
              suffixIcon: IconButton(
                onPressed: _load,
                icon: const Icon(Icons.arrow_forward_rounded),
              ),
            ),
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 280,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(18),
              child: Stack(
                children: [
                  FlutterMap(
                    mapController: _mapController,
                    options: MapOptions(
                      initialCenter: LatLng(
                        (SouthAfrica.bounds.south + SouthAfrica.bounds.north) /
                            2,
                        (SouthAfrica.bounds.west + SouthAfrica.bounds.east) / 2,
                      ),
                      initialZoom: 5,
                      onMapReady: () {
                        _mapReady = true;
                        _frameMap();
                      },
                    ),
                    children: [
                      TileLayer(
                        urlTemplate:
                            'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                        userAgentPackageName: 'co.spazasure.app',
                      ),
                      MarkerLayer(
                        markers: [
                          for (final shop in _shops)
                            if (shop.latitude != null && shop.longitude != null)
                              Marker(
                                point: LatLng(shop.latitude!, shop.longitude!),
                                width: 48,
                                height: 48,
                                child: GestureDetector(
                                  onTap: () => _showShop(shop),
                                  child: const Icon(
                                    Icons.location_on_rounded,
                                    color: AppColors.primary,
                                    size: 42,
                                  ),
                                ),
                              ),
                          if (_userPosition != null)
                            Marker(
                              point: _userPosition!,
                              width: 28,
                              height: 28,
                              child: const Icon(
                                Icons.circle,
                                color: Colors.blue,
                                size: 22,
                              ),
                            ),
                        ],
                      ),
                    ],
                  ),
                  Positioned(
                    right: 8,
                    top: 8,
                    child: FloatingActionButton.small(
                      heroTag: 'shops-recenter',
                      tooltip: 'Centre on my area',
                      onPressed: _recenter,
                      child: const Icon(Icons.my_location_rounded),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            _pins.length == _shops.length
                ? 'Showing $_viewLabel \u2022 ${_pins.length} shops on the map'
                : 'Showing $_viewLabel \u2022 ${_pins.length} of ${_shops.length} shops have a map location',
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 16),
          if (_loading)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(40),
                child: CircularProgressIndicator(),
              ),
            ),
          if (_error != null) ...[
            Text(
              _error!,
              style: AppTextStyles.body.copyWith(color: AppColors.error),
            ),
            const SizedBox(height: 12),
            FilledButton(onPressed: _load, child: const Text('Retry')),
          ],
          if (!_loading && _error == null && _shops.isEmpty)
            const Padding(
              padding: EdgeInsets.all(40),
              child: Center(child: Text('No verified shops found yet.')),
            ),
          ..._shops.map(_shopCard),
        ],
      ),
    ),
  );

  void _openShop(CustomerShop shop) {
    Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => CustomerShopDetailScreen(shop: shop)),
    );
  }

  void _showShop(CustomerShop shop) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (context) => Padding(
        padding: const EdgeInsets.fromLTRB(20, 4, 20, 28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  backgroundColor: AppColors.primary.withValues(alpha: .12),
                  child: const Icon(
                    Icons.storefront_rounded,
                    color: AppColors.primary,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(child: Text(shop.name, style: AppTextStyles.h3)),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              [
                shop.address,
                shop.city,
                shop.province,
              ].where((value) => value.isNotEmpty).join(', '),
            ),
            const SizedBox(height: 8),
            Text(
              shop.rating > 0
                  ? '${shop.rating.toStringAsFixed(1)} stars (${shop.ratingCount} reviews)'
                  : 'No reviews yet',
            ),
            const SizedBox(height: 8),
            Chip(label: Text('Compliance: ${shop.complianceStatus}')),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: () {
                  Navigator.pop(context);
                  _openShop(shop);
                },
                child: const Text('View shop profile'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _shopCard(CustomerShop shop) => Card(
    margin: const EdgeInsets.only(bottom: 12),
    child: ListTile(
      onTap: () => _openShop(shop),
      leading: CircleAvatar(
        backgroundColor: AppColors.primary.withValues(alpha: .12),
        child: const Icon(Icons.storefront_rounded, color: AppColors.primary),
      ),
      title: Text(shop.name, style: AppTextStyles.subtitle),
      subtitle: Text(
        [
          if (shop.address.isNotEmpty) shop.address,
          if (shop.city.isNotEmpty) shop.city,
          if (shop.complianceStatus.isNotEmpty)
            'Status: ${shop.complianceStatus}',
        ].join(' • '),
      ),
      trailing: shop.rating > 0
          ? Text('${shop.rating.toStringAsFixed(1)} ★')
          : null,
    ),
  );
}
