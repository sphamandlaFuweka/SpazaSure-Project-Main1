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

  Future<void> _openReport(CustomerReport report) async {
    try {
      final detail = await CustomerReportService.getById(report.id);
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(detail.reportType ?? 'Product report'),
          content: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(detail.description),
                if (detail.barcode?.isNotEmpty == true) ...[
                  const SizedBox(height: 12),
                  Text('Barcode: ${detail.barcode}'),
                ],
                if (detail.escalatedTo?.isNotEmpty == true) ...[
                  const SizedBox(height: 12),
                  Text('Referred to ${detail.escalatedTo}'),
                ],
                if (detail.resolutionNote?.isNotEmpty == true) ...[
                  const SizedBox(height: 12),
                  const Text(
                    'SpazaSure update',
                    style: TextStyle(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 4),
                  Text(detail.resolutionNote!),
                ],
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Close'),
            ),
          ],
        ),
      );
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Could not load report details: $error')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: AppBar(
      title: const Text('My reports'),
      actions: [
        IconButton(onPressed: _load, icon: const Icon(Icons.refresh_rounded)),
      ],
    ),
    floatingActionButton: FloatingActionButton.extended(
      onPressed: () async {
        await Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => const ReportScreen()),
        );
        _load();
      },
      icon: const Icon(Icons.add_rounded),
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
                  Icons.verified_user_rounded,
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
    final statusColor = switch (report.status) {
      'resolved' => AppColors.success,
      'escalated' => AppColors.info,
      'dismissed' => AppColors.textHint,
      _ => AppColors.warning,
    };
    final statusLabel = switch (report.status) {
      'under_review' => 'Under review',
      'escalated' => 'Escalated',
      'resolved' => 'Resolved',
      'dismissed' => 'Closed',
      _ => 'Submitted',
    };
    final response = report.resolutionNote?.trim();
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: ListTile(
        onTap: () => _openReport(report),
        leading: Icon(Icons.flag_rounded, color: statusColor),
        title: Text(
          report.reportType ?? 'Product report',
          style: AppTextStyles.subtitle,
        ),
        subtitle: Text(
          [
            if (report.productName?.isNotEmpty == true) report.productName!,
            report.description,
            if (date.isNotEmpty) date,
            if (report.escalatedTo?.isNotEmpty == true)
              'Referred to ${report.escalatedTo}',
            if (response?.isNotEmpty == true) 'SpazaSure replied: $response',
          ].join(' • '),
          maxLines: 5,
          overflow: TextOverflow.ellipsis,
        ),
        trailing: Chip(
          label: Text(statusLabel),
          labelStyle: TextStyle(color: statusColor),
        ),
      ),
    );
  }
}
