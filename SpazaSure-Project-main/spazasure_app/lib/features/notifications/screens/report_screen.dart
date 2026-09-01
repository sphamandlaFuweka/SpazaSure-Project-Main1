import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';

class ReportScreen extends StatefulWidget {
  const ReportScreen({super.key});

  @override
  State<ReportScreen> createState() => _ReportScreenState();
}

class _ReportScreenState extends State<ReportScreen> {
  int _step = 0;
  String? _selectedType;
  bool? _isAnonymous;
  String? _complaintId;

  final _shopNameController = TextEditingController();
  final _locationController = TextEditingController();
  final _productController = TextEditingController();
  final _descriptionController = TextEditingController();

  static const _reportTypes = [
    _ReportType(Icons.dangerous_rounded, 'Fake / Counterfeit Product',
        AppColors.error),
    _ReportType(Icons.schedule_outlined, 'Expired Goods', AppColors.warning),
    _ReportType(Icons.price_change_outlined, 'Misleading Pricing',
        AppColors.secondary),
    _ReportType(Icons.cleaning_services_outlined, 'Unhygienic Conditions',
        AppColors.info),
    _ReportType(Icons.more_horiz_rounded, 'Other', AppColors.textSecondary),
  ];

  @override
  void dispose() {
    _shopNameController.dispose();
    _locationController.dispose();
    _productController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  void _nextStep() {
    if (_step == 2) {
      // Generate complaint ID and go to success
      final rand = Random();
      final id =
          'RPT-2025-${(1000 + rand.nextInt(8999)).toString().padLeft(4, '0')}';
      setState(() {
        _complaintId = id;
        _step = 3;
      });
    } else {
      setState(() => _step++);
    }
  }

  bool get _canProceed {
    switch (_step) {
      case 0:
        return _selectedType != null;
      case 1:
        return _shopNameController.text.trim().isNotEmpty &&
            _descriptionController.text.trim().isNotEmpty;
      case 2:
        return _isAnonymous != null;
      default:
        return true;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [Color(0xFF0D3B0F), Color(0xFF1B5E20), Color(0xFF2E7D32)],
          ),
        ),
        child: SafeArea(
          child: Column(
            children: [
              _buildHeader(),
              if (_step < 3) _buildStepIndicator(),
              Expanded(
                child: _step == 3
                    ? _buildSuccess()
                    : SingleChildScrollView(
                        padding: const EdgeInsets.all(20),
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

  Widget _buildHeader() {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
      child: Row(
        children: [
          GestureDetector(
            onTap: () {
              if (_step > 0 && _step < 3) {
                setState(() => _step--);
              } else {
                Navigator.pop(context);
              }
            },
            child: Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(14),
                border:
                    Border.all(color: Colors.white.withValues(alpha: 0.15)),
              ),
              child: const Icon(Icons.arrow_back_rounded,
                  color: Colors.white, size: 22),
            ),
          ),
          const Spacer(),
          Text(
            _step == 3 ? 'Report Submitted' : 'Report an Issue',
            style: GoogleFonts.poppins(
                fontSize: 18,
                fontWeight: FontWeight.w600,
                color: Colors.white),
          ),
          const Spacer(),
          const SizedBox(width: 44),
        ],
      ),
    );
  }

  Widget _buildStepIndicator() {
    final labels = ['Type', 'Details', 'Privacy'];
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 8),
      child: Row(
        children: List.generate(3, (i) {
          final isActive = i <= _step;
          final isCompleted = i < _step;
          return Expanded(
            child: Row(
              children: [
                if (i > 0)
                  Expanded(
                    child: Container(
                      height: 2,
                      color: isActive
                          ? AppColors.primaryLight
                          : Colors.white.withValues(alpha: 0.15),
                    ),
                  ),
                Column(
                  children: [
                    AnimatedContainer(
                      duration: 300.ms,
                      width: 32,
                      height: 32,
                      decoration: BoxDecoration(
                        color: isActive
                            ? AppColors.primaryLight
                            : Colors.white.withValues(alpha: 0.1),
                        shape: BoxShape.circle,
                      ),
                      child: Center(
                        child: isCompleted
                            ? const Icon(Icons.check_rounded,
                                color: Colors.white, size: 16)
                            : Text('${i + 1}',
                                style: GoogleFonts.poppins(
                                    color: Colors.white,
                                    fontWeight: FontWeight.w600,
                                    fontSize: 13)),
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(labels[i],
                        style: GoogleFonts.poppins(
                            fontSize: 10,
                            color: isActive
                                ? Colors.white
                                : Colors.white.withValues(alpha: 0.4))),
                  ],
                ),
                if (i < 2)
                  Expanded(
                    child: Container(
                      height: 2,
                      color: i < _step
                          ? AppColors.primaryLight
                          : Colors.white.withValues(alpha: 0.15),
                    ),
                  ),
              ],
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

  // Step 1: Select report type
  Widget _buildStep1() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _glassCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  _iconBox(Icons.report_problem_outlined,
                      AppColors.secondary),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('What are you reporting?',
                            style: GoogleFonts.poppins(
                                fontSize: 18,
                                fontWeight: FontWeight.w700,
                                color: Colors.white)),
                        Text('Select the issue type',
                            style: GoogleFonts.poppins(
                                fontSize: 13,
                                color:
                                    Colors.white.withValues(alpha: 0.6))),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 24),
              ..._reportTypes.map((t) => _buildTypeOption(t)),
            ],
          ),
        ),
      ],
    ).animate().fadeIn(duration: 400.ms).slideX(begin: 0.05, end: 0);
  }

  Widget _buildTypeOption(_ReportType type) {
    final isSelected = _selectedType == type.label;
    return GestureDetector(
      onTap: () => setState(() => _selectedType = type.label),
      child: AnimatedContainer(
        duration: 200.ms,
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: isSelected
              ? type.color.withValues(alpha: 0.2)
              : Colors.white.withValues(alpha: 0.06),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: isSelected
                ? type.color.withValues(alpha: 0.6)
                : Colors.white.withValues(alpha: 0.1),
            width: isSelected ? 1.5 : 1,
          ),
        ),
        child: Row(
          children: [
            Icon(type.icon, color: type.color, size: 22),
            const SizedBox(width: 14),
            Expanded(
              child: Text(type.label,
                  style: GoogleFonts.poppins(
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                      color: Colors.white)),
            ),
            if (isSelected)
              Icon(Icons.check_circle_rounded, color: type.color, size: 20),
          ],
        ),
      ),
    );
  }

  // Step 2: Capture details
  Widget _buildStep2() {
    return Column(
      children: [
        _glassCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  _iconBox(Icons.edit_note_rounded, AppColors.info),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Report Details',
                            style: GoogleFonts.poppins(
                                fontSize: 18,
                                fontWeight: FontWeight.w700,
                                color: Colors.white)),
                        Text('Provide as much detail as possible',
                            style: GoogleFonts.poppins(
                                fontSize: 13,
                                color:
                                    Colors.white.withValues(alpha: 0.6))),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 24),
              _glassField(_shopNameController, 'Shop Name *',
                  'Enter shop name', Icons.store_outlined),
              const SizedBox(height: 16),
              _glassField(_locationController, 'Location',
                  'Street address or area', Icons.location_on_outlined),
              const SizedBox(height: 16),
              _glassField(_productController, 'Product Name',
                  'Product involved (if any)', Icons.inventory_2_outlined),
              const SizedBox(height: 16),
              _glassField(_descriptionController, 'Description *',
                  'Describe the issue in detail...', Icons.description_outlined,
                  maxLines: 4),
              const SizedBox(height: 16),
              // Photo placeholder
              GestureDetector(
                onTap: () {},
                child: Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.06),
                    borderRadius: BorderRadius.circular(14),
                    border: Border.all(
                        color: Colors.white.withValues(alpha: 0.1)),
                  ),
                  child: Row(
                    children: [
                      Container(
                        width: 44,
                        height: 44,
                        decoration: BoxDecoration(
                          color: AppColors.info.withValues(alpha: 0.2),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: const Icon(Icons.add_a_photo_outlined,
                            color: AppColors.info, size: 22),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('Add Photo (Optional)',
                                style: GoogleFonts.poppins(
                                    fontSize: 14,
                                    fontWeight: FontWeight.w500,
                                    color: Colors.white)),
                            Text('Tap to attach evidence',
                                style: GoogleFonts.poppins(
                                    fontSize: 11,
                                    color: Colors.white
                                        .withValues(alpha: 0.5))),
                          ],
                        ),
                      ),
                      Icon(Icons.chevron_right_rounded,
                          color: Colors.white.withValues(alpha: 0.4)),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    ).animate().fadeIn(duration: 400.ms).slideX(begin: 0.05, end: 0);
  }

