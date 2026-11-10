import '../core/geo/south_africa.dart';
import 'address_service.dart';
import 'api_service.dart';

/// Where a Bulk Buyer's orders are delivered (or which suppliers they can collect from).
class BulkAddress {
  final String address;
  final String? city;
  final String province;
  final String? postalCode;
  final String phone;

  const BulkAddress({
    required this.address,
    this.city,
    required this.province,
    this.postalCode,
    this.phone = '',
  });

  String get fullLine => [
    address,
    if (city != null && city!.isNotEmpty) city!,
    province,
  ].where((p) => p.isNotEmpty).join(', ');

  factory BulkAddress.fromJson(Map<String, dynamic> j) => BulkAddress(
    address: j['address']?.toString() ?? '',
    city: j['city']?.toString(),
    province: j['province']?.toString() ?? '',
    postalCode: j['postalCode']?.toString(),
    phone: j['phone']?.toString() ?? '',
  );
}

/// A shopper's Bulk Buyer status: whether the plan is active, the minimum order, and their address.
class BulkAccount {
  final bool isBulkActive;
  final double monthlyPrice;
  final double minOrderAmount;
  final BulkAddress? address;

  const BulkAccount({
    required this.isBulkActive,
    required this.monthlyPrice,
    required this.minOrderAmount,
    this.address,
  });

  factory BulkAccount.fromJson(Map<String, dynamic> j) => BulkAccount(
    isBulkActive: j['isBulkActive'] == true,
    monthlyPrice: (j['monthlyPrice'] as num?)?.toDouble() ?? 150,
    minOrderAmount: (j['minOrderAmount'] as num?)?.toDouble() ?? 1000,
    address: j['address'] is Map<String, dynamic>
        ? BulkAddress.fromJson(j['address'] as Map<String, dynamic>)
        : null,
  );
}

class CustomerBulkService {
  /// The province as suppliers list it; from the map position when the address search names it differently.
  static String _province(AddressSuggestion place) {
    for (final p in SouthAfrica.provinces) {
      if (p.name.toLowerCase() == place.province.toLowerCase()) return p.name;
    }
    return SouthAfrica.provinceAt(place.latitude, place.longitude)?.name ??
        place.province;
  }

  static Future<BulkAccount> getAccount() async {
    final res = await ApiService.get('/customer/bulk/account');
    return BulkAccount.fromJson(res['data'] as Map<String, dynamic>);
  }

  /// Saves the delivery address picked from the suggestions (it carries the map location).
  static Future<BulkAddress> saveAddress(
    AddressSuggestion place, {
    String? phone,
  }) async {
    final res = await ApiService.put('/customer/bulk/account', {
      'address': place.addressLine.isNotEmpty ? place.addressLine : place.label,
      'city': place.city,
      'province': _province(place),
      'postalCode': place.postalCode,
      'latitude': place.latitude,
      'longitude': place.longitude,
      if (phone != null && phone.trim().isNotEmpty) 'phone': phone.trim(),
    });
    return BulkAddress.fromJson(res['data'] as Map<String, dynamic>);
  }
}
