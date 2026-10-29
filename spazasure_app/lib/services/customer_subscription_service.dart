import 'api_service.dart';

class CustomerSubscriptionPlan {
  final String key;
  final String name;
  final double monthlyPrice;
  final bool isAvailable;
  final String description;
  final List<String> features;

  const CustomerSubscriptionPlan({
    required this.key,
    required this.name,
    required this.monthlyPrice,
    required this.isAvailable,
    required this.description,
    required this.features,
  });

  factory CustomerSubscriptionPlan.fromJson(Map<String, dynamic> json) =>
      CustomerSubscriptionPlan(
        key: json['key']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        monthlyPrice: (json['monthlyPrice'] as num?)?.toDouble() ?? 0,
        isAvailable: json['isAvailable'] == true,
        description: json['description']?.toString() ?? '',
        features:
            (json['features'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
      );
}

class CustomerSubscriptionOverview {
  final String currentPlan;
  final String status;
  final bool isPlusActive;
  // False when Plus was paid once-off for a month and has to be paid again
  final bool autoRenews;
  final bool cancelAtPeriodEnd;
  final DateTime? currentPeriodEnd;
  final List<CustomerSubscriptionPlan> plans;

  const CustomerSubscriptionOverview({
    required this.currentPlan,
    required this.status,
    required this.isPlusActive,
    required this.autoRenews,
    required this.cancelAtPeriodEnd,
    required this.currentPeriodEnd,
    required this.plans,
  });

  factory CustomerSubscriptionOverview.fromJson(Map<String, dynamic> json) =>
      CustomerSubscriptionOverview(
        currentPlan: json['currentPlan']?.toString() ?? 'free',
        status: json['status']?.toString() ?? 'active',
        isPlusActive: json['isPlusActive'] == true,
        autoRenews: json['autoRenews'] == true,
        cancelAtPeriodEnd: json['cancelAtPeriodEnd'] == true,
        currentPeriodEnd: DateTime.tryParse(
          json['currentPeriodEnd']?.toString() ?? '',
        ),
        plans:
            (json['plans'] as List?)
                ?.whereType<Map<String, dynamic>>()
                .map(CustomerSubscriptionPlan.fromJson)
                .toList() ??
            const [],
      );
}

class CustomerSubscriptionService {
  static Future<CustomerSubscriptionOverview> getOverview() async {
    final response = await ApiService.get('/customer/subscription');
    return CustomerSubscriptionOverview.fromJson(
      response['data'] as Map<String, dynamic>,
    );
  }

  /// Plus switches on as soon as Paystack confirms, so there's no wait for the webhook.
  static Future<CustomerSubscriptionOverview> verifyPayment() async {
    final response = await ApiService.post('/customer/subscription/verify', {});
    return CustomerSubscriptionOverview.fromJson(
      response['data'] as Map<String, dynamic>,
    );
  }

  /// The Paystack page to pay on, or null when an earlier payment already went through.
  static Future<String?> createCheckout(String planKey) async {
    final response = await ApiService.post('/customer/subscription/checkout', {
      'planKey': planKey,
    });
    final data = response['data'] as Map<String, dynamic>;
    if (data['alreadyActive'] == true) return null;
    final url = data['checkoutUrl']?.toString();
    if (url == null || url.isEmpty) {
      throw StateError('Paystack checkout was not returned for this plan.');
    }
    return url;
  }

  static Future<void> cancelSubscription() async {
    await ApiService.post('/customer/subscription/cancel', {});
  }
}
