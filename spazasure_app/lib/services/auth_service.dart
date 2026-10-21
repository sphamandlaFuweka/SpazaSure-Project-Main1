import 'package:shared_preferences/shared_preferences.dart';
import 'api_service.dart';

class AuthSession {
  final String userId;
  final String shopName;
  final String phone;
  final String fullName;
  final String firstName;
  final String lastName;
  final String email;
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
  final String token;
  final String refreshToken;
  final String role;

  AuthSession({
    required this.userId,
    required this.shopName,
    required this.phone,
    this.fullName = '',
    this.firstName = '',
    this.lastName = '',
    this.email = '',
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
    required this.token,
    required this.refreshToken,
    this.role = 'spaza_owner',
  });
}

class AuthService {
  static const _tokenKey = 'access_token';
  static const _refreshKey = 'refresh_token';
  static const _userIdKey = 'user_id';
  static const _shopNameKey = 'shop_name';
  static const _phoneKey = 'phone';
  static const _fullNameKey = 'full_name';
  static const _firstNameKey = 'first_name';
  static const _lastNameKey = 'last_name';
  static const _emailKey = 'email';
  static const _ageKey = 'age';
  static const _allergiesKey = 'allergies';
  static const _preferredNameKey = 'customer_preferred_name';
  static const _preferredLanguageKey = 'customer_preferred_language';
  static const _provinceKey = 'customer_province';
  static const _cityKey = 'customer_city';
  static const _suburbKey = 'customer_suburb';
  static const _nearbyRecommendationsKey = 'customer_nearby_recommendations';
  static const _latitudeKey = 'customer_latitude';
  static const _longitudeKey = 'customer_longitude';
  static const _interestsKey = 'customer_interests';
  static const _notificationPreferencesKey =
      'customer_notification_preferences';
  static const _marketingOptInKey = 'customer_marketing_opt_in';

  static Future<AuthSession> loginShop(String phone, String password) async {
    final formatted = _formatPhone(phone);
    final res = await ApiService.post('/shop/auth/login', {
      'phone': formatted,
      'password': password,
    }, auth: false);
    return _parseAndSave(res['data'] as Map<String, dynamic>);
  }

  static Future<AuthSession> loginCustomer(
    String phone,
    String password,
  ) async {
    final res = await ApiService.post('/customer/auth/login', {
      'phone': _formatPhone(phone),
      'password': password,
    }, auth: false);
    return _parseAndSave(res['data'] as Map<String, dynamic>, role: 'customer');
  }

  static Future<AuthSession> registerCustomer({
    required String phone,
    required String firstName,
    required String lastName,
    required String password,
    int? age,
    List<String>? allergies,
    String? email,
    String? preferredName,
    String? preferredLanguage,
    String? province,
    String? city,
    String? suburb,
    bool nearbyRecommendationsEnabled = false,
    double? latitude,
    double? longitude,
    List<String>? interests,
    List<String>? notificationPreferences,
    bool marketingOptIn = false,
    bool acceptedPrivacyNotice = false,
  }) async {
    final res = await ApiService.post('/customer/auth/register', {
      'phone': _formatPhone(phone),
      'firstName': firstName,
      'lastName': lastName,
      'password': password,
      if (age != null) 'age': age,
      if (email != null && email.isNotEmpty) 'email': email,
      if (preferredName != null && preferredName.isNotEmpty)
        'preferredName': preferredName,
      if (preferredLanguage != null) 'preferredLanguage': preferredLanguage,
      if (province != null) 'province': province,
      if (city != null) 'city': city,
      if (suburb != null) 'suburb': suburb,
      'nearbyRecommendationsEnabled': nearbyRecommendationsEnabled,
      if (latitude != null && longitude != null) ...{
        'latitude': latitude,
        'longitude': longitude,
      },
      'allergies': allergies ?? const <String>[],
      'interests': interests ?? const <String>[],
      'notificationPreferences': notificationPreferences ?? const <String>[],
      'marketingOptIn': marketingOptIn,
      'acceptedPrivacyNotice': acceptedPrivacyNotice,
    }, auth: false);
    return _parseAndSave(res['data'] as Map<String, dynamic>, role: 'customer');
  }

