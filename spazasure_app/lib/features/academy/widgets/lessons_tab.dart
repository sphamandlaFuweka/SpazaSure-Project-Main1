import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/academy/screens/lesson_screen.dart';
import 'package:spazasure_app/features/academy/widgets/academy_widgets.dart';
import 'package:spazasure_app/services/shop_academy_service.dart';

/// The list of Shop Academy lessons with the shop's progress.
class LessonsTab extends StatefulWidget {
  final VoidCallback? onProgress;

  const LessonsTab({super.key, this.onProgress});

  @override
  State<LessonsTab> createState() => _LessonsTabState();
}

class _LessonsTabState extends State<LessonsTab>
    with AutomaticKeepAliveClientMixin {
  bool _loading = true;
  bool _failed = false;
  List<Map<String, dynamic>> _lessons = [];

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _failed = false;
    });
    try {
      final lessons = await ShopAcademyService.lessons();
      if (mounted) setState(() => _lessons = lessons);
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _open(Map<String, dynamic> lesson) async {
    final completed = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => LessonScreen(lessonKey: lesson['key'])),
    );
    if (completed == true) {
      _load();
      widget.onProgress?.call();
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_failed) return AcademyLoadError(onRetry: _load);

    final done = _lessons.where((l) => l['completed'] == true).length;
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.only(top: 12, bottom: 40),
        children: [
          AcademyCard(
            child: Row(
              children: [
                const Icon(
                  Icons.school_rounded,
                  size: 40,
                  color: AppColors.primary,
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '$done of ${_lessons.length} lessons passed',
                        style: AppTextStyles.subtitle.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 6),
                      ClipRRect(
                        borderRadius: BorderRadius.circular(4),
                        child: LinearProgressIndicator(
                          minHeight: 8,
                          value: _lessons.isEmpty ? 0 : done / _lessons.length,
                          backgroundColor: AppColors.divider,
                          color: AppColors.success,
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        'Short practical lessons. Pass the quiz at the end of each one.',
                        style: AppTextStyles.caption.copyWith(
                          color: AppColors.textHint,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          for (final lesson in _lessons)
            AcademyCard(
              margin: const EdgeInsets.fromLTRB(16, 0, 16, 8),
              child: InkWell(
                onTap: () => _open(lesson),
                child: Row(
                  children: [
                    CircleAvatar(
                      radius: 20,
                      backgroundColor: lesson['completed'] == true
                          ? AppColors.secondaryTint
                          : AppColors.primaryTint,
                      child: Icon(
                        lesson['completed'] == true
                            ? Icons.check_rounded
                            : Icons.menu_book_rounded,
                        color: lesson['completed'] == true
                            ? AppColors.success
                            : AppColors.primary,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            lesson['title'] ?? '',
                            style: AppTextStyles.body.copyWith(
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            '${lesson['category']} · ${lesson['minutes']} min'
                            '${lesson['completed'] == true ? ' · passed ${lesson['score']}/${lesson['total']}' : ''}',
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textHint,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const Icon(
                      Icons.chevron_right_rounded,
                      color: AppColors.textHint,
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}
