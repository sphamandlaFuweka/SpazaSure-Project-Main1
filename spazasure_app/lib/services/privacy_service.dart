import 'api_service.dart';

/// A request to delete an account (POPIA s24): pending, approved, rejected or cancelled.
class DeletionRequest {
  final String status;
  final String? decisionNote;
  final DateTime? createdAt;

  const DeletionRequest({
    required this.status,
    this.decisionNote,
    this.createdAt,
  });

  bool get isPending => status == 'pending';

  factory DeletionRequest.fromJson(Map<String, dynamic> json) =>
      DeletionRequest(
        status: json['status']?.toString() ?? 'pending',
        decisionNote: json['decisionNote']?.toString(),
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
      );
}

/// POPIA rights for the signed-in shop owner or customer: a copy of their personal
/// information (s23) and asking for their account to be deleted (s24).
class PrivacyService {
  /// '/shop/profile/privacy' or '/customer/profile/privacy'.
  final String basePath;

  const PrivacyService(this.basePath);
  static const shop = PrivacyService('/shop/profile/privacy');
  static const customer = PrivacyService('/customer/profile/privacy');

  /// Whether the person has accepted the current privacy notice.
  Future<bool> hasAcceptedNotice() async {
    final res = await ApiService.get('$basePath/consent');
    return (res['data'] as Map<String, dynamic>?)?['accepted'] == true;
  }

  Future<void> acceptNotice() => ApiService.post('$basePath/consent', {});

  Future<Map<String, dynamic>> myData() async {
    final res = await ApiService.get('$basePath/my-data');
    return res['data'] as Map<String, dynamic>? ?? {};
  }

  Future<DeletionRequest?> deletionRequest() async {
    final res = await ApiService.get('$basePath/deletion-request');
    final data = res['data'];
    return data is Map<String, dynamic> ? DeletionRequest.fromJson(data) : null;
  }

  /// Returns the server's message, e.g. what still has to be settled first.
  Future<String> requestDeletion(String reason) async {
    final res = await ApiService.post('$basePath/deletion-request', {
      'reason': reason,
    });
    return res['message']?.toString() ?? 'We received your request.';
  }

  Future<void> cancelDeletion() =>
      ApiService.delete('$basePath/deletion-request');
}
