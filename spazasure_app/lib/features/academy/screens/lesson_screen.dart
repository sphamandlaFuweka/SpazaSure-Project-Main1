import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/academy/widgets/academy_widgets.dart';
import 'package:spazasure_app/services/shop_academy_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// One Shop Academy lesson: the reading, a "do this week" list and the quiz.
/// Pops with true when the quiz is passed.
class LessonScreen extends StatefulWidget {
  final String lessonKey;

  const LessonScreen({super.key, required this.lessonKey});

  @override
  State<LessonScreen> createState() => _LessonScreenState();
}

class _LessonScreenState extends State<LessonScreen> {
  bool _loading = true;
  bool _failed = false;
  bool _submitting = false;
  bool _passedNow = false;
  Map<String, dynamic> _lesson = {};
  List<int?> _answers = [];

  /// The quiz result once submitted: which answers were right and the correct options
  List<bool>? _correct;
  List<int>? _rightAnswers;

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
      final lesson = await ShopAcademyService.lesson(widget.lessonKey);
      if (!mounted) return;
      setState(() {
        _lesson = lesson;
        _answers = List.filled(((lesson['quiz'] as List?) ?? []).length, null);
      });
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _submit() async {
    if (_answers.contains(null)) {
      showAcademyError(context, 'none', 'Answer every question first.');
      return;
    }
    setState(() => _submitting = true);
    try {
      final result = await ShopAcademyService.submitQuiz(
        widget.lessonKey,
        _answers.cast<int>(),
      );
      if (!mounted) return;
      final passed = result['passed'] == true;
      setState(() {
        _correct = ((result['correct'] as List?) ?? []).cast<bool>();
        _rightAnswers = ((result['answers'] as List?) ?? []).cast<int>();
        _passedNow = _passedNow || passed;
      });
      if (passed) {
        showAcademyMessage(
          context,
          'Lesson passed: ${result['score']}/${result['total']}. Well done!',
        );
      } else {
        showAcademyError(
          context,
          'none',
          'You got ${result['score']}/${result['total']}. Read the lesson again and retry.',
        );
      }
    } catch (e) {
      if (mounted) showAcademyError(context, e);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  void _retry() => setState(() {
    _answers = List.filled(_answers.length, null);
    _correct = null;
    _rightAnswers = null;
  });

  @override
  Widget build(BuildContext context) {
    final sections = ((_lesson['sections'] as List?) ?? [])
        .cast<Map<String, dynamic>>();
    final todo = ((_lesson['doThisWeek'] as List?) ?? []).cast<String>();
    final quiz = ((_lesson['quiz'] as List?) ?? [])
        .cast<Map<String, dynamic>>();

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.pop(context, _passedNow);
      },
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: BrandAppBar(title: Text(_lesson['title'] ?? 'Lesson')),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _failed
            ? AcademyLoadError(onRetry: _load)
            : ListView(
                padding: const EdgeInsets.only(top: 12, bottom: 40),
                children: [
                  AcademyCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${_lesson['category']} · ${_lesson['minutes']} min read',
                          style: AppTextStyles.caption.copyWith(
                            color: AppColors.primary,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          _lesson['summary'] ?? '',
                          style: AppTextStyles.subtitle,
                        ),
                        if (_lesson['completed'] == true || _passedNow) ...[
                          const SizedBox(height: 10),
                          Row(
                            children: [
                              const Icon(
                                Icons.verified_rounded,
                                color: AppColors.success,
                                size: 18,
                              ),
                              const SizedBox(width: 6),
                              Text(
                                'You have passed this lesson',
                                style: AppTextStyles.bodySmall.copyWith(
                                  color: AppColors.success,
                                ),
                              ),
                            ],
                          ),
                        ],
                      ],
                    ),
                  ),
                  for (final s in sections)
                    AcademyCard(
                      title: s['heading'],
                      child: Text(
                        s['body'] ?? '',
                        style: AppTextStyles.body.copyWith(height: 1.5),
                      ),
                    ),
                  if (todo.isNotEmpty)
                    AcademyCard(
                      title: 'Do this week',
                      icon: Icons.task_alt_rounded,
                      child: Column(
                        children: [
                          for (final t in todo)
                            Padding(
                              padding: const EdgeInsets.only(bottom: 6),
                              child: Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Icon(
                                    Icons.check_box_outline_blank_rounded,
                                    size: 18,
                                    color: AppColors.primary,
                                  ),
                                  const SizedBox(width: 8),
                                  Expanded(
                                    child: Text(t, style: AppTextStyles.body),
                                  ),
                                ],
                              ),
                            ),
                        ],
                      ),
                    ),
                  if (quiz.isNotEmpty)
                    AcademyCard(
                      title: 'Quiz',
                      icon: Icons.quiz_outlined,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          for (var q = 0; q < quiz.length; q++)
                            _question(q, quiz[q]),
                          const SizedBox(height: 8),
                          if (_correct == null)
                            FilledButton(
                              onPressed: _submitting ? null : _submit,
                              child: Text(
                                _submitting ? 'Checking…' : 'Check my answers',
                              ),
                            )
                          else
                            OutlinedButton(
                              onPressed: _retry,
                              child: const Text('Try the quiz again'),
                            ),
                        ],
                      ),
                    ),
                ],
              ),
      ),
    );
  }

  Widget _question(int index, Map<String, dynamic> q) {
    final options = ((q['options'] as List?) ?? []).cast<String>();
    final checked = _correct != null;
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${index + 1}. ${q['question']}',
            style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 6),
          for (var o = 0; o < options.length; o++)
            Builder(
              builder: (_) {
                final selected = _answers[index] == o;
                final isRight = checked && _rightAnswers![index] == o;
                final isWrongPick = checked && selected && !isRight;
                final color = isRight
                    ? AppColors.success
                    : isWrongPick
                    ? AppColors.red
                    : selected
                    ? AppColors.primary
                    : AppColors.divider;
                return Padding(
                  padding: const EdgeInsets.only(bottom: 6),
                  child: InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onTap: checked
                        ? null
                        : () => setState(() => _answers[index] = o),
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 10,
                      ),
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                          color: color,
                          width: selected || isRight ? 2 : 1,
                        ),
                        color: color.withValues(
                          alpha: selected || isRight ? 0.06 : 0,
                        ),
                      ),
                      child: Row(
                        children: [
                          Icon(
                            isRight
                                ? Icons.check_circle_rounded
                                : isWrongPick
                                ? Icons.cancel_rounded
                                : selected
                                ? Icons.radio_button_checked_rounded
                                : Icons.radio_button_off_rounded,
                            size: 20,
                            color: color == AppColors.divider
                                ? AppColors.textHint
                                : color,
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(options[o], style: AppTextStyles.body),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              },
            ),
        ],
      ),
    );
  }
}
