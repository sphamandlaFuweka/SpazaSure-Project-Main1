import 'api_service.dart';

class CustomerReport {
  final String id;
  final String? barcode;
  final String? productName;
  final String? reportType;
  final String? shopName;
  final String description;
  final String status;
  final DateTime? createdAt;

  const CustomerReport({
    required this.id,
    this.barcode,
    this.productName,
    this.reportType,
    this.shopName,
    required this.description,
    required this.status,
    this.createdAt,
  });

  factory CustomerReport.fromJson(Map<String, dynamic> json) => CustomerReport(
    id: json['id']?.toString() ?? '',
    barcode: json['barcode']?.toString(),
    productName: json['productName']?.toString(),
    reportType: json['reportType']?.toString(),
    shopName: json['shopName']?.toString(),
    description: json['description']?.toString() ?? '',
    status: json['status']?.toString() ?? 'submitted',
    createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
  );
}

class CustomerReportService {
  static Future<List<CustomerReport>> list() async {
    final response = await ApiService.get('/customer/reports');
    final raw = response['data'];
    final items = raw is List ? raw : const <dynamic>[];
    return items
        .whereType<Map<String, dynamic>>()
        .map(CustomerReport.fromJson)
        .toList();
  }
}
