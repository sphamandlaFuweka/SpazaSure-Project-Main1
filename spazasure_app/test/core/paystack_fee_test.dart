import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/core/payments/paystack_fee.dart';

void main() {
  test(
    'grosses up the confirmed tariff to the smallest cent-accurate total',
    () {
      expect(PaystackFeeCalculator.grossUp(20), 21.88);
      expect(PaystackFeeCalculator.grossUp(97.29), 101.84);
      expect(PaystackFeeCalculator.grossUp(100), 104.64);
    },
  );

  test('keeps zero at zero and rejects negative net amounts', () {
    expect(PaystackFeeCalculator.grossUp(0), 0);
    expect(() => PaystackFeeCalculator.grossUp(-1), throwsArgumentError);
  });
}
