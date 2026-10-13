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
  String? get preferredName => _session?.preferredName;
  String get preferredLanguage => _session?.preferredLanguage ?? 'English';
  String? get province => _session?.province;
  String? get city => _session?.city;
  String? get suburb => _session?.suburb;
  bool get nearbyRecommendationsEnabled =>
      _session?.nearbyRecommendationsEnabled ?? false;
  double? get latitude => _session?.latitude;
  double? get longitude => _session?.longitude;
  List<String> get interests => _session?.interests ?? const [];
  List<String> get notificationPreferences =>
      _session?.notificationPreferences ?? const [];
  bool get marketingOptIn => _session?.marketingOptIn ?? false;

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
  }) async {
    _session = await AuthService.registerCustomer(
      phone: phone,
      firstName: firstName,
      lastName: lastName,
      password: password,
      age: age,
      allergies: allergies,
      email: email,
      preferredName: preferredName,
      preferredLanguage: preferredLanguage,
      province: province,
      city: city,
      suburb: suburb,
      nearbyRecommendationsEnabled: nearbyRecommendationsEnabled,
      latitude: latitude,
      longitude: longitude,
      interests: interests,
      notificationPreferences: notificationPreferences,
      marketingOptIn: marketingOptIn,
    );
    notifyListeners();
  }

  Future<List<String>> registerShop({
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
      email: email,
      registrationDetails: registrationDetails,
    );
    notifyListeners();

    // Upload documents in the background after registration
    if (documents == null || documents.isEmpty) return const [];
    return _uploadDocuments(documents);
  }

  Future<List<String>> _uploadDocuments(Map<String, dynamic> documents) async {
    final failedDocuments = <String>[];
    for (final entry in documents.entries) {
      try {
        final file = entry.value;
        if (file.bytes == null) {
          failedDocuments.add(file.name);
          continue;
        }
        await ApiService.uploadFile(
          '/shop/profile/documents',
          fieldName: 'file',
          fileBytes: file.bytes!,
          fileName: file.name,
          fields: {'docType': entry.key},
        );
      } catch (e) {
        debugPrint('[AUTH] Failed to upload doc ${entry.key}: $e');
        failedDocuments.add(entry.value.name);
      }
    }
    return failedDocuments;
  }

  Future<void> logout() async {
    await AuthService.logout();
    _session = null;
    notifyListeners();
  }
}
