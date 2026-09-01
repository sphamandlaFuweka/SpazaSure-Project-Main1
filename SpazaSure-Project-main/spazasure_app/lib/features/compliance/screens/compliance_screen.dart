import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:percent_indicator/circular_percent_indicator.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/compliance_service.dart';

class ComplianceScreen extends StatefulWidget {
  const ComplianceScreen({super.key});

  @override
  State<ComplianceScreen> createState() => _ComplianceScreenState();
}

class _ComplianceScreenState extends State<ComplianceScreen> {
  static const _requiredDocs = {
    'business_permit': _DocMeta('Business Permit', Icons.description_outlined, 'Local Municipality', 'https://www.cogta.gov.za', true),
    'health_cert': _DocMeta('Health Certificate', Icons.health_and_safety_outlined, 'Dept. of Health', 'https://www.health.gov.za', true),
    'lease': _DocMeta('Lease Agreement', Icons.home_work_outlined, 'Download Template', 'https://www.justice.gov.za', false),
    'id_document': _DocMeta('ID Document', Icons.badge_outlined, 'Dept. of Home Affairs', 'https://www.dha.gov.za', true),
  };

  List<ComplianceDoc> _docs = [];
  bool _loading = true;
  bool _uploading = false;

  @override
  void initState() {
    super.initState();
    _loadDocs();
  }

  Future<void> _loadDocs() async {
    setState(() => _loading = true);
    try {
      final docs = await ComplianceService.getDocuments();
      if (mounted) setState(() { _docs = docs; _loading = false; });
    } catch (_) {
      if (mounted) setState(() => _loading = false);
    }
  }

  String _getDocStatus(String docType) {
    final doc = _docs.where((d) => d.docType == docType).firstOrNull;
    return doc?.status ?? 'missing';
  }

  ComplianceDoc? _getDoc(String docType) {
    return _docs.where((d) => d.docType == docType).firstOrNull;
  }

