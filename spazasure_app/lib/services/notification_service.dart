import 'package:flutter/widgets.dart';

import 'api_service.dart';
import 'order_service.dart';
import '../models/models.dart';

class NotificationService {
  /// Opens the order an order or delivery notification is about. Falls back to the
  /// orders list when there is no order id or the order can't be loaded.
  static Future<void> openOrder(NavigatorState navigator, String? orderId) async {
    if (orderId != null && orderId.isNotEmpty) {
      try {
        final order = await OrderService.getOrder(orderId);
        navigator.pushNamed('/order-detail', arguments: order);
        return;
      } catch (_) {
        // Deleted, or not this shop's order: show the list instead
      }
    }
    navigator.pushNamed('/orders');
  }

  /// Fetch notifications for the logged-in user (shop owner or supplier).
  static Future<List<AppNotification>> getNotifications({
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final res = await ApiService.get(
        '/shop/notifications?page=$page&pageSize=$pageSize',
      );
      final data = res['data'];
      if (data is List) {
        return data
            .map(
              (n) => AppNotification(
                id: n['id']?.toString() ?? '',
                title: n['title'] ?? '',
                body: n['message'] ?? n['body'] ?? '',
                type: n['type'] ?? 'system',
                createdAt:
                    DateTime.tryParse(n['createdAt'] ?? '') ?? DateTime.now(),
                isRead: n['isRead'] ?? false,
                referenceId: n['referenceId']?.toString(),
              ),
            )
            .toList();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  /// Get unread notification count
  static Future<int> getUnreadCount() async {
    try {
      final res = await ApiService.get('/shop/notifications/unread-count');
      // The endpoint returns { count } at the top level (not wrapped in data)
      final data = res['data'];
      final count =
          res['count'] ?? (data is Map<String, dynamic> ? data['count'] : null);
      return (count as num?)?.toInt() ?? 0;
    } catch (_) {
      return 0;
    }
  }

  /// Mark a single notification as read
  static Future<void> markAsRead(String id) async {
    try {
      await ApiService.put('/shop/notifications/$id/read');
    } catch (_) {}
  }

  /// Mark all notifications as read
  static Future<void> markAllAsRead() async {
    try {
      await ApiService.put('/shop/notifications/read-all');
    } catch (_) {}
  }
}
