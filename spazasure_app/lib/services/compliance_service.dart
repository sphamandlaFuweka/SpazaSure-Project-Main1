import 'api_service.dart';

class ComplianceDoc {
  final String id;
  final String docType;
  final String? docUrl;
  final String status;
  final String? expiryDate;
  final String? rejectionNote;
  final String? createdAt;

  ComplianceDoc({
    required this.id,
    required this.docType,
    this.docUrl,
    required this.status,
    this.expiryDate,
    this.rejectionNote,
    this.createdAt,
  });

  factory ComplianceDoc.fromJson(Map<String, dynamic> json) {
    return ComplianceDoc(
      id: json['id']?.toString() ?? '',
      docType: json['docType'] ?? '',
      docUrl: json['docUrl'],
      status: json['status'] ?? 'missing',
      expiryDate: json['expiryDate']?.toString(),
      rejectionNote: json['rejectionNote'],
      createdAt: json['createdAt']?.toString(),
    );
  }
}

class IdentityCheck {
  /// Didit status: Not Started, In Progress, In Review, Approved, Declined, Abandoned, Expired...
  final String status;
  final bool verified;
  final bool canStart;

  const IdentityCheck({required this.status, required this.verified, required this.canStart});

  factory IdentityCheck.fromJson(Map<String, dynamic> json) => IdentityCheck(
        status: json['status']?.toString() ?? 'Not Started',
        verified: json['verified'] == true,
        canStart: json['canStart'] != false,
      );
}

class ComplianceService {
  /// Get all documents for the logged-in shop owner
  static Future<List<ComplianceDoc>> getDocuments() async {
    final res = await ApiService.get('/shop/profile/documents');
    final data = res['data'];
    if (data is List) {
      return data.map((d) => ComplianceDoc.fromJson(d as Map<String, dynamic>)).toList();
    }
    return [];
  }

  /// Upload a document
  static Future<Map<String, dynamic>> uploadDocument({
    required String docType,
    required List<int> fileBytes,
    required String fileName,
  }) async {
    final res = await ApiService.uploadFile(
      '/shop/profile/documents',
      fieldName: 'file',
      fileBytes: fileBytes,
      fileName: fileName,
      fields: {'docType': docType},
    );
    return res['data'] as Map<String, dynamic>? ?? {};
  }

  /// The owner's identity check (ID document, selfie, Home Affairs and sanctions, run by Didit).
  static Future<IdentityCheck> getIdentityCheck() async {
    final res = await ApiService.get('/compliance/identity/shop');
    return IdentityCheck.fromJson(res['data'] as Map<String, dynamic>? ?? {});
  }

  /// Starts (or resumes) the identity check and returns the Didit link to open.
  static Future<String> startIdentityCheck() async {
    final res = await ApiService.post('/compliance/identity/shop/session', {});
    final url = (res['data'] as Map<String, dynamic>?)?['url']?.toString();
    if (url == null || url.isEmpty) throw ApiException('Could not start the identity check.', 0);
    return url;
  }
}
