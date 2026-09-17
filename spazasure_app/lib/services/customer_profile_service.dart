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
  final String? joinedAt;

  const CustomerProfile({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.fullName,
    required this.email,
    required this.phone,
    this.age,
    this.allergies = const [],
    this.joinedAt,
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
        joinedAt: json['joinedAt']?.toString(),
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
  }) async {
    await ApiService.patch('/customer/profile', {
      if (firstName != null) 'firstName': firstName,
      if (lastName != null) 'lastName': lastName,
      if (age != null) 'age': age,
      if (allergies != null) 'allergies': allergies,
    });
  }
}
