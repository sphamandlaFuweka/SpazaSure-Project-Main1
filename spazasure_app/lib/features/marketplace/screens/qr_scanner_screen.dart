import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/packaging_ocr_service.dart';
import 'package:spazasure_app/services/packaging_text_parser.dart';

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
  final _packagingBarcodeController = TextEditingController();
  MobileScannerController? _cameraController;
  bool _loading = false;
  bool _scanning = true;
  bool _reading = false;
  PackagingScan? _packaging;
  String? _packagingError;
  Map<String, dynamic>? _product;
  String? _error;
  String? _lastScanned;
  String? _verifiedCode;
  DateTime? _expiry;
  String? _batch;
  String? _packagingText;
  String? _rewardMessage;
  bool _rewardDuplicate = false;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(
      length: widget.customerMode ? 3 : 2,
      vsync: this,
    );
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
    _packagingBarcodeController.dispose();
    _cameraController?.dispose();
    super.dispose();
  }

  Future<void> _lookupProduct(
    String code, {
    DateTime? expiry,
    String? batch,
    String? packagingText,
    bool reward = true,
  }) async {
    if (code.isEmpty) return;
    if (_loading) return;

    setState(() {
      _loading = true;
      _error = null;
      _product = null;
      _verifiedCode = code.trim();
      _expiry = expiry;
      _batch = batch;
      _packagingText = packagingText;
      if (reward) {
        _rewardMessage = null;
        _rewardDuplicate = false;
      }
    });

    try {
      final encodedCode = Uri.encodeComponent(code.trim());
      final query = <String, String>{
        if (expiry != null)
          'expiryDate': expiry.toIso8601String().substring(0, 10),
        if (packagingText != null && packagingText.trim().isNotEmpty)
          'packagingText': packagingText.length > 600
              ? packagingText.substring(0, 600)
              : packagingText,
      };
      final queryString = query.isEmpty
          ? ''
          : '?${Uri(queryParameters: query).query}';
      final route = widget.customerMode
          ? '/customer/verify/$encodedCode$queryString'
          : '/shop/marketplace/scan/$encodedCode';
      final res = await ApiService.get(route);
      if (!mounted) return;
      final product = res['data'] as Map<String, dynamic>;
      setState(() => _product = product);

      if (widget.customerMode && reward) {
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
          tabs: [
            const Tab(
              icon: Icon(Icons.camera_alt_rounded, size: 18),
              text: 'Camera Scan',
            ),
            const Tab(
              icon: Icon(Icons.keyboard_rounded, size: 18),
              text: 'Manual Entry',
            ),
            if (widget.customerMode)
              const Tab(
                icon: Icon(Icons.document_scanner_rounded, size: 18),
                text: 'Scan Packaging',
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
              children: [
                _buildCameraTab(),
                _buildManualTab(),
                if (widget.customerMode) _buildPackagingTab(),
              ],
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

  Widget _buildPackagingTab() {
    if (!PackagingOcrService.supported) {
      return Padding(
        padding: const EdgeInsets.all(24),
        child: Center(
          child: Text(
            'Packaging scan reads the barcode, batch and expiry date from a photo. It is available in the SpazaSure mobile app on Android and iOS. Use Camera Scan or Manual Entry here.',
            textAlign: TextAlign.center,
            style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
          ),
        ),
      );
    }
    final scan = _packaging;
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Scan the packaging',
            textAlign: TextAlign.center,
            style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 6),
          Text(
            'Take a clear photo of the side of the pack with the barcode, batch number and expiry date.',
            textAlign: TextAlign.center,
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: FilledButton.icon(
                  onPressed: _reading
                      ? null
                      : () => _readPackaging(ImageSource.camera),
                  icon: const Icon(Icons.photo_camera),
                  label: const Text('Take photo'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _reading
                      ? null
                      : () => _readPackaging(ImageSource.gallery),
                  icon: const Icon(Icons.photo_library_outlined),
                  label: const Text('Choose photo'),
                ),
              ),
            ],
          ),
          if (_reading)
            const Padding(
              padding: EdgeInsets.all(20),
              child: Center(child: CircularProgressIndicator()),
            ),
          if (_packagingError != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                _packagingError!,
                style: AppTextStyles.caption.copyWith(color: AppColors.error),
              ),
            ),
          if (scan != null) ...[
            const SizedBox(height: 16),
            TextField(
              controller: _packagingBarcodeController,
              keyboardType: TextInputType.number,
              decoration: InputDecoration(
                labelText: 'Barcode',
                helperText: scan.barcode == null
                    ? 'No barcode could be read. Type the number printed under the bars.'
                    : 'Read from the photo. Correct it if it looks wrong.',
                filled: true,
                fillColor: AppColors.surface,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(14),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
            const SizedBox(height: 10),
            _infoRow(
              Icons.event,
              'Expiry',
              scan.expiry == null
                  ? 'Not found'
                  : scan.expiry!.toIso8601String().substring(0, 10),
            ),
            _infoRow(Icons.inventory, 'Batch', scan.batch ?? 'Not found'),
            const SizedBox(height: 6),
            FilledButton.icon(
              onPressed: _loading
                  ? null
                  : () => _lookupProduct(
                      _packagingBarcodeController.text.trim(),
                      expiry: scan.expiry,
                      batch: scan.batch,
                      packagingText: scan.rawText,
                    ),
              icon: const Icon(Icons.verified_user_rounded),
              label: const Text('Verify product'),
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _readPackaging(ImageSource source) async {
    setState(() {
      _reading = true;
      _packagingError = null;
    });
    try {
      final photo = await ImagePicker().pickImage(
        source: source,
        imageQuality: 90,
      );
      if (photo == null) return;
      final scan = await PackagingOcrService.scan(photo.path);
      var barcode = scan.barcode;
      if (barcode == null) {
        // The digits may be unreadable even when the bars decode fine.
        final capture = await _cameraController?.analyzeImage(photo.path);
        barcode = capture?.barcodes.firstOrNull?.rawValue;
      }
      if (!mounted) return;
      final hasText = scan.rawText.trim().isNotEmpty;
      setState(() {
        _packaging = PackagingScan(
          rawText: scan.rawText,
          barcode: barcode,
          expiry: scan.expiry,
          batch: scan.batch,
        );
        _packagingBarcodeController.text = barcode ?? '';
        if (!hasText && barcode == null) {
          _packagingError =
              'No text was found in that photo. Try again in better light.';
        }
      });
    } catch (e) {
      if (mounted) {
        setState(() => _packagingError = 'Could not read the photo: $e');
      }
    } finally {
      if (mounted) setState(() => _reading = false);
    }
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
                  widget.customerMode
                      ? 'Verification failed'
                      : 'Product Not Found',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.error,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  widget.customerMode
                      ? (_error ?? 'Could not verify this product.')
                      : 'This QR code, barcode, or SKU is not registered. The product may be unverified.',
                  style: AppTextStyles.caption.copyWith(color: AppColors.error),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCustomerResult() {
    final p = _product!;
    final source = p['source']?.toString() ?? 'not_registered';
    final level = p['riskLevel']?.toString() ?? 'review';
    final notRecognised = source == 'not_registered';
    final recalled = p['isRecalled'] == true;
    final color = switch (level) {
      'low' => AppColors.success,
      'review' => AppColors.warning,
      'suspicious' => Colors.deepOrange,
      _ => AppColors.error,
    };
    final title = notRecognised
        ? 'Product not recognised'
        : recalled
        ? 'Recalled product'
        : switch (level) {
            'low' => 'No major issues detected',
            'review' => 'Something needs checking',
            'suspicious' => 'Potentially suspicious product',
            _ => 'High risk: further assessment recommended',
          };
    final message = notRecognised
        ? (p['message']?.toString() ??
              "We couldn't find this product in the SpazaSure database.")
        : recalled
        ? 'Do not buy or use this product. It has an active recall.'
        : switch (level) {
            'low' => 'Based only on the information we checked.',
            'review' => 'Some information needs a closer look.',
            _ =>
              'Indicators were found that need further assessment. This is not a confirmed counterfeit determination.',
          };
    final indicators =
        (p['indicators'] as List?)?.whereType<Map>().toList() ?? [];
    final checks = (p['checks'] as List?)?.whereType<Map>().toList() ?? [];
    final tips = (p['tips'] as List?)?.map((e) => '$e').toList() ?? [];
    final allergens =
        (p['allergens'] as List?)?.map((e) => '$e').toList() ?? [];
    final matched =
        ((p['allergyWarning'] as Map?)?['matchedAllergens'] as List?) ?? [];
    final description = p['description']?.toString() ?? '';
    final batch = p['batchNumber']?.toString();
    final expiry = p['expiryDate']?.toString();

    return Column(
      children: [
        if (_rewardMessage != null)
          Container(
            width: double.infinity,
            margin: const EdgeInsets.only(bottom: 12),
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: (_rewardDuplicate ? AppColors.warning : AppColors.success)
                  .withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(16),
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
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: color.withValues(alpha: 0.1),
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: color.withValues(alpha: 0.3)),
          ),
          child: Row(
            children: [
              Icon(
                notRecognised
                    ? Icons.help_outline_rounded
                    : level == 'low'
                    ? Icons.verified_rounded
                    : level == 'review'
                    ? Icons.warning_amber_rounded
                    : Icons.dangerous_rounded,
                color: color,
                size: 28,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: AppTextStyles.body.copyWith(
                        fontWeight: FontWeight.w800,
                        color: color,
                      ),
                    ),
                    Text(
                      message,
                      style: AppTextStyles.caption.copyWith(color: color),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        if (matched.isNotEmpty) ...[
          const SizedBox(height: 12),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: AppColors.error.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Text(
              'Allergy warning: contains ${matched.join(', ')}',
              style: AppTextStyles.body.copyWith(
                fontWeight: FontWeight.w700,
                color: AppColors.error,
              ),
            ),
          ),
        ],
        const SizedBox(height: 12),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(16),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                p['name']?.toString() ?? 'Unknown product',
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
              _infoRow(
                Icons.barcode_reader,
                'Code',
                p['code']?.toString() ?? _lastScanned ?? _codeController.text,
              ),
              if (batch != null && batch.isNotEmpty)
                _infoRow(Icons.inventory, 'Batch', batch),
              if (expiry != null && expiry.isNotEmpty)
                _infoRow(Icons.event, 'Expiry', expiry),
              if (allergens.isNotEmpty)
                _infoRow(
                  Icons.warning_amber,
                  'Allergens',
                  allergens.join(', '),
                ),
              if (p['supplierName'] != null)
                _infoRow(Icons.business, 'Supplier', '${p['supplierName']}'),
            ],
          ),
        ),
        if (indicators.isNotEmpty) ...[
          const SizedBox(height: 12),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Indicators detected',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 8),
                for (final i in indicators)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(
                          i['severity'] == 'info'
                              ? Icons.info_outline
                              : Icons.warning_amber_rounded,
                          size: 18,
                          color: i['severity'] == 'critical'
                              ? AppColors.error
                              : i['severity'] == 'info'
                              ? AppColors.textHint
                              : AppColors.warning,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            '${i['message']}',
                            style: AppTextStyles.bodySmall,
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
        ],
        if (checks.isNotEmpty) ...[
          const SizedBox(height: 12),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'What we checked',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 8),
                for (final c in checks)
                  _riskCheckRow('${c['label']}', '${c['status']}'),
                if (!notRecognised && p['expiryDate'] == null) ...[
                  const SizedBox(height: 4),
                  OutlinedButton.icon(
                    onPressed: _loading ? null : _pickExpiry,
                    icon: const Icon(Icons.event, size: 18),
                    label: const Text('Check expiry date'),
                  ),
                ],
              ],
            ),
          ),
        ],
        if (tips.isNotEmpty) ...[
          const SizedBox(height: 12),
          Container(
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(16),
            ),
            child: ExpansionTile(
              title: Text(
                'How to check this product',
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
              ),
              childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              expandedCrossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (var n = 0; n < tips.length; n++)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Text(
                      '${n + 1}. ${tips[n]}',
                      style: AppTextStyles.bodySmall,
                    ),
                  ),
              ],
            ),
          ),
        ],
        const SizedBox(height: 12),
        SizedBox(
          width: double.infinity,
          child: FilledButton.icon(
            onPressed: () => _openReport(indicators, level, notRecognised),
            icon: const Icon(Icons.flag_outlined),
            label: Text(
              notRecognised ? 'Submit product for review' : 'Report product',
            ),
          ),
        ),
        const SizedBox(height: 10),
        Text(
          '${p['disclaimer'] ?? 'This check does not guarantee authenticity or product safety.'}',
          textAlign: TextAlign.center,
          style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
        ),
      ],
    );
  }

  Widget _riskCheckRow(String label, String status) {
    final (icon, color) = switch (status) {
      'pass' => (Icons.check_circle, AppColors.success),
      'fail' => (Icons.cancel, AppColors.error),
      'warn' => (Icons.error_outline, AppColors.warning),
      _ => (Icons.help_outline, AppColors.textHint),
    };
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Icon(icon, size: 18, color: color),
          const SizedBox(width: 8),
          Expanded(child: Text(label, style: AppTextStyles.bodySmall)),
        ],
      ),
    );
  }

  Future<void> _pickExpiry() async {
    final code = _verifiedCode;
    if (code == null) return;
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _expiry ?? now,
      firstDate: DateTime(now.year - 2),
      lastDate: DateTime(now.year + 10),
      helpText: 'Expiry or best-before date',
    );
    if (picked != null) {
      _lookupProduct(
        code,
        expiry: picked,
        batch: _batch,
        packagingText: _packagingText,
        reward: false,
      );
    }
  }

  void _openReport(List<Map> indicators, String level, bool notRecognised) {
    final p = _product!;
    final expired = indicators.any((i) => i['code'] == 'expired');
    final expiry =
        p['expiryDate']?.toString() ??
        _expiry?.toIso8601String().substring(0, 10);
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => ReportScreen(
          barcode:
              p['barcode']?.toString() ??
              p['code']?.toString() ??
              _verifiedCode,
          productId: p['productId']?.toString(),
          productName: p['name']?.toString(),
          batchNumber: p['batchNumber']?.toString() ?? _batch,
          expiryDate: expiry,
          initialType: expired
              ? 'Expired Goods'
              : (level == 'suspicious' || level == 'high')
              ? 'Fake / Counterfeit Product'
              : 'Other',
        ),
      ),
    );
  }

  Widget _buildProductResult() {
    if (widget.customerMode) return _buildCustomerResult();
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
