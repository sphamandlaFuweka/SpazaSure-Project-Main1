import 'api_service.dart';
import '../models/models.dart';

class NotificationService {
  /// Fetch notifications for the logged-in user (shop owner or supplier).
  static Future<List<AppNotification>> getNotifications({int page = 1, int pageSize = 20}) async {
    try {
      final res = await ApiService.get('/shop/notifications?page=$page&pageSize=$pageSize');
      final data = res['data'];
      if (data is List) {
        return data
            .map((n) => AppNotification(
                  id: n['id']?.toString() ?? '',
                  title: n['title'] ?? '',
                  body: n['message'] ?? n['body'] ?? '',
                  type: n['type'] ?? 'system',
                  createdAt: DateTime.tryParse(n['createdAt'] ?? '') ?? DateTime.now(),
                  isRead: n['isRead'] ?? false,
                ))
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
      final data = res['data'];
      if (data is Map<String, dynamic>) {
        return data['count'] ?? 0;
      }
      return 0;
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
