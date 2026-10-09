import 'api_service.dart';

class CustomerShop {
  final String id;
  final String name;
  final String address;
  final String city;
  final String province;
  final double? latitude;
  final double? longitude;
  final double rating;
  final int ratingCount;
  final String complianceStatus;
  final String ownerName;
  final String phone;
  final String email;
  final String postalCode;
  final String status;
  final bool isVerified;
  final DateTime? registeredAt;

  const CustomerShop({
    required this.id,
    required this.name,
    required this.address,
    required this.city,
    required this.province,
    required this.latitude,
    required this.longitude,
    required this.rating,
    required this.ratingCount,
    required this.complianceStatus,
    this.ownerName = '',
    this.phone = '',
    this.email = '',
    this.postalCode = '',
    this.status = 'pending',
    this.isVerified = false,
    this.registeredAt,
  });

  factory CustomerShop.fromJson(Map<String, dynamic> json) => CustomerShop(
    id: json['id']?.toString() ?? '',
    name: json['shopName']?.toString() ?? 'Shop',
    address: json['address']?.toString() ?? '',
    city: json['city']?.toString() ?? '',
    province: json['province']?.toString() ?? '',
    latitude: (json['latitude'] as num?)?.toDouble(),
    longitude: (json['longitude'] as num?)?.toDouble(),
    rating: (json['ratingAvg'] as num?)?.toDouble() ?? 0,
    ratingCount: (json['ratingCount'] as num?)?.toInt() ?? 0,
    complianceStatus: json['complianceStatus']?.toString() ?? 'pending',
    ownerName: json['ownerName']?.toString() ?? '',
    phone: json['phone']?.toString() ?? '',
    email: json['email']?.toString() ?? '',
    postalCode: json['postalCode']?.toString() ?? '',
    status: json['status']?.toString() ?? 'pending',
    isVerified: json['isVerified'] == true || json['status'] == 'verified',
    registeredAt: DateTime.tryParse(json['registeredAt']?.toString() ?? ''),
  );
}

class CustomerShopService {
  static Future<List<CustomerShop>> list({String? search}) async {
    final suffix = search == null || search.trim().isEmpty
        ? ''
        : '?search=${Uri.encodeQueryComponent(search.trim())}';
    final response = await ApiService.get('/customer/shops$suffix');
    final raw = response['data'];
    final items = raw is List ? raw : const <dynamic>[];
    return items
        .whereType<Map<String, dynamic>>()
        .map(CustomerShop.fromJson)
        .toList();
  }

  static Future<CustomerShop> getById(String id) async {
    final response = await ApiService.get('/customer/shops/$id');
    return CustomerShop.fromJson(response['data'] as Map<String, dynamic>);
  }

  static Future<List<Map<String, dynamic>>> reviews(String id) async {
    final response = await ApiService.get('/customer/shops/$id/reviews');
    final raw = response['data'];
    return raw is List ? raw.whereType<Map<String, dynamic>>().toList() : [];
  }

  static Future<void> review(String id, int rating, String? comment) async {
    await ApiService.post('/customer/shops/$id/reviews', {
      'rating': rating,
      'comment': comment,
    });
  }
}
