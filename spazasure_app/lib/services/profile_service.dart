import 'api_service.dart';

class ShopProfile {
  final String id;
  final String shopName;
  final String ownerName;
  final String phone;
  final String email;
  final String address;
  final String city;
  final String province;
  final String status;
  final String complianceStatus;
  final bool onboardingFeePaid;
  final double onboardingFeeAmount;
  final double ratingAvg;
  final int ratingCount;
  final String joinedAt;
  final double? latitude;
  final double? longitude;

  ShopProfile({
    required this.id,
    required this.shopName,
    required this.ownerName,
    required this.phone,
    required this.email,
    required this.address,
    required this.city,
    required this.province,
    required this.status,
    required this.complianceStatus,
    required this.onboardingFeePaid,
    required this.onboardingFeeAmount,
    required this.ratingAvg,
    required this.ratingCount,
    required this.joinedAt,
    this.latitude,
    this.longitude,
  });

  factory ShopProfile.fromJson(Map<String, dynamic> json) {
    return ShopProfile(
      id: json['id']?.toString() ?? '',
      shopName: json['shopName'] ?? '',
      ownerName: json['ownerName'] ?? '',
      phone: json['phone'] ?? '',
      email: json['email'] ?? '',
      address: json['address'] ?? '',
      city: json['city'] ?? '',
      province: json['province'] ?? '',
      status: json['status'] ?? 'pending',
      complianceStatus: json['complianceStatus'] ?? 'incomplete',
      onboardingFeePaid: json['onboardingFeePaid'] ?? false,
      onboardingFeeAmount:
          (json['onboardingFeeAmount'] as num?)?.toDouble() ?? 150,
      ratingAvg: (json['ratingAvg'] ?? 0).toDouble(),
      ratingCount: json['ratingCount'] ?? 0,
      joinedAt: json['joinedAt'] ?? '',
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
    );
  }
}

class ProfileService {
  static Future<ShopProfile> getProfile() async {
    final res = await ApiService.get('/shop/profile');
    return ShopProfile.fromJson(res['data'] as Map<String, dynamic>);
  }

  static Future<void> updateProfile(Map<String, dynamic> updates) async {
    await ApiService.patch('/shop/profile', updates);
  }

  static Future<OnboardingCheckout> initiateOnboardingPayment() async {
    final response = await ApiService.post(
      '/shop/profile/onboarding-fee/initiate',
      const {},
    );
    return OnboardingCheckout.fromJson(
      response['data'] as Map<String, dynamic>,
    );
  }

  static Future<OnboardingPaymentStatus> getOnboardingPaymentStatus() async {
    final response = await ApiService.get('/shop/profile/onboarding-fee');
    return OnboardingPaymentStatus.fromJson(
      response['data'] as Map<String, dynamic>,
    );
  }
}

class OnboardingCheckout {
  final bool paid;
  final String? paymentId;
  final double amount;
  final String? actionUrl;
  final Map<String, String> fields;
  final String? returnUrl;
  final String? cancelUrl;

  const OnboardingCheckout({
    required this.paid,
    required this.paymentId,
    required this.amount,
    required this.actionUrl,
    required this.fields,
    required this.returnUrl,
    required this.cancelUrl,
  });

  factory OnboardingCheckout.fromJson(Map<String, dynamic> json) {
    final rawFields = json['fields'] as Map<String, dynamic>? ?? const {};
    return OnboardingCheckout(
      paid: json['paid'] == true,
      paymentId: json['paymentId']?.toString(),
      amount: (json['amount'] as num?)?.toDouble() ?? 0,
      actionUrl: json['actionUrl']?.toString(),
      fields: rawFields.map((key, value) => MapEntry(key, value.toString())),
      returnUrl: json['returnUrl']?.toString(),
      cancelUrl: json['cancelUrl']?.toString(),
    );
  }
}

class OnboardingPaymentStatus {
  final bool paid;
  final double amount;
  final String? paymentReference;

  const OnboardingPaymentStatus({
    required this.paid,
    required this.amount,
    required this.paymentReference,
  });

  factory OnboardingPaymentStatus.fromJson(Map<String, dynamic> json) =>
      OnboardingPaymentStatus(
        paid: json['paid'] == true,
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        paymentReference: json['paymentReference']?.toString(),
      );
}
