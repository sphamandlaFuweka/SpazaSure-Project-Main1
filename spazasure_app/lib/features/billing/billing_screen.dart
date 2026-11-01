import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';
import 'package:spazasure_app/services/billing_service.dart';

/// Billing: saved cards, who invoices are made out to and (shops) the bank account
/// withdrawals are paid into. Shop owners and customers both open it from their profile.
class BillingScreen extends StatefulWidget {
  const BillingScreen({super.key});

  @override
  State<BillingScreen> createState() => _BillingScreenState();
}

class _BillingScreenState extends State<BillingScreen> {
  BillingOverview? _billing;
  String? _error;
  bool _loading = true;
  bool _addingCard = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = _billing == null;
      _error = null;
    });
    try {
      final billing = await BillingService.load();
      if (mounted) setState(() => _billing = billing);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _snack(String message) {
    if (!mounted || message.isEmpty) return;
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  /// Runs a change, shows its result and reloads.
  Future<void> _run(Future<void> Function() action, String done) async {
    try {
      await action();
      _snack(done);
    } catch (e) {
      _snack(e.toString());
    }
    await _load();
  }

  Future<bool> _confirm(String title, String message, String action) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Keep'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: TextButton.styleFrom(foregroundColor: AppColors.red),
            child: Text(action),
          ),
        ],
      ),
    );
    return ok == true;
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: BrandAppBar(title: const Text('Billing')),
    body: _loading
        ? const Center(child: CircularProgressIndicator())
        : _billing == null
        ? _errorView()
        : RefreshIndicator(
            onRefresh: _load,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
              children: [
                _cardsSection(_billing!),
                const SizedBox(height: 16),
                _infoSection(_billing!.info),
                if (_billing!.payoutAccountAllowed) ...[
                  const SizedBox(height: 16),
                  _payoutSection(_billing!.payoutAccount),
                ],
              ].animate(interval: 60.ms).fadeIn().slideY(begin: 0.04, end: 0),
            ),
          ),
  );

  Widget _errorView() => Center(
    child: Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(
            Icons.cloud_off_rounded,
            size: 48,
            color: AppColors.textHint,
          ),
          const SizedBox(height: 12),
          Text(
            _error ?? 'Could not load your billing details.',
            textAlign: TextAlign.center,
            style: AppTextStyles.body,
          ),
          const SizedBox(height: 16),
          FilledButton(onPressed: _load, child: const Text('Try again')),
        ],
      ),
    ),
  );

  // ─── Saved cards ─────────────────────────────────────────────────────────

  Widget _cardsSection(BillingOverview billing) => _Section(
    icon: Icons.credit_card_rounded,
    title: 'Saved cards',
    children: [
      if (billing.cards.isEmpty)
        _emptyText('No cards saved yet.')
      else
        for (final card in billing.cards) _cardTile(card),
      const SizedBox(height: 8),
      _note(
        'To add a card we charge R${billing.cardCheckAmount.toStringAsFixed(2)} to check it, '
        'then refund it straight away. Your card number stays with Paystack, our payment '
        'provider. SpazaSure never sees or stores it.',
      ),
      const SizedBox(height: 12),
      _primaryButton(
        icon: Icons.add_card_rounded,
        label: 'Add a card',
        busy: _addingCard,
        onPressed: billing.cardsEnabled ? _addCard : null,
      ),
      if (!billing.cardsEnabled)
        Padding(
          padding: const EdgeInsets.only(top: 8),
          child: _note('Card payments are not switched on yet.'),
        ),
    ],
  );

  Widget _cardTile(SavedCard card) => Container(
    margin: const EdgeInsets.only(bottom: 10),
    padding: const EdgeInsets.fromLTRB(14, 10, 4, 10),
    decoration: BoxDecoration(
      color: AppColors.background,
      borderRadius: BorderRadius.circular(14),
      border: Border.all(
        color: card.isDefault ? AppColors.primary : AppColors.divider,
        width: card.isDefault ? 1.5 : 1,
      ),
    ),
    child: Row(
      children: [
        Container(
          width: 44,
          height: 30,
          decoration: BoxDecoration(
            color: AppColors.primary,
            borderRadius: BorderRadius.circular(6),
          ),
          child: const Icon(
            Icons.credit_card_rounded,
            color: Colors.white,
            size: 18,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${_capitalise(card.brand)} •••• ${card.last4}',
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w800),
              ),
              const SizedBox(height: 2),
              Text(
                [
                  if (card.expiry.isNotEmpty)
                    card.expired
                        ? 'Expired ${card.expiry}'
                        : 'Expires ${card.expiry}',
                  if (card.bank?.isNotEmpty == true) card.bank!,
                ].join(' · '),
                style: AppTextStyles.caption.copyWith(
                  color: card.expired ? AppColors.red : AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
        if (card.isDefault) _chip('Default'),
        PopupMenuButton<String>(
          tooltip: 'Card options',
          onSelected: (choice) async {
            if (choice == 'default') {
              await _run(
                () => BillingService.makeDefault(card.id),
                'Default card updated.',
              );
            } else if (choice == 'remove' &&
                await _confirm(
                  'Remove this card?',
                  '${_capitalise(card.brand)} ending in ${card.last4} will be removed from SpazaSure.',
                  'Remove',
                )) {
              await _run(
                () => BillingService.removeCard(card.id),
                'Card removed.',
              );
            }
          },
          itemBuilder: (_) => [
            if (!card.isDefault)
              const PopupMenuItem(
                value: 'default',
                child: Text('Make default'),
              ),
            const PopupMenuItem(value: 'remove', child: Text('Remove card')),
          ],
        ),
      ],
    ),
  );

  Future<void> _addCard() async {
    setState(() => _addingCard = true);
    try {
      final start = await BillingService.startAddCard();
      if (!mounted) return;
      await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => OnboardingFeeCheckoutScreen(
            checkoutUrl: start.checkoutUrl,
            title: 'Add a card',
          ),
        ),
      );
      // Ask even when they closed the page: the check may still have gone through
      final result = await BillingService.confirmCard(start.cardId);
      _snack(
        result.status == 'pending'
            ? 'Card not added. If you completed the payment, it will show here shortly.'
            : result.message,
      );
    } catch (e) {
      _snack(e.toString());
    } finally {
      if (mounted) setState(() => _addingCard = false);
    }
    await _load();
  }

  // ─── Billing info ────────────────────────────────────────────────────────

  Widget _infoSection(BillingInfo? info) => _Section(
    icon: Icons.receipt_long_rounded,
    title: 'Billing details',
    subtitle: 'Who your invoices and receipts are made out to.',
    children: [
      if (info == null)
        _emptyText('No billing details added yet.')
      else ...[
        _row('Name', info.billingName),
        _row('Company', info.companyName),
        _row('VAT number', info.vatNumber),
        _row('Email', info.billingEmail),
        _row('Address', info.address),
      ],
      const SizedBox(height: 12),
      Row(
        children: [
          Expanded(
            child: _primaryButton(
              icon: info == null ? Icons.add_rounded : Icons.edit_rounded,
              label: info == null ? 'Add billing details' : 'Edit',
              onPressed: () => _editInfo(info),
            ),
          ),
          if (info != null) ...[
            const SizedBox(width: 10),
            _removeButton(() async {
              if (await _confirm(
                'Remove billing details?',
                'Your name, company, VAT number and address will be removed from your invoices.',
                'Remove',
              )) {
                await _run(
                  BillingService.removeInfo,
                  'Billing details removed.',
                );
              }
            }),
          ],
        ],
      ),
    ],
  );

  void _editInfo(BillingInfo? info) {
    final name = TextEditingController(text: info?.billingName);
    final company = TextEditingController(text: info?.companyName);
    final vat = TextEditingController(text: info?.vatNumber);
    final email = TextEditingController(text: info?.billingEmail);
    final line1 = TextEditingController(text: info?.addressLine1);
    final line2 = TextEditingController(text: info?.addressLine2);
    final city = TextEditingController(text: info?.city);
    final province = TextEditingController(text: info?.province);
    final postal = TextEditingController(text: info?.postalCode);
    _formSheet(
      title: info == null ? 'Add billing details' : 'Edit billing details',
      fields: [
        _field('Name on invoices *', name, hint: 'e.g. Thandi Mokoena'),
        _field('Company name', company, hint: 'Optional'),
        _field(
          'VAT number',
          vat,
          hint: 'Optional, 10 digits starting with 4',
          keyboardType: TextInputType.number,
          digitsOnly: true,
          maxLength: 10,
        ),
        _field(
          'Billing email',
          email,
          hint: 'Where invoices are sent',
          keyboardType: TextInputType.emailAddress,
        ),
        _field('Street address', line1),
        _field('Suburb / area', line2),
        _field('City', city),
        _provinceField(province),
        _field(
          'Postal code',
          postal,
          keyboardType: TextInputType.number,
          digitsOnly: true,
          maxLength: 4,
        ),
      ],
      onSave: () => BillingService.saveInfo(
        BillingInfo(
          billingName: name.text.trim(),
          companyName: company.text.trim(),
          vatNumber: vat.text.trim(),
          billingEmail: email.text.trim(),
          addressLine1: line1.text.trim(),
          addressLine2: line2.text.trim(),
          city: city.text.trim(),
          province: province.text.trim(),
          postalCode: postal.text.trim(),
        ),
      ),
      done: 'Billing details saved.',
    );
  }

  // ─── Payout account (shops) ──────────────────────────────────────────────

  Widget _payoutSection(PayoutAccount? account) => _Section(
    icon: Icons.account_balance_rounded,
    title: 'Payout bank account',
    subtitle: 'Where your wallet withdrawals are paid.',
    children: [
      if (account == null)
        _emptyText('No bank account added yet.')
      else ...[
        _row('Bank', account.bankName),
        _row('Account holder', account.accountHolder),
        _row('Account number', '•••• ${account.accountNumberLast4}'),
        _row('Branch code', account.branchCode),
        _row('Account type', _capitalise(account.accountType ?? '')),
      ],
      const SizedBox(height: 12),
      Row(
        children: [
          Expanded(
            child: _primaryButton(
              icon: account == null ? Icons.add_rounded : Icons.edit_rounded,
              label: account == null ? 'Add bank account' : 'Edit',
              onPressed: () => _editPayout(account),
            ),
          ),
          if (account != null) ...[
            const SizedBox(width: 10),
            _removeButton(() async {
              if (await _confirm(
                'Remove bank account?',
                'You will need to enter bank details again for your next withdrawal.',
                'Remove',
              )) {
                await _run(
                  BillingService.removePayoutAccount,
                  'Payout account removed.',
                );
              }
            }),
          ],
        ],
      ),
    ],
  );

  void _editPayout(PayoutAccount? account) {
    final bank = TextEditingController(text: account?.bankName);
    final holder = TextEditingController(text: account?.accountHolder);
    final number = TextEditingController(text: account?.accountNumber);
    final branch = TextEditingController(text: account?.branchCode);
    var type = account?.accountType ?? 'cheque';
    _formSheet(
      title: account == null ? 'Add bank account' : 'Edit bank account',
      fields: [
        _field('Bank name *', bank, hint: 'e.g. Capitec, FNB'),
        _field('Account holder *', holder, hint: 'Name on the account'),
        _field(
          'Account number *',
          number,
          keyboardType: TextInputType.number,
          digitsOnly: true,
          maxLength: 20,
        ),
        _field(
          'Branch code',
          branch,
          hint: '6 digits',
          keyboardType: TextInputType.number,
          digitsOnly: true,
          maxLength: 6,
        ),
        StatefulBuilder(
          builder: (ctx, setTypeState) => Padding(
            padding: const EdgeInsets.only(bottom: 14),
            child: SegmentedButton<String>(
              segments: const [
                ButtonSegment(value: 'cheque', label: Text('Cheque / current')),
                ButtonSegment(value: 'savings', label: Text('Savings')),
              ],
              selected: {type},
              onSelectionChanged: (s) => setTypeState(() => type = s.first),
            ),
          ),
        ),
      ],
      onSave: () => BillingService.savePayoutAccount(
        bankName: bank.text.trim(),
        accountHolder: holder.text.trim(),
        accountNumber: number.text.trim(),
        branchCode: branch.text.trim(),
        accountType: type,
      ),
      done: 'Payout account saved.',
    );
  }

  // ─── Shared pieces ───────────────────────────────────────────────────────

  void _formSheet({
    required String title,
    required List<Widget> fields,
    required Future<void> Function() onSave,
    required String done,
  }) {
    var saving = false;
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: true,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setSheetState) => Container(
          decoration: const BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
          ),
          padding: EdgeInsets.fromLTRB(
            20,
            12,
            20,
            20 + MediaQuery.of(ctx).viewInsets.bottom,
          ),
          child: SafeArea(
            top: false,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Center(
                    child: Container(
                      width: 40,
                      height: 4,
                      decoration: BoxDecoration(
                        color: AppColors.divider,
                        borderRadius: BorderRadius.circular(2),
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Text(title, style: AppTextStyles.h3),
                  const SizedBox(height: 16),
                  ...fields,
                  const SizedBox(height: 6),
                  _primaryButton(
                    icon: Icons.check_rounded,
                    label: 'Save',
                    busy: saving,
                    onPressed: () async {
                      setSheetState(() => saving = true);
                      try {
                        await onSave();
                        if (ctx.mounted) Navigator.pop(ctx);
                        _snack(done);
                        await _load();
                      } catch (e) {
                        // Shown over the sheet, which stays open to fix the details
                        if (ctx.mounted) {
                          ScaffoldMessenger.of(
                            ctx,
                          ).showSnackBar(SnackBar(content: Text(e.toString())));
                          setSheetState(() => saving = false);
                        }
                      }
                    },
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _field(
    String label,
    TextEditingController ctrl, {
    String? hint,
    TextInputType? keyboardType,
    bool digitsOnly = false,
    int? maxLength,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: 14),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: ctrl,
          keyboardType: keyboardType,
          maxLength: maxLength,
          inputFormatters: digitsOnly
              ? [FilteringTextInputFormatter.digitsOnly]
              : null,
          decoration: _inputDecoration(hint),
        ),
      ],
    ),
  );

  static const _provinces = [
    'Eastern Cape',
    'Free State',
    'Gauteng',
    'KwaZulu-Natal',
    'Limpopo',
    'Mpumalanga',
    'North West',
    'Northern Cape',
    'Western Cape',
  ];

  Widget _provinceField(TextEditingController ctrl) => Padding(
    padding: const EdgeInsets.only(bottom: 14),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Province',
          style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600),
        ),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: _provinces.contains(ctrl.text) ? ctrl.text : null,
          items: [
            for (final p in _provinces)
              DropdownMenuItem(value: p, child: Text(p)),
          ],
          onChanged: (p) => ctrl.text = p ?? '',
          decoration: _inputDecoration('Choose a province'),
        ),
      ],
    ),
  );

  InputDecoration _inputDecoration(String? hint) => InputDecoration(
    hintText: hint,
    counterText: '',
    filled: true,
    fillColor: const Color(0xFFF5F5F5),
    border: OutlineInputBorder(
      borderRadius: BorderRadius.circular(14),
      borderSide: BorderSide.none,
    ),
  );

  Widget _primaryButton({
    required IconData icon,
    required String label,
    required VoidCallback? onPressed,
    bool busy = false,
  }) => SizedBox(
    width: double.infinity,
    height: 48,
    child: FilledButton.icon(
      onPressed: busy ? null : onPressed,
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.primary,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
      icon: busy
          ? const SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Colors.white,
              ),
            )
          : Icon(icon, size: 20),
      label: Text(label, style: const TextStyle(fontWeight: FontWeight.w800)),
    ),
  );

  Widget _removeButton(VoidCallback onPressed) => SizedBox(
    height: 48,
    child: OutlinedButton.icon(
      onPressed: onPressed,
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.red,
        side: const BorderSide(color: AppColors.red),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
      icon: const Icon(Icons.delete_outline_rounded, size: 20),
      label: const Text('Remove'),
    ),
  );

  Widget _row(String label, String? value) => value == null || value.isEmpty
      ? const SizedBox.shrink()
      : Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(
                width: 120,
                child: Text(
                  label,
                  style: AppTextStyles.bodySmall.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ),
              Expanded(
                child: Text(
                  value,
                  style: AppTextStyles.bodySmall.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
        );

  Widget _chip(String label) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
    decoration: BoxDecoration(
      color: AppColors.primary.withValues(alpha: 0.1),
      borderRadius: BorderRadius.circular(10),
    ),
    child: Text(
      label,
      style: AppTextStyles.caption.copyWith(
        color: AppColors.primary,
        fontWeight: FontWeight.w800,
      ),
    ),
  );

  Widget _emptyText(String text) => Padding(
    padding: const EdgeInsets.only(bottom: 4),
    child: Text(
      text,
      style: AppTextStyles.bodySmall.copyWith(color: AppColors.textSecondary),
    ),
  );

  Widget _note(String text) => Row(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      const Icon(
        Icons.lock_outline_rounded,
        size: 14,
        color: AppColors.textHint,
      ),
      const SizedBox(width: 6),
      Expanded(
        child: Text(
          text,
          style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
        ),
      ),
    ],
  );

  static String _capitalise(String s) =>
      s.isEmpty ? s : s[0].toUpperCase() + s.substring(1);
}

class _Section extends StatelessWidget {
  final IconData icon;
  final String title;
  final String? subtitle;
  final List<Widget> children;

  const _Section({
    required this.icon,
    required this.title,
    this.subtitle,
    required this.children,
  });

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(16),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(20),
      boxShadow: [
        BoxShadow(
          color: Colors.black.withValues(alpha: 0.04),
          blurRadius: 12,
          offset: const Offset(0, 4),
        ),
      ],
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Icon(icon, color: AppColors.primary, size: 22),
            const SizedBox(width: 8),
            Text(title, style: AppTextStyles.h3.copyWith(fontSize: 17)),
          ],
        ),
        if (subtitle != null) ...[
          const SizedBox(height: 4),
          Text(
            subtitle!,
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
        ],
        const SizedBox(height: 14),
        ...children,
      ],
    ),
  );
}
