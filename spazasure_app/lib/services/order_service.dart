import 'api_service.dart';
import '../models/models.dart';

class OrderService {
  static Future<String> placeOrder({
    required List<CartItem> items,
    required String deliveryOption,
    required String paymentMethod,
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
    return data['orderNumber'] as String;
  }

  static Future<List<Order>> getOrders({String? status}) async {
    final query = status != null ? '?status=$status' : '';
    final res = await ApiService.get('/shop/orders$query');
    final data = res['data'] as Map<String, dynamic>;
    final items = data['items'] as List<dynamic>;
    return items.map((i) => _mapOrder(i as Map<String, dynamic>)).toList();
  }

  static Future<Order> getOrder(String id) async {
    final res = await ApiService.get('/shop/orders/$id');
    return _mapOrder(res['data'] as Map<String, dynamic>);
  }

  static Future<void> confirmDelivery(String orderId) async {
    await ApiService.patch('/shop/orders/$orderId/confirm-delivery', {});
  }

  static Order _mapOrder(Map<String, dynamic> o) {
    final items = (o['items'] as List<dynamic>? ?? [])
        .map(
          (i) => OrderItem(
            productId: i['productId'].toString(),
            productName: i['name'] ?? i['productName'] ?? '',
            productImage: '',
            quantity: i['quantity'] as int,
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
      supplierName: o['supplier']?['companyName'] ?? '',
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
      createdAt: DateTime.tryParse(o['createdAt'] ?? '') ?? DateTime.now(),
      estimatedDelivery: o['estimatedDelivery'] != null
          ? DateTime.tryParse(o['estimatedDelivery'])
          : null,
    );
  }
}
