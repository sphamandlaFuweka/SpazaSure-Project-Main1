import 'package:intl/intl.dart';
import 'package:spazasure_app/services/api_service.dart';

/// Shop Academy: the shop's business details, stock book, cash book, reports, lessons and
/// Business Record PDF.
class ShopAcademyService {
  static const _base = '/shop/academy';

  static String day(DateTime d) => DateFormat('yyyy-MM-dd').format(d);
  static String month(DateTime d) => DateFormat('yyyy-MM').format(d);

  static Future<Map<String, dynamic>> _data(
    Future<Map<String, dynamic>> call,
  ) async {
    final res = await call;
    return (res['data'] as Map?)?.cast<String, dynamic>() ?? {};
  }

  static Future<Map<String, dynamic>> overview() =>
      _data(ApiService.get(_base));

  // ── Business details ──

  static Future<Map<String, dynamic>> profile() =>
      _data(ApiService.get('$_base/profile'));

  static Future<Map<String, dynamic>> saveProfile(
    Map<String, dynamic> body,
  ) async {
    final res = await ApiService.put('$_base/profile', body);
    return (res['data'] as Map?)?.cast<String, dynamic>() ?? {};
  }

  // ── Stock book ──

  static Future<Map<String, dynamic>> stock() =>
      _data(ApiService.get('$_base/stock'));

  static Future<void> addItem(Map<String, dynamic> body) =>
      ApiService.post('$_base/stock', body);

  static Future<void> updateItem(String id, Map<String, dynamic> body) =>
      ApiService.put('$_base/stock/$id', body);

  static Future<void> removeItem(String id) =>
      ApiService.delete('$_base/stock/$id');

  /// Records stock received, sold, lost or counted. Returns the server's message.
  static Future<String> addMovement(
    String id, {
    required String type,
    required int quantity,
    double? unitCost,
    double? unitPrice,
    String? note,
    bool addToCashBook = false,
  }) async {
    final res = await ApiService.post('$_base/stock/$id/movements', {
      'type': type,
      'quantity': quantity,
      'unitCost': ?unitCost,
      'unitPrice': ?unitPrice,
      if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      'occurredOn': day(DateTime.now()),
      'addToCashBook': addToCashBook,
    });
    return res['message'] as String? ?? 'Stock book updated.';
  }

  static Future<List<Map<String, dynamic>>> movements(String id) async {
    final res = await ApiService.get('$_base/stock/$id/movements');
    return ((res['data'] as List?) ?? []).cast<Map<String, dynamic>>();
  }

  /// Books delivered SpazaSure orders into the stock book. Returns the server's message.
  static Future<String> importOrders() async {
    final res = await ApiService.post('$_base/stock/import-orders', {});
    return res['message'] as String? ?? 'Done.';
  }

  // ── Cash book ──

  static Future<Map<String, dynamic>> cashBook(DateTime forMonth) =>
      _data(ApiService.get('$_base/cashbook?month=${month(forMonth)}'));

  static Future<void> addCashEntry({
    required String direction,
    required String category,
    required double amount,
    String? note,
    required DateTime occurredOn,
  }) => ApiService.post('$_base/cashbook', {
    'direction': direction,
    'category': category,
    'amount': amount,
    if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
    'occurredOn': day(occurredOn),
  });

  static Future<void> deleteCashEntry(String id) =>
      ApiService.delete('$_base/cashbook/$id');

  // ── Reports and lessons ──

  static Future<Map<String, dynamic>> report({int months = 6}) =>
      _data(ApiService.get('$_base/report?months=$months'));

  static Future<List<Map<String, dynamic>>> lessons() async {
    final res = await ApiService.get('$_base/lessons');
    return ((res['data'] as List?) ?? []).cast<Map<String, dynamic>>();
  }

  static Future<Map<String, dynamic>> lesson(String key) =>
      _data(ApiService.get('$_base/lessons/$key'));

  static Future<Map<String, dynamic>> submitQuiz(
    String key,
    List<int> answers,
  ) => _data(ApiService.post('$_base/lessons/$key/quiz', {'answers': answers}));

  /// A link to the Business Record PDF, valid for 30 minutes.
  static Future<String> recordLink() async {
    final data = await _data(ApiService.get('$_base/record-link'));
    return '${ApiService.baseUrl}${data['path']}';
  }

  // ── Labels ──

  static const categoryLabels = {
    'cash_sales': 'Cash sales',
    'card_sales': 'Card sales',
    'airtime_electricity': 'Airtime & electricity sales',
    'other_income': 'Other income',
    'stock_purchase': 'Stock bought',
    'rent': 'Rent',
    'electricity': 'Electricity',
    'transport': 'Transport',
    'wages': 'Wages',
    'airtime_data': 'Airtime & data',
    'repairs': 'Repairs',
    'licences_fees': 'Licences & fees',
    'owner_salary': 'My salary',
    'other_expense': 'Other expense',
  };

  static const movementLabels = {
    'received': 'Stock received',
    'sold': 'Sold',
    'spoiled': 'Spoiled / expired',
    'stolen': 'Stolen / missing',
    'own_use': 'Own use',
    'count': 'Stock count',
  };

  static String categoryLabel(String key) => categoryLabels[key] ?? key;

  static String rand(num? value) => NumberFormat.currency(
    locale: 'en_ZA',
    symbol: 'R ',
    decimalDigits: 2,
  ).format(value ?? 0);
}
