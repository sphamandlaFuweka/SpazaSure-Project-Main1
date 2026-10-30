import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/services/deep_link_service.dart';

void main() {
  test('reads the group buy id from a shared link', () {
    expect(DeepLinkService.groupBuyIdFrom(Uri.parse('spazasure://group-buy/abc-123')), 'abc-123');
    expect(DeepLinkService.groupBuyIdFrom(Uri.parse('https://api.spazasure.co.za/g/abc-123')), 'abc-123');
  });

  test('ignores links that are not a group buy', () {
    expect(DeepLinkService.groupBuyIdFrom(Uri.parse('spazasure://group-buy/')), isNull);
    expect(DeepLinkService.groupBuyIdFrom(Uri.parse('spazasure://orders/abc')), isNull);
    expect(DeepLinkService.groupBuyIdFrom(Uri.parse('https://api.spazasure.co.za/api/shop')), isNull);
  });
}
