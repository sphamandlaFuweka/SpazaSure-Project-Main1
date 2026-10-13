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

  Future<void> loginShop(String phone, String password) async {
    _session = await AuthService.loginShop(phone, password);
    notifyListeners();
  }

  Future<void> loginCustomer(String phone, String password) async {
    _session = await AuthService.loginCustomer(phone, password);
    notifyListeners();
  }

  Future<void> registerCustomer({
    required String phone,
    required String firstName,
    required String lastName,
    required String password,
    int? age,
    List<String>? allergies,
  }) async {
    _session = await AuthService.registerCustomer(
      phone: phone,
      firstName: firstName,
      lastName: lastName,
      password: password,
      age: age,
      allergies: allergies,
    );
    notifyListeners();
  }

  Future<void> registerShop({
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
    Map<String, dynamic>? documents,
  }) async {
    _session = await AuthService.registerShop(
      phone: phone,
      password: password,
      fullName: fullName,
      shopName: shopName,
      address: address,
      city: city,
      province: province,
      postalCode: postalCode,
      latitude: latitude,
      longitude: longitude,
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
