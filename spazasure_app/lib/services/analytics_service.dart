import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';

/// Product analytics through Firebase Analytics: which features testers use and where they
/// drop off. Only an anonymous account id and the role are attached; never names, phone
/// numbers or email addresses. Every call is best effort, so analytics can't break the app.
class AnalyticsService {
  static bool _ready = false;
  static FirebaseAnalytics? _analytics;

  static bool get _supported =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS);

  /// Call once at startup.
  static Future<void> init() async {
    if (_ready || !_supported) return;
    try {
      if (Firebase.apps.isEmpty) await Firebase.initializeApp();
      _analytics = FirebaseAnalytics.instance;
      _ready = true;
    } catch (e) {
      debugPrint('[Analytics] Firebase unavailable: $e');
    }
  }

  /// Records a screen view for every named route (e.g. /login, /cart).
  static List<NavigatorObserver> get observers =>
      _ready ? [FirebaseAnalyticsObserver(analytics: _analytics!)] : const [];

  /// Ties events to the signed-in account (its id only) and role; null on sign-out.
  static Future<void> setUser({String? userId, String? role}) =>
      _safe(() async {
        await _analytics!.setUserId(
          id: userId == null || userId.isEmpty ? null : userId,
        );
        await _analytics!.setUserProperty(name: 'role', value: role);
      });

  static Future<void> login(String role) =>
      _safe(() => _analytics!.logLogin(loginMethod: 'phone_$role'));

  static Future<void> signUp(String role) =>
      _safe(() => _analytics!.logSignUp(signUpMethod: role));

  /// A product check: how it was scanned and what SpazaSure found.
  static Future<void> productScan({
    required String method,
    String? source,
    String? riskLevel,
  }) => event('product_scan', {
    'method': method,
    'source': source ?? 'unknown',
    'risk_level': riskLevel ?? 'unknown',
  });

  /// The "compare with my pack" check and what the customer decided.
  static Future<void> packCompare({
    required int matched,
    required int total,
    String? answer,
  }) => event('pack_compare', {
    'matched': matched,
    'total': total,
    'answer': answer ?? 'none',
  });

  static Future<void> reportSubmitted(String type) =>
      event('report_submitted', {'type': type});

  static Future<void> feedbackSent(String type) =>
      event('feedback_sent', {'type': type});

  static Future<void> orderPlaced({
    required double value,
    required int items,
  }) => _safe(
    () => _analytics!.logPurchase(
      currency: 'ZAR',
      value: value,
      parameters: {'items': items},
    ),
  );

  /// Any other event. Names: lower_snake_case, up to 40 characters.
  static Future<void> event(String name, [Map<String, Object>? parameters]) =>
      _safe(() => _analytics!.logEvent(name: name, parameters: parameters));

  static Future<void> _safe(Future<void> Function() action) async {
    if (!_ready) return;
    try {
      await action();
    } catch (e) {
      debugPrint('[Analytics] $e');
    }
  }
}
