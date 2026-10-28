import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// Turns a typed or scanned value into a shop number (e.g. "ss-482 913", "482913" or a
/// verify link become "SS482913"). Returns null if it can't be a shop number.
String? normalizeShopNumber(String? input) {
  if (input == null || input.trim().isEmpty) return null;
  var value = input.trim();
  final slash = value.lastIndexOf('/');
  if (slash >= 0) value = value.substring(slash + 1);
  var compact = value.replaceAll(RegExp(r'[^A-Za-z0-9]'), '').toUpperCase();
  if (compact.startsWith('SS')) compact = compact.substring(2);
  return RegExp(r'^\d{6}$').hasMatch(compact) ? 'SS$compact' : null;
}

/// True when a scanned QR code is a SpazaSure shop sticker rather than a product code.
bool isShopStickerCode(String code) {
  final value = code.trim();
  return value.contains('spazasure.co.za/verify/') ||
      RegExp(r'^SS\d{6}$', caseSensitive: false).hasMatch(value);
}

/// Check whether a shop is SpazaSure Verified, by shop number or a scanned sticker.
class ShopVerifyScreen extends StatefulWidget {
  const ShopVerifyScreen({super.key, this.initialCode});

  final String? initialCode;

  @override
  State<ShopVerifyScreen> createState() => _ShopVerifyScreenState();
}

class _ShopVerifyScreenState extends State<ShopVerifyScreen> {
  final _controller = TextEditingController();
  bool _loading = false;
  Map<String, dynamic>? _result;
  String? _error;

  @override
  void initState() {
    super.initState();
    final code = normalizeShopNumber(widget.initialCode);
    if (code != null) {
      _controller.text = code;
      _check();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _check() async {
    final code = normalizeShopNumber(_controller.text);
    FocusScope.of(context).unfocus();
    if (code == null) {
      setState(() {
        _result = null;
        _error =
            'Shop numbers look like SS482913. Check the number on the sticker.';
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final res = await ApiService.get('/public/shops/$code');
      if (!mounted) return;
      setState(() => _result = res['data'] as Map<String, dynamic>);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _result = null;
        _error = e.toString().replaceFirst(
          RegExp(r'^(Exception|ApiException)[^:]*: '),
          '',
        );
      });
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final result = _result;
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(title: const Text('Verify a shop')),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (result != null)
            ShopVerificationCard(result: result)
          else if (_error != null)
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: AppColors.error.withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Row(
                children: [
                  const Icon(Icons.gpp_maybe_outlined, color: AppColors.error),
                  const SizedBox(width: 12),
                  Expanded(child: Text(_error!, style: AppTextStyles.body)),
                ],
              ),
            ),
          const SizedBox(height: 20),
          Text(
            result == null
                ? 'Enter the shop number on the sticker'
                : 'Check another shop',
            style: AppTextStyles.bodySmall.copyWith(
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _controller,
                  textCapitalization: TextCapitalization.characters,
                  textInputAction: TextInputAction.search,
                  onSubmitted: (_) => _check(),
                  decoration: const InputDecoration(hintText: 'e.g. SS482913'),
                ),
              ),
              const SizedBox(width: 8),
              SizedBox(
                height: 52,
                child: ElevatedButton(
                  onPressed: _loading ? null : _check,
                  child: const Text('Check'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Text(
            'A SpazaSure Verified shop has had its business permit, health certificate and '
            'owner ID checked by SpazaSure. Make sure the shop name matches the shop you are in.',
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

/// The result: a green "SpazaSure Verified" sticker, or a grey "not verified" badge.
class ShopVerificationCard extends StatelessWidget {
  const ShopVerificationCard({super.key, required this.result});

  final Map<String, dynamic> result;

  @override
  Widget build(BuildContext context) {
    final verified = result['verified'] == true;
    final validUntil = DateTime.tryParse(
      result['validUntil']?.toString() ?? '',
    );
    final area = result['area']?.toString() ?? '';
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(
          color: verified ? AppColors.success : AppColors.divider,
          width: 2,
        ),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          Container(
            width: double.infinity,
            color: verified
                ? AppColors.success.withValues(alpha: 0.1)
                : const Color(0xFFF5F5F5),
            padding: const EdgeInsets.symmetric(vertical: 28, horizontal: 20),
            child: Column(
              children: [
                if (verified)
                  const GreenSticker(size: 140)
                else
                  Container(
                    width: 110,
                    height: 110,
                    decoration: const BoxDecoration(
                      color: Colors.white,
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.gpp_maybe_outlined,
                      size: 48,
                      color: AppColors.textHint,
                    ),
                  ),
                const SizedBox(height: 16),
                Text(
                  result['shopName']?.toString() ?? '',
                  style: AppTextStyles.h2,
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 4),
                Text(
                  verified ? 'SpazaSure Verified shop' : 'Not verified',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                    color: verified
                        ? AppColors.success
                        : AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  result['message']?.toString() ?? '',
                  style: AppTextStyles.bodySmall,
                ),
                const SizedBox(height: 12),
                _row(
                  Icons.storefront_outlined,
                  'Shop number ${result['shopNumber'] ?? ''}',
                ),
                if (area.isNotEmpty) _row(Icons.place_outlined, area),
                if (verified && validUntil != null)
                  _row(
                    Icons.event_available_outlined,
                    'Documents valid until ${DateFormat('d MMMM yyyy').format(validUntil)}',
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _row(IconData icon, String text) => Padding(
    padding: const EdgeInsets.only(top: 6),
    child: Row(
      children: [
        Icon(icon, size: 18, color: AppColors.textSecondary),
        const SizedBox(width: 8),
        Expanded(child: Text(text, style: AppTextStyles.body)),
      ],
    ),
  );
}

/// The round green "SpazaSure VERIFIED" sticker.
class GreenSticker extends StatelessWidget {
  const GreenSticker({super.key, this.size = 140});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: AppColors.success,
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white, width: size * 0.05),
        boxShadow: [
          BoxShadow(
            color: AppColors.success.withValues(alpha: 0.35),
            blurRadius: 20,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.verified_rounded, color: Colors.white, size: size * 0.32),
          Text(
            'SPAZASURE',
            style: TextStyle(
              color: Colors.white,
              fontSize: size * 0.08,
              fontWeight: FontWeight.w800,
              letterSpacing: 1.5,
            ),
          ),
          Text(
            'VERIFIED',
            style: TextStyle(
              color: Colors.white,
              fontSize: size * 0.13,
              fontWeight: FontWeight.w900,
              height: 1.1,
            ),
          ),
        ],
      ),
    );
  }
}
