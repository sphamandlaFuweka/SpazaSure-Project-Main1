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

  // ── Step 1: Request OTP ───────────────────────────────────────────────────
  static Future<String?> sendOtp(
    String phone, {
    String purpose = 'login',
  }) async {
    final formatted = _formatPhone(phone);
    final res = await ApiService.post('/shop/auth/send-otp?purpose=$purpose', {
      'phone': formatted,
    }, auth: false);
    // In QA/dev, backend returns the OTP for auto-fill
    final data = res['data'];
    if (data is Map<String, dynamic> && data.containsKey('otp')) {
      return data['otp'] as String;
    }
    return null;
  }

  // ── Step 2a: Verify OTP + Login ───────────────────────────────────────────
  static Future<AuthSession> verifyLogin(String phone, String otp) async {
    final formatted = _formatPhone(phone);
    final res = await ApiService.post('/shop/auth/login', {
      'phone': formatted,
      'otp': otp,
    }, auth: false);
    return _parseAndSave(res['data'] as Map<String, dynamic>);
  }

  static Future<String?> sendCustomerOtp(
    String phone, {
    String purpose = 'login',
  }) async {
    final res = await ApiService.post(
      '/customer/auth/send-otp?purpose=$purpose',
      {'phone': _formatPhone(phone)},
      auth: false,
    );
    final data = res['data'];
    return data is Map<String, dynamic> ? data['otp']?.toString() : null;
  }

  static Future<AuthSession> verifyCustomerLogin(
    String phone,
    String otp,
  ) async {
    final res = await ApiService.post('/customer/auth/login', {
      'phone': _formatPhone(phone),
      'otp': otp,
    }, auth: false);
    return _parseAndSave(res['data'] as Map<String, dynamic>, role: 'customer');
  }

  static Future<AuthSession> verifyCustomerRegister({
    required String phone,
    required String otp,
    required String firstName,
    required String lastName,
    required String email,
    required String password,
    int? age,
    List<String>? allergies,
  }) async {
    final res = await ApiService.post('/customer/auth/register', {
      'phone': _formatPhone(phone),
      'otp': otp,
      'firstName': firstName,
      'lastName': lastName,
      'email': email,
      'password': password,
      if (age != null) 'age': age,
      'allergies': allergies ?? const <String>[],
    }, auth: false);
    return _parseAndSave(res['data'] as Map<String, dynamic>, role: 'customer');
  }

  // ── Step 2b: Verify OTP + Register ───────────────────────────────────────
  static Future<AuthSession> verifyRegister({
    required String phone,
    required String otp,
    required String fullName,
    required String shopName,
    required String address,
    String? idNumber,
  }) async {
    final formatted = _formatPhone(phone);
    final res = await ApiService.post('/shop/auth/register', {
      'phone': formatted,
      'otp': otp,
      'fullName': fullName,
      'shopName': shopName,
      'address': address,
      if (idNumber != null && idNumber.isNotEmpty) 'idNumber': idNumber,
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
      token: token,
      refreshToken: prefs.getString(_refreshKey) ?? '',
      role: prefs.getString('user_role') ?? 'spaza_owner',
    );
  }

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
  }

  static Future<bool> isLoggedIn() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_tokenKey) != null;
  }

  // ── Private ───────────────────────────────────────────────────────────────
  static String _formatPhone(String phone) {
    final digits = phone.replaceAll(RegExp(r'\s+'), '');
    if (digits.startsWith('+27')) return digits;
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
    await prefs.setString('user_role', session.role);
    return session;
  }
}
