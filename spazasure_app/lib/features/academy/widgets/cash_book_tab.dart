import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/academy/widgets/academy_widgets.dart';
import 'package:spazasure_app/services/shop_academy_service.dart';

/// The cash book: money in (takings) and money out (expenses), one month at a time.
class CashBookTab extends StatefulWidget {
  const CashBookTab({super.key});

  @override
  State<CashBookTab> createState() => _CashBookTabState();
}

class _CashBookTabState extends State<CashBookTab>
    with AutomaticKeepAliveClientMixin {
  static const incomeCategories = [
    'cash_sales',
    'card_sales',
    'airtime_electricity',
    'other_income',
  ];
  static const expenseCategories = [
    'stock_purchase',
    'rent',
    'electricity',
    'transport',
    'wages',
    'airtime_data',
    'repairs',
    'licences_fees',
    'owner_salary',
    'other_expense',
  ];

  DateTime _month = DateTime(DateTime.now().year, DateTime.now().month);
  bool _loading = true;
  bool _failed = false;
  Map<String, dynamic> _book = {};

  @override
  bool get wantKeepAlive => true;

  bool get _isThisMonth =>
      _month.year == DateTime.now().year &&
      _month.month == DateTime.now().month;

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
      final data = await ShopAcademyService.cashBook(_month);
      if (mounted) setState(() => _book = data);
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _shiftMonth(int by) {
    setState(() => _month = DateTime(_month.year, _month.month + by));
    _load();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final entries = ((_book['entries'] as List?) ?? [])
        .cast<Map<String, dynamic>>();
    final byCategory = ((_book['expenseByCategory'] as List?) ?? [])
        .cast<Map<String, dynamic>>();
    final expenses = (_book['expenses'] as num?)?.toDouble() ?? 0;
    final net = (_book['net'] as num?)?.toDouble() ?? 0;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.only(top: 8, bottom: 100),
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              IconButton(
                onPressed: () => _shiftMonth(-1),
                icon: const Icon(Icons.chevron_left_rounded),
              ),
              Text(
                DateFormat('MMMM yyyy').format(_month),
                style: AppTextStyles.subtitle.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
              IconButton(
                onPressed: _isThisMonth ? null : () => _shiftMonth(1),
                icon: const Icon(Icons.chevron_right_rounded),
              ),
            ],
          ),
          if (_loading)
            const Padding(
              padding: EdgeInsets.all(40),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_failed)
            AcademyLoadError(onRetry: _load)
          else ...[
            AcademyCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: AcademyStat(
                          ShopAcademyService.rand(_book['income']),
                          'Money in',
                          color: AppColors.success,
                        ),
                      ),
                      Expanded(
                        child: AcademyStat(
                          ShopAcademyService.rand(expenses),
                          'Money out',
                          color: AppColors.red,
                        ),
                      ),
                      Expanded(
                        child: AcademyStat(
                          ShopAcademyService.rand(net),
                          net >= 0 ? 'Profit' : 'Loss',
                          color: net >= 0 ? AppColors.primary : AppColors.red,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Text(
                    '${_book['daysRecorded'] ?? 0} days recorded this month. '
                    'Record at least 8 days a month to build a trading record.',
                    style: AppTextStyles.caption.copyWith(
                      color: AppColors.textHint,
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: Row(
                children: [
                  Expanded(
                    child: FilledButton.icon(
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.success,
                      ),
                      onPressed: () => _addEntry('income'),
                      icon: const Icon(Icons.south_west_rounded),
                      label: const Text('Money in'),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: FilledButton.icon(
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.red,
                      ),
                      onPressed: () => _addEntry('expense'),
                      icon: const Icon(Icons.north_east_rounded),
                      label: const Text('Money out'),
                    ),
                  ),
                ],
              ),
            ),
            if (byCategory.isNotEmpty)
              AcademyCard(
                title: 'Where the money went',
                icon: Icons.pie_chart_outline_rounded,
                child: Column(
                  children: [
                    for (final c in byCategory)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    ShopAcademyService.categoryLabel(
                                      c['category'],
                                    ),
                                    style: AppTextStyles.bodySmall,
                                  ),
                                ),
                                Text(
                                  ShopAcademyService.rand(c['amount']),
                                  style: AppTextStyles.bodySmall.copyWith(
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            ClipRRect(
                              borderRadius: BorderRadius.circular(4),
                              child: LinearProgressIndicator(
                                minHeight: 6,
                                value: expenses > 0
                                    ? (c['amount'] as num).toDouble() / expenses
                                    : 0,
                                backgroundColor: AppColors.divider,
                                color: AppColors.red.withValues(alpha: 0.7),
                              ),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              ),
            if (entries.isEmpty)
              AcademyCard(
                child: Text(
                  'Nothing recorded for this month yet. At closing, add your takings under "Money in" and every expense under "Money out".',
                  style: AppTextStyles.bodySmall,
                ),
              )
            else
              AcademyCard(
                title: 'Entries',
                icon: Icons.list_alt_rounded,
                child: Column(children: entries.map(_entryTile).toList()),
              ),
          ],
        ],
      ),
    );
  }

  Widget _entryTile(Map<String, dynamic> e) {
    final income = e['direction'] == 'income';
    final date = DateTime.tryParse('${e['occurredOn']}');
    return ListTile(
      contentPadding: EdgeInsets.zero,
      dense: true,
      leading: CircleAvatar(
        radius: 16,
        backgroundColor: (income ? AppColors.success : AppColors.red)
            .withValues(alpha: 0.12),
        child: Icon(
          income ? Icons.south_west_rounded : Icons.north_east_rounded,
          size: 16,
          color: income ? AppColors.success : AppColors.red,
        ),
      ),
      title: Text(
        ShopAcademyService.categoryLabel(e['category']),
        style: AppTextStyles.body,
      ),
      subtitle: Text(
        '${date == null ? '' : DateFormat('EEE d MMM').format(date)}${e['note'] != null ? ' · ${e['note']}' : ''}',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: Text(
        '${income ? '+' : '-'}${ShopAcademyService.rand(e['amount'])}',
        style: AppTextStyles.body.copyWith(
          fontWeight: FontWeight.w700,
          color: income ? AppColors.success : AppColors.red,
        ),
      ),
      onLongPress: () => _delete(e),
    );
  }

  Future<void> _delete(Map<String, dynamic> e) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete entry?'),
        content: Text(
          '${ShopAcademyService.categoryLabel(e['category'])}: ${ShopAcademyService.rand(e['amount'])}',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await ShopAcademyService.deleteCashEntry(e['id']);
      _load();
    } catch (err) {
      if (mounted) showAcademyError(context, err);
    }
  }

  Future<void> _addEntry(String direction) async {
    final categories = direction == 'income'
        ? incomeCategories
        : expenseCategories;
    var category = categories.first;
    var date = _isThisMonth
        ? DateTime.now()
        : DateTime(_month.year, _month.month + 1, 0);
    final amount = TextEditingController();
    final note = TextEditingController();

    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setSheet) => Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            0,
            20,
            MediaQuery.of(ctx).viewInsets.bottom + 20,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                direction == 'income' ? 'Money in' : 'Money out',
                style: AppTextStyles.h3,
              ),
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  for (final c in categories)
                    ChoiceChip(
                      label: Text(ShopAcademyService.categoryLabel(c)),
                      selected: category == c,
                      onSelected: (_) => setSheet(() => category = c),
                    ),
                ],
              ),
              const SizedBox(height: 12),
              TextField(
                controller: amount,
                autofocus: true,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: academyInput('Amount', prefix: 'R '),
              ),
              const SizedBox(height: 10),
              TextField(
                controller: note,
                decoration: academyInput('Note (optional)'),
              ),
              const SizedBox(height: 4),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.event_outlined),
                title: Text(DateFormat('EEEE d MMMM yyyy').format(date)),
                trailing: const Text('Change'),
                onTap: () async {
                  final picked = await showDatePicker(
                    context: ctx,
                    initialDate: date,
                    firstDate: DateTime.now().subtract(
                      const Duration(days: 366),
                    ),
                    lastDate: DateTime.now(),
                  );
                  if (picked != null) setSheet(() => date = picked);
                },
              ),
              const SizedBox(height: 8),
              FilledButton(
                onPressed: () => Navigator.pop(ctx, true),
                child: const Text('Save'),
              ),
            ],
          ),
        ),
      ),
    );
    if (saved != true || !mounted) return;
    final value = parseAmount(amount.text);
    if (value == null || value <= 0) {
      showAcademyError(context, 'none', 'Enter an amount above R0.');
      return;
    }
    try {
      await ShopAcademyService.addCashEntry(
        direction: direction,
        category: category,
        amount: value,
        note: note.text,
        occurredOn: date,
      );
      if (!mounted) return;
      showAcademyMessage(context, 'Saved to your cash book.');
      // Jump to the month the entry was saved in
      _month = DateTime(date.year, date.month);
      _load();
    } catch (e) {
      if (mounted) showAcademyError(context, e);
    }
  }
}
