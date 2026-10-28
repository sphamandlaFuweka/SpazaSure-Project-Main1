import 'dart:convert';
import 'package:flutter/foundation.dart'
    show
        debugPrint,
        kIsWeb,
        kReleaseMode,
        defaultTargetPlatform,
        TargetPlatform;
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
    if (kReleaseMode) return 'https://api.spazasure.co.za/api';
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

  /// Turns a stored upload path like /uploads/x.jpg into a full URL on the same server.
  static String? resolveUploadUrl(String? url) {
    if (url == null || url.trim().isEmpty) return null;
    if (url.startsWith('http://') || url.startsWith('https://')) return url;
    final root = baseUrl.replaceFirst(RegExp(r'/api/?$'), '');
    return '$root${url.startsWith('/') ? '' : '/'}$url';
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

  /// Called when the session can't be renewed (refresh token expired or revoked), so the app
  /// can sign the person out and return to the login screen. Set in main.dart.
  static Future<void> Function()? onSessionExpired;

  static Future<bool>? _refreshing;

  /// Access tokens are short-lived (30 minutes in production). When one expires, swap the
  /// stored refresh token for a new pair once, shared by every request waiting on it.
  static Future<bool> _refreshSession() {
    return _refreshing ??= () async {
      try {
        final prefs = await SharedPreferences.getInstance();
        final refresh = prefs.getString('refresh_token');
        if (refresh == null || refresh.isEmpty) return false;
        final res = await http
            .post(
              Uri.parse('$baseUrl/supplier/auth/refresh'),
              headers: {'Content-Type': 'application/json'},
              body: jsonEncode({'refreshToken': refresh}),
            )
            .timeout(const Duration(seconds: 10));
        if (res.statusCode != 200) return false;
        final body = jsonDecode(res.body);
        final data = body is Map ? (body['data'] ?? body) : null;
        final access = data is Map ? data['accessToken'] as String? : null;
        final newRefresh = data is Map ? data['refreshToken'] as String? : null;
        if (access == null || access.isEmpty) return false;
        await prefs.setString('access_token', access);
        if (newRefresh != null && newRefresh.isNotEmpty) {
          await prefs.setString('refresh_token', newRefresh);
        }
        return true;
      } catch (_) {
        return false;
      }
    }().whenComplete(() => _refreshing = null);
  }

  /// Sends a request; on 401 renews the session once and retries. If renewal fails the
  /// person is signed out, so screens don't keep failing while looking signed in.
  static Future<Map<String, dynamic>> _send(
    String path,
    Future<http.Response> Function(Map<String, String> headers) request, {
    bool auth = true,
    Duration timeout = const Duration(seconds: 15),
    String offlineMessage = 'Unable to connect to server',
  }) async {
    try {
      var res = await request(await _headers(auth: auth)).timeout(timeout);
      final isAuthCall = path.contains('/auth/');
      if (res.statusCode == 401 && auth && !isAuthCall) {
        if (await _refreshSession()) {
          res = await request(await _headers(auth: auth)).timeout(timeout);
        }
        if (res.statusCode == 401) {
          final expired = onSessionExpired;
          if (expired != null) await expired();
          throw ApiException(
            'Your session has ended. Please sign in again.',
            401,
          );
        }
      }
      return _handle(res);
    } catch (e) {
      if (e is ApiException) rethrow;
      debugPrint('[API] $path error: $e');
      throw ApiException(offlineMessage, 0);
    }
  }

  static Future<Map<String, dynamic>> get(String path) =>
      _send(path, (h) => http.get(Uri.parse('$baseUrl$path'), headers: h));

  static Future<Map<String, dynamic>> post(
    String path,
    Map<String, dynamic> body, {
    bool auth = true,
  }) => _send(
    path,
    (h) => http.post(
      Uri.parse('$baseUrl$path'),
      headers: h,
      body: jsonEncode(body),
    ),
    auth: auth,
    timeout: const Duration(seconds: 30),
  );

  static Future<Map<String, dynamic>> delete(String path) =>
      _send(path, (h) => http.delete(Uri.parse('$baseUrl$path'), headers: h));

  static Future<Map<String, dynamic>> patch(
    String path,
    Map<String, dynamic> body,
  ) => _send(
    path,
    (h) => http.patch(
      Uri.parse('$baseUrl$path'),
      headers: h,
      body: jsonEncode(body),
    ),
  );

  static Future<Map<String, dynamic>> put(
    String path, [
    Map<String, dynamic>? body,
  ]) => _send(
    path,
    (h) => http.put(
      Uri.parse('$baseUrl$path'),
      headers: h,
      body: body != null ? jsonEncode(body) : null,
    ),
  );

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
    final msg =
        _errorMessage(body) ?? 'Request failed (HTTP ${res.statusCode}).';
    throw ApiException(msg, res.statusCode);
  }

  static String? _errorMessage(Map<String, dynamic> body) {
    for (final key in ['message', 'error', 'detail']) {
      final value = body[key];
      if (value is String && value.trim().isNotEmpty) return value.trim();
      if (value is Map<String, dynamic>) {
        final nestedMessage = _errorMessage(value);
        if (nestedMessage != null) return nestedMessage;
      }
    }

    final errors = body['errors'];
    if (errors is List) {
      final messages = errors
          .whereType<String>()
          .map((message) => message.trim())
          .where((message) => message.isNotEmpty)
          .toList();
      if (messages.isNotEmpty) return messages.join(' ');
    } else if (errors is Map) {
      final messages = <String>[];
      for (final entry in errors.entries) {
        final values = entry.value is List
            ? entry.value as List
            : [entry.value];
        for (final value in values) {
          if (value is String && value.trim().isNotEmpty) {
            messages.add('${entry.key}: ${value.trim()}');
          }
        }
      }
      if (messages.isNotEmpty) return messages.join(' ');
    }

    final title = body['title'];
    if (title is String && title.trim().isNotEmpty) return title.trim();

    final data = body['data'];
    if (data is Map<String, dynamic>) return _errorMessage(data);
    return null;
  }

  /// Upload a file using multipart form data
  static Future<Map<String, dynamic>> uploadFile(
    String path, {
    required String fieldName,
    required List<int> fileBytes,
    required String fileName,
    Map<String, String>? fields,
  }) => _send(
    path,
    (headers) async {
      // A multipart request can only be sent once, so it is rebuilt for a retry.
      final request = http.MultipartRequest('POST', Uri.parse('$baseUrl$path'));
      final auth = headers['Authorization'];
      if (auth != null) request.headers['Authorization'] = auth;
      request.files.add(
        http.MultipartFile.fromBytes(fieldName, fileBytes, filename: fileName),
      );
      if (fields != null) request.fields.addAll(fields);
      return http.Response.fromStream(await request.send());
    },
    timeout: const Duration(seconds: 45),
    offlineMessage: 'Upload failed. Check your connection and try again.',
  );

  /// A multipart form where the file is optional (e.g. feedback with or without a screenshot).
  static Future<Map<String, dynamic>> sendForm(
    String path, {
    required Map<String, String> fields,
    String fileField = 'file',
    List<int>? fileBytes,
    String? fileName,
  }) => _send(
    path,
    (headers) async {
      final request = http.MultipartRequest('POST', Uri.parse('$baseUrl$path'));
      final auth = headers['Authorization'];
      if (auth != null) request.headers['Authorization'] = auth;
      request.fields.addAll(fields);
      if (fileBytes != null && fileName != null) {
        request.files.add(
          http.MultipartFile.fromBytes(
            fileField,
            fileBytes,
            filename: fileName,
          ),
        );
      }
      return http.Response.fromStream(await request.send());
    },
    timeout: const Duration(seconds: 45),
    offlineMessage: 'Sending failed. Check your connection and try again.',
  );
}

class ApiException implements Exception {
  final String message;
  final int statusCode;
  ApiException(this.message, this.statusCode);

  @override
  String toString() => message;
}
