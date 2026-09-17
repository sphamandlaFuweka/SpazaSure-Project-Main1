import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/customer_shop_service.dart';

class CustomerShopsScreen extends StatefulWidget {
  const CustomerShopsScreen({super.key});

  @override
  State<CustomerShopsScreen> createState() => _CustomerShopsScreenState();
}

class _CustomerShopsScreenState extends State<CustomerShopsScreen> {
  final _searchController = TextEditingController();
  List<CustomerShop> _shops = [];
  bool _loading = true;
  String? _error;

  LatLng get _mapCenter {
    final shop = _shops.firstWhere(
      (item) => item.latitude != null && item.longitude != null,
      orElse: () => const CustomerShop(
        id: '',
        name: '',
        address: '',
        city: '',
        province: '',
        latitude: -29,
        longitude: 24.5,
        rating: 0,
        ratingCount: 0,
        complianceStatus: '',
      ),
    );
    return LatLng(shop.latitude ?? -29, shop.longitude ?? 24.5);
  }

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
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
              prefixIcon: const Icon(Icons.search),
              suffixIcon: IconButton(
                onPressed: _load,
                icon: const Icon(Icons.arrow_forward),
              ),
            ),
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 280,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(18),
              child: FlutterMap(
                options: MapOptions(initialCenter: _mapCenter, initialZoom: 5),
                children: [
                  TileLayer(
                    urlTemplate:
                        'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                    userAgentPackageName: 'co.spazasure.app',
                  ),
                  MarkerLayer(
                    markers: _shops
                        .where(
                          (shop) =>
                              shop.latitude != null && shop.longitude != null,
                        )
                        .map(
                          (shop) => Marker(
                            point: LatLng(shop.latitude!, shop.longitude!),
                            width: 48,
                            height: 48,
                            child: GestureDetector(
                              onTap: () => _showShop(shop),
                              child: const Icon(
                                Icons.location_on,
                                color: AppColors.primary,
                                size: 42,
                              ),
                            ),
                          ),
                        )
                        .toList(),
                  ),
                ],
              ),
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
            Text(shop.name, style: AppTextStyles.h3),
            const SizedBox(height: 8),
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
          ],
        ),
      ),
    );
  }

  Widget _shopCard(CustomerShop shop) => Card(
    margin: const EdgeInsets.only(bottom: 12),
    child: ListTile(
      leading: CircleAvatar(
        backgroundColor: AppColors.primary.withValues(alpha: .12),
        child: const Icon(Icons.storefront, color: AppColors.primary),
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
