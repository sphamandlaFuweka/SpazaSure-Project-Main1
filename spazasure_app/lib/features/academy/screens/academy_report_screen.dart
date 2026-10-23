import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/academy/widgets/academy_widgets.dart';
import 'package:spazasure_app/services/shop_academy_service.dart';

/// Six months of trading from the cash book and stock book: money in, costs, profit and losses.
class AcademyReportScreen extends StatefulWidget {
  const AcademyReportScreen({super.key});

  @override
  State<AcademyReportScreen> createState() => _AcademyReportScreenState();
}

class _AcademyReportScreenState extends State<AcademyReportScreen> {
  bool _loading = true;
  bool _failed = false;
  Map<String, dynamic> _report = {};

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
      final data = await ShopAcademyService.report(months: 6);
      if (mounted) setState(() => _report = data);
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  static String _monthLabel(String ym) {
    final d = DateTime.tryParse('$ym-01');
    return d == null ? ym : DateFormat('MMM').format(d);
  }

  @override
  Widget build(BuildContext context) {
    final months = ((_report['months'] as List?) ?? []).cast<Map<String, dynamic>>();
    final totals = (_report['totals'] as Map?)?.cast<String, dynamic>() ?? {};
    final top = months.fold<double>(1, (m, r) => math.max(m, math.max(
      (r['income'] as num).toDouble(),
      (r['stockPurchases'] as num).toDouble() + (r['operatingExpenses'] as num).toDouble(),
    )));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('6-month report')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _failed
          ? AcademyLoadError(onRetry: _load)
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.only(top: 12, bottom: 32),
                children: [
                  AcademyCard(
                    child: Column(
                      children: [
                        Row(
                          children: [
                            Expanded(child: AcademyStat(ShopAcademyService.rand(totals['income']), 'Money in', color: AppColors.success)),
                            Expanded(
                              child: AcademyStat(
                                ShopAcademyService.rand(((totals['stockPurchases'] as num?) ?? 0) + ((totals['operatingExpenses'] as num?) ?? 0)),
                                'Money out',
                                color: AppColors.red,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        Row(
                          children: [
                            Expanded(child: AcademyStat(ShopAcademyService.rand(totals['netProfit']), 'Profit', color: AppColors.primary)),
                            Expanded(child: AcademyStat(ShopAcademyService.rand(totals['stockLosses']), 'Stock losses', color: AppColors.accentDark)),
                          ],
                        ),
                      ],
                    ),
                  ),
                  AcademyCard(
                    title: 'Money in vs money out',
                    icon: Icons.bar_chart_rounded,
                    child: Column(
                      children: [
                        SizedBox(
                          height: 160,
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.end,
                            children: [
                              for (final m in months)
                                Expanded(
                                  child: Column(
                                    mainAxisAlignment: MainAxisAlignment.end,
                                    children: [
                                      Row(
                                        mainAxisAlignment: MainAxisAlignment.center,
                                        crossAxisAlignment: CrossAxisAlignment.end,
                                        children: [
                                          _bar((m['income'] as num).toDouble() / top, AppColors.success),
                                          const SizedBox(width: 3),
                                          _bar(((m['stockPurchases'] as num) + (m['operatingExpenses'] as num)).toDouble() / top, AppColors.red),
                                        ],
                                      ),
                                      const SizedBox(height: 6),
                                      Text(_monthLabel(m['month']), style: AppTextStyles.caption),
                                    ],
                                  ),
                                ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 8),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            _legend('Money in', AppColors.success),
                            const SizedBox(width: 16),
                            _legend('Money out', AppColors.red),
                          ],
                        ),
                      ],
                    ),
                  ),
                  AcademyCard(
                    title: 'Month by month',
                    icon: Icons.table_rows_outlined,
                    child: Column(
                      children: [
                        for (final m in months.reversed)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 10),
                            child: Row(
                              children: [
                                SizedBox(
                                  width: 70,
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(_monthLabel(m['month']), style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700)),
                                      Text('${m['daysRecorded']} days', style: AppTextStyles.caption.copyWith(color: AppColors.textHint)),
                                    ],
                                  ),
                                ),
                                Expanded(
                                  child: Text(
                                    'In ${ShopAcademyService.rand(m['income'])}\n'
                                    'Out ${ShopAcademyService.rand((m['stockPurchases'] as num) + (m['operatingExpenses'] as num))}',
                                    style: AppTextStyles.bodySmall,
                                  ),
                                ),
                                Text(
                                  ShopAcademyService.rand(m['netProfit']),
                                  style: AppTextStyles.body.copyWith(
                                    fontWeight: FontWeight.w800,
                                    color: (m['netProfit'] as num) >= 0 ? AppColors.primary : AppColors.red,
                                  ),
                                ),
                              ],
                            ),
                          ),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 24),
                    child: Text(
                      '${_report['recordedMonths'] ?? 0} of these months have entries on 8 or more days. '
                      'Funders usually want 3 to 6 recorded months.',
                      style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
                      textAlign: TextAlign.center,
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  Widget _bar(double fraction, Color color) => Container(
    width: 12,
    height: math.max(2, 120 * fraction.clamp(0, 1)),
    decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(4)),
  );

  Widget _legend(String label, Color color) => Row(
    children: [
      Container(width: 10, height: 10, decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(3))),
      const SizedBox(width: 6),
      Text(label, style: AppTextStyles.caption),
    ],
  );
}
