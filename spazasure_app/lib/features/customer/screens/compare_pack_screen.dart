import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/widgets/product_gallery.dart';
import 'package:spazasure_app/services/pack_match.dart';
import 'package:spazasure_app/services/packaging_ocr_service.dart';

/// Puts the customer's own photo of the pack next to photos of the real product, reads
/// the text on their pack to check brand, name, size and barcode, and lets them report
/// it with the photo attached when it doesn't match.
class ComparePackScreen extends StatefulWidget {
  final List<GalleryPhoto> photos;
  final String? name;
  final String? brand;
  final String? quantity;
  final String? barcode;

  /// Called with the photo and a summary of what didn't match, to open a report.
  final void Function(PlatformFile photo, String details) onReport;

  /// Replaces on-device text reading in tests.
  final Future<String> Function(String imagePath)? readText;

  const ComparePackScreen({
    super.key,
    required this.photos,
    required this.onReport,
    this.name,
    this.brand,
    this.quantity,
    this.barcode,
    this.readText,
  });

  @override
  State<ComparePackScreen> createState() => _ComparePackScreenState();
}

enum _Answer { same, different, unsure }

class _ComparePackScreenState extends State<ComparePackScreen> {
  final _refController = PageController();
  int _ref = 0;
  XFile? _photo;
  Uint8List? _bytes;
  bool _reading = false;
  String? _readError;
  PackMatch? _match;
  _Answer? _answer;

  bool get _canReadText =>
      widget.readText != null || PackagingOcrService.supported;

  @override
  void dispose() {
    _refController.dispose();
    super.dispose();
  }

  Future<void> _takePhoto(ImageSource source) async {
    final photo = await ImagePicker().pickImage(
      source: source,
      imageQuality: 85,
      maxWidth: 2000,
    );
    if (photo == null) return;
    final bytes = await photo.readAsBytes();
    if (!mounted) return;
    setState(() {
      _photo = photo;
      _bytes = bytes;
      _match = null;
      _answer = null;
      _readError = null;
    });
    if (_canReadText) await _check(photo.path);
  }

  Future<void> _check(String path) async {
    setState(() => _reading = true);
    try {
      final text = widget.readText != null
          ? await widget.readText!(path)
          : (await PackagingOcrService.scan(path)).rawText;
      final digits = widget.barcode?.trim() ?? '';
      final match = comparePackText(
        packText: text,
        name: widget.name,
        brand: widget.brand,
        quantity: widget.quantity,
        // Only a printed retail barcode can be checked, not a QR or SKU
        barcode: RegExp(r'^\d{8,14}$').hasMatch(digits) ? digits : null,
      );
      if (mounted) setState(() => _match = match);
    } catch (e) {
      if (mounted) setState(() => _readError = 'Could not read the photo: $e');
    } finally {
      if (mounted) setState(() => _reading = false);
    }
  }

