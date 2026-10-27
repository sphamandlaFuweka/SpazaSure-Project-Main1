import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:file_picker/file_picker.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/address_autocomplete_field.dart';
import 'package:spazasure_app/services/address_service.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/auth_service.dart';
import 'package:spazasure_app/services/device_context.dart';

class ReportScreen extends StatefulWidget {
  final String? barcode;
  final String? productId;
  final String? productName;
  final String? batchNumber;
  final String? expiryDate;
  final String? initialType;
  final String? initialDescription;
  /// Evidence photo already taken, e.g. of a pack that didn't match the reference photos.
  final PlatformFile? initialPhoto;

  const ReportScreen({
    super.key,
    this.barcode,
    this.productId,
    this.productName,
    this.batchNumber,
    this.expiryDate,
    this.initialType,
    this.initialDescription,
    this.initialPhoto,
  });

  @override
  State<ReportScreen> createState() => _ReportScreenState();
}

class _ReportScreenState extends State<ReportScreen> {
  int _step = 0;
  String? _selectedType;
  bool? _isAnonymous;
  String? _complaintId;
  PlatformFile? _photoFile;
  bool _submitting = false;
  String? _submitError;

  final _shopNameController = TextEditingController();
  final _locationController = TextEditingController();

  /// The shop's place once picked from suggestions or pinned on the map
  AddressSuggestion? _shopPlace;
  final _productController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _serialController = TextEditingController();
  final _priceController = TextEditingController();
  bool? _scratchIntact;
  bool? _sealTampered;
  DeviceFix? _fix;
  bool _locating = false;

  // Product reports without a scanned barcode must identify the item by serial or PIN.
  bool get _needsSerial =>
      (widget.barcode ?? '').trim().isEmpty &&
      (_selectedType == 'Fake / Counterfeit Product' ||
          _selectedType == 'Expired Goods');

  static const _reportTypes = [
    _ReportType(
      Icons.gpp_bad_rounded,
      'Fake / Counterfeit Product',
      'Fake or counterfeit product',
      'Looks fake, wrong packaging, or failed a SpazaSure scan',
      AppColors.error,
      AppColors.redTint,
    ),
    _ReportType(
      Icons.event_busy_rounded,
      'Expired Goods',
      'Expired goods',
      'Past its sell-by or expiry date, or spoiled',
      AppColors.accentDark,
      AppColors.accentTint,
    ),
    _ReportType(
      Icons.sell_rounded,
      'Misleading Pricing',
      'Unfair or misleading price',
      'Charged more than the shelf price, or hidden extras',
      AppColors.primary,
      AppColors.primaryTint,
    ),
    _ReportType(
      Icons.sanitizer_rounded,
      'Unhygienic Conditions',
      'Unhygienic shop',
      'Dirty storage, pests, or food kept unsafely',
      AppColors.secondary,
      AppColors.secondaryTint,
    ),
    _ReportType(
      Icons.chat_bubble_rounded,
      'Other',
      'Something else',
      'Any other concern about a shop or product',
      AppColors.ink2,
      AppColors.divider,
    ),
  ];

  @override
  void initState() {
    super.initState();
    _selectedType = widget.initialType;
    _productController.text = widget.productName ?? '';
    _descriptionController.text = widget.initialDescription ?? '';
    _photoFile = widget.initialPhoto;
    DeviceContext.location().then((fix) {
      if (mounted) setState(() => _fix = fix);
    });
  }

