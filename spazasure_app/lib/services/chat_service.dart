import 'api_service.dart';

/// A chat between a customer and a spaza shop, as shown in the inbox list.
class ChatSummary {
  final String id;

  /// The other party: the shop's name for customers, the customer's name for shops
  final String title;
  final String? subtitle;
  final bool verified;
  final String preview;
  final DateTime lastMessageAt;
  final int unread;

  const ChatSummary({
    required this.id,
    required this.title,
    this.subtitle,
    this.verified = false,
    required this.preview,
    required this.lastMessageAt,
    required this.unread,
  });
}

class ChatMessage {
  final String id;
  final String senderRole; // "customer" or "shop"
  final String body;
  final DateTime sentAt;

  const ChatMessage({
    required this.id,
    required this.senderRole,
    required this.body,
    required this.sentAt,
  });

  factory ChatMessage.fromJson(Map<String, dynamic> j) => ChatMessage(
    id: j['id'].toString(),
    senderRole: j['senderRole']?.toString() ?? 'customer',
    body: j['body']?.toString() ?? '',
    sentAt:
        DateTime.tryParse(j['sentAt']?.toString() ?? '')?.toLocal() ??
        DateTime.now(),
  );
}

/// In-app chat between customers and spaza shops. [asShop] picks the shop owner's side.
class ChatService {
  static String _base(bool asShop) =>
      asShop ? '/shop/chats' : '/customer/chats';

  static Future<List<ChatSummary>> list({required bool asShop}) async {
    final res = await ApiService.get(_base(asShop));
    final items = (res['data'] as List?) ?? const [];
    return items.whereType<Map<String, dynamic>>().map((c) {
      return ChatSummary(
        id: c['id'].toString(),
        title: asShop
            ? (c['customerName']?.toString() ?? 'Customer')
            : (c['shopName']?.toString() ?? 'Shop'),
        subtitle: asShop ? null : c['city']?.toString(),
        verified: c['shopVerified'] == true,
        preview: c['lastMessagePreview']?.toString() ?? 'No messages yet',
        lastMessageAt:
            DateTime.tryParse(
              c['lastMessageAt']?.toString() ?? '',
            )?.toLocal() ??
            DateTime.now(),
        unread: (c['unread'] as num?)?.toInt() ?? 0,
      );
    }).toList();
  }

  static Future<int> unreadCount({required bool asShop}) async {
    try {
      final res = await ApiService.get('${_base(asShop)}/unread-count');
      return ((res['data'] as Map?)?['count'] as num?)?.toInt() ?? 0;
    } catch (_) {
      return 0;
    }
  }

  /// Opens (or creates) the customer's chat with a shop. Returns the chat id.
  static Future<String> startWithShop(String shopId) async {
    final res = await ApiService.post('/customer/chats', {'shopId': shopId});
    return (res['data'] as Map)['id'].toString();
  }

  /// Messages, oldest first; pass [after] to get only newer ones.
  static Future<List<ChatMessage>> messages(
    String chatId, {
    required bool asShop,
    DateTime? after,
  }) async {
    final query = after == null
        ? ''
        : '?after=${Uri.encodeQueryComponent(after.toUtc().toIso8601String())}';
    final res = await ApiService.get('${_base(asShop)}/$chatId/messages$query');
    final items = ((res['data'] as Map?)?['messages'] as List?) ?? const [];
    return items
        .whereType<Map<String, dynamic>>()
        .map(ChatMessage.fromJson)
        .toList();
  }

  static Future<ChatMessage> send(
    String chatId,
    String body, {
    required bool asShop,
  }) async {
    final res = await ApiService.post('${_base(asShop)}/$chatId/messages', {
      'body': body,
    });
    return ChatMessage.fromJson(res['data'] as Map<String, dynamic>);
  }
}
