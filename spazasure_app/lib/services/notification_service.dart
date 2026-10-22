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

  /// Screens notifications without an order or chat open, by type.
  static const _routes = {
    'wallet': '/wallet',
    'group_buy': '/group-buy',
    'compliance': '/compliance',
  };

  /// Whether a notification of this type has a screen to open.
  static bool canOpen(String type) =>
      type == 'order' ||
      type == 'delivery' ||
      type == 'chat' ||
      _routes.containsKey(type);

  /// Button text for the screen a notification opens, e.g. "View order".
  static String actionLabel(String type) => switch (type) {
    'order' => 'View order',
    'delivery' => 'Track delivery',
    'chat' => 'Open chat',
    'wallet' => 'Open wallet',
    'group_buy' => 'View group buy',
    'compliance' => 'Open my documents',
    _ => 'Open',
  };

  /// Opens the screen a notification is about. Returns false when it has none.
  static Future<bool> openTarget(
    NavigatorState navigator,
    String type,
    String? referenceId,
  ) async {
    if (type == 'order' || type == 'delivery') {
      await openOrder(navigator, referenceId);
      return true;
    }
    if (type == 'chat') {
      navigator.pushNamed('/inbox', arguments: referenceId);
      return true;
    }
    final route = _routes[type];
    if (route == null) return false;
    navigator.pushNamed(route);
    return true;
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
