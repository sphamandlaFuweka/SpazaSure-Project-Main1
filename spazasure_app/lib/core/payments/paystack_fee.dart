/// South African Paystack fees confirmed for SpazaSure, inclusive of VAT.
class PaystackFeeCalculator {
  static const int _rateNumerator = 3335;
  static const int _rateDenominator = 100000;
  static const int _fixedFeeMinorUnits = 115;

  static double grossUp(double netAmount) =>
      grossUpMinorUnits((netAmount * 100).round()) / 100;

  static int grossUpMinorUnits(int netMinorUnits) {
    if (netMinorUnits < 0) {
      throw ArgumentError.value(netMinorUnits, 'netMinorUnits');
    }
    if (netMinorUnits == 0) return 0;

    final numerator = (netMinorUnits + _fixedFeeMinorUnits) * _rateDenominator;
    final denominator = _rateDenominator - _rateNumerator;
    var gross = (numerator + denominator - 1) ~/ denominator;
    while (gross - feeMinorUnits(gross) < netMinorUnits) {
      gross++;
    }
    while (gross > 0 && gross - 1 - feeMinorUnits(gross - 1) >= netMinorUnits) {
      gross--;
    }
    return gross;
  }

  static int feeMinorUnits(int chargeMinorUnits) =>
      (chargeMinorUnits * _rateNumerator + _rateDenominator ~/ 2) ~/
          _rateDenominator +
      _fixedFeeMinorUnits;
}
