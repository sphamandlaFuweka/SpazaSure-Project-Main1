import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/customer_report_service.dart';
import 'package:spazasure_app/services/customer_scan_service.dart';

/// Shows every product the customer has scanned AND every product they've
/// reported, in one timeline — Reports and Scans are separate backend
/// resources so this screen merges the two lists client-side.
class CustomerScanHistoryScreen extends StatefulWidget {
  const CustomerScanHistoryScreen({super.key});

  @override
  State<CustomerScanHistoryScreen> createState() =>
      _CustomerScanHistoryScreenState();
}

class _CustomerScanHistoryScreenState extends State<CustomerScanHistoryScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  List<CustomerScanEvent> _scans = [];
  List<CustomerReport> _reports = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
    _load();
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        CustomerScanService.list(),
        CustomerReportService.list(),
      ]);
      if (mounted) {
        setState(() {
          _scans = results[0] as List<CustomerScanEvent>;
          _reports = results[1] as List<CustomerReport>;
        });
      }
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: AppBar(
      title: const Text('Scan history'),
      actions: [IconButton(onPressed: _load, icon: const Icon(Icons.refresh))],
      bottom: TabBar(
        controller: _tabController,
        tabs: [
          const Tab(text: 'All'),
          Tab(text: 'Scanned (${_scans.length})'),
          Tab(text: 'Reported (${_reports.length})'),
        ],
      ),
    ),
    body: _loading
        ? const Center(child: CircularProgressIndicator())
        : _error != null
        ? _errorView()
        : RefreshIndicator(
            onRefresh: _load,
            child: TabBarView(
              controller: _tabController,
              children: [
                _timeline(
                  [
                    ..._scans.map(_ScanOrReport.scan),
                    ..._reports.map(_ScanOrReport.report),
                  ]..sort(
                    (a, b) => (b.date ?? DateTime(0)).compareTo(
                      a.date ?? DateTime(0),
                    ),
                  ),
                ),
                _timeline(_scans.map(_ScanOrReport.scan).toList()),
                _timeline(_reports.map(_ScanOrReport.report).toList()),
              ],
            ),
          ),
  );

  Widget _errorView() => ListView(
    padding: const EdgeInsets.all(24),
    children: [
      Text(_error!, style: AppTextStyles.body.copyWith(color: AppColors.error)),
      const SizedBox(height: 12),
      FilledButton(onPressed: _load, child: const Text('Retry')),
    ],
  );

  Widget _timeline(List<_ScanOrReport> items) {
    if (items.isEmpty) {
      return ListView(
        padding: const EdgeInsets.all(40),
        children: const [
          Icon(Icons.history_rounded, size: 56, color: AppColors.textHint),
          SizedBox(height: 12),
          Center(child: Text('Nothing here yet.')),
        ],
      );
    }
    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: items.length,
      itemBuilder: (_, index) => _card(items[index]),
    );
  }

  Widget _card(_ScanOrReport item) {
    final date = item.date == null
        ? ''
        : DateFormat('dd MMM yyyy, HH:mm').format(item.date!);
    final isReport = item.isReport;
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: ListTile(
        leading: Icon(
          isReport
              ? Icons.report_problem_outlined
              : Icons.qr_code_scanner_rounded,
          color: isReport ? AppColors.warning : AppColors.primary,
        ),
        title: Text(item.title, style: AppTextStyles.subtitle),
        subtitle: Text(
          [
            if (item.subtitle.isNotEmpty) item.subtitle,
            date,
          ].where((s) => s.isNotEmpty).join(' • '),
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
        ),
        trailing: Chip(
          label: Text(isReport ? item.status : '+${item.points} pts'),
        ),
      ),
    );
  }
}

class _ScanOrReport {
  final bool isReport;
  final String title;
  final String subtitle;
  final String status;
  final int points;
  final DateTime? date;

  const _ScanOrReport({
    required this.isReport,
    required this.title,
    required this.subtitle,
    this.status = '',
    this.points = 0,
    this.date,
  });

  factory _ScanOrReport.scan(CustomerScanEvent scan) => _ScanOrReport(
    isReport: false,
    title: scan.productName ?? 'Code ${scan.code}',
    subtitle: scan.source,
    points: scan.pointsAwarded,
    date: scan.createdAt,
  );

  factory _ScanOrReport.report(CustomerReport report) => _ScanOrReport(
    isReport: true,
    title: report.productName ?? report.reportType ?? 'Product report',
    subtitle: report.shopName ?? '',
    status: report.status,
    date: report.createdAt,
  );
}