  Future<void> _useCurrentLocation() async {
    setState(() => _locating = true);
    try {
      final fix = _fix ?? await DeviceContext.location();
      if (fix == null) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Turn on location and allow access to use this.'),
            ),
          );
        }
        return;
      }
      final place = await AddressService.reverse(fix.latitude, fix.longitude);
      if (!mounted) return;
      setState(() {
        _fix = fix;
        if (place != null) {
          _locationController.text = place.label;
          _shopPlace = place;
        }
      });
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not find your address.')),
        );
      }
    } finally {
      if (mounted) setState(() => _locating = false);
    }
  }

  @override
  void dispose() {
    _shopNameController.dispose();
    _locationController.dispose();
    _productController.dispose();
    _descriptionController.dispose();
    _serialController.dispose();
    _priceController.dispose();
    super.dispose();
  }

  Future<void> _nextStep() async {
    if (_step == 2) {
      await _submitReport();
    } else {
      setState(() => _step++);
    }
  }

  Future<void> _pickPhoto() async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.image,
      withData:
          true, // portable bytes across mobile/web/desktop, no dart:io needed
    );
    if (result != null && result.files.isNotEmpty) {
      setState(() => _photoFile = result.files.single);
    }
  }

  Future<void> _submitReport() async {
    setState(() {
      _submitting = true;
      _submitError = null;
    });
    try {
      String? photoUrl;
      if (_photoFile?.bytes != null) {
        final res = await ApiService.uploadFile(
          '/customer/reports/photo',
          fieldName: 'file',
          fileBytes: _photoFile!.bytes!,
          fileName: _photoFile!.name,
        );
        photoUrl = (res['data'] as Map?)?['url'] as String?;
      }

      final deviceId = await DeviceContext.deviceId();
      final fix = _fix ?? await DeviceContext.location();
      final price = double.tryParse(
        _priceController.text.trim().replaceAll(',', '.'),
      );

      final res = await ApiService.post('/customer/reports', {
        'reportType': _selectedType,
        'shopName': _shopNameController.text.trim(),
        'purchaseLocation': _locationController.text.trim().isEmpty
            ? null
            : _locationController.text.trim(),
        'shopAddress': _locationController.text.trim().isEmpty
            ? null
            : _locationController.text.trim(),
        if (_serialController.text.trim().isNotEmpty)
          'serialCode': _serialController.text.trim(),
        if (price != null) 'purchasePrice': price,
        if (_scratchIntact != null) 'scratchPanelIntact': _scratchIntact,
        if (_sealTampered != null) 'sealTampered': _sealTampered,
        'deviceId': deviceId,
        // Where the shop is: the picked/pinned address, else where the phone is
        if (_shopPlace != null) ...{
          'latitude': _shopPlace!.latitude,
          'longitude': _shopPlace!.longitude,
        } else if (fix != null) ...{
          'latitude': fix.latitude,
          'longitude': fix.longitude,
        },
        'description': _descriptionController.text.trim().isEmpty
            ? '${_productController.text.trim().isNotEmpty ? "Product: ${_productController.text.trim()}. " : ""}$_selectedType'
            : _descriptionController.text.trim(),
        'isAnonymous': _isAnonymous ?? false,
        'photoUrl': photoUrl,
        if (widget.barcode != null) 'barcode': widget.barcode,
        if (widget.productId != null) 'productId': widget.productId,
        if (widget.batchNumber != null) 'batchNumber': widget.batchNumber,
        if (widget.expiryDate != null) 'expiryDate': widget.expiryDate,
      });

      final reportId = (res['data'] as Map?)?['id'] as String?;
      final shortId = reportId != null && reportId.length >= 8
          ? 'RPT-${reportId.substring(0, 8).toUpperCase()}'
          : 'RPT-SUBMITTED';

      setState(() {
        _complaintId = shortId;
        _step = 3;
      });
    } on ApiException catch (e) {
      setState(() => _submitError = e.message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  bool get _canProceed {
    switch (_step) {
      case 0:
        return _selectedType != null;
      case 1:
        return _shopNameController.text.trim().isNotEmpty &&
            _descriptionController.text.trim().isNotEmpty &&
            (!_needsSerial || _serialController.text.trim().length >= 4);
      case 2:
        return _isAnonymous != null;
      default:
        return true;
    }
  }

  /// Fake and expired goods are about one specific item, so ask the product questions
  bool get _isProductIssue =>
      _selectedType == 'Fake / Counterfeit Product' ||
      _selectedType == 'Expired Goods';

  _ReportType? get _selectedReportType =>
      _reportTypes.where((t) => t.label == _selectedType).firstOrNull;

  @override
  Widget build(BuildContext context) {
    return PopScope(
      // Android back goes to the previous step instead of leaving the report
      canPop: _step == 0 || _step == 3,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop && _step > 0 && _step < 3) setState(() => _step--);
      },
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          backgroundColor: AppColors.surface,
          surfaceTintColor: AppColors.surface,
          elevation: 0,
          leading: _step == 3
              ? null
              : IconButton(
                  tooltip: 'Back',
                  icon: const Icon(Icons.arrow_back_rounded),
                  onPressed: () {
                    if (_step > 0) {
                      setState(() => _step--);
                    } else {
                      Navigator.pop(context);
                    }
                  },
                ),
          automaticallyImplyLeading: false,
          centerTitle: true,
          title: Text(
            _step == 3 ? 'Report submitted' : 'Report a concern',
            style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w800),
          ),
          bottom: _step < 3
              ? PreferredSize(
                  preferredSize: const Size.fromHeight(64),
                  child: _buildStepIndicator(),
                )
              : null,
        ),
        body: SafeArea(
          top: false,
          child: Column(
            children: [
              Expanded(
                child: _step == 3
                    ? _buildSuccess()
                    : SingleChildScrollView(
                        padding: const EdgeInsets.fromLTRB(16, 20, 16, 24),
                        child: _buildCurrentStep(),
                      ),
              ),
              if (_step < 3) _buildBottomBar(),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildStepIndicator() {
    const labels = ['Issue', 'Details', 'Privacy'];
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 14),
      child: Row(
        children: List.generate(3, (i) {
          final done = i < _step;
          final active = i == _step;
          return Expanded(
            child: Padding(
              padding: EdgeInsets.only(right: i < 2 ? 8 : 0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  AnimatedContainer(
                    duration: 250.ms,
                    height: 5,
                    decoration: BoxDecoration(
                      color: done || active
                          ? AppColors.primary
                          : AppColors.primaryTint,
                      borderRadius: BorderRadius.circular(3),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      if (done)
                        const Padding(
                          padding: EdgeInsets.only(right: 4),
                          child: Icon(
                            Icons.check_circle_rounded,
                            size: 14,
                            color: AppColors.primary,
                          ),
                        ),
                      Text(
                        '${i + 1}. ${labels[i]}',
                        style: AppTextStyles.caption.copyWith(
                          fontWeight: active
                              ? FontWeight.w800
                              : FontWeight.w600,
                          color: done || active
                              ? AppColors.textPrimary
                              : AppColors.textHint,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          );
        }),
      ),
    );
  }

  Widget _buildCurrentStep() {
    switch (_step) {
      case 0:
        return _buildStep1();
      case 1:
        return _buildStep2();
      case 2:
        return _buildStep3();
      default:
        return const SizedBox.shrink();
    }
  }

  Widget _stepTitle(String title, String subtitle) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 16, left: 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: AppTextStyles.h2),
          const SizedBox(height: 4),
          Text(
            subtitle,
            style: AppTextStyles.bodySmall.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }

  // Step 1: what is the issue
  Widget _buildStep1() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _stepTitle(
          'What would you like to report?',
          'Choose the option that best describes the problem.',
        ),
        if (widget.productName != null && widget.productName!.isNotEmpty)
          Container(
            margin: const EdgeInsets.only(bottom: 14),
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.primaryTint,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Row(
              children: [
                const Icon(
                  Icons.qr_code_2_rounded,
                  color: AppColors.primary,
                  size: 20,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    'About: ${widget.productName}',
                    style: AppTextStyles.bodySmall.copyWith(
                      fontWeight: FontWeight.w700,
                      color: AppColors.primary,
                    ),
                  ),
                ),
              ],
            ),
          ),
        for (var i = 0; i < _reportTypes.length; i++)
          _buildTypeOption(_reportTypes[i])
              .animate()
              .fadeIn(delay: (40 * i).ms, duration: 250.ms)
              .slideY(begin: 0.08, end: 0, delay: (40 * i).ms),
      ],
    );
  }

  Widget _buildTypeOption(_ReportType type) {
    final selected = _selectedType == type.label;
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Material(
        color: selected ? type.tint : AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        child: InkWell(
          borderRadius: BorderRadius.circular(18),
          onTap: () => setState(() => _selectedType = type.label),
          child: AnimatedContainer(
            duration: 200.ms,
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(18),
              border: Border.all(
                color: selected ? type.color : AppColors.divider,
                width: selected ? 2 : 1,
              ),
            ),
            child: Row(
              children: [
                Container(
                  width: 48,
                  height: 48,
                  decoration: BoxDecoration(
                    color: selected ? type.color : type.tint,
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: Icon(
                    type.icon,
                    color: selected ? Colors.white : type.color,
                    size: 26,
                  ),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        type.title,
                        style: AppTextStyles.body.copyWith(
                          fontWeight: FontWeight.w800,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        type.description,
                        style: AppTextStyles.caption.copyWith(
                          color: AppColors.textSecondary,
                          height: 1.3,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                AnimatedSwitcher(
                  duration: 200.ms,
                  child: Icon(
                    selected
                        ? Icons.check_circle_rounded
                        : Icons.radio_button_unchecked_rounded,
                    key: ValueKey(selected),
                    color: selected ? type.color : AppColors.divider,
                    size: 24,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  // Step 2: details
  Widget _buildStep2() {
    final type = _selectedReportType;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _stepTitle(
          'Tell us what happened',
          'The more detail you give, the faster we can act.',
        ),
        if (type != null) _selectedTypeChip(type),
        _section(
          icon: Icons.storefront_rounded,
          title: 'Where',
          children: [
            _field(
              _shopNameController,
              'Shop name',
              'e.g. Mama Thandi\'s Spaza',
              Icons.store_outlined,
              required: true,
              onChanged: () => setState(() {}),
            ),
            const SizedBox(height: 12),
            AddressAutocompleteField(
              controller: _locationController,
              label: 'Shop address',
              hint: 'Start typing the street or area',
              confirmed: _shopPlace != null,
              onSelected: (place) => setState(() => _shopPlace = place),
              onEdited: () {
                if (_shopPlace != null) setState(() => _shopPlace = null);
              },
            ),
            const SizedBox(height: 4),
            TextButton.icon(
              onPressed: _locating ? null : _useCurrentLocation,
              icon: _locating
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.my_location_rounded, size: 18),
              label: Text(
                _locating ? 'Finding you...' : 'Use my current location',
              ),
              style: TextButton.styleFrom(
                foregroundColor: AppColors.primary,
                padding: const EdgeInsets.symmetric(horizontal: 4),
              ),
            ),
          ],
        ),
        _section(
          icon: Icons.inventory_2_rounded,
          title: 'What',
          children: [
            _field(
              _productController,
              'Product',
              'Product involved (if any)',
              Icons.shopping_bag_outlined,
            ),
            if (_isProductIssue) ...[
              const SizedBox(height: 12),
              _field(
                _serialController,
                'Serial or PIN code',
                'Code on the pack or scratch panel',
                Icons.pin_outlined,
                required: _needsSerial,
                helper: _needsSerial
                    ? 'Needed so we can identify the exact item (at least 4 characters).'
                    : null,
                onChanged: () => setState(() {}),
              ),
            ],
            if (_isProductIssue || _selectedType == 'Misleading Pricing') ...[
              const SizedBox(height: 12),
              _field(
                _priceController,
                'Price you paid (R)',
                'e.g. 24.99',
                Icons.payments_outlined,
                prefixText: 'R ',
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
              ),
            ],
            if (_isProductIssue) ...[
              const SizedBox(height: 16),
              _yesNo(
                'Was the scratch panel intact when you bought it?',
                _scratchIntact,
                (v) => setState(() => _scratchIntact = v),
              ),
              const SizedBox(height: 12),
              _yesNo(
                'Does the seal look tampered with?',
                _sealTampered,
                (v) => setState(() => _sealTampered = v),
              ),
            ],
            const SizedBox(height: 16),
            _field(
              _descriptionController,
              'What happened?',
              'Describe the problem in your own words...',
              Icons.notes_rounded,
              required: true,
              maxLines: 4,
              onChanged: () => setState(() {}),
            ),
          ],
        ),
        _section(
          icon: Icons.photo_camera_rounded,
          title: 'Evidence',
          trailing: Text(
            'Optional',
            style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
          ),
          children: [_photoPicker()],
        ),
      ],
    );
  }

  Widget _selectedTypeChip(_ReportType type) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.fromLTRB(8, 6, 12, 6),
            decoration: BoxDecoration(
              color: type.tint,
              borderRadius: BorderRadius.circular(20),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(type.icon, size: 16, color: type.color),
                const SizedBox(width: 6),
                Text(
                  type.title,
                  style: AppTextStyles.caption.copyWith(
                    color: type.color,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          TextButton(
            onPressed: () => setState(() => _step = 0),
            child: const Text('Change'),
          ),
        ],
      ),
    );
  }

  Widget _section({
    required IconData icon,
    required String title,
    required List<Widget> children,
    Widget? trailing,
  }) {
    return Container(
      margin: const EdgeInsets.only(bottom: 14),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.divider),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 32,
                height: 32,
                decoration: BoxDecoration(
                  color: AppColors.primaryTint,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(icon, size: 18, color: AppColors.primary),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  title,
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              ?trailing,
            ],
          ),
          const SizedBox(height: 14),
          ...children,
        ],
      ),
    );
  }

  Widget _photoPicker() {
    final photo = _photoFile;
    if (photo == null) {
      return InkWell(
        onTap: _pickPhoto,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(vertical: 22, horizontal: 16),
          decoration: BoxDecoration(
            color: AppColors.background,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: AppColors.primary.withValues(alpha: 0.25),
            ),
          ),
          child: Column(
            children: [
              Container(
                width: 52,
                height: 52,
                decoration: const BoxDecoration(
                  color: AppColors.primaryTint,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.add_a_photo_rounded,
                  color: AppColors.primary,
                  size: 24,
                ),
              ),
              const SizedBox(height: 10),
              Text(
                'Add a photo',
                style: AppTextStyles.body.copyWith(
                  fontWeight: FontWeight.w800,
                  color: AppColors.primary,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                'The product, your receipt or the shop front',
                style: AppTextStyles.caption.copyWith(
                  color: AppColors.textSecondary,
                ),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      );
    }
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: AppColors.secondaryTint,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: photo.bytes != null
                ? Image.memory(
                    photo.bytes!,
                    width: 64,
                    height: 64,
                    fit: BoxFit.cover,
                  )
                : Container(
                    width: 64,
                    height: 64,
                    color: AppColors.surface,
                    child: const Icon(
                      Icons.image_outlined,
                      color: AppColors.textHint,
                    ),
                  ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Icon(
                      Icons.check_circle_rounded,
                      size: 16,
                      color: AppColors.success,
                    ),
                    const SizedBox(width: 6),
                    Text(
                      'Photo attached',
                      style: AppTextStyles.bodySmall.copyWith(
                        fontWeight: FontWeight.w800,
                        color: AppColors.success,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 2),
                Text(
                  photo.name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                TextButton(
                  onPressed: _pickPhoto,
                  style: TextButton.styleFrom(
                    padding: EdgeInsets.zero,
                    minimumSize: const Size(0, 28),
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                  child: const Text('Replace'),
                ),
              ],
            ),
          ),
          IconButton(
            tooltip: 'Remove photo',
            icon: const Icon(
              Icons.close_rounded,
              color: AppColors.textSecondary,
            ),
            onPressed: () => setState(() => _photoFile = null),
          ),
        ],
      ),
    );
  }

  // Step 3: privacy
  Widget _buildStep3() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _stepTitle(
          'How should we handle your identity?',
          'Your report is taken just as seriously either way.',
        ),
        _buildAnonOption(
          true,
          Icons.visibility_off_rounded,
          'Stay anonymous',
          'The shop and supplier never see who reported. Recommended.',
          AppColors.success,
          AppColors.secondaryTint,
        ),
        const SizedBox(height: 10),
        _buildAnonOption(
          false,
          Icons.contact_phone_rounded,
          'Share my contact details',
          'SpazaSure or health inspectors may contact you to follow up.',
          AppColors.primary,
          AppColors.primaryTint,
        ),
        const SizedBox(height: 16),
        Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.accentTint,
            borderRadius: BorderRadius.circular(16),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Icon(
                Icons.schedule_rounded,
                color: AppColors.accentDark,
                size: 20,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Our compliance team reviews every report within 48 hours. '
                  'Serious health risks can be passed on to the authorities.',
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textPrimary,
                    height: 1.4,
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildAnonOption(
    bool value,
    IconData icon,
    String title,
    String subtitle,
    Color color,
    Color tint,
  ) {
    final selected = _isAnonymous == value;
    return Material(
      color: selected ? tint : AppColors.surface,
      borderRadius: BorderRadius.circular(18),
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: () => setState(() => _isAnonymous = value),
        child: AnimatedContainer(
          duration: 200.ms,
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(18),
            border: Border.all(
              color: selected ? color : AppColors.divider,
              width: selected ? 2 : 1,
            ),
          ),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: selected ? color : tint,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Icon(
                  icon,
                  color: selected ? Colors.white : color,
                  size: 24,
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: AppTextStyles.body.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      subtitle,
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.textSecondary,
                        height: 1.3,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Icon(
                selected
                    ? Icons.check_circle_rounded
                    : Icons.radio_button_unchecked_rounded,
                color: selected ? color : AppColors.divider,
                size: 24,
              ),
            ],
          ),
        ),
      ),
    );
  }

  // Success
  Widget _buildSuccess() {
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(24, 40, 24, 24),
      child: Column(
        children: [
          Container(
                width: 112,
                height: 112,
                decoration: const BoxDecoration(
                  color: AppColors.secondaryTint,
                  shape: BoxShape.circle,
                ),
                child: Center(
                  child: Container(
                    width: 76,
                    height: 76,
                    decoration: const BoxDecoration(
                      color: AppColors.success,
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.check_rounded,
                      color: Colors.white,
                      size: 44,
                    ),
                  ),
                ),
              )
              .animate()
              .scale(
                begin: const Offset(0.4, 0.4),
                duration: 500.ms,
                curve: Curves.easeOutBack,
              )
              .fadeIn(),
          const SizedBox(height: 24),
          Text(
            'Thank you!',
            style: AppTextStyles.h1,
            textAlign: TextAlign.center,
          ).animate().fadeIn(delay: 200.ms),
          const SizedBox(height: 8),
          Text(
            'Your report helps keep shoppers in your community safe. '
            'Our compliance team will review it within 48 hours.',
            style: AppTextStyles.body.copyWith(
              color: AppColors.textSecondary,
              height: 1.4,
            ),
            textAlign: TextAlign.center,
          ).animate().fadeIn(delay: 300.ms),
          const SizedBox(height: 24),
          Container(
                width: double.infinity,
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(color: AppColors.divider),
                ),
                child: Column(
                  children: [
                    Text(
                      'Your reference number',
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      _complaintId ?? '',
                      style: AppTextStyles.h2.copyWith(
                        color: AppColors.primary,
                        letterSpacing: 1.5,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Quote it if you contact SpazaSure about this report',
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.textHint,
                      ),
                      textAlign: TextAlign.center,
                    ),
                  ],
                ),
              )
              .animate()
              .fadeIn(delay: 400.ms)
              .slideY(begin: 0.1, end: 0, delay: 400.ms),
          const SizedBox(height: 28),
          SizedBox(
            width: double.infinity,
            height: 52,
            child: ElevatedButton(
              onPressed: () async {
                final home = await AuthService.homeRoute();
                if (!mounted) return;
                Navigator.pushNamedAndRemoveUntil(context, home, (r) => false);
              },
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(16),
                ),
              ),
              child: Text('Back to home', style: AppTextStyles.button),
            ),
          ).animate().fadeIn(delay: 500.ms),
        ],
      ),
    );
  }

  Widget _buildBottomBar() {
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        boxShadow: [
          BoxShadow(
            color: AppColors.primary.withValues(alpha: 0.06),
            blurRadius: 16,
            offset: const Offset(0, -4),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (_submitError != null)
            Container(
              width: double.infinity,
              margin: const EdgeInsets.only(bottom: 10),
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: AppColors.redTint,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Row(
                children: [
                  const Icon(
                    Icons.error_outline_rounded,
                    color: AppColors.error,
                    size: 18,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      _submitError!,
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.error,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          SizedBox(
            width: double.infinity,
            height: 52,
            child: ElevatedButton.icon(
              onPressed: (_canProceed && !_submitting) ? _nextStep : null,
              style: ElevatedButton.styleFrom(
                backgroundColor: _step == 2
                    ? AppColors.error
                    : AppColors.primary,
                disabledBackgroundColor: AppColors.primaryTint,
                disabledForegroundColor: AppColors.textHint,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(16),
                ),
              ),
              icon: _submitting
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(
                        color: Colors.white,
                        strokeWidth: 2.5,
                      ),
                    )
                  : Icon(
                      _step == 2
                          ? Icons.send_rounded
                          : Icons.arrow_forward_rounded,
                      size: 20,
                    ),
              label: Text(
                _submitting
                    ? 'Sending...'
                    : _step == 2
                    ? 'Submit report'
                    : 'Continue',
                style: AppTextStyles.button,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _yesNo(String label, bool? value, ValueChanged<bool> onChanged) {
    Widget option(String text, IconData icon, bool v) {
      final selected = value == v;
      return Expanded(
        child: Material(
          color: selected ? AppColors.primary : AppColors.background,
          borderRadius: BorderRadius.circular(12),
          child: InkWell(
            borderRadius: BorderRadius.circular(12),
            onTap: () => onChanged(v),
            child: Container(
              padding: const EdgeInsets.symmetric(vertical: 11),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(12),
                border: Border.all(
                  color: selected ? AppColors.primary : AppColors.divider,
                ),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    icon,
                    size: 18,
                    color: selected ? Colors.white : AppColors.textSecondary,
                  ),
                  const SizedBox(width: 6),
                  Text(
                    text,
                    style: AppTextStyles.bodySmall.copyWith(
                      fontWeight: FontWeight.w800,
                      color: selected ? Colors.white : AppColors.textPrimary,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: AppTextStyles.bodySmall.copyWith(
            fontWeight: FontWeight.w700,
            color: AppColors.textPrimary,
          ),
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            option('Yes', Icons.check_rounded, true),
            const SizedBox(width: 10),
            option('No', Icons.close_rounded, false),
          ],
        ),
      ],
    );
  }

  Widget _field(
    TextEditingController controller,
    String label,
    String hint,
    IconData icon, {
    bool required = false,
    int maxLines = 1,
    String? helper,
    String? prefixText,
    TextInputType? keyboardType,
    VoidCallback? onChanged,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text.rich(
          TextSpan(
            text: label,
            children: [
              if (required)
                const TextSpan(
                  text: ' *',
                  style: TextStyle(color: AppColors.error),
                ),
            ],
          ),
          style: AppTextStyles.bodySmall.copyWith(
            fontWeight: FontWeight.w700,
            color: AppColors.textPrimary,
          ),
        ),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          maxLines: maxLines,
          keyboardType: keyboardType,
          textCapitalization: maxLines > 1
              ? TextCapitalization.sentences
              : TextCapitalization.words,
          onChanged: onChanged == null ? null : (_) => onChanged(),
          style: AppTextStyles.body.copyWith(color: AppColors.textPrimary),
          decoration: InputDecoration(
            hintText: hint,
            helperText: helper,
            helperMaxLines: 2,
            prefixText: prefixText,
            filled: true,
            fillColor: AppColors.background,
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 14,
              vertical: 14,
            ),
            prefixIcon: maxLines == 1
                ? Icon(icon, color: AppColors.textHint, size: 20)
                : null,
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: const BorderSide(color: AppColors.divider),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: const BorderSide(color: AppColors.divider),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(14),
              borderSide: const BorderSide(
                color: AppColors.primary,
                width: 1.6,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _ReportType {
  final IconData icon;

  /// Value sent to the backend; keep these exact strings
  final String label;
  final String title;
  final String description;
  final Color color;
  final Color tint;
  const _ReportType(
    this.icon,
    this.label,
    this.title,
    this.description,
    this.color,
    this.tint,
  );
}
