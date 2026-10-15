import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/chat_service.dart';

/// One conversation between a customer and a spaza shop.
class ChatThreadScreen extends StatefulWidget {
  const ChatThreadScreen({
    super.key,
    required this.chatId,
    required this.title,
    required this.asShop,
    this.verified = false,
  });

  final String chatId;
  final String title;
  final bool asShop;
  final bool verified;

  @override
  State<ChatThreadScreen> createState() => _ChatThreadScreenState();
}

class _ChatThreadScreenState extends State<ChatThreadScreen>
    with WidgetsBindingObserver {
  final _input = TextEditingController();
  final _scroll = ScrollController();
  final _messages = <ChatMessage>[];
  Timer? _poll;
  bool _loading = true;
  bool _sending = false;
  String? _error;

  String get _myRole => widget.asShop ? 'shop' : 'customer';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _load();
    // New messages arrive while the chat is open
    _poll = Timer.periodic(const Duration(seconds: 4), (_) => _fetchNew());
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _poll?.cancel();
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _fetchNew();
  }

  Future<void> _load() async {
    try {
      final items = await ChatService.messages(
        widget.chatId,
        asShop: widget.asShop,
      );
      if (!mounted) return;
      setState(() {
        _messages
          ..clear()
          ..addAll(items);
        _loading = false;
        _error = null;
      });
      _scrollToEnd(jump: true);
    } catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = e.toString();
        });
      }
    }
  }

  Future<void> _fetchNew() async {
    if (_loading || !mounted) return;
    try {
      final after = _messages.isEmpty ? null : _messages.last.sentAt;
      final items = await ChatService.messages(
        widget.chatId,
        asShop: widget.asShop,
        after: after,
      );
      final known = _messages.map((m) => m.id).toSet();
      final fresh = items.where((m) => !known.contains(m.id)).toList();
      if (fresh.isEmpty || !mounted) return;
      setState(() => _messages.addAll(fresh));
      _scrollToEnd();
    } catch (_) {
      // Try again on the next tick
    }
  }

  Future<void> _send() async {
    final text = _input.text.trim();
    if (text.isEmpty || _sending) return;
    setState(() => _sending = true);
    try {
      final message = await ChatService.send(
        widget.chatId,
        text,
        asShop: widget.asShop,
      );
      if (!mounted) return;
      _input.clear();
      setState(() {
        if (!_messages.any((m) => m.id == message.id)) _messages.add(message);
      });
      _scrollToEnd();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(e.toString())));
      }
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  void _scrollToEnd({bool jump = false}) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scroll.hasClients) return;
      final end = _scroll.position.maxScrollExtent;
      jump
          ? _scroll.jumpTo(end)
          : _scroll.animateTo(
              end,
              duration: const Duration(milliseconds: 250),
              curve: Curves.easeOut,
            );
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        titleSpacing: 0,
        title: Row(
          children: [
            CircleAvatar(
              radius: 18,
              backgroundColor: AppColors.primaryTint,
              child: Icon(
                widget.asShop ? Icons.person_rounded : Icons.storefront_rounded,
                color: AppColors.primary,
                size: 20,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    widget.title,
                    style: AppTextStyles.subtitle,
                    overflow: TextOverflow.ellipsis,
                  ),
                  if (widget.verified)
                    Row(
                      children: [
                        const Icon(
                          Icons.verified_rounded,
                          size: 13,
                          color: AppColors.success,
                        ),
                        const SizedBox(width: 3),
                        Text(
                          'SpazaSure Verified',
                          style: AppTextStyles.caption.copyWith(
                            color: AppColors.success,
                          ),
                        ),
                      ],
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
      body: Column(
        children: [
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(_error!, textAlign: TextAlign.center),
                          const SizedBox(height: 12),
                          ElevatedButton(
                            onPressed: _load,
                            child: const Text('Try again'),
                          ),
                        ],
                      ),
                    ),
                  )
                : _messages.isEmpty
                ? _emptyState()
                : ListView.builder(
                    controller: _scroll,
                    padding: const EdgeInsets.fromLTRB(12, 16, 12, 8),
                    itemCount: _messages.length,
                    itemBuilder: (_, i) =>
                        _bubble(_messages[i], i == 0 ? null : _messages[i - 1]),
                  ),
          ),
          _composer(),
        ],
      ),
    );
  }

  Widget _emptyState() => Center(
    child: Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.forum_outlined, size: 56, color: AppColors.textHint),
          const SizedBox(height: 12),
          Text(
            widget.asShop ? 'No messages yet' : 'Say hello to ${widget.title}',
            style: AppTextStyles.subtitle,
          ),
          const SizedBox(height: 4),
          Text(
            widget.asShop
                ? 'Messages from this customer will appear here.'
                : 'Ask about stock, prices or opening hours.',
            style: AppTextStyles.bodySmall.copyWith(
              color: AppColors.textSecondary,
            ),
            textAlign: TextAlign.center,
          ),
        ],
      ),
    ),
  );

  Widget _bubble(ChatMessage m, ChatMessage? previous) {
    final mine = m.senderRole == _myRole;
    final newDay =
        previous == null ||
        previous.sentAt.day != m.sentAt.day ||
        previous.sentAt.month != m.sentAt.month;
    return Column(
      children: [
        if (newDay)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 10),
            child: Text(
              DateFormat('EEE d MMM').format(m.sentAt),
              style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
            ),
          ),
        Align(
          alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
          child: Container(
            constraints: BoxConstraints(
              maxWidth: MediaQuery.of(context).size.width * 0.78,
            ),
            margin: const EdgeInsets.symmetric(vertical: 3),
            padding: const EdgeInsets.fromLTRB(14, 10, 14, 8),
            decoration: BoxDecoration(
              color: mine ? AppColors.primary : AppColors.surface,
              borderRadius: BorderRadius.only(
                topLeft: const Radius.circular(18),
                topRight: const Radius.circular(18),
                bottomLeft: Radius.circular(mine ? 18 : 4),
                bottomRight: Radius.circular(mine ? 4 : 18),
              ),
              border: mine ? null : Border.all(color: AppColors.divider),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text(
                  m.body,
                  style: AppTextStyles.body.copyWith(
                    color: mine ? Colors.white : AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  DateFormat('HH:mm').format(m.sentAt),
                  style: AppTextStyles.caption.copyWith(
                    fontSize: 10,
                    color: mine ? Colors.white70 : AppColors.textHint,
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _composer() => Container(
    padding: const EdgeInsets.fromLTRB(12, 8, 8, 8),
    decoration: const BoxDecoration(
      color: AppColors.surface,
      border: Border(top: BorderSide(color: AppColors.divider)),
    ),
    child: SafeArea(
      top: false,
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: _input,
              minLines: 1,
              maxLines: 4,
              maxLength: 1000,
              textCapitalization: TextCapitalization.sentences,
              decoration: InputDecoration(
                hintText: 'Type a message',
                counterText: '',
                filled: true,
                fillColor: AppColors.background,
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: 16,
                  vertical: 10,
                ),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(24),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
          ),
          const SizedBox(width: 6),
          IconButton.filled(
            tooltip: 'Send',
            onPressed: _sending ? null : _send,
            style: IconButton.styleFrom(backgroundColor: AppColors.primary),
            icon: _sending
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : const Icon(Icons.send_rounded, color: Colors.white),
          ),
        ],
      ),
    ),
  );
}
