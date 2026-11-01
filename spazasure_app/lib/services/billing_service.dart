import 'api_service.dart';

/// Who invoices are made out to.
class BillingInfo {
  final String? billingName;
  final String? companyName;
  final String? vatNumber;
  final String? billingEmail;
  final String? addressLine1;
  final String? addressLine2;
  final String? city;
  final String? province;
  final String? postalCode;

  const BillingInfo({
    this.billingName,
    this.companyName,
    this.vatNumber,
    this.billingEmail,
    this.addressLine1,
    this.addressLine2,
    this.city,
    this.province,
    this.postalCode,
  });

  bool get isEmpty => billingName == null || billingName!.isEmpty;

  factory BillingInfo.fromJson(Map<String, dynamic> json) => BillingInfo(
    billingName: json['billingName'] as String?,
    companyName: json['companyName'] as String?,
    vatNumber: json['vatNumber'] as String?,
    billingEmail: json['billingEmail'] as String?,
    addressLine1: json['addressLine1'] as String?,
    addressLine2: json['addressLine2'] as String?,
    city: json['city'] as String?,
    province: json['province'] as String?,
    postalCode: json['postalCode'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'billingName': billingName,
    'companyName': companyName,
    'vatNumber': vatNumber,
    'billingEmail': billingEmail,
    'addressLine1': addressLine1,
    'addressLine2': addressLine2,
    'city': city,
    'province': province,
    'postalCode': postalCode,
  };

  /// The address on one line, without the empty parts.
  String get address => [
    addressLine1,
    addressLine2,
    city,
    province,
    postalCode,
  ].where((p) => p != null && p.isNotEmpty).join(', ');
}

/// A card saved through Paystack. Only what it looks like: never the card number.
class SavedCard {
  final String id;
  final String brand;
  final String last4;
  final String? expMonth;
  final String? expYear;
  final String? bank;
  final bool isDefault;
  final bool expired;

  const SavedCard({
    required this.id,
    required this.brand,
    required this.last4,
    this.expMonth,
    this.expYear,
    this.bank,
    required this.isDefault,
    required this.expired,
  });

  factory SavedCard.fromJson(Map<String, dynamic> json) => SavedCard(
    id: json['id'].toString(),
    brand: (json['brand'] as String?)?.trim().isNotEmpty == true
        ? json['brand'] as String
        : 'Card',
    last4: json['last4']?.toString() ?? '••••',
    expMonth: json['expMonth']?.toString(),
    expYear: json['expYear']?.toString(),
    bank: json['bank'] as String?,
    isDefault: json['isDefault'] == true,
    expired: json['expired'] == true,
  );

  String get expiry => expMonth == null || expYear == null
      ? ''
      : '${expMonth!.padLeft(2, '0')}/${expYear!.length == 4 ? expYear!.substring(2) : expYear}';
}

/// The bank account a shop's wallet withdrawals are paid into.
class PayoutAccount {
  final String bankName;
  final String accountHolder;
  final String accountNumber;
  final String accountNumberLast4;
  final String? branchCode;
  final String? accountType;

  const PayoutAccount({
    required this.bankName,
    required this.accountHolder,
    required this.accountNumber,
    required this.accountNumberLast4,
    this.branchCode,
    this.accountType,
  });

  factory PayoutAccount.fromJson(Map<String, dynamic> json) => PayoutAccount(
    bankName: json['bankName']?.toString() ?? '',
    accountHolder: json['accountHolder']?.toString() ?? '',
    accountNumber: json['accountNumber']?.toString() ?? '',
    accountNumberLast4: json['accountNumberLast4']?.toString() ?? '',
    branchCode: json['branchCode'] as String?,
    accountType: json['accountType'] as String?,
  );
}

class BillingOverview {
  final BillingInfo? info;
  final List<SavedCard> cards;
  final bool cardsEnabled;
  final double cardCheckAmount;
  final bool payoutAccountAllowed;
  final PayoutAccount? payoutAccount;

  const BillingOverview({
    required this.info,
    required this.cards,
    required this.cardsEnabled,
    required this.cardCheckAmount,
    required this.payoutAccountAllowed,
    required this.payoutAccount,
  });

  factory BillingOverview.fromJson(Map<String, dynamic> json) {
    final info = json['info'] is Map<String, dynamic>
        ? BillingInfo.fromJson(json['info'] as Map<String, dynamic>)
        : null;
    return BillingOverview(
      info: info == null || info.isEmpty ? null : info,
      cards:
          (json['cards'] as List?)
              ?.map((c) => SavedCard.fromJson(c as Map<String, dynamic>))
              .toList() ??
          const [],
      cardsEnabled: json['cardsEnabled'] == true,
      cardCheckAmount: (json['cardCheckAmount'] as num?)?.toDouble() ?? 1,
      payoutAccountAllowed: json['payoutAccountAllowed'] == true,
      payoutAccount: json['payoutAccount'] is Map<String, dynamic>
          ? PayoutAccount.fromJson(
              json['payoutAccount'] as Map<String, dynamic>,
            )
          : null,
    );
  }
}

/// The result of coming back from Paystack after adding a card.
class CardConfirmation {
  /// "saved", "already_saved", "not_usable" or "pending".
  final String status;
  final String message;

  const CardConfirmation(this.status, this.message);
}

class BillingService {
  static Future<BillingOverview> load() async {
    final response = await ApiService.get('/billing');
    return BillingOverview.fromJson(response['data'] as Map<String, dynamic>);
  }

  static Future<void> saveInfo(BillingInfo info) =>
      ApiService.put('/billing/info', info.toJson());

  static Future<void> removeInfo() => ApiService.delete('/billing/info');

  static Future<void> savePayoutAccount({
    required String bankName,
    required String accountHolder,
    required String accountNumber,
    String? branchCode,
    String? accountType,
  }) => ApiService.put('/billing/payout-account', {
    'bankName': bankName,
    'accountHolder': accountHolder,
    'accountNumber': accountNumber,
    'branchCode': branchCode,
    'accountType': accountType,
  });

  static Future<void> removePayoutAccount() =>
      ApiService.delete('/billing/payout-account');

  /// Starts saving a card: returns the new card's id and Paystack's checkout page.
  static Future<({String cardId, String checkoutUrl})> startAddCard() async {
    final response = await ApiService.post('/billing/cards', {});
    final data = response['data'] as Map<String, dynamic>;
    return (
      cardId: data['cardId'].toString(),
      checkoutUrl: data['checkoutUrl'].toString(),
    );
  }

  static Future<CardConfirmation> confirmCard(String cardId) async {
    final response = await ApiService.post(
      '/billing/cards/$cardId/confirm',
      {},
    );
    final data = response['data'] as Map<String, dynamic>? ?? const {};
    return CardConfirmation(
      data['status']?.toString() ?? 'pending',
      response['message']?.toString() ?? '',
    );
  }

  static Future<void> makeDefault(String cardId) =>
      ApiService.post('/billing/cards/$cardId/default', {});

  static Future<void> removeCard(String cardId) =>
      ApiService.delete('/billing/cards/$cardId');
}
