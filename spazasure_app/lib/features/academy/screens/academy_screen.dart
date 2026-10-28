import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';
import 'package:spazasure_app/features/academy/screens/academy_report_screen.dart';
import 'package:spazasure_app/features/academy/screens/business_profile_screen.dart';
import 'package:spazasure_app/features/academy/widgets/academy_widgets.dart';
import 'package:spazasure_app/features/academy/widgets/cash_book_tab.dart';
import 'package:spazasure_app/features/academy/widgets/lessons_tab.dart';
import 'package:spazasure_app/features/academy/widgets/stock_tab.dart';
import 'package:spazasure_app/services/shop_academy_service.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// Shop Academy: run the spaza as an official business. Stock book, cash book, reports,
/// funding readiness, practical lessons and a Business Record PDF for funders.
class AcademyScreen extends StatefulWidget {
  const AcademyScreen({super.key});

  @override
  State<AcademyScreen> createState() => _AcademyScreenState();
}

class _AcademyScreenState extends State<AcademyScreen> {
  final _overviewKey = GlobalKey<_OverviewTabState>();

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 4,
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: BrandAppBar(
          title: Text(tr('academy.title')),
          bottom: TabBar(
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            tabs: [
              Tab(text: tr('academy.overview')),
              Tab(text: tr('academy.stock')),
              Tab(text: tr('academy.cashBook')),
              Tab(text: tr('academy.lessons')),
            ],
          ),
        ),
        body: TabBarView(
          children: [
            _OverviewTab(key: _overviewKey),
            const StockTab(),
            const CashBookTab(),
            LessonsTab(onProgress: () => _overviewKey.currentState?._load()),
          ],
        ),
      ),
    );
  }
}

class _OverviewTab extends StatefulWidget {
  const _OverviewTab({super.key});

  @override
  State<_OverviewTab> createState() => _OverviewTabState();
}