  // Step 3: Anonymous option
  Widget _buildStep3() {
    return Column(
      children: [
        _glassCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  _iconBox(Icons.privacy_tip_outlined, AppColors.warning),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Privacy Settings',
                            style: GoogleFonts.poppins(
                                fontSize: 18,
                                fontWeight: FontWeight.w700,
                                color: Colors.white)),
                        Text('How should we handle your identity?',
                            style: GoogleFonts.poppins(
                                fontSize: 13,
                                color:
                                    Colors.white.withValues(alpha: 0.6))),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 24),
              _buildAnonOption(
                true,
                Icons.visibility_off_outlined,
                'Submit Anonymously',
                'Your identity will not be shared with the reported party.',
                AppColors.success,
              ),
              const SizedBox(height: 12),
              _buildAnonOption(
                false,
                Icons.person_outlined,
                'Include My Details',
                'Your contact info may be used for follow-up by authorities.',
                AppColors.info,
              ),
              const SizedBox(height: 20),
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: AppColors.warning.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                      color: AppColors.warning.withValues(alpha: 0.3)),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.info_outline,
                        color: AppColors.warning, size: 18),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        'All reports are reviewed by our compliance team within 48 hours.',
                        style: GoogleFonts.poppins(
                            fontSize: 12,
                            color: Colors.white.withValues(alpha: 0.8)),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
    ).animate().fadeIn(duration: 400.ms).slideX(begin: 0.05, end: 0);
  }

  Widget _buildAnonOption(bool value, IconData icon, String title,
      String subtitle, Color color) {
    final isSelected = _isAnonymous == value;
    return GestureDetector(
      onTap: () => setState(() => _isAnonymous = value),
      child: AnimatedContainer(
        duration: 200.ms,
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: isSelected
              ? color.withValues(alpha: 0.15)
              : Colors.white.withValues(alpha: 0.06),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: isSelected
                ? color.withValues(alpha: 0.5)
                : Colors.white.withValues(alpha: 0.1),
            width: isSelected ? 1.5 : 1,
          ),
        ),
        child: Row(
          children: [
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.2),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(icon, color: color, size: 22),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title,
                      style: GoogleFonts.poppins(
                          fontSize: 14,
                          fontWeight: FontWeight.w600,
                          color: Colors.white)),
                  Text(subtitle,
                      style: GoogleFonts.poppins(
                          fontSize: 11,
                          color: Colors.white.withValues(alpha: 0.6))),
                ],
              ),
            ),
            if (isSelected)
              Icon(Icons.check_circle_rounded, color: color, size: 22),
          ],
        ),
      ),
    );
  }

  // Success screen
  Widget _buildSuccess() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              width: 100,
              height: 100,
              decoration: BoxDecoration(
                color: AppColors.success.withValues(alpha: 0.15),
                shape: BoxShape.circle,
                border: Border.all(
                    color: AppColors.success.withValues(alpha: 0.3),
                    width: 2),
              ),
              child: const Icon(Icons.check_circle_rounded,
                  color: AppColors.success, size: 56),
            )
                .animate()
                .scale(
                    begin: const Offset(0.3, 0.3),
                    duration: 600.ms,
                    curve: Curves.elasticOut)
                .fadeIn(),
            const SizedBox(height: 28),
            Text('Report Submitted!',
                style: GoogleFonts.poppins(
                    fontSize: 26,
                    fontWeight: FontWeight.w700,
                    color: Colors.white),
                textAlign: TextAlign.center)
                .animate()
                .fadeIn(delay: 300.ms)
                .slideY(begin: 0.3, end: 0, delay: 300.ms),
            const SizedBox(height: 12),
            Text(
              'Thank you for helping keep the community safe. Our team will review your report.',
              style: GoogleFonts.poppins(
                  fontSize: 14,
                  color: Colors.white.withValues(alpha: 0.7)),
              textAlign: TextAlign.center,
            ).animate().fadeIn(delay: 400.ms),
            const SizedBox(height: 28),
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(20),
                border: Border.all(
                    color: Colors.white.withValues(alpha: 0.15)),
              ),
              child: Column(
                children: [
                  Text('Complaint ID',
                      style: GoogleFonts.poppins(
                          fontSize: 12,
                          color: Colors.white.withValues(alpha: 0.6))),
                  const SizedBox(height: 6),
                  Text(_complaintId ?? '',
                      style: GoogleFonts.poppins(
                          fontSize: 22,
                          fontWeight: FontWeight.w800,
                          color: AppColors.primaryLight,
                          letterSpacing: 2)),
                  const SizedBox(height: 6),
                  Text('Save this for reference',
                      style: GoogleFonts.poppins(
                          fontSize: 11,
                          color: Colors.white.withValues(alpha: 0.5))),
                ],
              ),
            ).animate().fadeIn(delay: 500.ms).scale(
                begin: const Offset(0.9, 0.9), delay: 500.ms),
            const SizedBox(height: 32),
            SizedBox(
              width: double.infinity,
              height: 52,
              child: ElevatedButton(
                onPressed: () =>
                    Navigator.pushNamedAndRemoveUntil(
                        context, '/home', (r) => false),
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.primaryLight,
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16)),
                ),
                child: Text('Back to Home',
                    style: AppTextStyles.button),
              ),
            ).animate().fadeIn(delay: 600.ms),
          ],
        ),
      ),
    );
  }

  Widget _buildBottomBar() {
    return Container(
      padding: const EdgeInsets.fromLTRB(20, 12, 20, 20),
      child: SizedBox(
        width: double.infinity,
        height: 52,
        child: ElevatedButton(
          onPressed: _canProceed ? _nextStep : null,
          style: ElevatedButton.styleFrom(
            backgroundColor: AppColors.primaryLight,
            disabledBackgroundColor: Colors.white.withValues(alpha: 0.15),
            foregroundColor: Colors.white,
            shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(16)),
          ),
          child: Text(
            _step == 2 ? 'Submit Report' : 'Continue',
            style: AppTextStyles.button,
          ),
        ),
      ),
    );
  }

  Widget _glassCard({required Widget child}) {
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: Colors.white.withValues(alpha: 0.15)),
        boxShadow: [
          BoxShadow(
              color: Colors.black.withValues(alpha: 0.1),
              blurRadius: 20,
              offset: const Offset(0, 8))
        ],
      ),
      child: child,
    );
  }

  Widget _iconBox(IconData icon, Color color) {
    return Container(
      width: 50,
      height: 50,
      decoration: BoxDecoration(
        gradient: LinearGradient(
            colors: [color.withValues(alpha: 0.8), color]),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: Colors.white, size: 26),
    );
  }

  Widget _glassField(TextEditingController controller, String label,
      String hint, IconData icon,
      {int maxLines = 1}) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label,
            style: GoogleFonts.poppins(
                fontSize: 13,
                fontWeight: FontWeight.w500,
                color: Colors.white.withValues(alpha: 0.8))),
        const SizedBox(height: 8),
        Container(
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.08),
            borderRadius: BorderRadius.circular(14),
            border:
                Border.all(color: Colors.white.withValues(alpha: 0.12)),
          ),
          child: TextField(
            controller: controller,
            maxLines: maxLines,
            style: GoogleFonts.poppins(color: Colors.white, fontSize: 14),
            decoration: InputDecoration(
              hintText: hint,
              hintStyle: GoogleFonts.poppins(
                  color: Colors.white.withValues(alpha: 0.35)),
              border: InputBorder.none,
              contentPadding: const EdgeInsets.symmetric(
                  horizontal: 16, vertical: 14),
              prefixIcon: maxLines == 1
                  ? Padding(
                      padding: const EdgeInsets.only(left: 14),
                      child: Icon(icon,
                          color: Colors.white.withValues(alpha: 0.5),
                          size: 20),
                    )
                  : null,
            ),
          ),
        ),
      ],
    );
  }
}

class _ReportType {
  final IconData icon;
  final String label;
  final Color color;
  const _ReportType(this.icon, this.label, this.color);
}
