import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/shop_verification/shop_verify_screen.dart';
import 'package:spazasure_app/services/api_service.dart';

/// The shop owner's own shop number and QR code. Customers scan the QR (with any phone
/// camera or the SpazaSure app) or type the number to see if the shop is verified.
class MyStickerScreen extends StatefulWidget {
  const MyStickerScreen({super.key});

  @override
  State<MyStickerScreen> createState() => _MyStickerScreenState();
}

class _MyStickerScreenState extends State<MyStickerScreen> {
  Map<String, dynamic>? _data;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final res = await ApiService.get('/shop/profile/verification');
      if (!mounted) return;
      setState(() => _data = res['data'] as Map<String, dynamic>);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final data = _data;
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('My SpazaSure sticker')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : data == null
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      _error ?? 'Could not load your sticker.',
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 12),
                    ElevatedButton(
                      onPressed: _load,
                      child: const Text('Try again'),
                    ),
                  ],
                ),
              ),
            )
          : RefreshIndicator(onRefresh: _load, child: _body(data)),
    );
  }

  Widget _body(Map<String, dynamic> data) {
    final verified = data['verified'] == true;
    final shopNumber = data['shopNumber']?.toString() ?? '';
    final verifyUrl = data['verifyUrl']?.toString() ?? '';
    final outstanding =
        (data['outstanding'] as List?)?.map((e) => e.toString()).toList() ??
        const [];

    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        // The sticker customers see in the shop
        Container(
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(24),
            border: Border.all(
              color: verified ? AppColors.success : AppColors.divider,
              width: 2,
            ),
          ),
          child: Column(
            children: [
              if (verified)
                const GreenSticker(size: 120)
              else
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 6,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.warning.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    'Not verified yet',
                    style: AppTextStyles.bodySmall.copyWith(
                      color: AppColors.warning,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              const SizedBox(height: 16),
              Text(
                data['shopName']?.toString() ?? '',
                style: AppTextStyles.h3,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              if (verifyUrl.isNotEmpty)
                QrImageView(
                  data: verifyUrl,
                  size: 200,
                  backgroundColor: Colors.white,
                  eyeStyle: const QrEyeStyle(
                    eyeShape: QrEyeShape.square,
                    color: AppColors.primary,
                  ),
                  dataModuleStyle: const QrDataModuleStyle(
                    dataModuleShape: QrDataModuleShape.square,
                    color: AppColors.primary,
                  ),
                ),
              const SizedBox(height: 12),
              Text(
                'Shop number',
                style: AppTextStyles.caption.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 2),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text(
                    shopNumber,
                    style: AppTextStyles.h2.copyWith(
                      letterSpacing: 2,
                      color: AppColors.primary,
                    ),
                  ),
                  IconButton(
                    tooltip: 'Copy shop number',
                    icon: const Icon(
                      Icons.copy_rounded,
                      size: 18,
                      color: AppColors.primary,
                    ),
                    onPressed: () {
                      Clipboard.setData(ClipboardData(text: shopNumber));
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text('Shop number copied'),
                          duration: Duration(seconds: 1),
                        ),
                      );
                    },
                  ),
                ],
              ),
              const SizedBox(height: 4),
              Text(
                'Customers scan this code or enter your shop number to check you are SpazaSure Verified.',
                style: AppTextStyles.caption.copyWith(
                  color: AppColors.textSecondary,
                ),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: (verified ? AppColors.success : AppColors.warning)
                .withValues(alpha: 0.08),
            borderRadius: BorderRadius.circular(16),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                data['message']?.toString() ?? '',
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
              ),
              for (final item in outstanding) ...[
                const SizedBox(height: 8),
                Row(
                  children: [
                    const Icon(
                      Icons.radio_button_unchecked,
                      size: 16,
                      color: AppColors.warning,
                    ),
                    const SizedBox(width: 8),
                    Expanded(child: Text(item, style: AppTextStyles.bodySmall)),
                  ],
                ),
              ],
              if (!verified) ...[
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  onPressed: () async {
                    await Navigator.pushNamed(context, '/compliance');
                    _load();
                  },
                  icon: const Icon(Icons.upload_file_outlined, size: 18),
                  label: const Text('Upload documents'),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}
