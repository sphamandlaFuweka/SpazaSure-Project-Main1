import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
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

  /// Checks as soon as a whole shop number is typed, so there's no need to press Check.
  void _onTyped(String value) {
    final code = normalizeShopNumber(value);
    if (code != null && code != _result?['shopNumber'] && !_loading) _check();
  }

  /// Scans the QR code on the shop's sticker.
  Future<void> _scan() async {
    final scanned = await Navigator.push<String>(
      context,
      MaterialPageRoute(builder: (_) => const _StickerScanScreen()),
    );
    if (!mounted || scanned == null) return;
    final code = normalizeShopNumber(scanned);
    if (code == null) {
      setState(() {
        _result = null;
        _error =
            "That QR code isn't a SpazaSure shop sticker. Scan the code on the green sticker, or type the shop number.";
      });
      return;
    }
    _controller.text = code;
    _check();
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
          if (result != null || _error != null) const SizedBox(height: 20),
          // Scanning is quickest when standing at the shop
          SizedBox(
            height: 52,
            child: ElevatedButton.icon(
              onPressed: _loading ? null : _scan,
              icon: const Icon(Icons.qr_code_scanner_rounded),
              label: Text(
                result == null
                    ? 'Scan the sticker\'s QR code'
                    : 'Scan another shop',
              ),
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              const Expanded(child: Divider()),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 10),
                child: Text('or', style: AppTextStyles.caption),
              ),
              const Expanded(child: Divider()),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            'Type the shop number on the sticker',
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
                  onChanged: _onTyped,
                  onSubmitted: (_) => _check(),
                  decoration: const InputDecoration(hintText: 'e.g. SS482913'),
                ),
              ),
              const SizedBox(width: 8),
              SizedBox(
                height: 52,
                child: OutlinedButton(
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
    final address = result['address']?.toString() ?? '';
    final rating = (result['ratingAvg'] as num?)?.toDouble();
    final ratingCount = (result['ratingCount'] as num?)?.toInt() ?? 0;
    final memberSince = DateTime.tryParse(
      result['memberSince']?.toString() ?? '',
    );
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
                if (address.isNotEmpty)
                  _row(Icons.place_outlined, address)
                else if (area.isNotEmpty)
                  _row(Icons.place_outlined, area),
                if (address.isNotEmpty && area.isNotEmpty)
                  _row(Icons.map_outlined, area),
                if (rating != null)
                  _row(
                    Icons.star_rounded,
                    '${rating.toStringAsFixed(1)} out of 5 from $ratingCount '
                    '${ratingCount == 1 ? 'review' : 'reviews'}',
                  ),
                if (memberSince != null)
                  _row(
                    Icons.calendar_month_outlined,
                    'On SpazaSure since ${DateFormat('MMMM yyyy').format(memberSince)}',
                  ),
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

/// A camera that reads one QR code (the shop's sticker) and returns what it says.
class _StickerScanScreen extends StatefulWidget {
  const _StickerScanScreen();

  @override
  State<_StickerScanScreen> createState() => _StickerScanScreenState();
}

class _StickerScanScreenState extends State<_StickerScanScreen> {
  final _camera = MobileScannerController(
    detectionSpeed: DetectionSpeed.normal,
    formats: const [BarcodeFormat.qrCode],
  );
  bool _done = false;

  @override
  void dispose() {
    _camera.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    final value = capture.barcodes
        .map((b) => b.rawValue)
        .whereType<String>()
        .firstOrNull;
    if (value == null || _done) return;
    _done = true;
    Navigator.pop(context, value);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: BrandAppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: const Text('Scan the shop sticker'),
      ),
      body: Stack(
        children: [
          MobileScanner(
            controller: _camera,
            onDetect: _onDetect,
            errorBuilder: (context, error) => const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text(
                  'The camera isn\'t available. Allow camera access, or go back and type the shop number.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: Colors.white70),
                ),
              ),
            ),
          ),
          // A square frame to aim the sticker's QR code into
          Center(
            child: Container(
              width: 240,
              height: 240,
              decoration: BoxDecoration(
                border: Border.all(color: Colors.white, width: 3),
                borderRadius: BorderRadius.circular(20),
              ),
            ),
          ),
          const Positioned(
            left: 24,
            right: 24,
            bottom: 48,
            child: Text(
              'Point the camera at the QR code on the green SpazaSure sticker',
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.white, fontSize: 15),
            ),
          ),
        ],
      ),
    );
  }
}
