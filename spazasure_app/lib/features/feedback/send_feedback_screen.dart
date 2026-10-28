import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/analytics_service.dart';
import 'package:spazasure_app/services/feedback_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// "Send feedback": what works, what doesn't and what testers would like, with an optional
/// rating and screenshot. The team reads it in the admin console.
class SendFeedbackScreen extends StatefulWidget {
  final FeedbackService service;

  /// Where the person came from, e.g. "Scan result", so the team knows what it is about.
  final String? fromScreen;
  final String initialType;

  const SendFeedbackScreen({
    super.key,
    required this.service,
    this.fromScreen,
    this.initialType = 'bug',
  });

  @override
  State<SendFeedbackScreen> createState() => _SendFeedbackScreenState();
}

class _SendFeedbackScreenState extends State<SendFeedbackScreen> {
  final _message = TextEditingController();
  late String _type = widget.initialType;
  int? _rating;
  Uint8List? _screenshot;
  String? _screenshotName;
  bool _sending = false;
  String? _error;

  @override
  void dispose() {
    _message.dispose();
    super.dispose();
  }

  Future<void> _pickScreenshot() async {
    final picked = await ImagePicker().pickImage(
      source: ImageSource.gallery,
      imageQuality: 80,
      maxWidth: 1600,
    );
    if (picked == null) return;
    final bytes = await picked.readAsBytes();
    if (!mounted) return;
    setState(() {
      _screenshot = bytes;
      _screenshotName = picked.name;
    });
  }

  Future<void> _send() async {
    final message = _message.text.trim();
    if (message.length < 3) {
      setState(() => _error = 'Please tell us a little more.');
      return;
    }
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      await widget.service.send(
        type: _type,
        message: message,
        rating: _rating,
        screen: widget.fromScreen,
        screenshotBytes: _screenshot,
        screenshotName: _screenshotName,
      );
      AnalyticsService.feedbackSent(_type);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Thank you! Your feedback helps us make SpazaSure better.',
          ),
        ),
      );
      Navigator.pop(context, true);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(title: const Text('Send feedback'), elevation: 0),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            "SpazaSure is still being tested. Tell us what works, what doesn't and what you'd like to see. We read every message.",
            style: AppTextStyles.bodySmall.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'What is it about?',
            style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final entry in FeedbackService.types.entries)
                ChoiceChip(
                  label: Text(entry.value),
                  selected: _type == entry.key,
                  onSelected: (_) => setState(() => _type = entry.key),
                ),
            ],
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _message,
            minLines: 4,
            maxLines: 8,
            maxLength: 2000,
            textCapitalization: TextCapitalization.sentences,
            decoration: InputDecoration(
              hintText: _type == 'bug'
                  ? 'What happened, and what did you expect to happen?'
                  : 'Tell us more...',
              filled: true,
              fillColor: AppColors.surface,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(14),
                borderSide: BorderSide.none,
              ),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'How do you rate SpazaSure so far? (optional)',
            style: AppTextStyles.bodySmall.copyWith(
              fontWeight: FontWeight.w600,
            ),
          ),
          Row(
            children: [
              for (var star = 1; star <= 5; star++)
                IconButton(
                  tooltip: '$star of 5',
                  onPressed: () =>
                      setState(() => _rating = _rating == star ? null : star),
                  icon: Icon(
                    _rating != null && star <= _rating!
                        ? Icons.star_rounded
                        : Icons.star_outline_rounded,
                    color: AppColors.warning,
                    size: 32,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 8),
          if (_screenshot == null)
            OutlinedButton.icon(
              onPressed: _sending ? null : _pickScreenshot,
              icon: const Icon(Icons.add_photo_alternate_outlined),
              label: const Text('Add a screenshot (optional)'),
            )
          else
            Row(
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(10),
                  child: Image.memory(
                    _screenshot!,
                    width: 72,
                    height: 72,
                    fit: BoxFit.cover,
                  ),
                ),
                const SizedBox(width: 12),
                const Expanded(child: Text('Screenshot added')),
                TextButton(
                  onPressed: _sending
                      ? null
                      : () => setState(() => _screenshot = null),
                  child: const Text('Remove'),
                ),
              ],
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                _error!,
                style: AppTextStyles.caption.copyWith(color: AppColors.error),
              ),
            ),
          const SizedBox(height: 20),
          FilledButton.icon(
            onPressed: _sending ? null : _send,
            icon: _sending
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : const Icon(Icons.send_rounded),
            label: const Text('Send feedback'),
            style: FilledButton.styleFrom(
              padding: const EdgeInsets.symmetric(vertical: 14),
            ),
          ),
        ],
      ),
    );
  }
}

/// The "Help us improve SpazaSure" card shown on profile screens.
class FeedbackCard extends StatelessWidget {
  final FeedbackService service;

  const FeedbackCard({super.key, required this.service});

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 8),
      child: ListTile(
        leading: const Icon(
          Icons.rate_review_outlined,
          color: AppColors.primary,
        ),
        title: const Text('Send feedback'),
        subtitle: const Text(
          'Report a problem or share an idea while we test SpazaSure',
        ),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => Navigator.push(
          context,
          MaterialPageRoute(
            settings: const RouteSettings(name: '/send-feedback'),
            builder: (_) =>
                SendFeedbackScreen(service: service, fromScreen: 'Profile'),
          ),
        ),
      ),
    );
  }
}