  void _report() {
    final photo = _photo;
    final bytes = _bytes;
    if (photo == null || bytes == null) return;
    final lines = <String>[
      'My pack does not look the same as the SpazaSure reference photos.',
      if (_match != null)
        for (final c in _match!.missing)
          '- ${c.label} on the pack does not match "${c.expected}".',
    ];
    widget.onReport(
      PlatformFile(name: photo.name, size: bytes.length, bytes: bytes),
      lines.join('\n'),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Compare with your pack'),
        backgroundColor: AppColors.surface,
        foregroundColor: AppColors.textPrimary,
        elevation: 0,
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            widget.photos.isEmpty
                ? 'Take a clear photo of the front of your pack. We will read the text on it and check it against this product.'
                : 'Take a clear photo of the front of your pack. It will appear next to the real product so you can compare them.',
            style: AppTextStyles.bodySmall.copyWith(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 14),
          _sideBySide(),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: FilledButton.icon(
                  onPressed: _reading ? null : () => _takePhoto(ImageSource.camera),
                  icon: const Icon(Icons.photo_camera),
                  label: Text(_photo == null ? 'Take photo' : 'Retake'),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _reading ? null : () => _takePhoto(ImageSource.gallery),
                  icon: const Icon(Icons.photo_library_outlined),
                  label: const Text('Choose photo'),
                ),
              ),
            ],
          ),
          if (_reading)
            const Padding(
              padding: EdgeInsets.all(16),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                  SizedBox(width: 10),
                  Text('Reading the text on your pack...'),
                ],
              ),
            ),
          if (_readError != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                _readError!,
                style: AppTextStyles.caption.copyWith(color: AppColors.error),
              ),
            ),
          if (_match != null) ...[const SizedBox(height: 14), _textCheck(_match!)],
          if (_photo != null && !_reading) ...[
            const SizedBox(height: 14),
            _decision(),
          ],
        ],
      ),
    );
  }

  Widget _sideBySide() {
    Widget panel(String title, Widget child, {String? caption}) => Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 6),
          AspectRatio(
            aspectRatio: 3 / 4,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(12),
              child: ColoredBox(color: AppColors.surface, child: child),
            ),
          ),
          if (caption != null) ...[
            const SizedBox(height: 4),
            Text(
              caption,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppTextStyles.caption.copyWith(
                color: AppColors.textSecondary,
                fontSize: 11,
              ),
            ),
          ],
        ],
      ),
    );

    final reference = widget.photos.isEmpty
        ? _emptyPanel(Icons.hide_image_outlined, 'No reference photo on record')
        : PageView.builder(
            controller: _refController,
            itemCount: widget.photos.length,
            onPageChanged: (i) => setState(() => _ref = i),
            itemBuilder: (context, i) => GestureDetector(
              onTap: () => Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) =>
                      PhotoViewerScreen(photos: widget.photos, initialIndex: i),
                ),
              ),
              child: ProductImage(widget.photos[i].url, fit: BoxFit.contain),
            ),
          );

    final yours = _bytes == null
        ? InkWell(
            onTap: () => _takePhoto(ImageSource.camera),
            child: _emptyPanel(Icons.add_a_photo_outlined, 'Tap to photograph your pack'),
          )
        : GestureDetector(
            onTap: () => showDialog<void>(
              context: context,
              builder: (_) => Dialog(
                insetPadding: const EdgeInsets.all(12),
                child: InteractiveViewer(maxScale: 5, child: Image.memory(_bytes!)),
              ),
            ),
            child: Image.memory(_bytes!, fit: BoxFit.contain),
          );

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        panel(
          'Real product',
          reference,
          caption: widget.photos.isEmpty
              ? null
              : '${widget.photos[_ref].label}'
                    '${widget.photos.length > 1 ? '  ·  swipe for more' : ''}',
        ),
        const SizedBox(width: 12),
        panel('Your pack', yours, caption: _bytes == null ? null : 'Tap to zoom'),
      ],
    );
  }

  Widget _emptyPanel(IconData icon, String text) => Padding(
    padding: const EdgeInsets.all(12),
    child: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Icon(icon, size: 36, color: AppColors.textHint),
        const SizedBox(height: 8),
        Text(
          text,
          textAlign: TextAlign.center,
          style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
        ),
      ],
    ),
  );

  Widget _textCheck(PackMatch m) {
    final allGood = m.total > 0 && m.matched == m.total;
    final color = !m.readAnyText
        ? AppColors.warning
        : allGood
        ? AppColors.success
        : m.matched == 0
        ? AppColors.error
        : AppColors.warning;
    final headline = !m.readAnyText
        ? 'We could not read any text in your photo'
        : m.total == 0
        ? 'Nothing on record to check the text against'
        : allGood
        ? 'All ${m.total} details on your pack match'
        : '${m.matched} of ${m.total} details on your pack match';

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            headline,
            style: AppTextStyles.body.copyWith(
              fontWeight: FontWeight.w800,
              color: color,
            ),
          ),
          const SizedBox(height: 8),
          if (!m.readAnyText)
            Text(
              'Retake it closer to the front of the pack, in good light and without glare.',
              style: AppTextStyles.bodySmall,
            )
          else
            for (final c in m.checks)
              Padding(
                padding: const EdgeInsets.only(bottom: 6),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(
                      c.found ? Icons.check_circle : Icons.help_outline,
                      size: 18,
                      color: c.found ? AppColors.success : AppColors.warning,
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        c.found
                            ? '${c.label}: ${c.expected}'
                            : '${c.label}: expected "${c.expected}", not found on your photo',
                        style: AppTextStyles.bodySmall,
                      ),
                    ),
                  ],
                ),
              ),
          if (m.readAnyText && !allGood && m.total > 0)
            Text(
              'Not found can also mean that part of the pack was not in the photo.',
              style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
            ),
          const SizedBox(height: 6),
          Text(
            'Matching text shows it is the same product, not that it is genuine. Fakes copy the words too, so also compare the logo, colours and print quality.',
            style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
          ),
        ],
      ),
    );
  }

  Widget _decision() {
    return Material(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(16),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              widget.photos.isEmpty
                  ? 'Does everything on your pack look right?'
                  : 'Does your pack look the same as the real product?',
              style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                ChoiceChip(
                  label: const Text('Yes, it matches'),
                  selected: _answer == _Answer.same,
                  onSelected: (_) => setState(() => _answer = _Answer.same),
                ),
                ChoiceChip(
                  label: const Text('No, something is different'),
                  selected: _answer == _Answer.different,
                  onSelected: (_) => setState(() => _answer = _Answer.different),
                ),
                ChoiceChip(
                  label: const Text('Not sure'),
                  selected: _answer == _Answer.unsure,
                  onSelected: (_) => setState(() => _answer = _Answer.unsure),
                ),
              ],
            ),
            if (_answer == _Answer.same) ...[
              const SizedBox(height: 12),
              Text(
                'Good. Before you use it, check that the seal is unbroken and the expiry date has not passed.',
                style: AppTextStyles.bodySmall,
              ),
              const SizedBox(height: 10),
              FilledButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('Done'),
              ),
            ],
            if (_answer == _Answer.different) ...[
              const SizedBox(height: 12),
              Text(
                'Do not use it. Report it so SpazaSure can follow up with the shop; your photo will be attached.',
                style: AppTextStyles.bodySmall,
              ),
              const SizedBox(height: 10),
              FilledButton.icon(
                style: FilledButton.styleFrom(backgroundColor: AppColors.error),
                onPressed: _report,
                icon: const Icon(Icons.flag_rounded),
                label: const Text('Report with this photo'),
              ),
            ],
            if (_answer == _Answer.unsure) ...[
              const SizedBox(height: 12),
              for (final tip in const [
                'Logo: same shape, colours and spacing, no blurry edges.',
                'Spelling: fakes often have small spelling mistakes.',
                'Size: the amount (e.g. 2 L or 500 g) matches.',
                'Print: sharp text and colours, not faded or smudged.',
                'Barcode: in the same place, with the number printed under it.',
                'Seal and lid: unbroken and the same type as the real product.',
              ])
                Padding(
                  padding: const EdgeInsets.only(bottom: 6),
                  child: Text('• $tip', style: AppTextStyles.bodySmall),
                ),
              const SizedBox(height: 4),
              OutlinedButton.icon(
                onPressed: _report,
                icon: const Icon(Icons.flag_outlined),
                label: const Text('Still unsure? Report it for a check'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