class _OverviewTabState extends State<_OverviewTab>
    with AutomaticKeepAliveClientMixin {
  bool _loading = true;
  bool _failed = false;
  bool _openingRecord = false;
  Map<String, dynamic> _data = {};

  @override
  bool get wantKeepAlive => true;

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
      final data = await ShopAcademyService.overview();
      if (mounted) setState(() => _data = data);
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  /// Opens the Business Record PDF in the phone's browser, where it can be saved or shared.
  Future<void> _openRecord() async {
    setState(() => _openingRecord = true);
    try {
      final url = await ShopAcademyService.recordLink();
      final opened = await launchUrl(
        Uri.parse(url),
        mode: LaunchMode.externalApplication,
      );
      if (!opened) throw StateError('Could not open the Business Record.');
    } catch (e) {
      if (mounted) {
        showAcademyError(
          context,
          e,
          'Could not open the Business Record. Try again.',
        );
      }
    } finally {
      if (mounted) setState(() => _openingRecord = false);
    }
  }

  Future<void> _push(Widget screen) async {
    await Navigator.push(context, MaterialPageRoute(builder: (_) => screen));
    _load();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_failed) return AcademyLoadError(onRetry: _load);

    final readiness =
        (_data['readiness'] as Map?)?.cast<String, dynamic>() ?? {};
    final score = (readiness['score'] as num?)?.toInt() ?? 0;
    final items = ((readiness['items'] as List?) ?? [])
        .cast<Map<String, dynamic>>();
    final month = (_data['thisMonth'] as Map?)?.cast<String, dynamic>() ?? {};
    final stock = (_data['stock'] as Map?)?.cast<String, dynamic>() ?? {};
    final alerts = ((_data['stockAlerts'] as List?) ?? [])
        .cast<Map<String, dynamic>>();
    final lessons = (_data['lessons'] as Map?)?.cast<String, dynamic>() ?? {};
    final moneyOut =
        ((month['stockPurchases'] as num?) ?? 0) +
        ((month['operatingExpenses'] as num?) ?? 0);
    final net = (month['netProfit'] as num?) ?? 0;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.only(top: 12, bottom: 40),
        children: [
          _readinessHeader(score),
          AcademyCard(
            title: 'This month (${DateFormat('MMMM').format(DateTime.now())})',
            icon: Icons.calendar_month_rounded,
            trailing: TextButton(
              onPressed: () => _push(const AcademyReportScreen()),
              child: const Text('6-month report'),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: AcademyStat(
                        ShopAcademyService.rand(month['income']),
                        'Money in',
                        color: AppColors.success,
                      ),
                    ),
                    Expanded(
                      child: AcademyStat(
                        ShopAcademyService.rand(moneyOut),
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
                const SizedBox(height: 8),
                Text(
                  '${month['daysRecorded'] ?? 0} days recorded',
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textHint,
                  ),
                ),
              ],
            ),
          ),
          AcademyCard(
            title: 'Stock',
            icon: Icons.inventory_2_outlined,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: AcademyStat(
                        ShopAcademyService.rand(stock['valueAtCost']),
                        'Value at cost',
                      ),
                    ),
                    Expanded(
                      child: AcademyStat('${stock['items'] ?? 0}', 'Products'),
                    ),
                    Expanded(
                      child: AcademyStat(
                        '${stock['lowStock'] ?? 0}',
                        'Running low',
                        color: AppColors.red,
                      ),
                    ),
                  ],
                ),
                if (alerts.isNotEmpty) ...[
                  const Divider(height: 24),
                  for (final a in alerts.take(5))
                    Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Row(
                        children: [
                          Icon(
                            a['expiryDate'] != null &&
                                    (a['quantity'] as num) >
                                        (a['reorderLevel'] as num)
                                ? Icons.event_busy_rounded
                                : Icons.warning_amber_rounded,
                            size: 16,
                            color: AppColors.accentDark,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              a['name'] ?? '',
                              style: AppTextStyles.bodySmall,
                            ),
                          ),
                          Text(
                            _alertText(a),
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textHint,
                            ),
                          ),
                        ],
                      ),
                    ),
                ],
              ],
            ),
          ),
          AcademyCard(
            title: 'Funding readiness checklist',
            icon: Icons.checklist_rounded,
            child: Column(children: items.map(_readinessRow).toList()),
          ),
          AcademyCard(
            child: Column(
              children: [
                _action(
                  Icons.business_center_outlined,
                  'Business details',
                  '${_data['profileCompleteness'] ?? 0}% complete',
                  () => _push(const BusinessProfileScreen()),
                ),
                _action(
                  Icons.school_outlined,
                  'Lessons',
                  '${lessons['done'] ?? 0} of ${lessons['total'] ?? 0} passed',
                  () => DefaultTabController.of(context).animateTo(3),
                ),
                _action(
                  Icons.bar_chart_rounded,
                  '6-month report',
                  'Money in, money out and profit',
                  () => _push(const AcademyReportScreen()),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static String _alertText(Map<String, dynamic> a) {
    final qty = a['quantity'] as num;
    if (qty <= (a['reorderLevel'] as num)) return '$qty left';
    final d = DateTime.tryParse('${a['expiryDate']}');
    return d == null ? '' : 'expires ${DateFormat('d MMM').format(d)}';
  }

  Widget _readinessHeader(int score) {
    final color = score >= 70
        ? AppColors.success
        : score >= 40
        ? AppColors.accentDark
        : AppColors.red;
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [AppColors.primary, AppColors.primaryLight],
        ),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              SizedBox(
                width: 76,
                height: 76,
                child: Stack(
                  alignment: Alignment.center,
                  children: [
                    SizedBox.expand(
                      child: CircularProgressIndicator(
                        value: score / 100,
                        strokeWidth: 8,
                        backgroundColor: Colors.white24,
                        color: color == AppColors.red
                            ? Colors.white
                            : AppColors.secondaryLight,
                      ),
                    ),
                    Text(
                      '$score',
                      style: AppTextStyles.h2.copyWith(color: Colors.white),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Funding readiness',
                      style: AppTextStyles.subtitle.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      score >= 70
                          ? 'Your records look strong. Keep them up to date.'
                          : 'Complete the checklist below to show funders a well-run business.',
                      style: AppTextStyles.bodySmall.copyWith(
                        color: Colors.white.withValues(alpha: 0.9),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          SizedBox(
            width: double.infinity,
            child: FilledButton.icon(
              style: FilledButton.styleFrom(
                backgroundColor: Colors.white,
                foregroundColor: AppColors.primary,
              ),
              onPressed: _openingRecord ? null : _openRecord,
              icon: const Icon(Icons.picture_as_pdf_outlined),
              label: Text(
                _openingRecord ? 'Opening…' : 'Business Record (PDF)',
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _readinessRow(Map<String, dynamic> item) {
    final done = item['done'] == true;
    return Theme(
      data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
      child: ExpansionTile(
        tilePadding: EdgeInsets.zero,
        childrenPadding: const EdgeInsets.only(left: 40, bottom: 8),
        leading: Icon(
          done
              ? Icons.check_circle_rounded
              : Icons.radio_button_unchecked_rounded,
          color: done ? AppColors.success : AppColors.textHint,
        ),
        title: Text(item['label'] ?? '', style: AppTextStyles.body),
        trailing: Text(
          '${item['earned']}/${item['points']}',
          style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
        ),
        children: [
          Align(
            alignment: Alignment.centerLeft,
            child: Text(item['tip'] ?? '', style: AppTextStyles.bodySmall),
          ),
        ],
      ),
    );
  }

  Widget _action(
    IconData icon,
    String title,
    String subtitle,
    VoidCallback onTap,
  ) => ListTile(
    contentPadding: EdgeInsets.zero,
    leading: CircleAvatar(
      backgroundColor: AppColors.primaryTint,
      child: Icon(icon, color: AppColors.primary),
    ),
    title: Text(
      title,
      style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
    ),
    subtitle: Text(subtitle),
    trailing: const Icon(Icons.chevron_right_rounded),
    onTap: onTap,
  );
}