  static Future<AuthSession> registerShop({
    required String phone,
    required String password,
    required String fullName,
    required String shopName,
    required String address,
    String? city,
    String? province,
    String? postalCode,
    double? latitude,
    double? longitude,
    String? idNumber,
    String? email,
    Map<String, dynamic>? registrationDetails,
    bool acceptedPrivacyNotice = false,
  }) async {
    final formatted = _formatPhone(phone);
    final res = await ApiService.post('/shop/auth/register', {
      'phone': formatted,
      'password': password,
      'fullName': fullName,
      'shopName': shopName,
      'address': address,
      if (city != null && city.isNotEmpty) 'city': city,
      if (province != null && province.isNotEmpty) 'province': province,
      if (postalCode != null && postalCode.isNotEmpty) 'postalCode': postalCode,
      if (latitude != null && longitude != null) ...{
        'latitude': latitude,
        'longitude': longitude,
      },
      if (idNumber != null && idNumber.isNotEmpty) 'idNumber': idNumber,
      if (email != null && email.isNotEmpty) 'email': email,
      if (registrationDetails != null)
        'registrationDetails': registrationDetails,
      'acceptedPrivacyNotice': acceptedPrivacyNotice,
    }, auth: false);
    return _parseAndSave(res['data'] as Map<String, dynamic>);
  }

