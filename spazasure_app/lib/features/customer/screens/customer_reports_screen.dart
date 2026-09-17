import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';
import 'package:spazasure_app/services/customer_report_service.dart';

class CustomerReportsScreen extends StatefulWidget {
  const CustomerReportsScreen({super.key});

  @override
  State<CustomerReportsScreen> createState() => _CustomerReportsScreenState();
}

class _CustomerReportsScreenState extends State<CustomerReportsScreen> {
  List<CustomerReport> _reports = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final reports = await CustomerReportService.list();
      if (mounted) setState(() => _reports = reports);
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
      title: const Text('My reports'),
      actions: [IconButton(onPressed: _load, icon: const Icon(Icons.refresh))],
    ),
    floatingActionButton: FloatingActionButton.extended(
      onPressed: () async {
        await Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => const ReportScreen()),
        );
        _load();
      },
      icon: const Icon(Icons.add),
      label: const Text('New report'),
    ),
    body: RefreshIndicator(
      onRefresh: _load,
      child: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
          ? ListView(
              padding: const EdgeInsets.all(24),
              children: [
                Text(
                  _error!,
                  style: AppTextStyles.body.copyWith(color: AppColors.error),
                ),
                const SizedBox(height: 12),
                FilledButton(onPressed: _load, child: const Text('Retry')),
              ],
            )
          : _reports.isEmpty
          ? ListView(
              padding: const EdgeInsets.all(40),
              children: const [
                Icon(
                  Icons.verified_user_outlined,
                  size: 56,
                  color: AppColors.textHint,
                ),
                SizedBox(height: 12),
                Center(child: Text('No reports submitted yet.')),
              ],
            )
          : ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: _reports.length,
              itemBuilder: (_, index) => _card(_reports[index]),
            ),
    ),
  );

  Widget _card(CustomerReport report) {
    final date = report.createdAt == null
        ? ''
        : DateFormat('dd MMM yyyy').format(report.createdAt!);
    final statusColor = report.status == 'resolved'
        ? AppColors.success
        : AppColors.warning;
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: ListTile(
        leading: Icon(Icons.report_problem_outlined, color: statusColor),
        title: Text(
          report.reportType ?? 'Product report',
          style: AppTextStyles.subtitle,
        ),
        subtitle: Text(
          [
            if (report.productName?.isNotEmpty == true) report.productName!,
            report.description,
            if (date.isNotEmpty) date,
          ].join(' • '),
          maxLines: 3,
          overflow: TextOverflow.ellipsis,
        ),
        trailing: Chip(
          label: Text(report.status),
          labelStyle: TextStyle(color: statusColor),
        ),
      ),
    );
  }
}
