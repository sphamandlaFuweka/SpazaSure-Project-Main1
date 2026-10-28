import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/core/validation/sa_validation.dart';

void main() {
  group('ID number', () {
    test('accepts valid SA ID numbers, with or without spaces', () {
      expect(SaValidation.isValidIdNumber('8001015009087'), isTrue);
      expect(SaValidation.isValidIdNumber('800101 5009 087'), isTrue);
      expect(SaValidation.isValidIdNumber('9002150123088'), isTrue);
    });

    test('rejects wrong check digits, dates, lengths and letters', () {
      expect(SaValidation.isValidIdNumber('8001015009088'), isFalse);
      expect(SaValidation.isValidIdNumber('8013015009087'), isFalse);
      expect(SaValidation.isValidIdNumber('8001325009087'), isFalse);
      expect(SaValidation.isValidIdNumber('8001015009387'), isFalse);
      expect(SaValidation.isValidIdNumber('800101500908'), isFalse);
      expect(SaValidation.isValidIdNumber('80010150O9087'), isFalse);
      expect(SaValidation.isValidIdNumber(''), isFalse);
      expect(SaValidation.isValidIdNumber(null), isFalse);
    });
  });

  group('Phone', () {
    test('accepts common South African forms', () {
      for (final phone in [
        '082 123 4567',
        '+27 82 123 4567',
        '27821234567',
        '821234567',
        '(011) 555-1234',
      ]) {
        expect(SaValidation.isValidPhone(phone), isTrue, reason: phone);
      }
    });

    test('rejects short, foreign and malformed numbers', () {
      for (final phone in [
        '08212345',
        '+44 20 7946 0958',
        '0921234567',
        '082-ABC-4567',
        '',
      ]) {
        expect(SaValidation.isValidPhone(phone), isFalse, reason: phone);
      }
    });
  });
}
