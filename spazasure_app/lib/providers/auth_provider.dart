import 'package:flutter/material.dart';
import '../services/auth_service.dart';
import '../services/api_service.dart';

class AuthProvider extends ChangeNotifier {
  AuthSession? _session;
  bool _initialized = false;

  AuthSession? get session => _session;
  bool get isLoggedIn => _session != null;
  bool get initialized => _initialized;
  String get shopName => _session?.shopName ?? '';
  String get fullName => _session?.fullName ?? '';
  String get phone => _session?.phone ?? '';
  String get firstName => _session?.firstName ?? '';
  String get lastName => _session?.lastName ?? '';
  String get email => _session?.email ?? '';
  int? get age => _session?.age;
  List<String> get allergies => _session?.allergies ?? const [];

  Future<void> init() async {
    _session = await AuthService.getSession();
    _initialized = true;
    notifyListeners();
  }

  // Step 1 — send OTP (login flow)
  Future<String?> sendLoginOtp(String phone) async {
    return await AuthService.sendOtp(phone, purpose: 'login');
  }

  // Step 1 — send OTP (registration flow)
  Future<String?> sendRegisterOtp(String phone) async {
    return await AuthService.sendOtp(phone, purpose: 'registration');
  }

  Future<String?> sendCustomerOtp(
    String phone, {
    String purpose = 'login',
  }) async {
    return await AuthService.sendCustomerOtp(phone, purpose: purpose);
  }

  // Step 2 — verify OTP + login
  Future<void> verifyLogin(String phone, String otp) async {
    _session = await AuthService.verifyLogin(phone, otp);
    notifyListeners();
  }

  Future<void> verifyCustomerLogin(String phone, String otp) async {
    _session = await AuthService.verifyCustomerLogin(phone, otp);
    notifyListeners();
  }

  Future<void> verifyCustomerRegister({
    required String phone,
    required String otp,
    required String firstName,
    required String lastName,
    required String email,
    required String password,
    int? age,
    List<String>? allergies,
  }) async {
    _session = await AuthService.verifyCustomerRegister(
      phone: phone,
      otp: otp,
      firstName: firstName,
      lastName: lastName,
      email: email,
      password: password,
      age: age,
      allergies: allergies,
    );
    notifyListeners();
  }

  // Step 2 — verify OTP + register
  Future<void> verifyRegister({
    required String phone,
    required String otp,
    required String fullName,
    required String shopName,
    required String address,
    String? idNumber,
    Map<String, dynamic>? documents,
  }) async {
    _session = await AuthService.verifyRegister(
      phone: phone,
      otp: otp,
      fullName: fullName,
      shopName: shopName,
      address: address,
      idNumber: idNumber,
    );
    notifyListeners();

    // Upload documents in the background after registration
    if (documents != null && documents.isNotEmpty) {
      _uploadDocuments(documents);
    }
  }

  Future<void> _uploadDocuments(Map<String, dynamic> documents) async {
    for (final entry in documents.entries) {
      try {
        final file = entry.value;
        if (file.bytes != null) {
          await ApiService.uploadFile(
            '/shop/profile/documents',
            fieldName: 'file',
            fileBytes: file.bytes!,
            fileName: file.name,
            fields: {'docType': entry.key},
          );
        }
      } catch (e) {
        print('[AUTH] Failed to upload doc ${entry.key}: $e');
      }
    }
  }

  Future<void> logout() async {
    await AuthService.logout();
    _session = null;
    notifyListeners();
  }
}
