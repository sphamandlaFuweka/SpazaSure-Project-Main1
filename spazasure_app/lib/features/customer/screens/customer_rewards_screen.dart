import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/api_service.dart';

class CustomerRewardsScreen extends StatefulWidget {
  const CustomerRewardsScreen({super.key});

  @override
  State<CustomerRewardsScreen> createState() => _CustomerRewardsScreenState();
}

class _CustomerRewardsScreenState extends State<CustomerRewardsScreen> {
  Map<String, dynamic>? _summary;
  bool _loading = true;
  bool _redeeming = false;
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
      final response = await ApiService.get('/customer/rewards');
      if (mounted) {
        setState(
          () => _summary = response['data'] as Map<String, dynamic>? ?? {},
        );
      }
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _redeem() async {
    setState(() => _redeeming = true);
    try {
      final response = await ApiService.post('/customer/rewards/redeem', {});
      final data = response['data'] as Map<String, dynamic>? ?? {};
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              'Voucher ${data['code']} is ready for R20 off at a participating spaza shop.',
            ),
          ),
        );
        await _load();
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(error.toString())));
      }
    } finally {
      if (mounted) setState(() => _redeeming = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final points = (_summary?['points'] as num?)?.toInt() ?? 0;
    final progress = (points % 500) / 500;
    final vouchers = (_summary?['vouchers'] as List?) ?? const [];
    return Scaffold(
      appBar: AppBar(title: const Text('My Rewards')),
      backgroundColor: AppColors.background,
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(20),
                children: [
                  if (_error != null)
                    Text(_error!, style: TextStyle(color: AppColors.error)),
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(20),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('$points points', style: AppTextStyles.h1),
                          const SizedBox(height: 6),
                          const Text(
                            'Earn 5 points every day you scan a product code.',
                          ),
                          const SizedBox(height: 16),
                          LinearProgressIndicator(value: progress),
                          const SizedBox(height: 8),
                          Text(
                            '${500 - (points % 500)} points to your next R20 voucher.',
                          ),
                          const SizedBox(height: 16),
                          SizedBox(
                            width: double.infinity,
                            child: FilledButton.icon(
                              onPressed: points >= 500 && !_redeeming
                                  ? _redeem
                                  : null,
                              icon: _redeeming
                                  ? const SizedBox(
                                      width: 18,
                                      height: 18,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                      ),
                                    )
                                  : const Icon(Icons.redeem),
                              label: const Text('Redeem 500 points for R20'),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),
                  Text('Active vouchers', style: AppTextStyles.h3),
                  const SizedBox(height: 8),
                  if (vouchers.isEmpty)
                    const Card(
                      child: ListTile(
                        title: Text('No vouchers yet'),
                        subtitle: Text(
                          'Keep scanning to earn your first R20 voucher.',
                        ),
                      ),
                    )
                  else
                    ...vouchers.map((voucher) {
                      final item = voucher as Map<String, dynamic>;
                      return Card(
                        child: ListTile(
                          leading: const Icon(
                            Icons.local_offer,
                            color: AppColors.primary,
                          ),
                          title: Text('R${item['amount']} voucher'),
                          subtitle: Text(item['code'].toString()),
                          trailing: const Text('ACTIVE'),
                        ),
                      );
                    }),
                ],
              ),
            ),
    );
  }
}
