import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/academy/widgets/academy_widgets.dart';
import 'package:spazasure_app/services/shop_academy_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// How the shop is set up as a business: registration, premises, staff, banking and goals.
/// These details go on the Business Record PDF for funders. Pops with true when saved.
class BusinessProfileScreen extends StatefulWidget {
  const BusinessProfileScreen({super.key});

  @override
  State<BusinessProfileScreen> createState() => _BusinessProfileScreenState();
}

class _BusinessProfileScreenState extends State<BusinessProfileScreen> {
  static const legalStructures = {
    'sole_proprietor': 'Sole proprietor',
    'partnership': 'Partnership',
    'pty_ltd': '(Pty) Ltd company',
    'cooperative': 'Co-operative',
  };
  static const premisesTypes = {
    'rented': 'Rented',
    'owned': 'Owned',
    'home': 'At home',
  };

  bool _loading = true;
  bool _failed = false;
  bool _saving = false;
  String _legal = 'sole_proprietor';
  String _premises = 'rented';
  DateTime? _started;
  int _days = 7;
  bool _bankAccount = false;
  bool _cards = false;
  final _cipc = TextEditingController();
  final _sars = TextEditingController();
  final _vat = TextEditingController();
  final _permit = TextEditingController();
  final _rent = TextEditingController();
  final _fullTime = TextEditingController();
  final _partTime = TextEditingController();
  final _hours = TextEditingController();
  final _bank = TextEditingController();
  final _suppliers = TextEditingController();
  final _goals = TextEditingController();

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _failed = false;
    });
    try {
      final p = await ShopAcademyService.profile();
      if (!mounted) return;
      setState(() {
        _legal = p['legalStructure'] ?? 'sole_proprietor';
        _premises = p['premisesType'] ?? 'rented';
        _started = DateTime.tryParse('${p['startedTradingOn'] ?? ''}');
        _days = (p['tradingDaysPerWeek'] as num?)?.toInt() ?? 7;
        _bankAccount = p['hasBusinessBankAccount'] == true;
        _cards = p['acceptsCardPayments'] == true;
        _cipc.text = p['cipcRegistrationNumber'] ?? '';
        _sars.text = p['sarsTaxNumber'] ?? '';
        _vat.text = p['vatNumber'] ?? '';
        _permit.text = p['tradingLicenceNumber'] ?? '';
        _rent.text = p['monthlyRent'] == null ? '' : '${p['monthlyRent']}';
        _fullTime.text = '${p['fullTimeStaff'] ?? 0}';
        _partTime.text = '${p['partTimeStaff'] ?? 0}';
        _hours.text = p['tradingHours'] ?? '';
        _bank.text = p['bankName'] ?? '';
        _suppliers.text = p['mainSuppliers'] ?? '';
        _goals.text = p['goals'] ?? '';
      });
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      await ShopAcademyService.saveProfile({
        'legalStructure': _legal,
        'cipcRegistrationNumber': _cipc.text,
        'sarsTaxNumber': _sars.text,
        'vatNumber': _vat.text,
        'tradingLicenceNumber': _permit.text,
        'startedTradingOn': _started == null
            ? null
            : ShopAcademyService.day(_started!),
        'premisesType': _premises,
        'monthlyRent': _premises == 'rented' ? parseAmount(_rent.text) : null,
        'fullTimeStaff': int.tryParse(_fullTime.text) ?? 0,
        'partTimeStaff': int.tryParse(_partTime.text) ?? 0,
        'tradingHours': _hours.text,
        'tradingDaysPerWeek': _days,
        'bankName': _bank.text,
        'hasBusinessBankAccount': _bankAccount,
        'acceptsCardPayments': _cards,
        'mainSuppliers': _suppliers.text,
        'goals': _goals.text,
      });
      if (!mounted) return;
      showAcademyMessage(context, 'Business details saved.');
      Navigator.pop(context, true);
    } catch (e) {
      if (mounted) showAcademyError(context, e);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Widget _heading(String text) => Padding(
    padding: const EdgeInsets.fromLTRB(4, 18, 4, 8),
    child: Text(
      text,
      style: AppTextStyles.subtitle.copyWith(
        fontWeight: FontWeight.w700,
        color: AppColors.primary,
      ),
    ),
  );

  Widget _numberField(TextEditingController c, String label) => TextField(
    controller: c,
    keyboardType: TextInputType.number,
    inputFormatters: [FilteringTextInputFormatter.digitsOnly],
    decoration: academyInput(label),
  );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(title: const Text('Business details')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _failed
          ? AcademyLoadError(onRetry: _load)
          : ListView(
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 32),
              children: [
                Text(
                  'Funders and banks ask for these details. They appear on your Business Record.',
                  style: AppTextStyles.bodySmall,
                ),
                _heading('Registration'),
                DropdownButtonFormField<String>(
                  initialValue: _legal,
                  decoration: academyInput('Business type'),
                  items: [
                    for (final e in legalStructures.entries)
                      DropdownMenuItem(value: e.key, child: Text(e.value)),
                  ],
                  onChanged: (v) => setState(() => _legal = v ?? _legal),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _cipc,
                  decoration: academyInput(
                    'CIPC registration number',
                    hint: 'e.g. 2024/123456/07',
                  ),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _sars,
                  keyboardType: TextInputType.number,
                  decoration: academyInput('SARS tax number'),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _vat,
                  keyboardType: TextInputType.number,
                  decoration: academyInput('VAT number (if registered)'),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _permit,
                  decoration: academyInput('Trading permit number'),
                ),
                _heading('Your shop'),
                ListTile(
                  tileColor: AppColors.surface,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  leading: const Icon(Icons.event_outlined),
                  title: Text(
                    _started == null
                        ? 'When did you start trading?'
                        : 'Trading since ${DateFormat('MMMM yyyy').format(_started!)}',
                  ),
                  onTap: () async {
                    final picked = await showDatePicker(
                      context: context,
                      initialDate: _started ?? DateTime.now(),
                      firstDate: DateTime(1970),
                      lastDate: DateTime.now(),
                    );
                    if (picked != null) setState(() => _started = picked);
                  },
                ),
                const SizedBox(height: 10),
                SegmentedButton<String>(
                  segments: [
                    for (final e in premisesTypes.entries)
                      ButtonSegment(value: e.key, label: Text(e.value)),
                  ],
                  selected: {_premises},
                  onSelectionChanged: (s) =>
                      setState(() => _premises = s.first),
                ),
                if (_premises == 'rented') ...[
                  const SizedBox(height: 10),
                  TextField(
                    controller: _rent,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    decoration: academyInput('Monthly rent', prefix: 'R '),
                  ),
                ],
                const SizedBox(height: 10),
                TextField(
                  controller: _hours,
                  decoration: academyInput(
                    'Trading hours',
                    hint: 'e.g. 06:00 - 21:00',
                  ),
                ),
                const SizedBox(height: 10),
                DropdownButtonFormField<int>(
                  initialValue: _days,
                  decoration: academyInput('Days open per week'),
                  items: [
                    for (var d = 1; d <= 7; d++)
                      DropdownMenuItem(value: d, child: Text('$d days')),
                  ],
                  onChanged: (v) => setState(() => _days = v ?? _days),
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(child: _numberField(_fullTime, 'Full-time staff')),
                    const SizedBox(width: 10),
                    Expanded(child: _numberField(_partTime, 'Part-time staff')),
                  ],
                ),
                _heading('Money'),
                SwitchListTile(
                  tileColor: AppColors.surface,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  value: _bankAccount,
                  onChanged: (v) => setState(() => _bankAccount = v),
                  title: const Text('Separate bank account for the shop'),
                ),
                if (_bankAccount) ...[
                  const SizedBox(height: 10),
                  TextField(
                    controller: _bank,
                    decoration: academyInput('Bank', hint: 'e.g. Capitec'),
                  ),
                ],
                const SizedBox(height: 10),
                SwitchListTile(
                  tileColor: AppColors.surface,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  value: _cards,
                  onChanged: (v) => setState(() => _cards = v),
                  title: const Text('I accept card payments'),
                ),
                _heading('Suppliers and plans'),
                TextField(
                  controller: _suppliers,
                  maxLines: 2,
                  decoration: academyInput(
                    'Where do you buy stock?',
                    hint: 'e.g. SpazaSure, a local wholesaler',
                  ),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _goals,
                  maxLines: 3,
                  decoration: academyInput(
                    'Plans for your shop',
                    hint:
                        'e.g. Buy a second fridge to sell cold drinks and meat',
                  ),
                ),
                const SizedBox(height: 20),
                FilledButton(
                  onPressed: _saving ? null : _save,
                  child: Text(_saving ? 'Saving…' : 'Save business details'),
                ),
              ],
            ),
    );
  }
}
