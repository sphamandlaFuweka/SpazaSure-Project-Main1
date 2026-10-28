import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

import 'api_service.dart';

/// Firebase Cloud Messaging: registers this phone with the backend so notifications arrive
/// as phone notifications even when the app is closed. iOS delivery also needs the APNs
/// key uploaded to Firebase (Project settings → Cloud Messaging).
class PushService {
  static bool _ready = false;
  static bool _registered = false;
  static String? _token;

  static bool get _supported =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS);

  static String get _platform =>
      defaultTargetPlatform == TargetPlatform.iOS ? 'ios' : 'android';

  /// Sets up Firebase once at startup.
  /// [onOpen] runs when the user taps a push (with its data, e.g. {type, referenceId}).
  /// [onForeground] runs when a push arrives while the app is open (the system doesn't
  /// show it then, so the app shows its own banner).
  static Future<void> init({
    required void Function(Map<String, dynamic> data) onOpen,
    required VoidCallback onForeground,
  }) async {
    if (_ready || !_supported) return;
    try {
      await Firebase.initializeApp();
    } catch (e) {
      debugPrint('[Push] Firebase unavailable: $e');
      return;
    }
    _ready = true;

    FirebaseMessaging.onMessage.listen((_) => onForeground());
    FirebaseMessaging.onMessageOpenedApp.listen((m) => onOpen(m.data));
    FirebaseMessaging.instance.onTokenRefresh.listen((token) {
      _token = token;
      if (_registered) unawaited(_sendToken(token));
    });

    // The app was opened by tapping a push while it was closed
    final initial = await FirebaseMessaging.instance.getInitialMessage();
    if (initial != null) onOpen(initial.data);
  }

  /// Call once signed in: asks for permission (iOS, Android 13+) and links this phone to the account.
  static Future<void> register() async {
    if (!_ready) return;
    try {
      final settings = await FirebaseMessaging.instance.requestPermission();
      if (settings.authorizationStatus == AuthorizationStatus.denied) return;
      // On iOS, getToken() fails until Apple has handed over the APNs token
      if (_platform == 'ios' && !await _waitForApnsToken()) return;
      final token = await FirebaseMessaging.instance.getToken();
      if (token == null) return;
      _token = token;
      await _sendToken(token);
      _registered = true;
    } catch (e) {
      debugPrint('[Push] Registration failed: $e');
    }
  }

  /// Call before signing out, while the session is still valid, so the next person
  /// to use this phone doesn't get the previous account's notifications.
  static Future<void> unregister() async {
    final token = _token;
    _registered = false;
    if (!_ready || token == null) return;
    try {
      await ApiService.delete(
        '/shop/notifications/devices?token=${Uri.encodeQueryComponent(token)}',
      );
    } catch (_) {
      // Signing out must not fail because of this; the server also drops dead tokens
    }
  }

  static Future<bool> _waitForApnsToken() async {
    for (var attempt = 0; attempt < 10; attempt++) {
      if (await FirebaseMessaging.instance.getAPNSToken() != null) return true;
      await Future<void>.delayed(const Duration(milliseconds: 500));
    }
    debugPrint(
      '[Push] No APNs token from Apple; push stays off on this phone.',
    );
    return false;
  }

  static Future<void> _sendToken(String token) => ApiService.post(
    '/shop/notifications/devices',
    {'token': token, 'platform': _platform},
  );
}
