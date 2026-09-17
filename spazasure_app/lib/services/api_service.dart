import 'dart:convert';
import 'package:flutter/foundation.dart'
    show debugPrint, kIsWeb, defaultTargetPlatform, TargetPlatform;
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class ApiService {
  // Override at build/run time, e.g.:
  //   flutter run --dart-define=API_URL=http://localhost:5181/api        (iOS sim / web / desktop)
  //   flutter run --dart-define=API_URL=http://10.0.2.2:5181/api         (Android emulator)
  //   flutter run --dart-define=API_URL=http://<your-lan-ip>:5181/api    (physical device)
  //   flutter run --dart-define=API_URL=https://api.spazasure.co.za/api    (Hetzner production server)
  static const _override = String.fromEnvironment('API_URL');

  static String get baseUrl {
    if (_override.isNotEmpty) return _override;
    if (kIsWeb) return 'http://localhost:5181/api';
    // Android emulator can't reach the host machine via localhost — 10.0.2.2
    // is the special alias Android's emulator provides for that. Uses
    // defaultTargetPlatform (not dart:io Platform) so this file still
    // compiles for web, where dart:io isn't available at all.
    if (defaultTargetPlatform == TargetPlatform.android) {
      return 'http://10.0.2.2:5181/api';
    }
    return 'http://localhost:5181/api';
  }

  static Future<String?> _getToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString('access_token');
  }

  static Future<Map<String, String>> _headers({bool auth = true}) async {
    final headers = {'Content-Type': 'application/json'};
    if (auth) {
      final token = await _getToken();
      if (token != null) headers['Authorization'] = 'Bearer $token';
    }
    return headers;
  }

  static Future<Map<String, dynamic>> get(String path) async {
    try {
      final res = await http
          .get(Uri.parse('$baseUrl$path'), headers: await _headers())
          .timeout(const Duration(seconds: 10));
      return _handle(res);
    } catch (e) {
      if (e is ApiException) rethrow;
      throw ApiException('Unable to connect to server', 0);
    }
  }

  static Future<Map<String, dynamic>> post(
    String path,
    Map<String, dynamic> body, {
    bool auth = true,
  }) async {
    try {
      final url = '$baseUrl$path';
      debugPrint('[API] POST $url');
      final res = await http
          .post(
            Uri.parse(url),
            headers: await _headers(auth: auth),
            body: jsonEncode(body),
          )
          .timeout(const Duration(seconds: 30));
      return _handle(res);
    } catch (e) {
      if (e is ApiException) rethrow;
      debugPrint('[API] POST error: $e');
      throw ApiException('Unable to connect to server', 0);
    }
  }

  static Future<Map<String, dynamic>> patch(
    String path,
    Map<String, dynamic> body,
  ) async {
    try {
      final res = await http
          .patch(
            Uri.parse('$baseUrl$path'),
            headers: await _headers(),
            body: jsonEncode(body),
          )
          .timeout(const Duration(seconds: 10));
      return _handle(res);
    } catch (e) {
      if (e is ApiException) rethrow;
      throw ApiException('Unable to connect to server', 0);
    }
  }

  static Future<Map<String, dynamic>> put(
    String path, [
    Map<String, dynamic>? body,
  ]) async {
    try {
      final res = await http
          .put(
            Uri.parse('$baseUrl$path'),
            headers: await _headers(),
            body: body != null ? jsonEncode(body) : null,
          )
          .timeout(const Duration(seconds: 10));
      return _handle(res);
    } catch (e) {
      if (e is ApiException) rethrow;
      throw ApiException('Unable to connect to server', 0);
    }
  }

  static Map<String, dynamic> _handle(http.Response res) {
    if (res.statusCode >= 300 && res.statusCode < 400) {
      throw ApiException(
        'API redirected the request (${res.statusCode}). Build the app with the current API URL.',
        res.statusCode,
      );
    }
    if (res.body.isEmpty) {
      if (res.statusCode >= 200 && res.statusCode < 300) {
        return {'success': true};
      }
      throw ApiException(
        'Server returned empty response (${res.statusCode})',
        res.statusCode,
      );
    }
    Map<String, dynamic> body;
    try {
      body = jsonDecode(res.body) as Map<String, dynamic>;
    } catch (_) {
      throw ApiException(
        'Server returned ${res.statusCode} with a non-JSON response.',
        res.statusCode,
      );
    }
    if (res.statusCode >= 200 && res.statusCode < 300) return body;
    final msg = body['message'] ?? body['error'] ?? 'Something went wrong';
    throw ApiException(msg.toString(), res.statusCode);
  }

  /// Upload a file using multipart form data
  static Future<Map<String, dynamic>> uploadFile(
    String path, {
    required String fieldName,
    required List<int> fileBytes,
    required String fileName,
    Map<String, String>? fields,
  }) async {
    try {
      final uri = Uri.parse('$baseUrl$path');
      final request = http.MultipartRequest('POST', uri);

      // Add auth header
      final token = await _getToken();
      if (token != null) request.headers['Authorization'] = 'Bearer $token';

      // Add file
      request.files.add(
        http.MultipartFile.fromBytes(fieldName, fileBytes, filename: fileName),
      );

      // Add extra fields
      if (fields != null) request.fields.addAll(fields);

      final streamed = await request.send().timeout(
        const Duration(seconds: 30),
      );
      final res = await http.Response.fromStream(streamed);
      return _handle(res);
    } catch (e) {
      if (e is ApiException) rethrow;
      throw ApiException('Upload failed: $e', 0);
    }
  }
}

class ApiException implements Exception {
  final String message;
  final int statusCode;
  ApiException(this.message, this.statusCode);

  @override
  String toString() => message;
}