  // ── Session helpers ───────────────────────────────────────────────────────
  static Future<AuthSession?> getSession() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString(_tokenKey);
    if (token == null) return null;
    return AuthSession(
      userId: prefs.getString(_userIdKey) ?? '',
      shopName: prefs.getString(_shopNameKey) ?? '',
      phone: prefs.getString(_phoneKey) ?? '',
      fullName: prefs.getString(_fullNameKey) ?? '',
      firstName: prefs.getString(_firstNameKey) ?? '',
      lastName: prefs.getString(_lastNameKey) ?? '',
      email: prefs.getString(_emailKey) ?? '',
      age: prefs.getInt(_ageKey),
      allergies: prefs.getStringList(_allergiesKey) ?? const [],
      preferredName: prefs.getString(_preferredNameKey),
      preferredLanguage: prefs.getString(_preferredLanguageKey) ?? 'English',
      province: prefs.getString(_provinceKey),
      city: prefs.getString(_cityKey),
      suburb: prefs.getString(_suburbKey),
      nearbyRecommendationsEnabled:
          prefs.getBool(_nearbyRecommendationsKey) ?? false,
      latitude: prefs.getDouble(_latitudeKey),
      longitude: prefs.getDouble(_longitudeKey),
      interests: prefs.getStringList(_interestsKey) ?? const [],
      notificationPreferences:
          prefs.getStringList(_notificationPreferencesKey) ?? const [],
      marketingOptIn: prefs.getBool(_marketingOptInKey) ?? false,
      token: token,
      refreshToken: prefs.getString(_refreshKey) ?? '',
      role: prefs.getString('user_role') ?? 'spaza_owner',
    );
  }

  /// Customers have their own shell; everyone else lands on the retailer home.
  static Future<String> homeRoute() async =>
      (await getSession())?.role == 'customer' ? '/customer-home' : '/home';

  static Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
    await prefs.remove(_refreshKey);
    await prefs.remove(_userIdKey);
    await prefs.remove(_shopNameKey);
    await prefs.remove(_phoneKey);
    await prefs.remove(_firstNameKey);
    await prefs.remove(_lastNameKey);
    await prefs.remove(_emailKey);
    await prefs.remove(_ageKey);
    await prefs.remove(_allergiesKey);
    await prefs.remove(_preferredNameKey);
    await prefs.remove(_preferredLanguageKey);
    await prefs.remove(_provinceKey);
    await prefs.remove(_cityKey);
    await prefs.remove(_suburbKey);
    await prefs.remove(_nearbyRecommendationsKey);
    await prefs.remove(_latitudeKey);
    await prefs.remove(_longitudeKey);
    await prefs.remove(_interestsKey);
    await prefs.remove(_notificationPreferencesKey);
    await prefs.remove(_marketingOptInKey);
  }

  static Future<bool> isLoggedIn() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_tokenKey) != null;
  }

  // ── Private ───────────────────────────────────────────────────────────────
  static String _formatPhone(String phone) {
    final digits = phone.replaceAll(RegExp(r'\D'), '');
    if (digits.startsWith('27')) return '+$digits';
    if (digits.startsWith('0')) return '+27${digits.substring(1)}';
    return '+27$digits';
  }

  static Future<AuthSession> _parseAndSave(
    Map<String, dynamic> data, {
    String role = 'spaza_owner',
  }) async {
    final session = AuthSession(
      userId: data['userId'].toString(),
      shopName: data['shopName'] ?? '',
      phone: data['phone'] ?? '',
      fullName: data['fullName'] ?? '',
      firstName: data['firstName'] ?? '',
      lastName: data['lastName'] ?? '',
      email: data['email'] ?? '',
      age: (data['age'] as num?)?.toInt(),
      allergies:
          (data['allergies'] as List?)?.map((a) => a.toString()).toList() ??
          const [],
      preferredName: data['preferredName']?.toString(),
      preferredLanguage: data['preferredLanguage']?.toString() ?? 'English',
      province: data['province']?.toString(),
      city: data['city']?.toString(),
      suburb: data['suburb']?.toString(),
      nearbyRecommendationsEnabled:
          data['nearbyRecommendationsEnabled'] == true,
      latitude: (data['latitude'] as num?)?.toDouble(),
      longitude: (data['longitude'] as num?)?.toDouble(),
      interests:
          (data['interests'] as List?)?.map((v) => v.toString()).toList() ??
          const [],
      notificationPreferences:
          (data['notificationPreferences'] as List?)
              ?.map((v) => v.toString())
              .toList() ??
          const [],
      marketingOptIn: data['marketingOptIn'] == true,
      token: data['accessToken'],
      refreshToken: data['refreshToken'],
      role: role,
    );
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, session.token);
    await prefs.setString(_refreshKey, session.refreshToken);
    await prefs.setString(_userIdKey, session.userId);
    await prefs.setString(_shopNameKey, session.shopName);
    await prefs.setString(_phoneKey, session.phone);
    await prefs.setString(_fullNameKey, session.fullName);
    await prefs.setString(_firstNameKey, session.firstName);
    await prefs.setString(_lastNameKey, session.lastName);
    await prefs.setString(_emailKey, session.email);
    if (session.age != null) await prefs.setInt(_ageKey, session.age!);
    await prefs.setStringList(_allergiesKey, session.allergies);
    await prefs.setString(_preferredNameKey, session.preferredName ?? '');
    await prefs.setString(_preferredLanguageKey, session.preferredLanguage);
    await prefs.setString(_provinceKey, session.province ?? '');
    await prefs.setString(_cityKey, session.city ?? '');
    await prefs.setString(_suburbKey, session.suburb ?? '');
    await prefs.setBool(
      _nearbyRecommendationsKey,
      session.nearbyRecommendationsEnabled,
    );
    if (session.latitude != null) {
      await prefs.setDouble(_latitudeKey, session.latitude!);
    }
    if (session.longitude != null) {
      await prefs.setDouble(_longitudeKey, session.longitude!);
    }
    await prefs.setStringList(_interestsKey, session.interests);
    await prefs.setStringList(
      _notificationPreferencesKey,
      session.notificationPreferences,
    );
    await prefs.setBool(_marketingOptInKey, session.marketingOptIn);
    await prefs.setString('user_role', session.role);
    return session;
  }
}
