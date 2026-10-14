import 'dart:convert';

import 'api_service.dart';
import '../models/models.dart';

class OrderCheckout {
  final String orderNumber;

  /// Paystack page to pay on. Null for cash on delivery (nothing to pay now).
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

class OrderService {
  static const paystack = 'paystack';
  static const cashOnDelivery = 'cash_on_delivery';

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

  static Future<void> confirmDelivery(String orderId) async {
    await ApiService.patch('/shop/orders/$orderId/confirm-delivery', {});
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
      createdAt: DateTime.tryParse(o['createdAt'] ?? '') ?? DateTime.now(),
      estimatedDelivery: o['estimatedDelivery'] != null
          ? DateTime.tryParse(o['estimatedDelivery'])
          : null,
    );
  }
}
