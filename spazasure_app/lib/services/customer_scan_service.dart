import 'api_service.dart';

class CustomerScanEvent {
  final String id;
  final String code;
  final String? productId;
  final String? productName;
  final String source;
  final int pointsAwarded;
  final DateTime? createdAt;

  const CustomerScanEvent({
    required this.id,
    required this.code,
    this.productId,
    this.productName,
    required this.source,
    required this.pointsAwarded,
    this.createdAt,
  });

  factory CustomerScanEvent.fromJson(Map<String, dynamic> json) =>
      CustomerScanEvent(
        id: json['id']?.toString() ?? '',
        code: json['code']?.toString() ?? '',
        productId: json['productId']?.toString(),
        productName: json['productName']?.toString(),
        source: json['source']?.toString() ?? 'unknown',
        pointsAwarded: (json['pointsAwarded'] as num?)?.toInt() ?? 0,
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
      );
}

class CustomerScanService {
  static Future<List<CustomerScanEvent>> list() async {
    final res = await ApiService.get('/customer/scans');
    final raw = res['data'];
    final items = raw is List ? raw : const <dynamic>[];
    return items
        .whereType<Map<String, dynamic>>()
        .map(CustomerScanEvent.fromJson)
        .toList();
  }
}
