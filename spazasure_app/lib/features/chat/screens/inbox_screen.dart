import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/notification_popup_host.dart';
import 'package:spazasure_app/features/chat/screens/chat_thread_screen.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/services/chat_service.dart';
import 'package:spazasure_app/services/notification_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// Chats with shops (or, for a shop owner, with customers) plus news and alerts, in two tabs.
class InboxScreen extends StatefulWidget {
  const InboxScreen({
    super.key,
    required this.asShop,
    this.initialTab = 0,
    this.initialChatId,
  });

  final bool asShop;
  final int initialTab;
  final String? initialChatId;

  @override
  State<InboxScreen> createState() => _InboxScreenState();
}

class _InboxScreenState extends State<InboxScreen> {
  List<ChatSummary>? _chats;
  List<AppNotification>? _alerts;
  String? _chatError;
  bool _openedInitialChat = false;

  @override
  void initState() {
    super.initState();
    _loadChats();
    _loadAlerts();
  }

  Future<void> _loadChats() async {
    try {
      final chats = await ChatService.list(asShop: widget.asShop);
      if (!mounted) return;
      setState(() {
        _chats = chats;
        _chatError = null;
      });
      if (!_openedInitialChat && widget.initialChatId != null) {
        _openedInitialChat = true;
        ChatSummary? initialChat;
        for (final chat in chats) {
          if (chat.id == widget.initialChatId) {
            initialChat = chat;
            break;
          }
        }
        final chatToOpen = initialChat;
        if (chatToOpen != null) {
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (mounted) _openChat(chatToOpen);
          });
        }
      }
    } catch (e) {
      if (mounted) setState(() => _chatError = e.toString());
    }
  }

  Future<void> _openChat(ChatSummary chat) async {
    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => ChatThreadScreen(
          chatId: chat.id,
          title: chat.title,
          asShop: widget.asShop,
          verified: chat.verified,
        ),
      ),
    );
    if (mounted) _loadChats();
  }

  Future<void> _loadAlerts() async {
    final items = await NotificationService.getNotifications(pageSize: 50);
    if (!mounted) return;
    // Chat messages live in the Chats tab
    setState(() => _alerts = items.where((n) => n.type != 'chat').toList());
  }

  int get _unreadChats => _chats?.fold<int>(0, (sum, c) => sum + c.unread) ?? 0;
  int get _unreadAlerts => _alerts?.where((n) => !n.isRead).length ?? 0;

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      initialIndex: widget.initialTab,
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: BrandAppBar(
          title: Text(widget.asShop ? 'Messages & alerts' : 'Inbox'),
          bottom: TabBar(
            labelColor: Colors.white,
            unselectedLabelColor: Colors.white70,
            indicatorColor: AppColors.accent,
            labelStyle: AppTextStyles.body.copyWith(
              fontWeight: FontWeight.w800,
            ),
            tabs: [
              Tab(child: _tabLabel(Icons.forum_rounded, 'Chats', _unreadChats)),
              Tab(
                child: _tabLabel(
                  Icons.campaign_rounded,
                  'News & alerts',
                  _unreadAlerts,
                ),
              ),
            ],
          ),
        ),
        body: TabBarView(children: [_chatsTab(), _alertsTab()]),
      ),
    );
  }

  Widget _tabLabel(IconData icon, String text, int count) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(icon, size: 18),
      const SizedBox(width: 6),
      Text(text),
      if (count > 0) ...[
        const SizedBox(width: 6),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
          decoration: BoxDecoration(
            color: AppColors.error,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Text(
            count > 9 ? '9+' : '$count',
            style: const TextStyle(
              color: Colors.white,
              fontSize: 11,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
      ],
    ],
  );

  // ── Chats ────────────────────────────────────────────────────────────────

  Widget _chatsTab() {
    final chats = _chats;
    if (chats == null && _chatError == null) {
      return const Center(child: CircularProgressIndicator());
    }
    return RefreshIndicator(
      onRefresh: _loadChats,
      child: _chatError != null
          ? ListView(
              children: [
                _empty(
                  Icons.cloud_off_rounded,
                  'Could not load chats',
                  _chatError!,
                ),
              ],
            )
          : chats!.isEmpty
          ? ListView(
              children: [
                _empty(
                  Icons.forum_outlined,
                  'No chats yet',
                  widget.asShop
                      ? 'When customers message your shop, their chats appear here.'
                      : 'Open a shop from the shop map and tap "Chat" to ask about stock, prices or opening hours.',
                ),
              ],
            )
          : ListView.separated(
              padding: const EdgeInsets.symmetric(vertical: 8),
              itemCount: chats.length,
              separatorBuilder: (_, _) => const Divider(height: 1, indent: 76),
              itemBuilder: (_, i) => _chatTile(chats[i]),
            ),
    );
  }

  Widget _chatTile(ChatSummary c) {
    final unread = c.unread > 0;
    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      leading: CircleAvatar(
        radius: 24,
        backgroundColor: AppColors.primaryTint,
        child: Icon(
          widget.asShop ? Icons.person_rounded : Icons.storefront_rounded,
          color: AppColors.primary,
        ),
      ),
      title: Row(
        children: [
          Flexible(
            child: Text(
              c.title,
              overflow: TextOverflow.ellipsis,
              style: AppTextStyles.body.copyWith(
                fontWeight: unread ? FontWeight.w800 : FontWeight.w600,
              ),
            ),
          ),
          if (c.verified) ...[
            const SizedBox(width: 4),
            const Icon(
              Icons.verified_rounded,
              size: 15,
              color: AppColors.success,
            ),
          ],
        ],
      ),
      subtitle: Text(
        c.preview,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: AppTextStyles.bodySmall.copyWith(
          color: unread ? AppColors.textPrimary : AppColors.textSecondary,
          fontWeight: unread ? FontWeight.w600 : FontWeight.w400,
        ),
      ),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Text(
            _when(c.lastMessageAt),
            style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
          ),
          const SizedBox(height: 4),
          if (unread)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
              decoration: BoxDecoration(
                color: AppColors.primary,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(
                '${c.unread}',
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
        ],
      ),
      onTap: () => _openChat(c),
    );
  }

  // ── News & alerts ────────────────────────────────────────────────────────

  Widget _alertsTab() {
    final alerts = _alerts;
    if (alerts == null) return const Center(child: CircularProgressIndicator());
    return RefreshIndicator(
      onRefresh: _loadAlerts,
      child: alerts.isEmpty
          ? ListView(
              children: [
                _empty(
                  Icons.notifications_none_rounded,
                  'You\'re all caught up',
                  'Order updates, safety alerts and SpazaSure news will appear here.',
                ),
              ],
            )
          : ListView(
              padding: const EdgeInsets.all(12),
              children: [
                if (_unreadAlerts > 0)
                  Align(
                    alignment: Alignment.centerRight,
                    child: TextButton.icon(
                      onPressed: () async {
                        await NotificationService.markAllAsRead();
                        NotificationPopupHost.unreadCount.value = 0;
                        _loadAlerts();
                      },
                      icon: const Icon(Icons.done_all_rounded, size: 18),
                      label: const Text('Mark all read'),
                    ),
                  ),
                for (final n in alerts) _alertTile(n),
              ],
            ),
    );
  }

  static const _alertStyles = <String, (IconData, Color)>{
    'order': (Icons.receipt_long_rounded, AppColors.primary),
    'delivery': (Icons.local_shipping_rounded, AppColors.primary),
    'wallet': (Icons.account_balance_wallet_rounded, AppColors.success),
    'compliance': (Icons.verified_user_rounded, AppColors.accentDark),
    'account': (Icons.verified_rounded, AppColors.success),
    'group_buy': (Icons.groups_rounded, AppColors.accentDark),
    'report': (Icons.flag_rounded, AppColors.error),
    'system': (Icons.campaign_rounded, AppColors.textSecondary),
  };

  Widget _alertTile(AppNotification n) {
    final (icon, color) = _alertStyles[n.type] ?? _alertStyles['system']!;
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: n.isRead
            ? AppColors.surface
            : AppColors.primaryTint.withValues(alpha: 0.6),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.divider),
      ),
      child: ListTile(
        contentPadding: const EdgeInsets.fromLTRB(12, 6, 12, 6),
        leading: Container(
          width: 42,
          height: 42,
          decoration: BoxDecoration(
            color: color.withValues(alpha: 0.12),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Icon(icon, color: color, size: 22),
        ),
        title: Text(
          n.title,
          style: AppTextStyles.body.copyWith(
            fontWeight: n.isRead ? FontWeight.w600 : FontWeight.w800,
          ),
        ),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 2),
          child: Text(
            '${n.body}\n${_when(n.createdAt.toLocal())}',
            style: AppTextStyles.bodySmall.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
        ),
        isThreeLine: true,
        onTap: () {
          if (n.isRead) return;
          NotificationService.markAsRead(n.id);
          if (NotificationPopupHost.unreadCount.value > 0) {
            NotificationPopupHost.unreadCount.value--;
          }
          _loadAlerts();
        },
      ),
    );
  }

  // ── Helpers ──────────────────────────────────────────────────────────────

  Widget _empty(IconData icon, String title, String text) => Padding(
    padding: const EdgeInsets.fromLTRB(32, 80, 32, 32),
    child: Column(
      children: [
        Container(
          width: 72,
          height: 72,
          decoration: const BoxDecoration(
            color: AppColors.primaryTint,
            shape: BoxShape.circle,
          ),
          child: Icon(icon, size: 34, color: AppColors.primary),
        ),
        const SizedBox(height: 14),
        Text(title, style: AppTextStyles.h3, textAlign: TextAlign.center),
        const SizedBox(height: 6),
        Text(
          text,
          style: AppTextStyles.bodySmall.copyWith(
            color: AppColors.textSecondary,
          ),
          textAlign: TextAlign.center,
        ),
      ],
    ),
  );

  static String _when(DateTime t) {
    final now = DateTime.now();
    if (t.year == now.year && t.month == now.month && t.day == now.day) {
      return DateFormat('HH:mm').format(t);
    }
    if (now.difference(t).inDays < 7) return DateFormat('EEE').format(t);
    return DateFormat('d MMM').format(t);
  }
}
