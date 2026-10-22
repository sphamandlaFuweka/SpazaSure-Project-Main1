import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// The app's display language. Customers choose a preferred language when they register and
/// the app starts in it when it has a translation; anyone can change it in their profile.
/// English, isiZulu and Sesotho are translated; other choices show English for now.
class AppLanguage {
  static const english = 'en';
  static const zulu = 'zu';
  static const sotho = 'st';

  /// Codes the app has translations for, with the name each language calls itself.
  static const supported = {english: 'English', zulu: 'isiZulu', sotho: 'Sesotho'};

  /// The current language; the whole app rebuilds when it changes.
  static final code = ValueNotifier<String>(english);

  static const _key = 'app_language';
  /// Set once the person picks a language in the app, so their registration choice no longer overrides it.
  static const _chosenKey = 'app_language_chosen';

  static Future<void> load() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final saved = prefs.getString(_key);
      if (saved != null && supported.containsKey(saved)) code.value = saved;
    } catch (_) {
      // Storage unavailable: stay in English
    }
  }

  /// The person chose a language in the app.
  static Future<void> choose(String newCode) async {
    if (!supported.containsKey(newCode)) return;
    code.value = newCode;
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_key, newCode);
      await prefs.setBool(_chosenKey, true);
    } catch (_) {}
  }

  /// Applies the preferred language from registration, unless the person already chose one in the app.
  static Future<void> applyPreference(String? preferredLanguage) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      if (prefs.getBool(_chosenKey) ?? false) return;
      final preferred = codeFor(preferredLanguage);
      code.value = preferred;
      await prefs.setString(_key, preferred);
    } catch (_) {
      code.value = codeFor(preferredLanguage);
    }
  }

  /// "isiZulu" or "Zulu" becomes "zu", "Sesotho" becomes "st"; anything else is English.
  static String codeFor(String? preferredLanguage) {
    final name = (preferredLanguage ?? '').toLowerCase().replaceAll(RegExp('[^a-z]'), '');
    if (name == 'isizulu' || name == 'zulu') return zulu;
    if (name == 'sesotho' || name == 'sotho' || name == 'southernsotho') return sotho;
    return english;
  }
}

/// The text for [key] in the current language, falling back to English.
String tr(String key) => _strings[AppLanguage.code.value]?[key] ?? _strings[AppLanguage.english]![key] ?? key;

