import 'api_service.dart';

class GroupBuyProduct {
  final String id;
  final String productId;
  final String productName;
  final int minOrderQty;
  final int targetQty;
  final int currentQty;
  final int participantCount;
  final double progress;
  final String status;
  final int discountPct;
  final double discountPrice;

  const GroupBuyProduct({
    required this.id,
    required this.productId,
    required this.productName,
    required this.minOrderQty,
    required this.targetQty,
    required this.currentQty,
    required this.participantCount,
    required this.progress,
    required this.status,
    this.discountPct = 0,
    this.discountPrice = 0,
  });

  factory GroupBuyProduct.fromJson(Map<String, dynamic> json) {
    final target = (json['targetQty'] as num?)?.toInt() ?? 0;
    final current = (json['currentQty'] as num?)?.toInt() ?? 0;
    return GroupBuyProduct(
      id: json['id']?.toString() ?? '',
      productId: json['productId']?.toString() ?? '',
      productName: json['productName']?.toString() ?? '',
      minOrderQty: (json['minOrderQty'] as num?)?.toInt() ?? 1,
      targetQty: target,
      currentQty: current,
      participantCount: (json['participantCount'] as num?)?.toInt() ?? 0,
      progress: json['progress'] is num
          ? (json['progress'] as num).toDouble() / 100
          : (target > 0 ? current / target : 0),
      status: json['status']?.toString() ?? 'active',
      discountPct: (json['discountPct'] as num?)?.toInt() ?? 0,
      discountPrice: (json['discountPrice'] as num?)?.toDouble() ?? 0,
    );
  }
}

class GroupBuy {
  final String id;
  final String title;
  final String description;
  final String? bannerImageUrl;
  final String supplierId;
  final String supplierName;
  final String createdByShopName;

  /// Where shops must be to join, e.g. "Within 40 km of Durban"; null for older campaigns.
  final String? areaLabel;
  final DateTime? expiresAt;
  final String status;
  final int participantCount;
  final DateTime? createdAt;
  final List<GroupBuyProduct> products;
  final List<dynamic> participants;

  const GroupBuy({
    required this.id,
    required this.title,
    required this.description,
    this.bannerImageUrl,
    required this.supplierId,
    required this.supplierName,
    required this.createdByShopName,
    this.areaLabel,
    required this.expiresAt,
    required this.status,
    required this.participantCount,
    required this.createdAt,
    required this.products,
    required this.participants,
  });

  factory GroupBuy.fromJson(Map<String, dynamic> json) {
    return GroupBuy(
      id: json['id']?.toString() ?? '',
      title: json['title']?.toString() ?? 'Group Buy',
      description: json['description']?.toString() ?? '',
      bannerImageUrl: ApiService.resolveUploadUrl(
        json['bannerImageUrl']?.toString(),
      ),
      supplierId: json['supplierId']?.toString() ?? '',
      supplierName: json['supplierName']?.toString() ?? '',
      createdByShopName: json['createdByShopName']?.toString() ?? '',
      areaLabel: _areaLabel(json['area']),
      expiresAt: DateTime.tryParse(json['expiresAt']?.toString() ?? ''),
      status: json['status']?.toString() ?? 'active',
      participantCount: (json['participantCount'] as num?)?.toInt() ?? 0,
      createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
      products: (json['products'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(GroupBuyProduct.fromJson)
          .toList(),
      participants: json['participants'] as List<dynamic>? ?? const [],
    );
  }

  static String? _areaLabel(dynamic area) {
    if (area is! Map<String, dynamic>) return null;
    final radius = (area['radiusKm'] as num?)?.toDouble();
    final city = area['city']?.toString().trim() ?? '';
    final place = city.isNotEmpty
        ? city
        : area['province']?.toString().trim() ?? '';
    if (radius == null || place.isEmpty) return null;
    final km = radius == radius.roundToDouble()
        ? radius.toStringAsFixed(0)
        : radius.toStringAsFixed(1);
    return 'Within $km km of $place';
  }
}

class GroupBuyService {
  static Future<List<GroupBuy>> getGroupBuys(String status) async {
    final response = await ApiService.get('/shop/group-buy?status=$status');
    final data = response['data'];
    final rawGroups = data is List<dynamic>
        ? data
        : data is Map<String, dynamic>
        ? data['items'] as List<dynamic>? ?? const []
        : const <dynamic>[];
    return rawGroups
        .whereType<Map<String, dynamic>>()
        .map(GroupBuy.fromJson)
        .toList();
  }

  static Future<GroupBuy> getGroupBuy(String id) async {
    final response = await ApiService.get('/shop/group-buy/$id');
    return GroupBuy.fromJson(response['data'] as Map<String, dynamic>);
  }

  static Future<void> join(String id, List<Map<String, dynamic>> items) async {
    await ApiService.post('/shop/group-buy/$id/join', {'items': items});
  }

  static Future<void> leave(String id) async {
    await ApiService.post('/shop/group-buy/$id/leave', {});
  }
}
