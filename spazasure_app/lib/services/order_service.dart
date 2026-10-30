import 'dart:convert';

import 'api_service.dart';
import '../models/models.dart';

class OrderCheckout {
  final String orderNumber;

  /// Paystack page to pay on. Null for wallet and cash on delivery (nothing to pay online).
  final String? checkoutUrl;
  final String paymentMethod;
  final double totalAmount;

  const OrderCheckout({
    required this.orderNumber,
    this.checkoutUrl,
    this.paymentMethod = OrderService.paystack,
    this.totalAmount = 0,
  });

  bool get isCashOnDelivery => paymentMethod == OrderService.cashOnDelivery;
}

/// What checkout will charge, worked out by the server: the branch each supplier ships from,
/// the distance-based delivery fee, and any stock problem that stops the order.
class CheckoutQuote {
  final double deliveryFeeTotal;
  final bool canCheckout;
  final List<SupplierQuote> suppliers;

  const CheckoutQuote({
    required this.deliveryFeeTotal,
    required this.canCheckout,
    required this.suppliers,
  });

  factory CheckoutQuote.fromJson(Map<String, dynamic> json) => CheckoutQuote(
    deliveryFeeTotal: (json['deliveryFeeTotal'] as num?)?.toDouble() ?? 0,
    canCheckout: json['canCheckout'] != false,
    suppliers: (json['suppliers'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(SupplierQuote.fromJson)
        .toList(),
  );

  List<String> get problems => [
    for (final s in suppliers)
      if (s.problem != null) s.problem!,
  ];
}

class SupplierQuote {
  final String supplierName;
  final String? shipsFrom;
  final String? shipsFromCity;
  final double? distanceKm;
  final double? deliveryFee;
  final String? problem;

  const SupplierQuote({
    required this.supplierName,
    this.shipsFrom,
    this.shipsFromCity,
    this.distanceKm,
    this.deliveryFee,
    this.problem,
  });

  factory SupplierQuote.fromJson(Map<String, dynamic> json) {
    final from = json['shipsFrom'] as Map<String, dynamic>?;
    return SupplierQuote(
      supplierName: json['supplierName']?.toString() ?? '',
      shipsFrom: from?['name']?.toString(),
      shipsFromCity: from?['city']?.toString(),
      distanceKm: (json['distanceKm'] as num?)?.toDouble(),
      deliveryFee: (json['deliveryFee'] as num?)?.toDouble(),
      problem: json['problem']?.toString(),
    );
  }
}

class OrderService {
  static const paystack = 'paystack';
  static const cashOnDelivery = 'cash_on_delivery';
  static const wallet = 'wallet';

  /// Asks the server to check with Paystack that this order was paid, so it's confirmed straight
  /// away instead of waiting for Paystack's webhook. Returns true when it's paid.
  static Future<bool> confirmPayment(String orderNumber) async {
    if (orderNumber.isEmpty) return false;
    try {
      final res = await ApiService.post('/shop/orders/confirm-payment', {
        'orderNumber': orderNumber,
      });
      return (res['data'] as Map<String, dynamic>?)?['paid'] == true;
    } catch (_) {
      return false; // the webhook still confirms it later
    }
  }

  /// Opens a new Paystack checkout for an unpaid card order. A null checkoutUrl means it was
  /// paid after all.
  static Future<OrderCheckout> payAgain(String orderId) async {
    final res = await ApiService.post('/shop/orders/$orderId/pay', {});
    final data = res['data'] as Map<String, dynamic>? ?? const {};
    return OrderCheckout(
      orderNumber: data['orderNumber']?.toString() ?? '',
      checkoutUrl: data['paid'] == true
          ? null
          : data['checkoutUrl']?.toString(),
      totalAmount: (data['totalAmount'] as num?)?.toDouble() ?? 0,
    );
  }

  /// Cancels a card order the shop hasn't paid for.
  static Future<void> cancelUnpaid(String orderId) async {
    await ApiService.post('/shop/orders/$orderId/cancel', {});
  }

  /// The delivery fees and shipping branches for the cart, before the shop pays.
  static Future<CheckoutQuote> quote({
    required List<CartItem> items,
    required String deliveryOption,
  }) async {
    final res = await ApiService.post('/shop/orders/quote', {
      'deliveryType': deliveryOption,
      'items': [
        for (final i in items)
          {
            'productId': i.product.id,
            'quantity': i.quantity,
            'unitPrice': i.product.price,
          },
      ],
    });
    return CheckoutQuote.fromJson(res['data'] as Map<String, dynamic>);
  }

  static Future<OrderCheckout> placeOrder({
    required List<CartItem> items,
    required String deliveryOption,
    String paymentMethod = paystack,
    required String deliveryAddress,
  }) async {
    final res = await ApiService.post('/shop/orders', {
      'deliveryType': deliveryOption,
      'paymentMethod': paymentMethod,
      'deliveryAddress': deliveryAddress,
      'items': items
          .map(
            (i) => <String, dynamic>{
              'productId': i.product.id,
              'quantity': i.quantity,
              'unitPrice': i.product.price,
            },
          )
          .toList(),
    });
    final data = res['data'] as Map<String, dynamic>;
    final method = data['paymentMethod']?.toString() ?? paymentMethod;
    final checkoutUrl = data['checkoutUrl']?.toString();
    if (method == paystack && (checkoutUrl == null || checkoutUrl.isEmpty)) {
      throw StateError('Paystack checkout was not returned for this order.');
    }
    return OrderCheckout(
      orderNumber: data['orderNumber']?.toString() ?? '',
      checkoutUrl: checkoutUrl,
      paymentMethod: method,
      totalAmount: (data['totalAmount'] as num?)?.toDouble() ?? 0,
    );
  }

  /// A link to the order's invoice PDF (valid for 30 minutes). The invoice is issued in the
  /// supplier's name the first time it's asked for; a tax invoice when the supplier is VAT registered.
  static Future<String> invoiceLink(String orderId) async {
    final res = await ApiService.get('/invoices/orders/$orderId');
    final url = (res['data'] as Map<String, dynamic>)['downloadUrl']
        ?.toString();
    if (url == null || url.isEmpty) {
      throw StateError('The invoice is not available yet.');
    }
    return url;
  }

  static Future<List<Order>> getOrders({String? status}) async {
    final query = status != null ? '?status=$status' : '';
    final res = await ApiService.get('/shop/orders$query');
    final rawData = res['data'];
    final items = rawData is List
        ? rawData
        : rawData is Map<String, dynamic>
        ? (rawData['items'] as List<dynamic>? ?? const [])
        : const <dynamic>[];
    return items.whereType<Map<String, dynamic>>().map(_mapOrder).toList();
  }

  static Future<Order> getOrder(String id) async {
    final res = await ApiService.get('/shop/orders/$id');
    return _mapOrder(res['data'] as Map<String, dynamic>);
  }

  static Future<void> confirmDelivery(String orderId, {String? code}) async {
    await ApiService.patch('/shop/orders/$orderId/confirm-delivery', {
      if (code != null && code.trim().isNotEmpty) 'code': code.trim(),
    });
  }

  /// Product images are stored as a JSON array string, e.g. '["https://..."]'
  static String _firstImage(dynamic raw) {
    if (raw is List && raw.isNotEmpty) return raw.first.toString();
    if (raw is! String || raw.isEmpty) return '';
    if (!raw.trimLeft().startsWith('[')) return raw;
    try {
      final decoded = jsonDecode(raw);
      if (decoded is List && decoded.isNotEmpty) {
        return decoded.first.toString();
      }
    } catch (_) {}
    return '';
  }

  static Order _mapOrder(Map<String, dynamic> o) {
    final items = (o['items'] as List<dynamic>? ?? [])
        .map(
          (i) => OrderItem(
            productId: i['productId'].toString(),
            productName: i['name'] ?? i['productName'] ?? '',
            productImage: _firstImage(i['productImage']),
            quantity: (i['quantity'] as num?)?.toInt() ?? 0,
            price: (i['unitPrice'] as num).toDouble(),
          ),
        )
        .toList();

    final subtotal = items.fold<double>(0, (s, i) => s + i.total);

    return Order(
      id: o['id'].toString(),
      orderNumber: o['orderNumber'] ?? '',
      shopId: o['shopId']?.toString() ?? '',
      supplierId: o['supplierId']?.toString() ?? '',
      // List/detail endpoints return a flat supplierName; older payloads nest it
      supplierName:
          o['supplierName']?.toString() ??
          o['supplier']?['companyName']?.toString() ??
          '',
      items: items,
      subtotal: subtotal,
      deliveryFee: (o['deliveryFee'] as num? ?? 0).toDouble(),
      platformFee: (o['platformCommission'] as num? ?? 0).toDouble(),
      total: (o['totalAmount'] as num? ?? 0).toDouble(),
      status: o['status'] ?? 'pending',
      deliveryOption: o['deliveryType'] ?? 'standard',
      paymentMethod: o['paymentMethod'] ?? 'eft',
      paymentStatus: o['paymentStatus'] ?? 'pending',
      groupBuyId: o['groupBuyId']?.toString(),
      isGroupBuy: o['isGroupBuy'] == true || o['groupBuyId'] != null,
      deliveryAddress: o['deliveryAddress']?.toString() ?? '',
      deliveryDistanceKm: (o['deliveryDistanceKm'] as num?)?.toDouble(),
      deliveryLatitude: (o['deliveryLatitude'] as num?)?.toDouble(),
      deliveryLongitude: (o['deliveryLongitude'] as num?)?.toDouble(),
      shipsFrom: o['shipsFrom']?.toString(),
      driverName: o['driverName']?.toString(),
      deliveryCode: o['deliveryCode']?.toString(),
      createdAt: DateTime.tryParse(o['createdAt'] ?? '') ?? DateTime.now(),
      estimatedDelivery: o['estimatedDelivery'] != null
          ? DateTime.tryParse(o['estimatedDelivery'])
          : null,
    );
  }
}
