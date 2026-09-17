import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/api_service.dart';

class QrScannerScreen extends StatefulWidget {
  final bool customerMode;

  const QrScannerScreen({super.key, this.customerMode = false});

  @override
  State<QrScannerScreen> createState() => _QrScannerScreenState();
}

class _QrScannerScreenState extends State<QrScannerScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final _codeController = TextEditingController();
  MobileScannerController? _cameraController;
  bool _loading = false;
  bool _scanning = true;
  Map<String, dynamic>? _product;
  String? _error;
  String? _lastScanned;
  String? _rewardMessage;
  bool _rewardDuplicate = false;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _tabController.addListener(() {
      if (_tabController.index == 0) {
        _startCamera();
      } else {
        _stopCamera();
      }
    });
    _startCamera();
  }

  void _startCamera() {
    _cameraController ??= MobileScannerController(
      detectionSpeed: DetectionSpeed.normal,
      facing: CameraFacing.back,
    );
    setState(() => _scanning = true);
  }

  void _stopCamera() {
    setState(() => _scanning = false);
  }

  @override
  void dispose() {
    _tabController.dispose();
    _codeController.dispose();
    _cameraController?.dispose();
    super.dispose();
  }

  Future<void> _lookupProduct(String code) async {
    if (code.isEmpty) return;
    if (_loading) return;

    setState(() {
      _loading = true;
      _error = null;
      _product = null;
      _rewardMessage = null;
      _rewardDuplicate = false;
    });

    try {
      final encodedCode = Uri.encodeComponent(code.trim());
      final route = widget.customerMode
          ? '/customer/verify/$encodedCode'
          : '/shop/marketplace/scan/$encodedCode';
      final res = await ApiService.get(route);
      if (!mounted) return;
      final product = res['data'] as Map<String, dynamic>;
      setState(() => _product = product);

      if (widget.customerMode) {
        try {
          final reward = await ApiService.post(
            '/customer/verify/$encodedCode/reward'
            '?productId=${product['productId'] ?? ''}&source=${product['source'] ?? 'unknown'}',
            {},
          );
          final rewardData = reward['data'] as Map<String, dynamic>? ?? {};
          if (!mounted) return;
          setState(() {
            _rewardMessage =
                rewardData['message']?.toString() ?? '+5 reward points earned.';
            _rewardDuplicate = rewardData['duplicate'] == true;
          });
        } catch (_) {
          // Verification remains useful if rewards are temporarily unavailable.
        }
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
      });
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _onBarcodeDetected(BarcodeCapture capture) {
    if (!_scanning) return;
    final barcodes = capture.barcodes;
    if (barcodes.isEmpty) return;

    final code = barcodes.first.rawValue;
    if (code == null || code.isEmpty) return;
    if (code == _lastScanned) return; // prevent duplicate scans

    _lastScanned = code;
    setState(() => _scanning = false);
    _lookupProduct(code);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Scan Product'),
        backgroundColor: AppColors.surface,
        foregroundColor: AppColors.textPrimary,
        elevation: 0,
        bottom: TabBar(
          controller: _tabController,
          labelColor: AppColors.primary,
          unselectedLabelColor: AppColors.textHint,
          indicatorColor: AppColors.primary,
          tabs: const [
            Tab(
              icon: Icon(Icons.camera_alt_rounded, size: 18),
              text: 'Camera Scan',
            ),
            Tab(
              icon: Icon(Icons.keyboard_rounded, size: 18),
              text: 'Manual Entry',
            ),
          ],
        ),
      ),
      body: Column(
        children: [
          // Scanner / Manual input area
          Expanded(
            flex: _product != null || _error != null ? 2 : 3,
            child: TabBarView(
              controller: _tabController,
              children: [_buildCameraTab(), _buildManualTab()],
            ),
          ),
          // Results area
          if (_loading)
            const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            ),
          if (_error != null) _buildErrorResult(),
          if (_product != null)
            Expanded(
              flex: 3,
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
                child: _buildProductResult(),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildCameraTab() {
    return Stack(
      children: [
        // Camera view
        if (_cameraController != null)
          ClipRRect(
            child: MobileScanner(
              controller: _cameraController!,
              onDetect: _onBarcodeDetected,
              errorBuilder: (context, error) {
                return Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          Icons.videocam_off_rounded,
                          size: 48,
                          color: AppColors.textHint,
                        ),
                        const SizedBox(height: 12),
                        Text(
                          'Camera not available',
                          style: AppTextStyles.body.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'Use the "Manual Entry" tab to type the barcode or SKU number instead.',
                          textAlign: TextAlign.center,
                          style: AppTextStyles.caption.copyWith(
                            color: AppColors.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 16),
                        ElevatedButton.icon(
                          onPressed: () => _tabController.animateTo(1),
                          icon: const Icon(Icons.keyboard, size: 18),
                          label: const Text('Enter Manually'),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppColors.primary,
                            foregroundColor: Colors.white,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(12),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),
        // Scan overlay
        if (_scanning)
          Center(
            child: Container(
              width: 250,
              height: 250,
              decoration: BoxDecoration(
                border: Border.all(color: AppColors.primary, width: 3),
                borderRadius: BorderRadius.circular(20),
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  Container(
                    margin: const EdgeInsets.only(bottom: 16),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 14,
                      vertical: 6,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.black54,
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: const Text(
                      'Point at barcode',
                      style: TextStyle(color: Colors.white, fontSize: 12),
                    ),
                  ),
                ],
              ),
            ),
          ),
        // Scan again button
        if ((!_scanning && _product != null) || _error != null)
          Positioned(
            bottom: 16,
            left: 16,
            right: 16,
            child: ElevatedButton.icon(
              onPressed: () {
                setState(() {
                  _scanning = true;
                  _product = null;
                  _error = null;
                  _lastScanned = null;
                });
              },
              icon: const Icon(Icons.refresh, size: 18),
              label: const Text('Scan Again'),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
            ),
          ),
      ],
    );
  }

  Widget _buildManualTab() {
    return Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Icons.qr_code_scanner_rounded,
            size: 48,
            color: AppColors.primary.withValues(alpha: 0.4),
          ),
          const SizedBox(height: 16),
          Text(
            'Enter Barcode or SKU',
            style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 6),
          Text(
            'Type the number printed on the product',
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 20),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _codeController,
                  decoration: InputDecoration(
                    hintText: 'e.g. 6001234567890 or SKU-001',
                    prefixIcon: const Icon(
                      Icons.search,
                      color: AppColors.primary,
                    ),
                    filled: true,
                    fillColor: AppColors.surface,
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(14),
                      borderSide: BorderSide.none,
                    ),
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 14,
                    ),
                  ),
                  onSubmitted: (v) => _lookupProduct(v.trim()),
                ),
              ),
              const SizedBox(width: 12),
              GestureDetector(
                onTap: _loading
                    ? null
                    : () => _lookupProduct(_codeController.text.trim()),
                child: Container(
                  width: 52,
                  height: 52,
                  decoration: BoxDecoration(
                    color: AppColors.primary,
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: _loading
                      ? const Padding(
                          padding: EdgeInsets.all(14),
                          child: CircularProgressIndicator(
                            color: Colors.white,
                            strokeWidth: 2,
                          ),
                        )
                      : const Icon(
                          Icons.verified_user_rounded,
                          color: Colors.white,
                          size: 22,
                        ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildErrorResult() {
    return Container(
      margin: const EdgeInsets.all(16),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.error.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.error.withValues(alpha: 0.2)),
      ),
      child: Row(
        children: [
          Icon(Icons.warning_amber_rounded, color: AppColors.error, size: 28),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Product Not Found',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.error,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'This QR code, barcode, or SKU is not registered. The product may be unverified.',
                  style: AppTextStyles.caption.copyWith(color: AppColors.error),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildProductResult() {
    final p = _product!;
    final isVerified = p['isVerified'] == true;
    final supplierVerified = p['supplierVerified'] == true;
    final productApproved = p['productApproved'] == true;
    final supplierName = p['supplierName'] ?? 'Unknown';
    final supplierTier = p['supplierTier'] ?? 'basic';
    final supplierCity = p['supplierCity'] ?? '';
    final supplierProvince = p['supplierProvince'] ?? '';
    final scanType = p['scanType'] ?? 'barcode';
    final isQrScan = scanType == 'qr';
    final isRecalled = p['isRecalled'] == true;
    final isExpired = p['isExpired'] == true;
    final repeatScan = p['repeatScan'] == true;
    final batchNumber = p['batchNumber']?.toString();
    final expiryDate = p['expiryDate']?.toString();
    final description = p['description']?.toString() ?? '';

    return Column(
      children: [
        if (widget.customerMode && _rewardMessage != null)
          Container(
            width: double.infinity,
            margin: const EdgeInsets.only(bottom: 12),
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: (_rewardDuplicate ? AppColors.warning : AppColors.success)
                  .withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(16),
              border: Border.all(
                color:
                    (_rewardDuplicate ? AppColors.warning : AppColors.success)
                        .withValues(alpha: 0.3),
              ),
            ),
            child: Row(
              children: [
                Icon(
                  _rewardDuplicate ? Icons.info_outline : Icons.stars_rounded,
                  color: _rewardDuplicate
                      ? AppColors.warning
                      : AppColors.success,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    _rewardMessage!,
                    style: AppTextStyles.body.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
          ),
        // Verification banner
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: isVerified
                ? AppColors.success.withValues(alpha: 0.1)
                : AppColors.warning.withValues(alpha: 0.1),
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: isVerified
                  ? AppColors.success.withValues(alpha: 0.3)
                  : AppColors.warning.withValues(alpha: 0.3),
            ),
          ),
          child: Row(
            children: [
              Icon(
                isVerified ? Icons.verified_rounded : Icons.warning_rounded,
                color: isVerified ? AppColors.success : AppColors.warning,
                size: 28,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      isVerified ? '✓ Verified Product' : '⚠ Unverified',
                      style: AppTextStyles.body.copyWith(
                        fontWeight: FontWeight.w800,
                        color: isVerified
                            ? AppColors.success
                            : AppColors.warning,
                      ),
                    ),
                    Text(
                      isVerified
                          ? 'Approved by admin, verified supplier'
                          : 'Not fully verified — exercise caution',
                      style: AppTextStyles.caption.copyWith(
                        color: isVerified
                            ? AppColors.success
                            : AppColors.warning,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        if (isQrScan) ...[
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: isRecalled || isExpired
                  ? AppColors.error.withValues(alpha: 0.08)
                  : repeatScan
                  ? AppColors.warning.withValues(alpha: 0.1)
                  : AppColors.success.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(16),
              border: Border.all(
                color: isRecalled || isExpired
                    ? AppColors.error.withValues(alpha: 0.25)
                    : repeatScan
                    ? AppColors.warning.withValues(alpha: 0.3)
                    : AppColors.success.withValues(alpha: 0.25),
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  isRecalled
                      ? '⚠ Recalled Product Code'
                      : isExpired
                      ? '⚠ Expired Product Code'
                      : repeatScan
                      ? '⚠ Previously Scanned QR'
                      : '✓ First Registered QR Scan',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w800,
                    color: isRecalled || isExpired
                        ? AppColors.error
                        : repeatScan
                        ? AppColors.warning
                        : AppColors.success,
                  ),
                ),
                const SizedBox(height: 6),
                _infoRow(
                  Icons.qr_code_2,
                  'Scan type',
                  'SpazaSure traceable QR',
                ),
                if (batchNumber != null && batchNumber.isNotEmpty)
                  _infoRow(Icons.inventory, 'Batch', batchNumber),
                if (expiryDate != null && expiryDate.isNotEmpty)
                  _infoRow(Icons.event, 'Expiry', expiryDate),
                if (p['scannedAt'] != null)
                  _infoRow(
                    Icons.schedule,
                    repeatScan ? 'First scanned' : 'Scanned at',
                    p['scannedAt'].toString(),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 12),
        ],
        // Product info
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 8,
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                p['name'] ?? '',
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
              ),
              if (description.isNotEmpty) ...[
                const SizedBox(height: 4),
                Text(
                  description,
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
              const SizedBox(height: 6),
              _infoRow(Icons.qr_code, 'SKU', p['sku'] ?? 'N/A'),
              if (p['barcode'] != null)
                _infoRow(Icons.barcode_reader, 'Barcode', p['barcode']),
              _infoRow(
                Icons.attach_money,
                'Price',
                'R ${(p['price'] as num?)?.toStringAsFixed(2) ?? '0.00'}',
              ),
              _infoRow(
                Icons.inventory_2,
                'Stock',
                '${p['stockQty'] ?? 0} ${p['unit'] ?? 'units'}',
              ),
              if (p['categoryName'] != null)
                _infoRow(Icons.category, 'Category', p['categoryName']),
            ],
          ),
        ),
        const SizedBox(height: 12),
        // Supplier info
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 8,
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Text(
                    'Supplier',
                    style: AppTextStyles.body.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const Spacer(),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 3,
                    ),
                    decoration: BoxDecoration(
                      color: supplierVerified
                          ? AppColors.success.withValues(alpha: 0.1)
                          : AppColors.warning.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          supplierVerified ? Icons.verified : Icons.pending,
                          size: 12,
                          color: supplierVerified
                              ? AppColors.success
                              : AppColors.warning,
                        ),
                        const SizedBox(width: 4),
                        Text(
                          supplierVerified ? 'Verified' : 'Pending',
                          style: AppTextStyles.caption.copyWith(
                            fontWeight: FontWeight.w600,
                            color: supplierVerified
                                ? AppColors.success
                                : AppColors.warning,
                            fontSize: 10,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              _infoRow(Icons.business, 'Company', supplierName),
              _infoRow(
                Icons.workspace_premium,
                'Tier',
                _tierLabel(supplierTier),
              ),
              if (supplierCity.isNotEmpty)
                _infoRow(
                  Icons.location_on,
                  'Location',
                  [
                    supplierCity,
                    supplierProvince,
                  ].where((s) => s.isNotEmpty).join(', '),
                ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        // Verification checks
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 8,
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Verification Checks',
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: 10),
              _checkItem('Product approved by admin', productApproved),
              _checkItem('Supplier verified', supplierVerified),
              if (isQrScan) _checkItem('QR code not recalled', !isRecalled),
              if (isQrScan && expiryDate != null)
                _checkItem('QR code not expired', !isExpired),
              _checkItem('Safe to purchase', isVerified),
            ],
          ),
        ),
      ],
    );
  }

  Widget _infoRow(IconData icon, String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Icon(icon, size: 15, color: AppColors.textHint),
          const SizedBox(width: 8),
          Text(
            '$label: ',
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: AppTextStyles.caption.copyWith(
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _checkItem(String label, bool passed) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Icon(
            passed ? Icons.check_circle : Icons.cancel,
            size: 18,
            color: passed ? AppColors.success : AppColors.warning,
          ),
          const SizedBox(width: 8),
          Text(
            label,
            style: AppTextStyles.bodySmall.copyWith(
              color: passed ? AppColors.success : AppColors.warning,
            ),
          ),
        ],
      ),
    );
  }

  String _tierLabel(String tier) {
    switch (tier) {
      case 'gold':
        return '🥇 Gold';
      case 'silver':
        return '🥈 Silver';
      case 'bronze':
        return '🥉 Bronze';
      default:
        return '✓ Basic';
    }
  }
}
