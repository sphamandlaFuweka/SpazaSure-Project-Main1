import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/auth_service.dart';
import 'package:spazasure_app/services/product_service.dart';

http.Response _json(Object body, [int status = 200]) =>
    http.Response(jsonEncode(body), status);

http.Response _tokens() => _json({
  'data': {
    'accessToken': 'renewed-access',
    'refreshToken': 'renewed-refresh',
  },
});

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    SharedPreferences.setMockInitialValues({
      'access_token': 'expired-access',
      'refresh_token': 'saved-refresh',
      'user_id': 'customer-1',
      'user_role': 'customer',
      'first_name': 'Test',
    });
  });

  test('loads products after refreshing without logout', () async {
    var productRequests = 0;
    final client = MockClient((request) async {
      if (request.url.path.endsWith('/supplier/auth/refresh')) {
        expect(request.method, 'POST');
        expect(request.headers.containsKey('Authorization'), isFalse);
        expect(jsonDecode(request.body), {'refreshToken': 'saved-refresh'});
        return _tokens();
      }
      productRequests++;
      expect(request.url.path, '/api/shop/marketplace/products');
      if (productRequests == 1) {
        expect(
          request.headers['Authorization']?.split(' ').last,
          'expired-access',
        );
        return _json({'message': 'Expired'}, 401);
      }
      expect(
        request.headers['Authorization']?.split(' ').last,
        'renewed-access',
      );
      return _json({
        'data': {
          'items': [
            {'id': 'product-1', 'name': 'Maize meal', 'price': 25},
          ],
        },
      });
    });

    final products = await http.runWithClient(
      () => ProductService.getProducts(),
      () => client,
    );
    expect(products.single.name, 'Maize meal');
    expect(productRequests, 2);
    final prefs = await SharedPreferences.getInstance();
    expect(prefs.getString('access_token'), 'renewed-access');
    expect(prefs.getString('refresh_token'), 'renewed-refresh');
    expect(prefs.getString('user_role'), 'customer');
    expect(prefs.getString('first_name'), 'Test');
  });

  test('concurrent unauthorized requests share one token refresh', () async {
    var refreshes = 0;
    var expiredRequests = 0;
    final bothExpired = Completer<void>();
    final client = MockClient((request) async {
      if (request.url.path.endsWith('/auth/refresh')) {
        refreshes++;
        await Future<void>.delayed(const Duration(milliseconds: 10));
        return _tokens();
      }
      if (request.headers['Authorization']?.split(' ').last == 'expired-access') {
        expiredRequests++;
        if (expiredRequests == 2) bothExpired.complete();
        await bothExpired.future;
        return _json({'message': 'Expired'}, 401);
      }
      return _json({'success': true});
    });

    await http.runWithClient(
      () => Future.wait([
        ApiService.get('/shop/marketplace/products'),
        ApiService.get('/shop/marketplace/categories'),
      ]),
      () => client,
    );
    expect(expiredRequests, 2);
    expect(refreshes, 1);
  });

  test('retries unauthorized requests only once', () async {
    var refreshes = 0;
    var requests = 0;
    final client = MockClient((request) async {
      if (request.url.path.endsWith('/auth/refresh')) {
        refreshes++;
        return _tokens();
      }
      requests++;
      return _json({'message': 'Unauthorized'}, 401);
    });

    await expectLater(
      http.runWithClient(() => ApiService.get('/products'), () => client),
      throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 401)),
    );
    expect(refreshes, 1);
    expect(requests, 2);
  });

  test('preserves the session when refresh is rejected', () async {
    var requests = 0;
    final client = MockClient((request) async {
      requests++;
      return _json({'message': 'Unauthorized'}, 401);
    });
    await expectLater(
      http.runWithClient(() => ApiService.get('/products'), () => client),
      throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 401)),
    );
    expect(requests, 2);
    final prefs = await SharedPreferences.getInstance();
    expect(prefs.getString('access_token'), 'expired-access');
    expect(prefs.getString('refresh_token'), 'saved-refresh');
  });

  test('does not restore a session logged out during refresh', () async {
    final client = MockClient((request) async {
      if (request.url.path.endsWith('/auth/refresh')) {
        await AuthService.logout();
        return _tokens();
      }
      return _json({'message': 'Expired'}, 401);
    });
    await expectLater(
      http.runWithClient(() => ApiService.get('/products'), () => client),
      throwsA(isA<ApiException>()),
    );
    final prefs = await SharedPreferences.getInstance();
    expect(prefs.getString('access_token'), isNull);
    expect(prefs.getString('refresh_token'), isNull);
  });

  test('does not overwrite a login completed during refresh', () async {
    final client = MockClient((request) async {
      if (request.url.path.endsWith('/customer/auth/login')) {
        return _json({
          'data': {
            'userId': 'customer-2',
            'accessToken': 'other-access',
            'refreshToken': 'other-refresh',
          },
        });
      }
      if (request.url.path.endsWith('/auth/refresh')) {
        await AuthService.loginCustomer('0000000000', 'test-password');
        return _tokens();
      }
      return _json({'message': 'Expired'}, 401);
    });
    await expectLater(
      http.runWithClient(() => ApiService.get('/products'), () => client),
      throwsA(isA<ApiException>()),
    );
    final prefs = await SharedPreferences.getInstance();
    expect(prefs.getString('access_token'), 'other-access');
    expect(prefs.getString('refresh_token'), 'other-refresh');
    expect(prefs.getString('user_id'), 'customer-2');
  });

  test('malformed refresh responses do not replace saved credentials', () async {
    final client = MockClient((request) async {
      if (request.url.path.endsWith('/auth/refresh')) {
        return _json({'data': {'accessToken': 'incomplete-access'}});
      }
      return _json({'message': 'Expired'}, 401);
    });
    await expectLater(
      http.runWithClient(() => ApiService.get('/products'), () => client),
      throwsA(isA<ApiException>()),
    );
    final prefs = await SharedPreferences.getInstance();
    expect(prefs.getString('access_token'), 'expired-access');
    expect(prefs.getString('refresh_token'), 'saved-refresh');
  });

  test('a refresh outage does not block later refresh attempts', () async {
    var refreshes = 0;
    final client = MockClient((request) async {
      if (request.url.path.endsWith('/auth/refresh')) {
        refreshes++;
        return refreshes == 1
            ? _json({'message': 'Unavailable'}, 503)
            : _tokens();
      }
      if (request.headers['Authorization']?.split(' ').last == 'expired-access') {
        return _json({'message': 'Expired'}, 401);
      }
      return _json({'success': true});
    });
    await http.runWithClient(() async {
      await expectLater(
        ApiService.get('/products'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 503)),
      );
      expect(await ApiService.get('/products'), {'success': true});
    }, () => client);
    expect(refreshes, 2);
  });

  test('login failures do not trigger refresh or attach stored tokens', () async {
    var requests = 0;
    final client = MockClient((request) async {
      requests++;
      expect(request.url.path, '/api/customer/auth/login');
      expect(request.headers.containsKey('Authorization'), isFalse);
      return _json({'message': 'Invalid credentials'}, 401);
    });
    await expectLater(
      http.runWithClient(
        () => ApiService.post('/customer/auth/login', {}, auth: false),
        () => client,
      ),
      throwsA(isA<ApiException>()),
    );
    expect(requests, 1);
  });

  test('server failures are not treated as expired sessions', () async {
    var requests = 0;
    final client = MockClient((request) async {
      requests++;
      return _json({'message': 'Unavailable'}, 503);
    });
    await expectLater(
      http.runWithClient(() => ApiService.get('/products'), () => client),
      throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 503)),
    );
    expect(requests, 1);
  });

  test('does not replay requests under a different logged-in user', () async {
    var requests = 0;
    final client = MockClient((request) async {
      requests++;
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('user_id', 'customer-2');
      await prefs.setString('access_token', 'other-access');
      await prefs.setString('refresh_token', 'other-refresh');
      return _json({'message': 'Expired'}, 401);
    });
    await expectLater(
      http.runWithClient(() => ApiService.get('/products'), () => client),
      throwsA(isA<ApiException>()),
    );
    expect(requests, 1);
  });

  test('missing refresh tokens leave requests unauthorized without retry', () async {
    SharedPreferences.setMockInitialValues({'access_token': 'expired-access'});
    var requests = 0;
    final client = MockClient((request) async {
      requests++;
      return _json({'message': 'Expired'}, 401);
    });
    await expectLater(
      http.runWithClient(() => ApiService.get('/products'), () => client),
      throwsA(isA<ApiException>()),
    );
    expect(requests, 1);
  });

  test('late unauthorized responses reuse the already renewed token', () async {
    final renewed = Completer<void>();
    var refreshes = 0;
    final client = MockClient((request) async {
      if (request.url.path.endsWith('/auth/refresh')) {
        refreshes++;
        return _tokens();
      }
      if (request.headers['Authorization']?.split(' ').last == 'expired-access') {
        if (request.url.path.endsWith('/slow')) await renewed.future;
        return _json({'message': 'Expired'}, 401);
      }
      if (!renewed.isCompleted) renewed.complete();
      return _json({'success': true});
    });
    await http.runWithClient(
      () => Future.wait([
        ApiService.get('/slow'),
        ApiService.get('/fast'),
      ]),
      () => client,
    );
    expect(refreshes, 1);
  });

  for (final method in ['POST', 'PATCH', 'PUT']) {
    test('$method retries preserve the method and body', () async {
      var requests = 0;
      final client = MockClient((request) async {
        if (request.url.path.endsWith('/auth/refresh')) return _tokens();
        requests++;
        expect(request.method, method);
        expect(jsonDecode(request.body), {'quantity': 2});
        return requests == 1
            ? _json({'message': 'Expired'}, 401)
            : _json({'success': true});
      });
      await http.runWithClient(() {
        const body = {'quantity': 2};
        return switch (method) {
          'POST' => ApiService.post('/cart', body),
          'PATCH' => ApiService.patch('/cart', body),
          _ => ApiService.put('/cart', body),
        };
      }, () => client);
      expect(requests, 2);
    });
  }
}
