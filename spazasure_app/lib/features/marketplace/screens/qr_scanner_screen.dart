import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/features/shop_verification/shop_verify_screen.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/widgets/customer_verification_result.dart';
import 'package:spazasure_app/features/customer/widgets/packaging_analysis_result.dart';
import 'package:spazasure_app/features/customer/widgets/scanned_code_card.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';
import 'package:spazasure_app/services/analytics_service.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/barcode_check.dart';
import 'package:spazasure_app/services/device_context.dart';
import 'package:spazasure_app/services/packaging_insights.dart';
import 'package:spazasure_app/services/packaging_ocr_service.dart';
import 'package:spazasure_app/services/product_service.dart';
import 'package:spazasure_app/services/packaging_text_parser.dart';
import 'package:spazasure_app/services/scanned_code.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

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
  final _pinController = TextEditingController();
  String? _pin;
  MobileScannerController? _cameraController;
  // Reads barcodes from packaging photos; separate from the live camera.
  MobileScannerController? _photoScanner;
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
  // What the last scanned or typed code turned out to be (link, GS1, payment...)
  ScannedCode? _code;
  // The packaging photo analysis, and the photo itself for a report
  Map<String, dynamic>? _packagingResult;
  XFile? _packagingPhoto;

  bool get _showCodeOnly => _code != null && !_code!.isProduct;
  bool get _hasResult =>
      _product != null ||
      _error != null ||
      _showCodeOnly ||
      _packagingResult != null;

  @override
  void initState() {
    super.initState();
    // Shops and customers scan the same way: live camera, manual entry (index 1, which the
    // camera error screen relies on) and a packaging photo.
    _tabController = TabController(length: 3, vsync: this);
    _tabController.addListener(_onTabChanged);
    _cameraController = MobileScannerController(
      detectionSpeed: DetectionSpeed.normal,
      facing: CameraFacing.back,
      formats: const [
        BarcodeFormat.ean13,
        BarcodeFormat.ean8,
        BarcodeFormat.upcA,
        BarcodeFormat.upcE,
        BarcodeFormat.itf14,
        BarcodeFormat.code128,
        BarcodeFormat.qrCode,
        BarcodeFormat.dataMatrix,
      ],
      autoZoom: true,
    );
  }

  void _onTabChanged() {
    if (_tabController.indexIsChanging) return;
    setState(() {
      _scanning = _tabController.index == 0;
      _lastScanned = null;
      // The result is shown in place of the tabs, so choosing a tab starts over
      _product = null;
      _error = null;
      _code = null;
      _packagingResult = null;
    });
  }

  void _resetScan() {
    setState(() {
      _scanning = _tabController.index == 0;
      _product = null;
      _error = null;
      _code = null;
      _packagingResult = null;
      _lastScanned = null;
    });
  }

  /// Works out what a scanned or typed code is. Product codes (barcodes, SpazaSure codes,
  /// GS1 data, SKUs) are checked; anything else (a link, payment, Wi-Fi...) is explained.
  void _handleCode(String raw, {required String method, String? pin}) {
    final code = ScannedCode.parse(raw);
    setState(() {
      _code = code;
      _packagingResult = null;
    });
    if (!code.isProduct) {
      AnalyticsService.productScan(
        method: method,
        source: 'code_${code.kind.name}',
        riskLevel: null,
      );
      return;
    }
    _lookupProduct(
      code.productCode!,
      batch: code.batch,
      expiry: code.expiry,
      pin: pin,
      method: method,
    );
  }

  @override
  void dispose() {
    _tabController.dispose();
    _codeController.dispose();
    _pinController.dispose();
    _cameraController?.dispose();
    _photoScanner?.dispose();
    super.dispose();
  }

  Future<void> _lookupProduct(
    String code, {
    DateTime? expiry,
    String? batch,
    String? packagingText,
    String? pin,
    bool reward = true,
    String method = 'manual',
  }) async {
    if (code.isEmpty) return;
    if (_loading) return;

    setState(() {
      _loading = true;
      _error = null;
      _product = null;
      _verifiedCode = code.trim();
      _pin = pin;
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
      // Location helps spot copied codes scanned far apart (shops and customers alike)
      final fix = await DeviceContext.location();
      final deviceId = widget.customerMode
          ? await DeviceContext.deviceId()
          : null;
      final query = <String, String>{
        if (fix != null) ...{
          'lat': '${fix.latitude}',
          'lng': '${fix.longitude}',
        },
        if (expiry != null) 'expiry': expiry.toIso8601String().substring(0, 10),
        if (batch != null && batch.isNotEmpty) 'batch': batch,
        if (pin != null && pin.trim().isNotEmpty) 'pin': pin.trim(),
        if (packagingText != null && packagingText.trim().isNotEmpty)
          'packagingText': packagingText.length > 1500
              ? packagingText.substring(0, 1500)
              : packagingText,
      };
      final verifyRoute = query.isEmpty
          ? '/customer/verify/$encodedCode'
          : '/customer/verify/$encodedCode?${Uri(queryParameters: query).query}';

      // Everyone gets the same product check. Shops also see whether it is sold on the
      // marketplace (price, supplier, stock), looked up at the same time.
      final marketplaceLookup = widget.customerMode
          ? Future<Map<String, dynamic>?>.value(null)
          : _marketplaceListing(encodedCode);
      final product = Map<String, dynamic>.from(
        (await ApiService.get(verifyRoute))['data'] as Map,
      );
      if (!widget.customerMode) {
        final listing = await marketplaceLookup;
        if (listing != null) {
          product['marketplace'] = listing;
        } else {
          product['notInMarketplace'] = true;
        }
      }
      if (!mounted) return;
      setState(() => _product = product);
      if (reward) {
        AnalyticsService.productScan(
          method: method,
          source: product['source']?.toString(),
          riskLevel: product['riskLevel']?.toString(),
        );
      }

      if (widget.customerMode &&
          reward &&
          product['requiresPin'] != true &&
          (product['pinStatus'] == null || product['pinStatus'] == 'valid')) {
        try {
          final reward = await ApiService.post(
            '/customer/verify/$encodedCode/reward'
            '?productId=${product['productId'] ?? ''}&source=${product['source'] ?? 'unknown'}'
            '${fix != null ? '&lat=${fix.latitude}&lng=${fix.longitude}' : ''}'
            '&deviceId=$deviceId',
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

  /// The shop's marketplace listing for this code, or null when no supplier sells it here.
  Future<Map<String, dynamic>?> _marketplaceListing(String encodedCode) async {
    try {
      final res = await ApiService.get('/shop/marketplace/scan/$encodedCode');
      return Map<String, dynamic>.from(res['data'] as Map);
    } on ApiException catch (e) {
      if (e.statusCode == 404) return null;
      rethrow;
    }
  }

  void _onBarcodeDetected(BarcodeCapture capture) {
    if (!_scanning || _loading) return;
    String? code;
    for (final b in capture.barcodes) {
      final value = b.rawValue?.trim();
      if (value != null && isPlausibleScan(value)) {
        code = value;
        break;
      }
    }
    if (code == null) return;
    if (code == _lastScanned) return; // prevent duplicate scans

    _lastScanned = code;
    HapticFeedback.mediumImpact();
    setState(() => _scanning = false);
    // A SpazaSure shop sticker, not a product: show whether the shop is verified
    if (isShopStickerCode(code)) {
      Navigator.push(
        context,
        MaterialPageRoute(builder: (_) => ShopVerifyScreen(initialCode: code)),
      ).then((_) {
        if (!mounted) return;
        setState(() {
          _scanning = true;
          _lastScanned = null;
        });
      });
      return;
    }
    _handleCode(code, method: 'camera');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(
        title: const Text('Scan Product'),
        elevation: 0,
        bottom: TabBar(
          controller: _tabController,
          labelColor: Colors.white,
          unselectedLabelColor: Colors.white70,
          indicatorColor: AppColors.accent,
          tabs: [
            Tab(
              icon: const Icon(Icons.qr_code_scanner_rounded, size: 18),
              text: 'Scan Barcode',
            ),
            const Tab(
              icon: Icon(Icons.keyboard_rounded, size: 18),
              text: 'Manual Entry',
            ),
            const Tab(
              icon: Icon(Icons.document_scanner_rounded, size: 18),
              text: 'Packaging Photo',
            ),
          ],
        ),
      ),
      body: Column(
        children: [
          // Customers get the full screen for the result; the camera switches off meanwhile
          // The result gets the full screen; the camera switches off meanwhile
          if (_hasResult)
            _scanAnotherBar()
          else
            Expanded(
              flex: 3,
              child: TabBarView(
                controller: _tabController,
                children: [
                  _buildCameraTab(),
                  _buildManualTab(),
                  _buildPackagingTab(),
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
          if (_showCodeOnly || _packagingResult != null || _product != null)
            Expanded(
              flex: 3,
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // A link, payment code etc. needs explaining; so does a GS1 code or a
                    // barcode that fails its check, shown above the product check.
                    if (_code != null &&
                        (_showCodeOnly ||
                            _code!.kind == ScannedCodeKind.gs1 ||
                            _code!.warnings.isNotEmpty))
                      ScannedCodeCard(code: _code!),
                    if (_packagingResult != null)
                      PackagingAnalysisResult(
                        result: _packagingResult!,
                        onVerifyBarcode: _packagingBarcode == null
                            ? null
                            : () => _verifyPackagingBarcode(_packagingBarcode!),
                        onReport: _reportPackaging,
                        onRetake: () {
                          _resetScan();
                          _readPackaging(ImageSource.camera);
                        },
                      ),
                    if (_product != null) _buildProductResult(),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _scanAnotherBar() {
    final live = _tabController.index == 0;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      child: SizedBox(
        width: double.infinity,
        child: OutlinedButton.icon(
          onPressed: _loading ? null : _resetScan,
          icon: Icon(live ? Icons.qr_code_scanner_rounded : Icons.refresh),
          label: Text(live ? 'Scan another product' : 'Check another product'),
          style: OutlinedButton.styleFrom(
            padding: const EdgeInsets.symmetric(vertical: 12),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
          ),
        ),
      ),
    );
  }

  Widget _torchButton() {
    return ValueListenableBuilder<MobileScannerState>(
      valueListenable: _cameraController!,
      builder: (context, state, _) {
        if (!state.isRunning || state.torchState == TorchState.unavailable) {
          return const SizedBox.shrink();
        }
        final on = state.torchState == TorchState.on;
        return IconButton.filled(
          onPressed: () => _cameraController!.toggleTorch(),
          tooltip: on ? 'Turn the light off' : 'Turn the light on',
          style: IconButton.styleFrom(
            backgroundColor: Colors.black54,
            foregroundColor: on ? Colors.amber : Colors.white,
          ),
          icon: Icon(on ? Icons.flash_on_rounded : Icons.flash_off_rounded),
        );
      },
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
        // Scan overlay: wide, because most product barcodes are wider than tall
        if (_scanning)
          Center(
            child: Container(
              width: 290,
              height: 190,
              decoration: BoxDecoration(
                border: Border.all(color: AppColors.primary, width: 3),
                borderRadius: BorderRadius.circular(20),
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  Container(
                    margin: const EdgeInsets.only(bottom: 12),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 14,
                      vertical: 6,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.black54,
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Text(
                      _loading
                          ? 'Checking...'
                          : 'Point at the barcode or QR code',
                      style: const TextStyle(color: Colors.white, fontSize: 12),
                    ),
                  ),
                ],
              ),
            ),
          ),
        if (_cameraController != null)
          Positioned(top: 12, right: 12, child: _torchButton()),
      ],
    );
  }

  Widget _buildPackagingTab() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Icon(
            Icons.document_scanner_rounded,
            size: 48,
            color: AppColors.primary.withValues(alpha: 0.4),
          ),
          const SizedBox(height: 12),
          Text(
            'Check the packaging itself',
            textAlign: TextAlign.center,
            style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 6),
          Text(
            'For when there is no barcode, or it isn\'t registered. Photograph the front or the side with '
            'the most writing. SpazaSure reads the product, maker, ingredients, dates and marks, and looks '
            'for signs of a fake.',
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
            Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                children: [
                  const CircularProgressIndicator(),
                  const SizedBox(height: 12),
                  Text(
                    'Analysing the pack...',
                    style: AppTextStyles.caption.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
          if (_packagingError != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                _packagingError!,
                style: AppTextStyles.caption.copyWith(color: AppColors.error),
              ),
            ),
          const SizedBox(height: 16),
          Text(
            'Tips: hold the phone steady, fill the frame with the label, and avoid glare. '
            'Photograph another side afterwards if the dates or maker are elsewhere.',
            textAlign: TextAlign.center,
            style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
          ),
        ],
      ),
    );
  }

  /// The barcode read off the pack (by the AI, or on the phone), for checking on SpazaSure.
  String? get _packagingBarcode {
    final code =
        _packagingResult?['barcode']?.toString() ?? _packaging?.barcode;
    return code == null || code.isEmpty ? null : code;
  }

  Future<void> _readPackaging(ImageSource source) async {
    setState(() {
      _reading = true;
      _packagingError = null;
    });
    try {
      final photo = await ImagePicker().pickImage(
        source: source,
        maxWidth: 1800,
        maxHeight: 1800,
        imageQuality: 85,
      );
      if (photo == null) return;

      // Text and barcode on the phone first: sent along as hints, and the fallback.
      var scan = const PackagingScan(rawText: '');
      if (PackagingOcrService.supported) {
        scan = await PackagingOcrService.scan(photo.path);
        if (scan.barcode == null) {
          // The digits may be unreadable even when the bars decode fine.
          try {
            _photoScanner ??= MobileScannerController(autoStart: false);
            final capture = await _photoScanner!.analyzeImage(photo.path);
            final bars = capture?.barcodes.firstOrNull?.rawValue;
            if (bars != null) {
              scan = PackagingScan(
                rawText: scan.rawText,
                barcode: bars,
                expiry: scan.expiry,
                batch: scan.batch,
              );
            }
          } catch (_) {
            // No barcode in the photo is fine.
          }
        }
      }

      final bytes = await photo.readAsBytes();
      final name = photo.name.contains('.') ? photo.name : 'packaging.jpg';
      Map<String, dynamic>? result;
      String? unavailable;
      try {
        final res = await ApiService.uploadFile(
          '/customer/verify/packaging',
          fieldName: 'photo',
          fileBytes: bytes,
          fileName: name,
          fields: {
            if (scan.rawText.trim().isNotEmpty)
              'ocrText': scan.rawText.length > 3000
                  ? scan.rawText.substring(0, 3000)
                  : scan.rawText,
            if (scan.barcode != null) 'barcode': scan.barcode!,
          },
        );
        final data = Map<String, dynamic>.from(res['data'] as Map);
        if (data['source'] == 'unavailable') {
          unavailable = data['reason']?.toString();
        } else {
          result = data;
        }
      } catch (e) {
        unavailable = 'offline';
      }

      if (result == null) {
        if (scan.rawText.trim().isEmpty && scan.barcode == null) {
          if (!mounted) return;
          setState(
            () => _packagingError = unavailable == 'daily_limit'
                ? 'You have used today\'s photo checks. The phone could not read any text in this one; try again in better light.'
                : 'No text could be read from that photo. Try again closer, in good light.',
          );
          return;
        }
        result = PackagingInsights.analyze(scan);
      }
      if (!mounted) return;
      AnalyticsService.productScan(
        method: 'packaging',
        source: result['source']?.toString(),
        riskLevel: result['riskLevel']?.toString(),
      );
      setState(() {
        _packaging = scan;
        _packagingPhoto = photo;
        _packagingResult = result;
        _code = null;
        _product = null;
        _error = null;
      });
    } catch (e) {
      if (mounted) {
        setState(() => _packagingError = 'Could not read the photo: $e');
      }
    } finally {
      if (mounted) setState(() => _reading = false);
    }
  }

  void _verifyPackagingBarcode(String barcode) {
    final scan = _packaging;
    final a = _packagingResult?['analysis'] as Map?;
    final expiry =
        DateTime.tryParse(a?['expiryDate']?.toString() ?? '') ?? scan?.expiry;
    final batch = a?['batchNumber']?.toString() ?? scan?.batch;
    setState(() => _packagingResult = null);
    _lookupProduct(
      barcode,
      expiry: expiry,
      batch: batch,
      packagingText: scan?.rawText,
      method: 'packaging',
    );
  }

  Future<void> _reportPackaging() async {
    final r = _packagingResult!;
    final a = Map<String, dynamic>.from(r['analysis'] as Map? ?? const {});
    final photo = _packagingPhoto;
    final bytes = photo == null ? null : await photo.readAsBytes();
    final flags = [
      ...((a['redFlags'] as List?) ?? const []),
      for (final c in (r['checks'] as List? ?? const []))
        if (c is Map && c['status'] == 'fail') c['detail'],
    ].map((e) => '- $e').join('\n');
    if (!mounted) return;
    final name = [
      a['brand'],
      a['productName'],
    ].where((e) => e != null).join(' ');
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => ReportScreen(
          barcode: _packagingBarcode,
          productName: name.isEmpty ? null : name,
          batchNumber: a['batchNumber']?.toString(),
          expiryDate: a['expiryDate']?.toString(),
          initialType: r['riskLevel'] == 'high'
              ? 'Fake / Counterfeit Product'
              : 'Other',
          initialDescription: flags.isEmpty
              ? null
              : 'Flagged by SpazaSure packaging check:\n$flags',
          initialPhoto: bytes == null
              ? null
              : PlatformFile(
                  name: photo!.name,
                  size: bytes.length,
                  bytes: bytes,
                ),
        ),
      ),
    );
  }

  void _submitManual(String value) {
    final code = value.trim();
    if (code.isEmpty || _loading) return;
    _handleCode(code, method: 'manual', pin: _pinController.text.trim());
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
                  onSubmitted: (v) => _submitManual(v),
                ),
              ),
              const SizedBox(width: 12),
              GestureDetector(
                onTap: _loading
                    ? null
                    : () => _submitManual(_codeController.text),
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
          if (widget.customerMode) ...[
            const SizedBox(height: 12),
            TextField(
              controller: _pinController,
              textCapitalization: TextCapitalization.characters,
              decoration: InputDecoration(
                hintText: 'Scratch-off PIN (if the pack has one)',
                prefixIcon: const Icon(
                  Icons.pin_outlined,
                  color: AppColors.primary,
                ),
                filled: true,
                fillColor: AppColors.surface,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(14),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
          ],
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
                  'Could not check this product',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.error,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  _error ?? 'Please try again.',
                  style: AppTextStyles.caption.copyWith(color: AppColors.error),
                ),
              ],
            ),
          ),
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
        pin: _pin,
        reward: false,
      );
    }
  }

  void _openReport({PlatformFile? photo, String? details}) {
    final p = _product!;
    final expiry =
        p['expiryDate']?.toString() ??
        _expiry?.toIso8601String().substring(0, 10);
    final level = p['riskLevel']?.toString();
    final indicators =
        (p['indicators'] as List?)?.map((e) => '- $e').join('\n') ?? '';
    final expired =
        _expiry != null &&
        _expiry!.isBefore(DateTime.now().subtract(const Duration(days: 1)));
    final recalled = p['isRecalled'] == true;
    final flagged = indicators.isEmpty
        ? null
        : 'Flagged by SpazaSure verification:\n$indicators';
    final description = [?details, ?flagged].join('\n\n');
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
              : (level == 'suspicious' ||
                    level == 'high' ||
                    recalled ||
                    details != null)
              ? 'Fake / Counterfeit Product'
              : 'Other',
          initialDescription: description.isEmpty ? null : description,
          initialPhoto: photo,
        ),
      ),
    );
  }

  Widget _buildProductResult() {
    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: CustomerVerificationResult(
        product: _product!,
        fallbackCode: _verifiedCode ?? '',
        enteredExpiry: _expiry,
        rewardMessage: _rewardMessage,
        rewardDuplicate: _rewardDuplicate,
        onCheckExpiry: _loading ? null : _pickExpiry,
        // The scratch-off PIN belongs to the customer who buys the item: a shop entering it
        // would use it up, and the buyer would then be told it was already used.
        onSubmitPin: !widget.customerMode || _loading
            ? null
            : (pin) => _lookupProduct(
                _verifiedCode ?? '',
                expiry: _expiry,
                batch: _batch,
                packagingText: _packagingText,
                pin: pin,
              ),
        onReport: _openReport,
        onReportWithPhoto: (photo, details) =>
            _openReport(photo: photo, details: details),
        onOrder: widget.customerMode ? null : _openMarketplaceProduct,
      ),
    );
  }

  Future<void> _openMarketplaceProduct(String productId) async {
    try {
      final product = await ProductService.getProduct(productId);
      if (!mounted) return;
      Navigator.pushNamed(context, '/product', arguments: product);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }
}
