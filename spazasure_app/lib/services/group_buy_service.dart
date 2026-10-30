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

  /// Units no shop has taken yet; the target is shared out until this is 0.
  final int remaining;

  /// The least this shop may take of this product (its own minimum order).
  final int minPerShop;

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
    this.remaining = 0,
    this.minPerShop = 1,
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
      remaining:
          (json['remaining'] as num?)?.toInt() ??
          (target - current).clamp(0, target),
      minPerShop: (json['minPerShop'] as num?)?.toInt() ?? 1,
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

  /// This shop's payment for the group buy ('paid'), or null when it hasn't joined or joined before paying existed.
  final String? myPaymentStatus;
  final double myAmountPaid;

  /// The fewest items a shop buys in total, any mix of products: 10, or all that's left when fewer remain.
  final int minTotal;

  /// The smallest amount a shop pays for the items (delivery not counted), alongside the
  /// item minimum: both apply. 0 = none. Waived when the shop takes everything left.
  final double minAmount;

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
    this.myPaymentStatus,
    this.myAmountPaid = 0,
    this.minTotal = 10,
    this.minAmount = 0,
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
      minTotal: (json['minTotal'] as num?)?.toInt() ?? 10,
      minAmount: (json['minAmount'] as num?)?.toDouble() ?? 0,
      myPaymentStatus: (json['my'] as Map<String, dynamic>?)?['paymentStatus']
          ?.toString(),
      myAmountPaid:
          ((json['my'] as Map<String, dynamic>?)?['amountPaid'] as num?)
              ?.toDouble() ??
          0,
    );
  }

  bool get isPaid => myPaymentStatus == 'paid';

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

  /// Items at the group price, delivery and the total for these quantities, plus the wallet balance.
  static Future<GroupBuyQuote> quote(
    String id,
    List<Map<String, dynamic>> items,
  ) async {
    final response = await ApiService.post('/shop/group-buy/$id/quote', {
      'items': items,
    });
    return GroupBuyQuote.fromJson(response['data'] as Map<String, dynamic>);
  }

  /// Joins and pays: 'wallet' pays at once; 'paystack' returns a checkoutUrl to pay by card.
  static Future<GroupBuyJoinResult> join(
    String id,
    List<Map<String, dynamic>> items,
    String paymentMethod,
  ) async {
    final response = await ApiService.post('/shop/group-buy/$id/join', {
      'items': items,
      'paymentMethod': paymentMethod,
    });
    final data = response['data'] as Map<String, dynamic>? ?? const {};
    return GroupBuyJoinResult(
      paid: data['paid'] == true,
      checkoutUrl: data['checkoutUrl']?.toString(),
      message: response['message']?.toString(),
    );
  }

  /// After the card checkout: 'joined', 'refunded' (the money went to the wallet) or 'pending'.
  static Future<String> confirmPayment(String id) async {
    final response = await ApiService.post(
      '/shop/group-buy/$id/confirm-payment',
      {},
    );
    return (response['data'] as Map<String, dynamic>?)?['status']?.toString() ??
        'pending';
  }

  static Future<void> leave(String id) async {
    await ApiService.post('/shop/group-buy/$id/leave', {});
  }
}

class GroupBuyQuoteLine {
  final String productName;
  final int quantity;
  final double unitPrice;
  final double lineTotal;

  const GroupBuyQuoteLine({
    required this.productName,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });
}

class GroupBuyQuote {
  final List<GroupBuyQuoteLine> lines;
  final double itemsTotal;
  final double deliveryFee;
  final double total;
  final double walletBalance;

  const GroupBuyQuote({
    required this.lines,
    required this.itemsTotal,
    required this.deliveryFee,
    required this.total,
    required this.walletBalance,
  });

  factory GroupBuyQuote.fromJson(Map<String, dynamic> json) {
    double money(String key) => (json[key] as num?)?.toDouble() ?? 0;
    return GroupBuyQuote(
      lines: (json['items'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(
            (line) => GroupBuyQuoteLine(
              productName: line['productName']?.toString() ?? '',
              quantity: (line['quantity'] as num?)?.toInt() ?? 0,
              unitPrice: (line['unitPrice'] as num?)?.toDouble() ?? 0,
              lineTotal: (line['lineTotal'] as num?)?.toDouble() ?? 0,
            ),
          )
          .toList(),
      itemsTotal: money('itemsTotal'),
      deliveryFee: money('deliveryFee'),
      total: money('total'),
      walletBalance: money('walletBalance'),
    );
  }
}

class GroupBuyJoinResult {
  final bool paid;
  final String? checkoutUrl;
  final String? message;

  const GroupBuyJoinResult({
    required this.paid,
    this.checkoutUrl,
    this.message,
  });
}
