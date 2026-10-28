import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';

/// "App language" row for profile screens. [onChanged] lets the caller also save the choice
/// to the person's account, e.g. a customer's preferred language.
class LanguagePickerTile extends StatelessWidget {
  final Future<void> Function(String code)? onChanged;

  const LanguagePickerTile({super.key, this.onChanged});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<String>(
      valueListenable: AppLanguage.code,
      builder: (context, code, _) => ListTile(
        leading: const Icon(Icons.translate_rounded, color: AppColors.primary),
        title: Text(tr('language.title')),
        subtitle: Text(AppLanguage.supported[code] ?? 'English'),
        trailing: const Icon(Icons.chevron_right_rounded),
        onTap: () => _choose(context, code),
      ),
    );
  }

  Future<void> _choose(BuildContext context, String current) async {
    final picked = await showModalBottomSheet<String>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 4),
              child: Text(tr('language.title'), style: AppTextStyles.h3),
            ),
            for (final entry in AppLanguage.supported.entries)
              ListTile(
                title: Text(entry.value),
                trailing: entry.key == current
                    ? const Icon(
                        Icons.check_circle_rounded,
                        color: AppColors.primary,
                      )
                    : const Icon(
                        Icons.circle_outlined,
                        color: AppColors.textHint,
                      ),
                onTap: () => Navigator.pop(ctx, entry.key),
              ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 16),
              child: Text(tr('language.partial'), style: AppTextStyles.caption),
            ),
          ],
        ),
      ),
    );
    if (picked == null || picked == current) return;
    await AppLanguage.choose(picked);
    await onChanged?.call(picked);
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(tr('language.changed')),
          backgroundColor: AppColors.success,
        ),
      );
    }
  }
}
