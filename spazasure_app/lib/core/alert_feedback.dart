import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// The notification tone and a short buzz, for alerts that arrive while the app is open
/// (pushes do this themselves through the Android notification channel, see MainActivity).
/// Follows the phone's silent and vibrate modes, and the switches in Profile > Notifications.
class AlertFeedback {
  static const _channel = MethodChannel('spazasure/alerts');
  static const _soundKey = 'alert_sound';
  static const _vibrateKey = 'alert_vibrate';

  static Future<bool> soundOn() async =>
      (await SharedPreferences.getInstance()).getBool(_soundKey) ?? true;

  static Future<bool> vibrateOn() async =>
      (await SharedPreferences.getInstance()).getBool(_vibrateKey) ?? true;

  static Future<void> setSound(bool on) async =>
      (await SharedPreferences.getInstance()).setBool(_soundKey, on);

  static Future<void> setVibrate(bool on) async =>
      (await SharedPreferences.getInstance()).setBool(_vibrateKey, on);

  static Future<void> play() async {
    final sound = await soundOn();
    final vibrate = await vibrateOn();
    if (!sound && !vibrate) return;
    try {
      await _channel.invokeMethod<void>('play', {
        'sound': sound,
        'vibrate': vibrate,
      });
    } on MissingPluginException {
      // Not on Android (tests, web): a light haptic is the best we can do
      if (vibrate) await HapticFeedback.mediumImpact();
    } on PlatformException {
      if (vibrate) await HapticFeedback.mediumImpact();
    }
  }

  /// The phone's own settings for SpazaSure's notifications, which decide sound and vibration
  /// while the app is closed. Returns false when the phone can't open them.
  static Future<bool> openPhoneSettings() async {
    try {
      await _channel.invokeMethod<void>('openSettings');
      return true;
    } on MissingPluginException {
      return false;
    } on PlatformException {
      return false;
    }
  }
}
