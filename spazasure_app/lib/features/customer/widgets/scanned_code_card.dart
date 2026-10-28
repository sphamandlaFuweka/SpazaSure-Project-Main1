import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/scanned_code.dart';
import 'package:url_launcher/url_launcher.dart';

/// Explains what a scanned code is: a link, a payment code, GS1 batch data, Wi-Fi, etc.
class ScannedCodeCard extends StatelessWidget {
  final ScannedCode code;

  const ScannedCodeCard({super.key, required this.code});

  static IconData _icon(ScannedCodeKind kind) => switch (kind) {
    ScannedCodeKind.url => Icons.link_rounded,
    ScannedCodeKind.payment => Icons.payments_outlined,
    ScannedCodeKind.wifi => Icons.wifi_rounded,
    ScannedCodeKind.contact => Icons.contact_page_outlined,
    ScannedCodeKind.email => Icons.email_outlined,
    ScannedCodeKind.phone => Icons.call_outlined,
    ScannedCodeKind.sms => Icons.sms_outlined,
    ScannedCodeKind.location => Icons.place_outlined,
    ScannedCodeKind.gs1 => Icons.qr_code_2_rounded,
    ScannedCodeKind.spazaSure => Icons.verified_user_outlined,
    _ => Icons.text_snippet_outlined,
  };

  @override
  Widget build(BuildContext context) {
    final warn = code.warnings.isNotEmpty;
    final tint = warn ? AppColors.warning : AppColors.primary;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: tint.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: tint.withValues(alpha: 0.3)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(_icon(code.kind), color: tint, size: 28),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'What this code is',
                      style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
                    ),
                    Text(
                      code.title,
                      style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w800, color: tint),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(code.explanation, style: AppTextStyles.caption),
          if (code.details.isNotEmpty) ...[
            const SizedBox(height: 10),
            for (final d in code.details) _detail(d.key, d.value),
          ],
          for (final w in code.warnings)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(Icons.warning_amber_rounded, size: 18, color: AppColors.warning),
                  const SizedBox(width: 8),
                  Expanded(child: Text(w, style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600))),
                ],
              ),
            ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              if (code.link != null)
                OutlinedButton.icon(
                  onPressed: () => _open(context),
                  icon: const Icon(Icons.open_in_new, size: 18),
                  label: Text(code.kind == ScannedCodeKind.location ? 'Open map' : 'Open link'),
                ),
              OutlinedButton.icon(
                onPressed: () {
                  Clipboard.setData(ClipboardData(text: code.raw));
                  ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Copied')));
                },
                icon: const Icon(Icons.copy_rounded, size: 18),
                label: const Text('Copy'),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _open(BuildContext context) async {
    final link = code.link!;
    if (code.warnings.isNotEmpty) {
      final go = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Open this link?'),
          content: Text('${link.host}\n\n${code.warnings.join('\n\n')}'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
            TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Open anyway')),
          ],
        ),
      );
      if (go != true) return;
    }
    await launchUrl(link, mode: LaunchMode.externalApplication);
  }

  Widget _detail(String label, String value) => Padding(
    padding: const EdgeInsets.only(bottom: 4),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 120,
          child: Text(label, style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary)),
        ),
        Expanded(child: Text(value, style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600))),
      ],
    ),
  );
}