  @override
  Widget build(BuildContext context) {
    final approvedCount = _requiredDocs.keys.where((k) => _getDocStatus(k) == 'approved').length;
    final progress = _requiredDocs.isEmpty ? 0.0 : approvedCount / _requiredDocs.length;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Compliance'),
        backgroundColor: AppColors.surface,
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _loadDocs),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _loadDocs,
              child: SingleChildScrollView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(16),
                child: Column(
                  children: [
                    // Progress card
                    _buildProgressCard(progress, approvedCount),
                    const SizedBox(height: 16),

                    // Upload indicator
                    if (_uploading)
                      Container(
                        padding: const EdgeInsets.all(12),
                        margin: const EdgeInsets.only(bottom: 12),
                        decoration: BoxDecoration(
                          color: AppColors.info.withOpacity(0.1),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Row(
                          children: [
                            const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)),
                            const SizedBox(width: 12),
                            Text('Uploading document...', style: AppTextStyles.body.copyWith(color: AppColors.info)),
                          ],
                        ),
                      ),

                    // Document cards
                    ..._requiredDocs.entries.map((entry) {
                      final docType = entry.key;
                      final meta = entry.value;
                      final doc = _getDoc(docType);
                      return _buildDocCard(context, docType, meta, doc);
                    }),
                  ],
                ),
              ),
            ),
    );
  }

  Widget _buildProgressCard(double progress, int approvedCount) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.05), blurRadius: 12)],
      ),
      child: Row(
        children: [
          CircularPercentIndicator(
            radius: 40,
            lineWidth: 8,
            percent: progress,
            center: Text('${(progress * 100).toInt()}%', style: AppTextStyles.h3.copyWith(color: AppColors.primary)),
            progressColor: AppColors.primary,
            backgroundColor: AppColors.divider,
            circularStrokeCap: CircularStrokeCap.round,
          ),
          const SizedBox(width: 20),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Compliance Progress', style: AppTextStyles.subtitle),
                const SizedBox(height: 4),
                Text(
                  '$approvedCount of ${_requiredDocs.length} documents approved',
                  style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
                ),
                const SizedBox(height: 6),
                if (progress == 1.0)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(color: AppColors.success.withOpacity(0.1), borderRadius: BorderRadius.circular(20)),
                    child: Text('✓ Fully Compliant', style: AppTextStyles.caption.copyWith(color: AppColors.success, fontWeight: FontWeight.w700)),
                  )
                else
                  Text('Upload missing documents to get verified', style: AppTextStyles.caption.copyWith(color: AppColors.warning)),
              ],
            ),
          ),
        ],
      ),
    ).animate().fadeIn(duration: 400.ms);
  }

  Widget _buildDocCard(BuildContext context, String docType, _DocMeta meta, ComplianceDoc? doc) {
    final status = doc?.status ?? 'missing';
    final isApproved = status == 'approved';
    final isPending = status == 'pending';
    final isRejected = status == 'rejected';

    Color statusColor;
    String statusLabel;
    IconData statusIcon;
    if (isApproved) {
      statusColor = AppColors.success;
      statusLabel = 'Approved';
      statusIcon = Icons.check_circle;
    } else if (isPending) {
      statusColor = AppColors.warning;
      statusLabel = 'Under Review';
      statusIcon = Icons.hourglass_top;
    } else if (isRejected) {
      statusColor = AppColors.error;
      statusLabel = 'Rejected';
      statusIcon = Icons.cancel;
    } else {
      statusColor = const Color(0xFF9E9E9E);
      statusLabel = 'Not Uploaded';
      statusIcon = Icons.cloud_upload_outlined;
    }

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: statusColor.withOpacity(0.3)),
        boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.04), blurRadius: 8)],
      ),
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Container(
                  width: 48, height: 48,
                  decoration: BoxDecoration(color: statusColor.withOpacity(0.1), borderRadius: BorderRadius.circular(12)),
                  child: Icon(meta.icon, color: statusColor),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(child: Text(meta.title, style: AppTextStyles.subtitle)),
                          if (meta.required)
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                              decoration: BoxDecoration(color: AppColors.error.withOpacity(0.1), borderRadius: BorderRadius.circular(6)),
                              child: Text('Required', style: AppTextStyles.caption.copyWith(color: AppColors.error, fontWeight: FontWeight.w600, fontSize: 10)),
                            ),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Row(
                        children: [
                          Icon(statusIcon, size: 14, color: statusColor),
                          const SizedBox(width: 4),
                          Text(statusLabel, style: AppTextStyles.caption.copyWith(color: statusColor, fontWeight: FontWeight.w600)),
                        ],
                      ),
                      if (isRejected && doc?.rejectionNote != null)
                        Padding(
                          padding: const EdgeInsets.only(top: 4),
                          child: Text(doc!.rejectionNote!, style: AppTextStyles.caption.copyWith(color: AppColors.error)),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          // Action buttons
          if (!isApproved)
            Container(
              decoration: BoxDecoration(
                color: statusColor.withOpacity(0.05),
                borderRadius: const BorderRadius.vertical(bottom: Radius.circular(16)),
                border: Border(top: BorderSide(color: statusColor.withOpacity(0.15))),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: TextButton.icon(
                      onPressed: () => _showHowTo(context, meta),
                      icon: Icon(Icons.help_outline_rounded, size: 16, color: statusColor),
                      label: Text('How to get', style: AppTextStyles.caption.copyWith(color: statusColor, fontWeight: FontWeight.w600)),
                    ),
                  ),
                  Container(width: 1, height: 32, color: statusColor.withOpacity(0.15)),
                  Expanded(
                    child: TextButton.icon(
                      onPressed: _uploading ? null : () => _pickAndUpload(docType, meta.title),
                      icon: Icon(Icons.cloud_upload_outlined, size: 16, color: statusColor),
                      label: Text(
                        isPending ? 'Re-upload' : 'Upload',
                        style: AppTextStyles.caption.copyWith(color: statusColor, fontWeight: FontWeight.w600),
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    ).animate().fadeIn(delay: 100.ms).slideY(begin: 0.05, end: 0);
  }

  Future<void> _pickAndUpload(String docType, String docTitle) async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['pdf', 'doc', 'docx', 'jpg', 'jpeg', 'png', 'webp'],
      withData: true,
    );

    if (result == null || result.files.isEmpty) return;

    final file = result.files.first;
    if (file.bytes == null) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: const Text('Could not read file data'),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ));
      }
      return;
    }

    if (file.size > 10 * 1024 * 1024) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: const Text('File too large. Max 10MB.'),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ));
      }
      return;
    }

    setState(() => _uploading = true);

    try {
      await ComplianceService.uploadDocument(
        docType: docType,
        fileBytes: file.bytes!,
        fileName: file.name,
      );

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text('$docTitle uploaded successfully'),
          backgroundColor: AppColors.success,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ));
        _loadDocs(); // Refresh the list
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text('Upload failed: $e'),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ));
      }
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  void _showHowTo(BuildContext context, _DocMeta meta) {
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      builder: (ctx) => Container(
        decoration: const BoxDecoration(color: AppColors.surface, borderRadius: BorderRadius.vertical(top: Radius.circular(28))),
        padding: const EdgeInsets.all(28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(child: Container(width: 40, height: 4, decoration: BoxDecoration(color: AppColors.divider, borderRadius: BorderRadius.circular(2)))),
            const SizedBox(height: 20),
            Row(
              children: [
                Container(
                  width: 44, height: 44,
                  decoration: BoxDecoration(color: AppColors.info.withOpacity(0.1), borderRadius: BorderRadius.circular(12)),
                  child: Icon(meta.icon, color: AppColors.info),
                ),
                const SizedBox(width: 12),
                Expanded(child: Text('How to get: ${meta.title}', style: AppTextStyles.h3)),
              ],
            ),
            const SizedBox(height: 20),
            _howToStep('1', 'Visit your nearest ${meta.department} office'),
            _howToStep('2', 'Bring your ID document and proof of address'),
            _howToStep('3', 'Complete the application form'),
            _howToStep('4', 'Pay the applicable fee (if any)'),
            _howToStep('5', 'Upload the document here once received'),
            const SizedBox(height: 20),
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: AppColors.info.withOpacity(0.08),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: AppColors.info.withOpacity(0.2)),
              ),
              child: Row(
                children: [
                  const Icon(Icons.link_rounded, color: AppColors.info, size: 20),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(meta.department, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600, color: AppColors.info)),
                        Text(meta.url, style: AppTextStyles.caption.copyWith(color: AppColors.info)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity, height: 52,
              child: ElevatedButton(
                onPressed: () => Navigator.pop(ctx),
                style: ElevatedButton.styleFrom(backgroundColor: AppColors.primary, foregroundColor: Colors.white, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14))),
                child: const Text('Got it'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _howToStep(String num, String text) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 24, height: 24,
            decoration: BoxDecoration(color: AppColors.primary.withOpacity(0.1), shape: BoxShape.circle),
            child: Center(child: Text(num, style: AppTextStyles.caption.copyWith(color: AppColors.primary, fontWeight: FontWeight.w700))),
          ),
          const SizedBox(width: 10),
          Expanded(child: Text(text, style: AppTextStyles.body)),
        ],
      ),
    );
  }
}

class _DocMeta {
  final String title, department, url;
  final IconData icon;
  final bool required;
  const _DocMeta(this.title, this.icon, this.department, this.url, this.required);
}
