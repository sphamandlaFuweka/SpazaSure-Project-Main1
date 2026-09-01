import 'package:shared_preferences/shared_preferences.dart';
import 'api_service.dart';

class AuthSession {
  final String userId;
  final String shopName;
  final String phone;
  final String fullName;
  final String token;
  final String refreshToken;

  AuthSession({
    required this.userId,
    required this.shopName,
    required this.phone,
    this.fullName = '',
    required this.token,
    required this.refreshToken,
  });
}

class AuthService {
  static const _tokenKey = 'access_token';
  static const _refreshKey = 'refresh_token';
  static const _userIdKey = 'user_id';
  static const _shopNameKey = 'shop_name';
  static const _phoneKey = 'phone';
  static const _fullNameKey = 'full_name';

  // ── Step 1: Request OTP ───────────────────────────────────────────────────
  static Future<String?> sendOtp(String phone, {String purpose = 'login'}) async {
    final formatted = _formatPhone(phone);
    final res = await ApiService.post(
      '/shop/auth/send-otp?purpose=$purpose',
      {'phone': formatted},
      auth: false,
    );
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
    final res = await ApiService.post(
      '/shop/auth/login',
      {'phone': formatted, 'otp': otp},
      auth: false,
    );
    return _parseAndSave(res['data'] as Map<String, dynamic>);
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
    final res = await ApiService.post(
      '/shop/auth/register',
      {
        'phone': formatted,
        'otp': otp,
        'fullName': fullName,
        'shopName': shopName,
        'address': address,
        if (idNumber != null && idNumber.isNotEmpty) 'idNumber': idNumber,
      },
      auth: false,
    );
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
      token: token,
      refreshToken: prefs.getString(_refreshKey) ?? '',
    );
  }

  static Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
    await prefs.remove(_refreshKey);
    await prefs.remove(_userIdKey);
    await prefs.remove(_shopNameKey);
    await prefs.remove(_phoneKey);
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

  static Future<AuthSession> _parseAndSave(Map<String, dynamic> data) async {
    final session = AuthSession(
      userId: data['userId'].toString(),
      shopName: data['shopName'] ?? '',
      phone: data['phone'] ?? '',
      fullName: data['fullName'] ?? '',
      token: data['accessToken'],
      refreshToken: data['refreshToken'],
    );
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, session.token);
    await prefs.setString(_refreshKey, session.refreshToken);
    await prefs.setString(_userIdKey, session.userId);
    await prefs.setString(_shopNameKey, session.shopName);
    await prefs.setString(_phoneKey, session.phone);
    await prefs.setString(_fullNameKey, session.fullName);
    // Debug: print what was saved
    print('[AUTH] Session saved - shopName: "${session.shopName}", fullName: "${session.fullName}", phone: "${session.phone}"');
    return session;
  }
}
