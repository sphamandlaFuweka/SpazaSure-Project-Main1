import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/marketplace/screens/qr_scanner_screen.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_rewards_screen.dart';
import 'package:spazasure_app/core/widgets/notification_popup_host.dart';
import 'package:spazasure_app/features/customer/screens/ask_spazasure_sheet.dart';
import 'package:spazasure_app/features/customer/screens/kwazi_chat_sheet.dart';
import 'package:spazasure_app/features/customer/screens/safety_basics_screen.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';

class CustomerHomeScreen extends StatelessWidget {
  const CustomerHomeScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: AppBar(
      title: const Text('SpazaSure'),
      actions: [
        // Chats with shops + news & alerts, with a dot for anything unread
        ValueListenableBuilder<int>(
          valueListenable: NotificationPopupHost.unreadCount,
          builder: (context, unread, _) => IconButton(
            tooltip: 'Inbox: chats and alerts',
            onPressed: () => Navigator.pushNamed(context, '/inbox'),
            icon: Badge(
              isLabelVisible: unread > 0,
              label: Text(unread > 9 ? '9+' : '$unread'),
              child: const Icon(Icons.forum_outlined),
            ),
          ),
        ),
        const SizedBox(width: 4),
      ],
    ),
    floatingActionButton: FloatingActionButton.small(
      onPressed: () => showAskSpazaSure(context),
      tooltip: 'Ask SpazaSure',
      child: const Icon(Icons.support_agent_rounded),
    ),
    body: ListView(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 84),
      children: [
        Container(
          height: 210,
          clipBehavior: Clip.antiAlias,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(24),
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [AppColors.primary, Color(0xFF3F5DB0)],
            ),
          ),
          child: Stack(
            children: [
              Positioned(
                right: -10,
                bottom: 0,
                top: 8,
                width: 170,
                child: Image.asset(
                  'assets/images/customer_hero.png',
                  fit: BoxFit.contain,
                  alignment: Alignment.bottomCenter,
                  errorBuilder: (_, __, ___) => const SizedBox.shrink(),
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 20, 150, 20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    // Website-style headline: punchline highlighted in brand yellow
                    Text.rich(
                      TextSpan(
                        text: tr('home.taglineShop'),
                        children: [
                          TextSpan(
                            text: tr('home.taglineSafe'),
                            style: TextStyle(color: AppColors.accent),
                          ),
                        ],
                      ),
                      style: AppTextStyles.h1.copyWith(
                        color: Colors.white,
                        fontSize: 24,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      tr('home.intro'),
                      style: AppTextStyles.body.copyWith(color: Colors.white70),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),
        _action(
          context,
          Icons.qr_code_scanner_rounded,
          tr('home.verifyProduct'),
          tr('home.verifyProductSub'),
          AppColors.primary,
          () => Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => const QrScannerScreen(customerMode: true),
            ),
          ),
        ),
        const SizedBox(height: 12),
        _action(
          context,
          Icons.verified_rounded,
          tr('home.verifyShop'),
          tr('home.verifyShopSub'),
          AppColors.success,
          () => Navigator.pushNamed(context, '/verify-shop'),
        ),
        const SizedBox(height: 12),
        _action(
          context,
          Icons.card_giftcard_rounded,
          tr('home.rewards'),
          tr('home.rewardsSub'),
          AppColors.secondary,
          () => Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => const CustomerRewardsScreen()),
          ),
        ),
        const SizedBox(height: 12),
        _action(
          context,
          Icons.warning_amber_rounded,
          tr('home.report'),
          tr('home.reportSub'),
          AppColors.error,
          () => Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => const ReportScreen()),
          ),
        ),
        const SizedBox(height: 24),
        Text(tr('home.learn'), style: AppTextStyles.h3),
        const SizedBox(height: 10),
        _infoCard(
          context,
          Icons.school_rounded,
          tr('home.safety'),
          tr('home.safetySub'),
          const SafetyBasicsScreen(),
        ),
        _infoCard(
          context,
          Icons.smart_toy_rounded,
          tr('home.kwazi'),
          tr('home.kwaziSub'),
          null,
        ),
        const SizedBox(height: 16),
        Text(tr('home.recent'), style: AppTextStyles.h3),
        const SizedBox(height: 10),
        Card(
          child: ListTile(
            leading: const Icon(
              Icons.history_rounded,
              color: AppColors.primary,
            ),
            title: Text(tr('home.recentEmpty')),
            subtitle: Text(tr('home.recentEmptySub')),
          ),
        ),
      ],
    ),
  );

  Widget _action(
    BuildContext context,
    IconData icon,
    String title,
    String subtitle,
    Color color,
    VoidCallback onTap,
  ) => Card(
    child: ListTile(
      onTap: onTap,
      contentPadding: const EdgeInsets.all(16),
      leading: Container(
        width: 52,
        height: 52,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          boxShadow: [
            BoxShadow(
              color: color.withValues(alpha: 0.15),
              blurRadius: 10,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Icon(icon, color: color, size: 26),
      ),
      title: Text(title, style: AppTextStyles.subtitle),
      subtitle: Text(subtitle),
      trailing: const Icon(Icons.chevron_right_rounded),
    ),
  );

  Widget _infoCard(
    BuildContext context,
    IconData icon,
    String title,
    String text,
    Widget? destination,
  ) => Card(
    child: ListTile(
      leading: Icon(icon, color: AppColors.secondary),
      title: Text(title, style: AppTextStyles.subtitle),
      subtitle: Text(text),
      trailing: const Icon(Icons.chevron_right_rounded),
      onTap: () => destination == null
          ? showKwaziChat(context)
          : _open(context, destination),
    ),
  );

  void _open(BuildContext context, Widget screen) =>
      Navigator.push(context, MaterialPageRoute(builder: (_) => screen));
}
