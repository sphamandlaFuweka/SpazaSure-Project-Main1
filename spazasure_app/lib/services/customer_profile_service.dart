import 'api_service.dart';

class CustomerProfile {
  final String id;
  final String firstName;
  final String lastName;
  final String fullName;
  final String email;
  final String phone;
  final int? age;
  final List<String> allergies;
  final String? preferredName;
  final String preferredLanguage;
  final String? province;
  final String? city;
  final String? suburb;
  final bool nearbyRecommendationsEnabled;
  final double? latitude;
  final double? longitude;
  final List<String> interests;
  final List<String> notificationPreferences;
  final bool marketingOptIn;
  final String? joinedAt;
  final String? profilePhotoUrl;

  const CustomerProfile({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.fullName,
    required this.email,
    required this.phone,
    this.age,
    this.allergies = const [],
    this.preferredName,
    this.preferredLanguage = 'English',
    this.province,
    this.city,
    this.suburb,
    this.nearbyRecommendationsEnabled = false,
    this.latitude,
    this.longitude,
    this.interests = const [],
    this.notificationPreferences = const [],
    this.marketingOptIn = false,
    this.joinedAt,
    this.profilePhotoUrl,
  });

  factory CustomerProfile.fromJson(Map<String, dynamic> json) =>
      CustomerProfile(
        id: json['id']?.toString() ?? '',
        firstName: json['firstName']?.toString() ?? '',
        lastName: json['lastName']?.toString() ?? '',
        fullName: json['fullName']?.toString() ?? '',
        email: json['email']?.toString() ?? '',
        phone: json['phone']?.toString() ?? '',
        age: (json['age'] as num?)?.toInt(),
        allergies:
            (json['allergies'] as List?)?.map((a) => a.toString()).toList() ??
            const [],
        preferredName: json['preferredName']?.toString(),
        preferredLanguage:
            json['preferredLanguage']?.toString() ?? 'English',
        province: json['province']?.toString(),
        city: json['city']?.toString(),
        suburb: json['suburb']?.toString(),
        nearbyRecommendationsEnabled:
            json['nearbyRecommendationsEnabled'] == true,
        latitude: (json['latitude'] as num?)?.toDouble(),
        longitude: (json['longitude'] as num?)?.toDouble(),
        interests:
            (json['interests'] as List?)?.map((v) => v.toString()).toList() ??
            const [],
        notificationPreferences:
            (json['notificationPreferences'] as List?)
                ?.map((v) => v.toString())
                .toList() ??
            const [],
        marketingOptIn: json['marketingOptIn'] == true,
        joinedAt: json['joinedAt']?.toString(),
        profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      );
}

class CustomerProfileService {
  static Future<CustomerProfile> getProfile() async {
    final res = await ApiService.get('/customer/profile');
    return CustomerProfile.fromJson(res['data'] as Map<String, dynamic>);
  }

  static Future<void> updateProfile({
    String? firstName,
    String? lastName,
    int? age,
    List<String>? allergies,
    String? email,
    String? preferredName,
    String? preferredLanguage,
    String? province,
    String? city,
    String? suburb,
    bool? nearbyRecommendationsEnabled,
    double? latitude,
    double? longitude,
    List<String>? interests,
    List<String>? notificationPreferences,
    bool? marketingOptIn,
  }) async {
    await ApiService.patch('/customer/profile', {
      if (firstName != null) 'firstName': firstName,
      if (lastName != null) 'lastName': lastName,
      if (age != null) 'age': age,
      if (allergies != null) 'allergies': allergies,
      if (email != null && email.isNotEmpty) 'email': email,
      if (preferredName != null) 'preferredName': preferredName,
      if (preferredLanguage != null) 'preferredLanguage': preferredLanguage,
      if (province != null) 'province': province,
      if (city != null) 'city': city,
      if (suburb != null) 'suburb': suburb,
      if (nearbyRecommendationsEnabled != null)
        'nearbyRecommendationsEnabled': nearbyRecommendationsEnabled,
      if (latitude != null && longitude != null) ...{
        'latitude': latitude,
        'longitude': longitude,
      },
      if (interests != null) 'interests': interests,
      if (notificationPreferences != null)
        'notificationPreferences': notificationPreferences,
      if (marketingOptIn != null) 'marketingOptIn': marketingOptIn,
    });
  }
}
