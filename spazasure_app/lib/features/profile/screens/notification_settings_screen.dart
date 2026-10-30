import 'package:flutter/material.dart';
import 'package:spazasure_app/core/alert_feedback.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// Sound and vibration for alerts. While the app is open SpazaSure plays them itself (these
/// switches); while it is closed Android does, so those are set in the phone's settings.
class NotificationSettingsScreen extends StatefulWidget {
  const NotificationSettingsScreen({super.key});

  @override
  State<NotificationSettingsScreen> createState() =>
      _NotificationSettingsScreenState();
}

class _NotificationSettingsScreenState
    extends State<NotificationSettingsScreen> {
  bool? _sound;
  bool? _vibrate;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final sound = await AlertFeedback.soundOn();
    final vibrate = await AlertFeedback.vibrateOn();
    if (mounted) {
      setState(() {
        _sound = sound;
        _vibrate = vibrate;
      });
    }
  }

  Future<void> _openPhoneSettings() async {
    final opened = await AlertFeedback.openPhoneSettings();
    if (!opened && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Open your phone\'s Settings > Apps > SpazaSure > Notifications.',
          ),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(title: const Text('Notifications')),
      body: _sound == null
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _heading('While the app is open'),
                _card([
                  SwitchListTile(
                    secondary: const Icon(Icons.vibration_rounded),
                    title: const Text('Vibrate'),
                    subtitle: const Text('Buzz when a new alert pops up'),
                    value: _vibrate!,
                    onChanged: (on) {
                      setState(() => _vibrate = on);
                      AlertFeedback.setVibrate(on);
                      if (on) AlertFeedback.play();
                    },
                  ),
                  const Divider(height: 1),
                  SwitchListTile(
                    secondary: const Icon(Icons.notifications_active_outlined),
                    title: const Text('Sound'),
                    subtitle: const Text('Play the notification tone'),
                    value: _sound!,
                    onChanged: (on) {
                      setState(() => _sound = on);
                      AlertFeedback.setSound(on);
                      if (on) AlertFeedback.play();
                    },
                  ),
                ]),
                const SizedBox(height: 24),
                _heading('While the app is closed'),
                _card([
                  ListTile(
                    leading: const Icon(Icons.settings_outlined),
                    title: const Text('Phone notification settings'),
                    subtitle: const Text(
                      'Turn vibration, sound or pop-ups on or off for SpazaSure alerts',
                    ),
                    trailing: const Icon(Icons.open_in_new_rounded, size: 18),
                    onTap: _openPhoneSettings,
                  ),
                ]),
                const SizedBox(height: 12),
                Text(
                  'Your phone in silent mode stays silent, and in vibrate mode only buzzes.',
                  style: AppTextStyles.caption,
                ),
              ],
            ),
    );
  }

  Widget _heading(String text) => Padding(
    padding: const EdgeInsets.fromLTRB(4, 0, 4, 8),
    child: Text(
      text,
      style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w700),
    ),
  );

  Widget _card(List<Widget> children) => Container(
    decoration: BoxDecoration(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(14),
    ),
    clipBehavior: Clip.antiAlias,
    child: Column(children: children),
  );
}
