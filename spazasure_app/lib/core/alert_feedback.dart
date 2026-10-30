import 'package:flutter/services.dart';

/// The notification tone and a short buzz, for alerts that arrive while the app is open
/// (pushes do this themselves through the Android notification channel, see MainActivity).
/// Follows the phone's silent and vibrate modes.
class AlertFeedback {
  static const _channel = MethodChannel('spazasure/alerts');

  static Future<void> play() async {
    try {
      await _channel.invokeMethod<void>('play');
    } on MissingPluginException {
      // Not on Android (tests, web): a light haptic is the best we can do
      await HapticFeedback.mediumImpact();
    } on PlatformException {
      await HapticFeedback.mediumImpact();
    }
  }
}
