import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/geo/south_africa.dart';
import 'package:spazasure_app/services/customer_shop_service.dart';
import 'package:url_launcher/url_launcher.dart';
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
  String _filter = 'all';

  List<CustomerShop> get _visibleShops => switch (_filter) {
    'verified' => _shops.where((s) => s.isVerified).toList(),
    'unverified' => _shops.where((s) => !s.isVerified).toList(),
    _ => _shops,
  };

  List<LatLng> get _pins => _visibleShops
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
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              for (final (value, label) in const [
                ('all', 'All'),
                ('verified', 'Verified'),
                ('unverified', 'Unverified'),
              ])
                ChoiceChip(
                  label: Text(label),
                  selected: _filter == value,
                  onSelected: (_) {
                    setState(() => _filter = value);
                    _frameMap();
                  },
                ),
            ],
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 320,
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
                          for (final shop in _visibleShops)
                            if (shop.latitude != null && shop.longitude != null)
                              Marker(
                                point: LatLng(shop.latitude!, shop.longitude!),
                                width: 48,
                                height: 48,
                                child: GestureDetector(
                                  onTap: () => _showShop(shop),
                                  child: Icon(
                                    Icons.location_on_rounded,
                                    color: _pinColor(shop),
                                    size: 42,
                                    semanticLabel:
                                        '${shop.name}, ${shop.isVerified ? 'verified' : 'unverified'}',
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
          Row(
            children: [
              _legendDot(_verifiedColor, 'Verified'),
              const SizedBox(width: 16),
              _legendDot(_unverifiedColor, 'Unverified'),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            _pins.length == _visibleShops.length
                ? 'Showing $_viewLabel \u2022 ${_pins.length} shops on the map'
                : 'Showing $_viewLabel \u2022 ${_pins.length} of ${_visibleShops.length} shops have a map location',
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
          if (!_loading && _error == null && _visibleShops.isEmpty)
            const Padding(
              padding: EdgeInsets.all(40),
              child: Center(child: Text('No shops found.')),
            ),
          ..._visibleShops.map(_shopCard),
        ],
      ),
    ),
  );

  static const _verifiedColor = Color(0xFF2E7D32);
  static const _unverifiedColor = Color(0xFFD32F2F);

  Color _pinColor(CustomerShop shop) =>
      shop.isVerified ? _verifiedColor : _unverifiedColor;

  Widget _legendDot(Color color, String label) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(Icons.location_on_rounded, color: color, size: 18),
      const SizedBox(width: 4),
      Text(label, style: AppTextStyles.caption),
    ],
  );

  Widget _statusPill(CustomerShop shop) {
    final color = _pinColor(shop);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: .12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        shop.isVerified ? 'Verified' : 'Unverified',
        style: AppTextStyles.caption.copyWith(
          color: color,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }

  Future<void> _call(CustomerShop shop) async {
    if (shop.phone.isEmpty) return;
    await launchUrl(Uri(scheme: 'tel', path: shop.phone));
  }

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
                _statusPill(shop),
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
            const SizedBox(height: 20),
            Row(
              children: [
                if (shop.phone.isNotEmpty) ...[
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => _call(shop),
                      icon: const Icon(Icons.call_rounded),
                      label: const Text('Call'),
                    ),
                  ),
                  const SizedBox(width: 12),
                ],
                Expanded(
                  child: FilledButton(
                    onPressed: () {
                      Navigator.pop(context);
                      _openShop(shop);
                    },
                    child: const Text('Full details'),
                  ),
                ),
              ],
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
        backgroundColor: _pinColor(shop).withValues(alpha: .12),
        child: Icon(Icons.storefront_rounded, color: _pinColor(shop)),
      ),
      title: Text(shop.name, style: AppTextStyles.subtitle),
      subtitle: Text(
        [
          if (shop.address.isNotEmpty) shop.address,
          if (shop.city.isNotEmpty) shop.city,
        ].join(' \u2022 '),
      ),
      trailing: _statusPill(shop),
    ),
  );
}