// Translations should be checked by first-language isiZulu and Sesotho speakers before release.
const _strings = <String, Map<String, String>>{
  AppLanguage.english: {
    // Navigation
    'nav.home': 'Home',
    'nav.shops': 'Shops',
    'nav.verify': 'Verify',
    'nav.reports': 'Reports',
    'nav.profile': 'Profile',
    'nav.orders': 'Orders',
    'nav.cart': 'Cart',
    'nav.docs': 'Docs',
    // Customer home
    'home.taglineShop': 'Shop smarter. ',
    'home.taglineSafe': 'Stay safe.',
    'home.intro': 'Verify products and find trusted shops in your community.',
    'home.verifyProduct': 'Verify a product',
    'home.verifyProductSub': 'Scan a barcode or QR code',
    'home.verifyShop': 'Verify a shop',
    'home.verifyShopSub': 'Scan the shop sticker or enter its shop number',
    'home.rewards': 'My rewards',
    'home.rewardsSub': 'Earn 5 points per scan and redeem R20 vouchers',
    'home.report': 'Report a concern',
    'home.reportSub': 'Flag fake, expired, or unsafe goods',
    'home.learn': 'Learn before you buy',
    'home.safety': 'Safety basics',
    'home.safetySub': 'Learn how to spot suspicious packaging and unsafe products.',
    'home.kwazi': 'Kwazi authenticity guide',
    'home.kwaziSub': 'Chat with Kwazi when a product does not look right.',
    'home.recent': 'Recent activity',
    'home.recentEmpty': 'Your recent scans will appear here',
    'home.recentEmptySub': 'Start by verifying a product.',
    // Customer profile
    'profile.title': 'My Profile',
    'profile.edit': 'Edit profile',
    'profile.account': 'Account details',
    'profile.firstName': 'First name',
    'profile.lastName': 'Last name',
    'profile.email': 'Email',
    'profile.phone': 'Phone',
    'profile.allergies': 'Declared allergies',
    'profile.noAllergies': 'None declared',
    'profile.activity': 'Activity',
    'profile.membership': 'SpazaSure membership',
    'profile.membershipSub': 'Manage Free and Plus plans',
    'profile.scanHistory': 'Scan history',
    'profile.scanHistorySub': "Everything you've scanned and reported",
    'profile.rewardsSub': 'Points, vouchers, and redemptions',
    'profile.signOut': 'Sign out',
    // Language picker
    'language.title': 'App language',
    'language.changed': 'Language changed',
    'language.partial': 'Some screens are still in English while we translate them.',
    // Shop profile
    'shop.account': 'Account',
    'shop.details': 'Shop Details',
    'shop.detailsSub': 'Edit your shop info',
    'shop.sticker': 'My SpazaSure sticker',
    'shop.stickerSub': 'Shop number, QR code & verification',
    'shop.wallet': 'SpazaSure Wallet',
    'shop.walletSub': 'Balance & transactions',
    'shop.orders': 'My Orders',
    'shop.ordersSub': 'View order history',
    'shop.compliance': 'Compliance & Security',
    'shop.documents': 'My Documents',
    'shop.documentsSub': 'Upload & manage docs',
    'shop.verification': 'Verification Status',
    'shop.verificationSub': 'Check account status',
    'shop.scan': 'Scan Product',
    'shop.scanSub': 'Verify product authenticity',
    'shop.app': 'App & Support',
    'shop.notifications': 'Notifications',
    'shop.notificationsSub': 'Alerts & updates',
    'shop.help': 'Help & Support',
    'shop.helpSub': 'Get assistance',
    'shop.about': 'About SpazaSure',
    'shop.aboutSub': 'Version & info',
    'shop.reviews': 'reviews',
    'shop.review': 'review',
    'shop.complianceStat': 'Compliance',
    'shop.onboardingStat': 'Onboarding',
    'shop.paid': 'Paid',
    'shop.due': 'Due',
  },
  AppLanguage.zulu: {
    'nav.home': 'Ikhaya',
    'nav.shops': 'Izitolo',
    'nav.verify': 'Qinisekisa',
    'nav.reports': 'Imibiko',
    'nav.profile': 'Iphrofayela',
    'nav.orders': 'Ama-oda',
    'nav.cart': 'Inqola',
    'nav.docs': 'Imibhalo',
    'home.taglineShop': 'Thenga ngobuhlakani. ',
    'home.taglineSafe': 'Hlala uphephile.',
    'home.intro': 'Qinisekisa imikhiqizo futhi uthole izitolo ezethembekile emphakathini wakho.',
    'home.verifyProduct': 'Qinisekisa umkhiqizo',
    'home.verifyProductSub': 'Skena ibhakhodi noma ikhodi ye-QR',
    'home.verifyShop': 'Qinisekisa isitolo',
    'home.verifyShopSub': 'Skena isitikha sesitolo noma ufake inombolo yesitolo',
    'home.rewards': 'Imiklomelo yami',
    'home.rewardsSub': 'Thola amaphuzu ama-5 ngokuskena ngakunye, ushintshe ngamavawusha e-R20',
    'home.report': 'Bika okukukhathazayo',
    'home.reportSub': 'Bika izimpahla mbumbulu, eziphelelwe yisikhathi, noma ezingaphephile',
    'home.learn': 'Funda ngaphambi kokuthenga',
    'home.safety': 'Izisekelo zokuphepha',
    'home.safetySub': 'Funda ukubona ukupakisha okusolisayo nemikhiqizo engaphephile.',
    'home.kwazi': 'Umhlahlandlela kaKwazi wobuqiniso',
    'home.kwaziSub': 'Xoxa noKwazi uma umkhiqizo ungabukeki kahle.',
    'home.recent': 'Okwenzeke muva nje',
    'home.recentEmpty': 'Okuskenile kwakamuva kuzovela lapha',
    'home.recentEmptySub': 'Qala ngokuqinisekisa umkhiqizo.',
    'profile.title': 'Iphrofayela yami',
    'profile.edit': 'Hlela iphrofayela',
    'profile.account': 'Imininingwane ye-akhawunti',
    'profile.firstName': 'Igama',
    'profile.lastName': 'Isibongo',
    'profile.email': 'I-imeyili',
    'profile.phone': 'Ifoni',
    'profile.allergies': 'Ukungezwani komzimba okushiwoyo',
    'profile.noAllergies': 'Akukho okushiwoyo',
    'profile.activity': 'Okwenziwayo',
    'profile.membership': 'Ubulungu be-SpazaSure',
    'profile.membershipSub': 'Phatha amapulani amahhala ne-Plus',
    'profile.scanHistory': 'Umlando wokuskena',
    'profile.scanHistorySub': 'Konke osukuskenile nokubikile',
    'profile.rewardsSub': 'Amaphuzu, amavawusha, nokushintshanisa',
    'profile.signOut': 'Phuma',
    'language.title': 'Ulimi lwe-app',
    'language.changed': 'Ulimi lushintshiwe',
    'language.partial': 'Ezinye izikrini zisesesiNgisini ngesikhathi sisazihumusha.',
    'shop.account': 'I-akhawunti',
    'shop.details': 'Imininingwane yesitolo',
    'shop.detailsSub': 'Hlela imininingwane yesitolo sakho',
    'shop.sticker': 'Isitikha sami se-SpazaSure',
    'shop.stickerSub': 'Inombolo yesitolo, ikhodi ye-QR nokuqinisekisa',
    'shop.wallet': 'I-wallet ye-SpazaSure',
    'shop.walletSub': 'Ibhalansi nokuthengiselana',
    'shop.orders': 'Ama-oda ami',
    'shop.ordersSub': 'Bheka umlando wama-oda',
    'shop.compliance': 'Ukuthobela imithetho nokuphepha',
    'shop.documents': 'Imibhalo yami',
    'shop.documentsSub': 'Layisha futhi uphathe imibhalo',
    'shop.verification': 'Isimo sokuqinisekiswa',
    'shop.verificationSub': 'Hlola isimo se-akhawunti',
    'shop.scan': 'Skena umkhiqizo',
    'shop.scanSub': 'Qinisekisa ubuqiniso bomkhiqizo',
    'shop.app': 'I-app nosizo',
    'shop.notifications': 'Izaziso',
    'shop.notificationsSub': 'Izexwayiso nezibuyekezo',
    'shop.help': 'Usizo',
    'shop.helpSub': 'Thola usizo',
    'shop.about': 'Mayelana ne-SpazaSure',
    'shop.aboutSub': 'Inguqulo nolwazi',
    'shop.reviews': 'izibuyekezo',
    'shop.review': 'isibuyekezo',
    'shop.complianceStat': 'Ukuthobela',
    'shop.onboardingStat': 'Ukubhalisa',
    'shop.paid': 'Kukhokhiwe',
    'shop.due': 'Kusafanele kukhokhwe',
  },
  AppLanguage.sotho: {
    'nav.home': 'Lehae',
    'nav.shops': 'Mabenkele',
    'nav.verify': 'Netefatsa',
    'nav.reports': 'Ditlaleho',
    'nav.profile': 'Profaele',
    'nav.orders': 'Diodara',
    'nav.cart': 'Kariki',
    'nav.docs': 'Ditokomane',
    'home.taglineShop': 'Reka ka bohlale. ',
    'home.taglineSafe': 'Dula o sireletsehile.',
    'home.intro': 'Netefatsa dihlahiswa mme o fumane mabenkele a tshepahalang setjhabeng sa hao.',
    'home.verifyProduct': 'Netefatsa sehlahiswa',
    'home.verifyProductSub': 'Skena barcode kapa khoutu ya QR',
    'home.verifyShop': 'Netefatsa lebenkele',
    'home.verifyShopSub': 'Skena setikara sa lebenkele kapa o kenye nomoro ya lona',
    'home.rewards': 'Meputso ya ka',
    'home.rewardsSub': 'Fumana dintlha tse 5 skeneng ka nngwe, o di fapanyetsane ka divoutjhara tsa R20',
    'home.report': 'Tlaleha ho tshwenyeha',
    'home.reportSub': 'Tlaleha thepa ya maiketsetso, e fetileng nako kapa e kotsi',
    'home.learn': 'Ithute pele o reka',
    'home.safety': 'Metheo ya polokeho',
    'home.safetySub': 'Ithute ho lemoha diphuthelwana tse belaetsang le dihlahiswa tse kotsi.',
    'home.kwazi': 'Tataiso ya Kwazi ya bonnete',
    'home.kwaziSub': 'Buisana le Kwazi ha sehlahiswa se sa shebahale hantle.',
    'home.recent': 'Diketso tsa morao tjena',
    'home.recentEmpty': 'Diskene tsa hao tsa morao di tla hlaha mona',
    'home.recentEmptySub': 'Qala ka ho netefatsa sehlahiswa.',
    'profile.title': 'Profaele ya ka',
    'profile.edit': 'Fetola profaele',
    'profile.account': 'Dintlha tsa akhaonte',
    'profile.firstName': 'Lebitso',
    'profile.lastName': 'Fane',
    'profile.email': 'Imeile',
    'profile.phone': 'Fono',
    'profile.allergies': 'Di-allergy tse boletsweng',
    'profile.noAllergies': 'Ha ho e boletsweng',
    'profile.activity': 'Diketso',
    'profile.membership': 'Ho ba setho sa SpazaSure',
    'profile.membershipSub': 'Laola merero ya mahala le ya Plus',
    'profile.scanHistory': 'Nalane ya diskene',
    'profile.scanHistorySub': 'Tsohle tseo o di skenneng le ho di tlaleha',
    'profile.rewardsSub': 'Dintlha, divoutjhara le phapanyetsano',
    'profile.signOut': 'Tswa',
    'language.title': 'Puo ya app',
    'language.changed': 'Puo e fetotswe',
    'language.partial': 'Dikgaolo tse ding di sa ntse di le ka Senyesemane ha re ntse re di fetolela.',
    'shop.account': 'Akhaonte',
    'shop.details': 'Dintlha tsa lebenkele',
    'shop.detailsSub': 'Fetola dintlha tsa lebenkele la hao',
    'shop.sticker': 'Setikara sa ka sa SpazaSure',
    'shop.stickerSub': 'Nomoro ya lebenkele, khoutu ya QR le netefatso',
    'shop.wallet': 'Wallet ya SpazaSure',
    'shop.walletSub': 'Tekatekano le ditransekshene',
    'shop.orders': 'Diodara tsa ka',
    'shop.ordersSub': 'Sheba nalane ya diodara',
    'shop.compliance': 'Ho latela melao le polokeho',
    'shop.documents': 'Ditokomane tsa ka',
    'shop.documentsSub': 'Kenya le ho laola ditokomane',
    'shop.verification': 'Boemo ba netefatso',
    'shop.verificationSub': 'Hlahloba boemo ba akhaonte',
    'shop.scan': 'Skena sehlahiswa',
    'shop.scanSub': 'Netefatsa bonnete ba sehlahiswa',
    'shop.app': 'App le thuso',
    'shop.notifications': 'Ditsebiso',
    'shop.notificationsSub': 'Ditemoso le dintlafatso',
    'shop.help': 'Thuso',
    'shop.helpSub': 'Fumana thuso',
    'shop.about': 'Mabapi le SpazaSure',
    'shop.aboutSub': 'Mofuta le tlhahisoleseding',
    'shop.reviews': 'maikutlo',
    'shop.review': 'maikutlo',
    'shop.complianceStat': 'Ho latela melao',
    'shop.onboardingStat': 'Ngodiso',
    'shop.paid': 'E lefilwe',
    'shop.due': 'E lokela ho lefuwa',
  },
};
