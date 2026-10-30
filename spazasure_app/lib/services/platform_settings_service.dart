import 'api_service.dart';

/// Checkout rules set by SpazaSure admins (Admin > Settings). Reading them from the
/// server keeps the cart total identical to what the backend charges.
class PlatformSettings {
  final double standardDeliveryFee;
  final double expressDeliveryFee;
  final double shopServiceFeePercent;
  final bool cashOnDeliveryEnabled;

  /// Largest order total allowed as cash on delivery. 0 = no limit.
  final double cashOnDeliveryMaxOrderTotal;

  /// Smallest order with one supplier, in products (delivery not counted). 0 = no minimum.
  final double minOrderAmount;

  const PlatformSettings({
    this.standardDeliveryFee = 150,
    this.expressDeliveryFee = 250,
    this.shopServiceFeePercent = 0,
    this.cashOnDeliveryEnabled = true,
    this.cashOnDeliveryMaxOrderTotal = 5000,
    this.minOrderAmount = 2000,
  });

  /// Matches the backend defaults, used until (or if) the server can't be reached.
  static const defaults = PlatformSettings();

  bool cashOnDeliveryAllowedFor(double total) =>
      cashOnDeliveryEnabled &&
      (cashOnDeliveryMaxOrderTotal <= 0 ||
          total <= cashOnDeliveryMaxOrderTotal);

  factory PlatformSettings.fromJson(Map<String, dynamic> j) {
    double d(String key, double fallback) =>
        (j[key] as num?)?.toDouble() ?? fallback;
    return PlatformSettings(
      standardDeliveryFee: d(
        'standardDeliveryFee',
        defaults.standardDeliveryFee,
      ),
      expressDeliveryFee: d('expressDeliveryFee', defaults.expressDeliveryFee),
      shopServiceFeePercent: d(
        'shopServiceFeePercent',
        defaults.shopServiceFeePercent,
      ),
      cashOnDeliveryEnabled:
          j['cashOnDeliveryEnabled'] as bool? ?? defaults.cashOnDeliveryEnabled,
      cashOnDeliveryMaxOrderTotal: d(
        'cashOnDeliveryMaxOrderTotal',
        defaults.cashOnDeliveryMaxOrderTotal,
      ),
      minOrderAmount: d('minOrderAmount', defaults.minOrderAmount),
    );
  }
}

class PlatformSettingsService {
  static Future<PlatformSettings> fetch() async {
    try {
      final res = await ApiService.get('/settings/public');
      final data = res['data'];
      if (data is Map<String, dynamic>) return PlatformSettings.fromJson(data);
    } catch (_) {
      // Fall back to defaults; the server still validates the real charge at checkout
    }
    return PlatformSettings.defaults;
  }
}
