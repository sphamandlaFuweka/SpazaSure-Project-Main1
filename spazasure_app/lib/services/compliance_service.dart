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
}
