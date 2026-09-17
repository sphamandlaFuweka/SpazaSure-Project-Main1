import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/marketplace/screens/qr_scanner_screen.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';

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
          onPressed: () => _showInfo(
            context,
            'Ask SpazaSure',
            'Ask about product safety, shop verification, or suspicious goods.',
          ),
          icon: const Icon(Icons.chat_bubble_outline),
        ),
      ],
    ),
    body: ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Text('Shop smarter. Stay safe.', style: AppTextStyles.h1),
        const SizedBox(height: 8),
        Text(
          'Verify products and find trusted shops in your community.',
          style: AppTextStyles.body,
        ),
        const SizedBox(height: 24),
        _action(
          context,
          Icons.qr_code_scanner,
          'Verify a product',
          'Scan a barcode or QR code',
          AppColors.primary,
          () => Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => const QrScannerScreen()),
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
          Icons.school_outlined,
          'Safety basics',
          'Learn how to spot suspicious packaging and unsafe products.',
        ),
        _infoCard(
          context,
          Icons.smart_toy_outlined,
          'Kwazi authenticity guide',
          'Answer a few questions when a product does not look right.',
        ),
        const SizedBox(height: 16),
        Text('Recent activity', style: AppTextStyles.h3),
        const SizedBox(height: 10),
        Card(
          child: ListTile(
            leading: const Icon(Icons.history, color: AppColors.primary),
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
      leading: CircleAvatar(
        backgroundColor: color.withValues(alpha: .12),
        child: Icon(icon, color: color),
      ),
      title: Text(title, style: AppTextStyles.subtitle),
      subtitle: Text(subtitle),
      trailing: const Icon(Icons.chevron_right),
    ),
  );

  Widget _infoCard(
    BuildContext context,
    IconData icon,
    String title,
    String text,
  ) => Card(
    child: ListTile(
      leading: Icon(icon, color: AppColors.secondary),
      title: Text(title, style: AppTextStyles.subtitle),
      subtitle: Text(text),
      onTap: () => _showInfo(context, title, text),
    ),
  );

  void _showInfo(BuildContext context, String title, String text) =>
      showDialog<void>(
        context: context,
        builder: (_) => AlertDialog(
          title: Text(title),
          content: Text(text),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Close'),
            ),
          ],
        ),
      );
}
