import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    AppLanguage.code.value = AppLanguage.english;
  });

  test('registration language names map to the translated languages', () {
    expect(AppLanguage.codeFor('isiZulu'), AppLanguage.zulu);
    expect(AppLanguage.codeFor('Sesotho'), AppLanguage.sotho);
    // Not translated yet, so English
    expect(AppLanguage.codeFor('isiXhosa'), AppLanguage.english);
    expect(AppLanguage.codeFor(null), AppLanguage.english);
  });

  test('text follows the current language and falls back to English', () {
    expect(tr('nav.home'), 'Home');
    AppLanguage.code.value = AppLanguage.zulu;
    expect(tr('nav.home'), 'Ikhaya');
    AppLanguage.code.value = AppLanguage.sotho;
    expect(tr('nav.home'), 'Lehae');
    expect(tr('no.such.key'), 'no.such.key');
  });

  test(
    'the registration language applies until the person picks one in the app',
    () async {
      await AppLanguage.applyPreference('isiZulu');
      expect(AppLanguage.code.value, AppLanguage.zulu);

      await AppLanguage.choose(AppLanguage.sotho);
      await AppLanguage.applyPreference('isiZulu');
      expect(AppLanguage.code.value, AppLanguage.sotho);
    },
  );

  test('every key has an isiZulu and a Sesotho translation', () {
    for (final key in [
      'nav.home',
      'home.verifyProduct',
      'profile.signOut',
      'shop.documents',
      'language.title',
      'academy.cashBook',
      'academy.lessons',
      'shop.academySub',
    ]) {
      AppLanguage.code.value = AppLanguage.english;
      final english = tr(key);
      for (final code in [AppLanguage.zulu, AppLanguage.sotho]) {
        AppLanguage.code.value = code;
        expect(tr(key), isNot(english), reason: '$key in $code');
      }
    }
  });
}
