import 'package:flutter/foundation.dart';

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
  final String? profilePhotoUrl;
  final Map<String, dynamic>? registrationDetails;

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
    this.profilePhotoUrl,
    this.registrationDetails,
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
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      registrationDetails: (json['registrationDetails'] as Map?)
          ?.cast<String, dynamic>(),
    );
  }
}

class ProfileService {
  /// The shop's profile picture, kept up to date by [getProfile] so every screen showing
  /// the avatar (home, profile) changes as soon as a new picture is uploaded.
  static final photoUrl = ValueNotifier<String?>(null);

  static Future<ShopProfile> getProfile() async {
    final res = await ApiService.get('/shop/profile');
    final profile = ShopProfile.fromJson(res['data'] as Map<String, dynamic>);
    photoUrl.value = profile.profilePhotoUrl;
    return profile;
  }

  static Future<void> updateProfile(Map<String, dynamic> updates) async {
    await ApiService.patch('/shop/profile', updates);
  }

  static Future<String?> initiatePaystackOnboardingPayment() async {
    final response = await ApiService.post(
      '/shop/profile/onboarding-fee/paystack/checkout-session',
      const {},
    );
    final data = response['data'] as Map<String, dynamic>;
    return data['checkoutUrl']?.toString();
  }

  static Future<ShopReviews> getReviews() async {
    final res = await ApiService.get('/shop/profile/reviews');
    return ShopReviews.fromJson(res['data'] as Map<String, dynamic>);
  }

  static Future<OnboardingPaymentStatus> getOnboardingPaymentStatus() async {
    final response = await ApiService.get('/shop/profile/onboarding-fee');
    return OnboardingPaymentStatus.fromJson(
      response['data'] as Map<String, dynamic>,
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

/// What customers said about the shop. Reviewers are anonymous.
class ShopReviews {
  final double average;
  final int count;

  /// Number of reviews per star rating, keyed 1 to 5.
  final Map<int, int> breakdown;
  final List<ShopReview> reviews;

  const ShopReviews({
    required this.average,
    required this.count,
    required this.breakdown,
    required this.reviews,
  });

  factory ShopReviews.fromJson(Map<String, dynamic> json) => ShopReviews(
    average: (json['average'] as num?)?.toDouble() ?? 0,
    count: (json['count'] as num?)?.toInt() ?? 0,
    breakdown: {
      for (final row in (json['breakdown'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>())
        (row['stars'] as num?)?.toInt() ?? 0: (row['count'] as num?)?.toInt() ?? 0,
    },
    reviews: (json['reviews'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(ShopReview.fromJson)
        .toList(),
  );
}

class ShopReview {
  final String id;
  final int rating;
  final String? comment;
  final DateTime? date;

  const ShopReview({
    required this.id,
    required this.rating,
    this.comment,
    this.date,
  });

  factory ShopReview.fromJson(Map<String, dynamic> json) => ShopReview(
    id: json['id']?.toString() ?? '',
    rating: (json['rating'] as num?)?.toInt() ?? 0,
    comment: json['comment']?.toString(),
    date: DateTime.tryParse(
      (json['updatedAt'] ?? json['createdAt'])?.toString() ?? '',
    )?.toLocal(),
  );
}
