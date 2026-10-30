import 'dart:async';

import 'package:flutter/material.dart';
import 'package:spazasure_app/core/alert_feedback.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/services/notification_service.dart';
import 'package:spazasure_app/services/push_service.dart';

/// Checks for new notifications while someone is signed in and the app is open, and shows
/// each new one as a banner over whatever screen is showing. Tapping it opens the
/// relevant screen. Wraps the whole app via MaterialApp.builder.
class NotificationPopupHost extends StatefulWidget {
  const NotificationPopupHost({
    super.key,
    required this.navigatorKey,
    required this.child,
  });

  final GlobalKey<NavigatorState> navigatorKey;
  final Widget child;

  /// Unread count, so screens (e.g. the home bell) can update without their own polling.
  static final ValueNotifier<int> unreadCount = ValueNotifier(0);

  @override
  State<NotificationPopupHost> createState() => _NotificationPopupHostState();
}

class _NotificationPopupHostState extends State<NotificationPopupHost>
    with WidgetsBindingObserver {
  static const _pollEvery = Duration(seconds: 20);

  Timer? _timer;
  bool _loggedIn = false;
  bool _foreground = true;
  bool _checking = false;

  /// Ids already known; null until the first check, so old notifications don't pop up
  Set<String>? _seen;
  final List<AppNotification> _queue = [];
  AppNotification? _showing;
  Timer? _hideTimer;

  /// A push tapped before the user was signed in (app cold start)
  Map<String, dynamic>? _pendingPush;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    // Phone notifications: a push received while the app is open triggers an immediate
    // check (which shows the banner); tapping a push opens the screen it is about
    PushService.init(onOpen: _openFromPush, onForeground: _check);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final loggedIn = context.watch<AuthProvider>().isLoggedIn;
    if (loggedIn != _loggedIn) {
      _loggedIn = loggedIn;
      if (loggedIn) {
        _start();
        PushService.register();
        final pending = _pendingPush;
        if (pending != null) {
          _pendingPush = null;
          // Let the splash screen finish routing to home first
          Future.delayed(
            const Duration(seconds: 2),
            () => _navigateFor(
              pending['type']?.toString() ?? 'system',
              pending['referenceId']?.toString(),
            ),
          );
        }
      } else {
        _stop();
        _seen = null;
        _queue.clear();
        NotificationPopupHost.unreadCount.value = 0;
        if (_showing != null) setState(() => _showing = null);
      }
    }
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    _foreground = state == AppLifecycleState.resumed;
    if (!_loggedIn) return;
    if (_foreground) {
      _start(); // check straight away when the user comes back to the app
    } else {
      _stop();
    }
  }

  void _start() {
    _timer?.cancel();
    _timer = Timer.periodic(_pollEvery, (_) => _check());
    _check();
  }

  void _stop() {
    _timer?.cancel();
    _timer = null;
  }

  Future<void> _check() async {
    if (_checking || !_loggedIn || !_foreground) return;
    _checking = true;
    try {
      final items = await NotificationService.getNotifications(pageSize: 10);
      if (!mounted || !_loggedIn) return;
      final unread = await NotificationService.getUnreadCount();
      NotificationPopupHost.unreadCount.value = unread;

      final seen = _seen;
      if (seen != null) {
        // Oldest first, and at most three at once so a backlog doesn't flood the screen
        final fresh = items
            .where((n) => !n.isRead && !seen.contains(n.id))
            .take(3)
            .toList()
            .reversed;
        _queue.addAll(fresh);
      }
      _seen = {...?seen, ...items.map((n) => n.id)};
      _showNext();
    } finally {
      _checking = false;
    }
  }

  void _showNext() {
    if (_showing != null || _queue.isEmpty || !mounted) return;
    final next = _queue.removeAt(0);
    // A tone and a buzz, like a push, so a new alert isn't missed
    AlertFeedback.play();
    setState(() => _showing = next);
    _hideTimer?.cancel();
    _hideTimer = Timer(const Duration(seconds: 6), _dismiss);
  }

  void _dismiss() {
    _hideTimer?.cancel();
    if (!mounted) return;
    setState(() => _showing = null);
    // Let the banner slide away before the next one appears
    Future.delayed(const Duration(milliseconds: 350), _showNext);
  }

  void _open(AppNotification n) {
    _dismiss();
    NotificationService.markAsRead(n.id);
    if (NotificationPopupHost.unreadCount.value > 0) {
      NotificationPopupHost.unreadCount.value--;
    }
    _navigateFor(n.type, n.referenceId);
  }

  /// Opens what the notification is about; ones without a screen (account, system)
  /// open the notifications list, where the full message can be read.
  Future<void> _navigateFor(String type, String? referenceId) async {
    final navigator = widget.navigatorKey.currentState;
    if (navigator == null) return;
    final opened = await NotificationService.openTarget(
      navigator,
      type,
      referenceId,
    );
    if (!opened) navigator.pushNamed('/notifications');
  }

  /// The user tapped a phone notification
  void _openFromPush(Map<String, dynamic> data) {
    final type = data['type']?.toString() ?? 'system';
    if (!_loggedIn) {
      _pendingPush = data; // opened once the session has loaded
      return;
    }
    _check();
    _navigateFor(type, data['referenceId']?.toString());
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _timer?.cancel();
    _hideTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final n = _showing;
    return Stack(
      children: [
        widget.child,
        Positioned(
          left: 12,
          right: 12,
          top: 0,
          child: SafeArea(
            bottom: false,
            child: AnimatedSwitcher(
              duration: const Duration(milliseconds: 300),
              transitionBuilder: (child, animation) => SlideTransition(
                position: Tween(begin: const Offset(0, -1.2), end: Offset.zero)
                    .animate(
                      CurvedAnimation(
                        parent: animation,
                        curve: Curves.easeOutCubic,
                      ),
                    ),
                child: FadeTransition(opacity: animation, child: child),
              ),
              child: n == null
                  ? const SizedBox.shrink(key: ValueKey('none'))
                  : _Banner(
                      key: ValueKey(n.id),
                      notification: n,
                      onTap: () => _open(n),
                      onDismiss: _dismiss,
                    ),
            ),
          ),
        ),
      ],
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({
    super.key,
    required this.notification,
    required this.onTap,
    required this.onDismiss,
  });

  final AppNotification notification;
  final VoidCallback onTap;
  final VoidCallback onDismiss;

  IconData get _icon => switch (notification.type) {
    'order' || 'delivery' => Icons.local_shipping_outlined,
    'wallet' || 'payout' => Icons.account_balance_wallet_outlined,
    'compliance' || 'account' => Icons.verified_outlined,
    'group_buy' => Icons.groups_outlined,
    _ => Icons.notifications_none_rounded,
  };

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Dismissible(
        key: ValueKey('banner-${notification.id}'),
        direction: DismissDirection.up,
        onDismissed: (_) => onDismiss(),
        child: Material(
          color: AppColors.surface,
          elevation: 10,
          shadowColor: Colors.black26,
          borderRadius: BorderRadius.circular(18),
          child: InkWell(
            borderRadius: BorderRadius.circular(18),
            onTap: onTap,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(14, 12, 6, 12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: 40,
                    height: 40,
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Icon(_icon, color: AppColors.primary, size: 20),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          notification.title,
                          style: AppTextStyles.body.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                        const SizedBox(height: 2),
                        Text(
                          notification.body,
                          style: AppTextStyles.caption.copyWith(
                            color: AppColors.textSecondary,
                          ),
                          maxLines: 3,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                  IconButton(
                    tooltip: 'Dismiss',
                    visualDensity: VisualDensity.compact,
                    icon: const Icon(
                      Icons.close,
                      size: 18,
                      color: AppColors.textHint,
                    ),
                    onPressed: onDismiss,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
