import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/services/notification_service.dart';

const notificationIcons = {
  'order': Icons.receipt_long,
  'delivery': Icons.local_shipping,
  'compliance': Icons.verified_user,
  'account': Icons.verified_outlined,
  'wallet': Icons.account_balance_wallet_outlined,
  'group_buy': Icons.groups_outlined,
  'chat': Icons.chat_bubble_outline_rounded,
  'system': Icons.info_outline,
};

const notificationColors = {
  'order': AppColors.info,
  'delivery': AppColors.accent,
  'compliance': AppColors.warning,
  'account': AppColors.info,
  'wallet': AppColors.success,
  'group_buy': AppColors.accent,
  'chat': AppColors.primary,
  'system': AppColors.textSecondary,
};

/// Shows the whole notification, with a button to the order, chat or screen it is about.
Future<void> showNotificationDetail(
  BuildContext context,
  AppNotification notif,
) {
  final navigator = Navigator.of(context);
  final color = notificationColors[notif.type] ?? AppColors.textSecondary;
  final canOpen = NotificationService.canOpen(notif.type);

  return showModalBottomSheet(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (ctx) => Container(
      constraints: BoxConstraints(
        maxHeight: MediaQuery.of(ctx).size.height * 0.85,
      ),
      decoration: const BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      padding: const EdgeInsets.fromLTRB(24, 12, 24, 24),
      child: SafeArea(
        top: false,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.divider,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 20),
            Row(
              children: [
                Container(
                  width: 48,
                  height: 48,
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Icon(
                    notificationIcons[notif.type] ?? Icons.notifications,
                    color: color,
                  ),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(notif.title, style: AppTextStyles.subtitle),
                      const SizedBox(height: 2),
                      Text(
                        DateFormat(
                          'd MMM yyyy, HH:mm',
                        ).format(notif.createdAt.toLocal()),
                        style: AppTextStyles.caption,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 18),
            Flexible(
              child: SingleChildScrollView(
                child: Text(
                  notif.body.isEmpty ? 'No further details.' : notif.body,
                  style: AppTextStyles.body,
                ),
              ),
            ),
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              height: 48,
              child: ElevatedButton(
                onPressed: () {
                  Navigator.pop(ctx);
                  if (canOpen) {
                    NotificationService.openTarget(
                      navigator,
                      notif.type,
                      notif.referenceId,
                    );
                  }
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14),
                  ),
                ),
                child: Text(
                  canOpen
                      ? NotificationService.actionLabel(notif.type)
                      : 'Close',
                ),
              ),
            ),
          ],
        ),
      ),
    ),
  );
}
