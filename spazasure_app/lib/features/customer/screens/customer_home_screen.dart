import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/marketplace/screens/qr_scanner_screen.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_rewards_screen.dart';
import 'package:spazasure_app/features/customer/screens/kwazi_chat_sheet.dart';
import 'package:spazasure_app/features/customer/screens/safety_basics_screen.dart';

class CustomerHomeScreen extends StatelessWidget {
  const CustomerHomeScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: AppBar(
      title: const Text('SpazaSure'),
      actions: [
        IconButton(
          tooltip: 'Ask SpazaSure',
          onPressed: () => showKwaziChat(context),
          icon: const Icon(Icons.chat_bubble_outline_rounded),
        ),
      ],
    ),
    floatingActionButton: FloatingActionButton.extended(
      onPressed: () => showKwaziChat(context),
      icon: const Icon(Icons.smart_toy_rounded),
      label: const Text('Ask SpazaSure'),
    ),
    body: ListView(
      padding: const EdgeInsets.all(20),
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
                    Text(
                      'Shop smarter. Stay safe.',
                      style: AppTextStyles.h1.copyWith(
                        color: Colors.white,
                        fontSize: 24,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Verify products and find trusted shops in your community.',
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
          'Verify a product',
          'Scan a barcode or QR code',
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
          Icons.card_giftcard_rounded,
          'My rewards',
          'Earn 5 points per scan and redeem R20 vouchers',
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
          'Report a concern',
          'Flag fake, expired, or unsafe goods',
          AppColors.error,
          () => Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => const ReportScreen()),
          ),
        ),
        const SizedBox(height: 24),
        Text('Learn before you buy', style: AppTextStyles.h3),
        const SizedBox(height: 10),
        _infoCard(
          context,
          Icons.school_rounded,
          'Safety basics',
          'Learn how to spot suspicious packaging and unsafe products.',
          const SafetyBasicsScreen(),
        ),
        _infoCard(
          context,
          Icons.smart_toy_rounded,
          'Kwazi authenticity guide',
          'Chat with Kwazi when a product does not look right.',
          null,
        ),
        const SizedBox(height: 16),
        Text('Recent activity', style: AppTextStyles.h3),
        const SizedBox(height: 10),
        Card(
          child: ListTile(
            leading: const Icon(
              Icons.history_rounded,
              color: AppColors.primary,
            ),
            title: const Text('Your recent scans will appear here'),
            subtitle: const Text('Start by verifying a product.'),
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
