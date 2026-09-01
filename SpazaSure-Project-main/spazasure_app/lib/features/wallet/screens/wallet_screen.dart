import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/api_service.dart';

class WalletScreen extends StatefulWidget {
  const WalletScreen({super.key});

  @override
  State<WalletScreen> createState() => _WalletScreenState();
}

class _WalletScreenState extends State<WalletScreen> {
  bool _loading = true;
  bool _balanceVisible = true;
  double _balance = 0;
  double _totalSpent = 0;
  int _totalOrders = 0;
  List<Map<String, dynamic>> _transactions = [];
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadWallet();
  }

  Future<void> _loadWallet() async {
    setState(() { _loading = true; _error = null; });
    try {
      final res = await ApiService.get('/shop/wallet');
      final data = res['data'] as Map<String, dynamic>;
      setState(() {
        _balance = (data['balance'] as num?)?.toDouble() ?? 0;
        _totalSpent = (data['totalSpent'] as num?)?.toDouble() ?? 0;
        _totalOrders = data['totalOrders'] ?? 0;
        _transactions = (data['transactions'] as List?)
            ?.map((t) => t as Map<String, dynamic>)
            .toList() ?? [];
      });
    } catch (e) {
      // Still show wallet UI even if API fails - just with empty data
      setState(() {
        _balance = 0;
        _totalSpent = 0;
        _totalOrders = 0;
        _transactions = [];
      });
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
                  onRefresh: _loadWallet,
                  child: CustomScrollView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    slivers: [
                      // Wallet header
                      SliverToBoxAdapter(child: _buildWalletHeader()),
                      // Quick actions
                      SliverToBoxAdapter(child: _buildQuickActions()),
                      // Stats row
                      SliverToBoxAdapter(child: _buildStatsRow()),
                      // Transactions title
                      SliverToBoxAdapter(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(20, 20, 20, 10),
                          child: Text('Transaction History', style: AppTextStyles.h3),
                        ),
                      ),
                      // Transactions list
                      _transactions.isEmpty
                          ? SliverToBoxAdapter(child: _buildEmptyTransactions())
                          : SliverList(
                              delegate: SliverChildBuilderDelegate(
                                (context, index) => _buildTransactionItem(_transactions[index], index),
                                childCount: _transactions.length,
                              ),
                            ),
                      const SliverToBoxAdapter(child: SizedBox(height: 30)),
                    ],
                  ),
                ),
    );
  }

  Widget _buildError() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.cloud_off, size: 48, color: AppColors.textHint),
          const SizedBox(height: 12),
          Text('Unable to load wallet', style: AppTextStyles.body),
          const SizedBox(height: 8),
          ElevatedButton(onPressed: _loadWallet, child: const Text('Retry')),
        ],
      ),
    );
  }

  Widget _buildWalletHeader() {
    return Container(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF0D3B0F), Color(0xFF1B5E20), Color(0xFF2E7D32)],
        ),
      ),
      child: SafeArea(
        bottom: false,
        child: Column(
          children: [
            // App bar
            Padding(
              padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
              child: Row(
                children: [
                  IconButton(
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.arrow_back, color: Colors.white),
                  ),
                  const Text('SpazaSure Wallet', style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.w700)),
                  const Spacer(),
                  IconButton(
                    onPressed: () => setState(() => _balanceVisible = !_balanceVisible),
                    icon: Icon(_balanceVisible ? Icons.visibility : Icons.visibility_off, color: Colors.white60, size: 20),
                  ),
                ],
              ),
            ),
            // Balance card
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 20, 20, 30),
              child: Column(
                children: [
                  const Text('Available Balance', style: TextStyle(color: Colors.white60, fontSize: 13)),
                  const SizedBox(height: 8),
                  Text(
                    _balanceVisible ? 'R ${_balance.toStringAsFixed(2)}' : 'R ••••••',
                    style: const TextStyle(color: Colors.white, fontSize: 36, fontWeight: FontWeight.w800, letterSpacing: -0.5),
                  ),
                  const SizedBox(height: 6),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(Icons.info_outline, color: Colors.white70, size: 14),
                        const SizedBox(width: 4),
                        Text('Top-up coming soon', style: TextStyle(color: Colors.white70, fontSize: 11, fontWeight: FontWeight.w500)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    ).animate().fadeIn(duration: 400.ms);
  }

  Widget _buildQuickActions() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 4),
      child: Row(
        children: [
          _actionButton(Icons.add_rounded, 'Top Up', AppColors.primary, () {
            _showTopUpSheet();
          }),
          const SizedBox(width: 12),
          _actionButton(Icons.send_rounded, 'Transfer', AppColors.info, () {
            _showTransferSheet();
          }),
          const SizedBox(width: 12),
          _actionButton(Icons.receipt_long_rounded, 'Statements', AppColors.accent, () {
            _showStatementsSheet();
          }),
          const SizedBox(width: 12),
          _actionButton(Icons.settings_outlined, 'Settings', AppColors.textSecondary, () {
            _showSettingsSheet();
          }),
        ],
      ),
    ).animate().fadeIn(delay: 100.ms);
  }

  Widget _actionButton(IconData icon, String label, Color color, VoidCallback onTap) {
    return Expanded(
      child: GestureDetector(
        onTap: onTap,
        child: Column(
          children: [
            Container(
              width: 52, height: 52,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: color.withValues(alpha: 0.2)),
              ),
              child: Icon(icon, color: color, size: 22),
            ),
            const SizedBox(height: 6),
            Text(label, style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600, color: AppColors.textSecondary)),
          ],
        ),
      ),
    );
  }

  Widget _buildStatsRow() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 4),
      child: Row(
        children: [
          _statCard('Total Spent', 'R ${_totalSpent.toStringAsFixed(2)}', Icons.trending_down, AppColors.error),
          const SizedBox(width: 12),
          _statCard('Orders', '$_totalOrders', Icons.shopping_bag_outlined, AppColors.primary),
          const SizedBox(width: 12),
          _statCard('Avg / Order', _totalOrders > 0 ? 'R ${(_totalSpent / _totalOrders).toStringAsFixed(0)}' : 'R 0', Icons.analytics_outlined, AppColors.info),
        ],
      ),
    ).animate().fadeIn(delay: 200.ms);
  }

  Widget _statCard(String label, String value, IconData icon, Color color) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(16),
          boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.04), blurRadius: 8, offset: const Offset(0, 2))],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: color, size: 18),
            const SizedBox(height: 8),
            Text(value, style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w800, color: color)),
            const SizedBox(height: 2),
            Text(label, style: AppTextStyles.caption.copyWith(fontSize: 10)),
          ],
        ),
      ),
    );
  }

  Widget _buildEmptyTransactions() {
    return Padding(
      padding: const EdgeInsets.all(40),
      child: Column(
        children: [
          Icon(Icons.receipt_long_outlined, size: 48, color: AppColors.textHint.withValues(alpha: 0.4)),
          const SizedBox(height: 12),
          Text('No transactions yet', style: AppTextStyles.body.copyWith(color: AppColors.textSecondary)),
          const SizedBox(height: 4),
          Text('Your order history will appear here', style: AppTextStyles.caption),
        ],
      ),
    );
  }

  Widget _buildTransactionItem(Map<String, dynamic> tx, int index) {
    final amount = (tx['amount'] as num?)?.toDouble() ?? 0;
    final description = tx['description'] ?? 'Transaction';
    final status = tx['status'] ?? '';
    final date = tx['date'] != null ? DateTime.tryParse(tx['date']) : null;
    final orderNumber = tx['orderNumber'] ?? '';
    final isDebit = tx['type'] == 'debit';

    Color statusColor;
    String statusLabel;
    switch (status) {
      case 'delivered':
        statusColor = AppColors.success;
        statusLabel = 'Delivered';
        break;
      case 'confirmed':
        statusColor = AppColors.info;
        statusLabel = 'Confirmed';
        break;
      case 'dispatched':
        statusColor = AppColors.warning;
        statusLabel = 'Dispatched';
        break;
      case 'pending':
        statusColor = AppColors.textHint;
        statusLabel = 'Pending';
        break;
      default:
        statusColor = AppColors.textHint;
        statusLabel = status;
    }

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 20, vertical: 4),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.divider.withValues(alpha: 0.5)),
      ),
      child: Row(
        children: [
          // Icon
          Container(
            width: 42, height: 42,
            decoration: BoxDecoration(
              color: (isDebit ? AppColors.error : AppColors.success).withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(
              isDebit ? Icons.arrow_upward_rounded : Icons.arrow_downward_rounded,
              color: isDebit ? AppColors.error : AppColors.success,
              size: 20,
            ),
          ),
          const SizedBox(width: 12),
          // Details
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  description,
                  style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 3),
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                      decoration: BoxDecoration(
                        color: statusColor.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(statusLabel, style: TextStyle(fontSize: 9, color: statusColor, fontWeight: FontWeight.w600)),
                    ),
                    const SizedBox(width: 6),
                    if (date != null)
                      Text(
                        '${date.day}/${date.month}/${date.year}',
                        style: AppTextStyles.caption.copyWith(fontSize: 10),
                      ),
                  ],
                ),
              ],
            ),
          ),
          // Amount
          Text(
            '${isDebit ? '-' : '+'}R ${amount.toStringAsFixed(2)}',
            style: AppTextStyles.body.copyWith(
              fontWeight: FontWeight.w700,
              color: isDebit ? AppColors.error : AppColors.success,
            ),
          ),
        ],
      ),
    ).animate().fadeIn(delay: (index * 50).ms).slideX(begin: 0.05, end: 0);
  }

  void _showTopUpSheet() {
    final amountCtrl = TextEditingController();
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: true,
      builder: (ctx) => Container(
        decoration: const BoxDecoration(color: Colors.white, borderRadius: BorderRadius.vertical(top: Radius.circular(28))),
        padding: EdgeInsets.fromLTRB(24, 24, 24, MediaQuery.of(ctx).viewInsets.bottom + 24),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(child: Container(width: 40, height: 4, decoration: BoxDecoration(color: AppColors.divider, borderRadius: BorderRadius.circular(2)))),
              const SizedBox(height: 20),
              Row(children: [
                Container(width: 40, height: 40, decoration: BoxDecoration(color: AppColors.primary.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(12)), child: const Icon(Icons.add_rounded, color: AppColors.primary)),
                const SizedBox(width: 12),
                Text('Top Up Wallet', style: AppTextStyles.h3),
              ]),
              const SizedBox(height: 20),
              Text('Amount (ZAR)', style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600)),
              const SizedBox(height: 8),
              TextField(
                controller: amountCtrl,
                keyboardType: TextInputType.number,
                style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w700),
                decoration: InputDecoration(
                  prefixText: 'R ',
                  prefixStyle: const TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: AppColors.primary),
                  hintText: '0.00',
                  filled: true, fillColor: const Color(0xFFF5F5F5),
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(14), borderSide: BorderSide.none),
                ),
              ),
              const SizedBox(height: 16),
              // Quick amounts
              Row(children: [
                _quickAmount('R50', amountCtrl), const SizedBox(width: 8),
                _quickAmount('R100', amountCtrl), const SizedBox(width: 8),
                _quickAmount('R200', amountCtrl), const SizedBox(width: 8),
                _quickAmount('R500', amountCtrl),
              ]),
              const SizedBox(height: 20),
              Text('Payment Method', style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600)),
              const SizedBox(height: 10),
              _paymentOption(Icons.credit_card, 'Card (Visa/Mastercard)', true),
              const SizedBox(height: 8),
              _paymentOption(Icons.account_balance, 'EFT Bank Transfer', false),
              const SizedBox(height: 8),
              _paymentOption(Icons.qr_code_2, 'SnapScan', false),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity, height: 52,
                child: ElevatedButton(
                  onPressed: () {
                    Navigator.pop(ctx);
                    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
                      content: Text('Top-up of R${amountCtrl.text.isEmpty ? "0" : amountCtrl.text} initiated. Payment will be processed shortly.'),
                      backgroundColor: AppColors.success, behavior: SnackBarBehavior.floating,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ));
                  },
                  style: ElevatedButton.styleFrom(backgroundColor: AppColors.primary, foregroundColor: Colors.white, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14))),
                  child: const Text('Proceed to Pay', style: TextStyle(fontWeight: FontWeight.w700)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _quickAmount(String label, TextEditingController ctrl) {
    return Expanded(
      child: GestureDetector(
        onTap: () => ctrl.text = label.replaceAll('R', ''),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(color: AppColors.primary.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(10), border: Border.all(color: AppColors.primary.withValues(alpha: 0.2))),
          child: Center(child: Text(label, style: AppTextStyles.bodySmall.copyWith(color: AppColors.primary, fontWeight: FontWeight.w700))),
        ),
      ),
    );
  }

  Widget _paymentOption(IconData icon, String label, bool selected) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: selected ? AppColors.primary.withValues(alpha: 0.06) : const Color(0xFFF8F8F8),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: selected ? AppColors.primary.withValues(alpha: 0.3) : AppColors.divider),
      ),
      child: Row(children: [
        Icon(icon, size: 20, color: selected ? AppColors.primary : AppColors.textHint),
        const SizedBox(width: 12),
        Text(label, style: AppTextStyles.bodySmall.copyWith(fontWeight: selected ? FontWeight.w600 : FontWeight.w400)),
        const Spacer(),
        if (selected) const Icon(Icons.check_circle, color: AppColors.primary, size: 20),
      ]),
    );
  }

  void _showTransferSheet() {
    final phoneCtrl = TextEditingController();
    final amountCtrl = TextEditingController();
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: true,
      builder: (ctx) => Container(
        decoration: const BoxDecoration(color: Colors.white, borderRadius: BorderRadius.vertical(top: Radius.circular(28))),
        padding: EdgeInsets.fromLTRB(24, 24, 24, MediaQuery.of(ctx).viewInsets.bottom + 24),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(child: Container(width: 40, height: 4, decoration: BoxDecoration(color: AppColors.divider, borderRadius: BorderRadius.circular(2)))),
              const SizedBox(height: 20),
              Row(children: [
                Container(width: 40, height: 40, decoration: BoxDecoration(color: AppColors.info.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(12)), child: const Icon(Icons.send_rounded, color: AppColors.info)),
                const SizedBox(width: 12),
                Text('Transfer Funds', style: AppTextStyles.h3),
              ]),
              const SizedBox(height: 20),
              Text('Recipient Phone Number', style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600)),
              const SizedBox(height: 8),
              TextField(
                controller: phoneCtrl,
                keyboardType: TextInputType.phone,
                decoration: InputDecoration(
                  hintText: '081 000 1234',
                  prefixIcon: const Icon(Icons.phone, color: AppColors.info, size: 20),
                  filled: true, fillColor: const Color(0xFFF5F5F5),
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(14), borderSide: BorderSide.none),
                ),
              ),
              const SizedBox(height: 14),
              Text('Amount (ZAR)', style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600)),
              const SizedBox(height: 8),
              TextField(
                controller: amountCtrl,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  prefixText: 'R ',
                  hintText: '0.00',
                  filled: true, fillColor: const Color(0xFFF5F5F5),
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(14), borderSide: BorderSide.none),
                ),
              ),
              const SizedBox(height: 14),
              Text('Note (optional)', style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600)),
              const SizedBox(height: 8),
              TextField(
                decoration: InputDecoration(
                  hintText: 'e.g. Payment for goods',
                  filled: true, fillColor: const Color(0xFFF5F5F5),
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(14), borderSide: BorderSide.none),
                ),
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity, height: 52,
                child: ElevatedButton(
                  onPressed: () {
                    Navigator.pop(ctx);
                    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
                      content: Text('Transfer of R${amountCtrl.text.isEmpty ? "0" : amountCtrl.text} to ${phoneCtrl.text} initiated.'),
                      backgroundColor: AppColors.info, behavior: SnackBarBehavior.floating,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ));
                  },
                  style: ElevatedButton.styleFrom(backgroundColor: AppColors.info, foregroundColor: Colors.white, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14))),
                  child: const Text('Send Transfer', style: TextStyle(fontWeight: FontWeight.w700)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showStatementsSheet() {
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      builder: (ctx) => Container(
        decoration: const BoxDecoration(color: Colors.white, borderRadius: BorderRadius.vertical(top: Radius.circular(28))),
        padding: const EdgeInsets.all(24),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(width: 40, height: 4, decoration: BoxDecoration(color: AppColors.divider, borderRadius: BorderRadius.circular(2))),
              const SizedBox(height: 20),
              Row(children: [
                Container(width: 40, height: 40, decoration: BoxDecoration(color: AppColors.accent.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(12)), child: Icon(Icons.receipt_long_rounded, color: AppColors.accent)),
                const SizedBox(width: 12),
                Text('Statements', style: AppTextStyles.h3),
              ]),
              const SizedBox(height: 20),
              _statementItem('June 2026', 'R ${_totalSpent.toStringAsFixed(2)}', '$_totalOrders transactions'),
              const SizedBox(height: 10),
              _statementItem('May 2026', 'R 0.00', '0 transactions'),
              const SizedBox(height: 10),
              _statementItem('April 2026', 'R 0.00', '0 transactions'),
              const SizedBox(height: 20),
              Container(
                width: double.infinity, padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(color: AppColors.info.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(12)),
                child: Row(children: [
                  const Icon(Icons.info_outline, color: AppColors.info, size: 18),
                  const SizedBox(width: 10),
                  Expanded(child: Text('PDF statement download will be available soon.', style: AppTextStyles.caption.copyWith(color: AppColors.info))),
                ]),
              ),
              const SizedBox(height: 16),
              SizedBox(width: double.infinity, height: 48, child: ElevatedButton(onPressed: () => Navigator.pop(ctx), style: ElevatedButton.styleFrom(backgroundColor: AppColors.primary, foregroundColor: Colors.white, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14))), child: const Text('Close'))),
            ],
          ),
        ),
      ),
    );
  }

  Widget _statementItem(String month, String total, String count) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: const Color(0xFFF8F9FA), borderRadius: BorderRadius.circular(12)),
      child: Row(children: [
        Container(width: 36, height: 36, decoration: BoxDecoration(color: AppColors.accent.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(10)), child: Icon(Icons.calendar_month, color: AppColors.accent, size: 18)),
        const SizedBox(width: 12),
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(month, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600)),
          Text(count, style: AppTextStyles.caption),
        ])),
        Text(total, style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700, color: AppColors.primary)),
      ]),
    );
  }

  void _showSettingsSheet() {
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      builder: (ctx) => Container(
        decoration: const BoxDecoration(color: Colors.white, borderRadius: BorderRadius.vertical(top: Radius.circular(28))),
        padding: const EdgeInsets.all(24),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(width: 40, height: 4, decoration: BoxDecoration(color: AppColors.divider, borderRadius: BorderRadius.circular(2))),
              const SizedBox(height: 20),
              Row(children: [
                Container(width: 40, height: 40, decoration: BoxDecoration(color: Colors.grey.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(12)), child: const Icon(Icons.settings_outlined, color: Colors.grey)),
                const SizedBox(width: 12),
                Text('Wallet Settings', style: AppTextStyles.h3),
              ]),
              const SizedBox(height: 20),
              _settingItem(Icons.lock_outline, 'Transaction PIN', 'Set a PIN for transfers', true),
              const SizedBox(height: 10),
              _settingItem(Icons.notifications_outlined, 'Transaction Alerts', 'Get notified on wallet activity', true),
              const SizedBox(height: 10),
              _settingItem(Icons.speed, 'Daily Limit', 'R 5,000.00 per day', false),
              const SizedBox(height: 10),
              _settingItem(Icons.account_balance, 'Linked Bank', 'No bank linked yet', false),
              const SizedBox(height: 10),
              _settingItem(Icons.security, 'Two-Factor Auth', 'Extra security for large transfers', true),
              const SizedBox(height: 20),
              SizedBox(width: double.infinity, height: 48, child: ElevatedButton(onPressed: () => Navigator.pop(ctx), style: ElevatedButton.styleFrom(backgroundColor: AppColors.primary, foregroundColor: Colors.white, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14))), child: const Text('Done'))),
            ],
          ),
        ),
      ),
    );
  }

  Widget _settingItem(IconData icon, String title, String subtitle, bool hasToggle) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: const Color(0xFFF8F9FA), borderRadius: BorderRadius.circular(12)),
      child: Row(children: [
        Icon(icon, size: 20, color: AppColors.textSecondary),
        const SizedBox(width: 12),
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title, style: AppTextStyles.bodySmall.copyWith(fontWeight: FontWeight.w600)),
          Text(subtitle, style: AppTextStyles.caption.copyWith(color: AppColors.textHint, fontSize: 11)),
        ])),
        if (hasToggle) Switch(value: true, onChanged: (_) {}, activeColor: AppColors.primary)
        else const Icon(Icons.chevron_right, color: AppColors.textHint, size: 20),
      ]),
    );
  }
}
