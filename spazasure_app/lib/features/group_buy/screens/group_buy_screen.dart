import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/features/profile/widgets/onboarding_fee_gate.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';
import 'package:spazasure_app/services/group_buy_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

class GroupBuyScreen extends StatefulWidget {
  /// Set when a shared group buy link opened the app: show that deal straight away.
  final String? openGroupBuyId;

  const GroupBuyScreen({super.key, this.openGroupBuyId});

  @override
  State<GroupBuyScreen> createState() => _GroupBuyScreenState();
}

class _GroupBuyScreenState extends State<GroupBuyScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  List<GroupBuy> _activeGroups = [];
  List<GroupBuy> _myGroups = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _loadData();
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  Future<void> _loadData() async {
    if (mounted) setState(() => _loading = true);
    try {
      final groups = await Future.wait([
        GroupBuyService.getGroupBuys('other'),
        GroupBuyService.getGroupBuys('my'),
      ]);
      if (!mounted) return;
      setState(() {
        _activeGroups = groups[0];
        _myGroups = groups[1];
        _error = null;
      });
      _openLinkedGroup();
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  bool _linkHandled = false;

  /// Opens the deal from a shared link once, after the first successful load.
  void _openLinkedGroup() {
    final id = widget.openGroupBuyId;
    if (id == null || _linkHandled) return;
    _linkHandled = true;
    final open = _activeGroups.where((g) => g.id == id).firstOrNull;
    if (open != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _showJoinDialog(open);
      });
    } else if (_myGroups.any((g) => g.id == id)) {
      _tabController.animateTo(1);
      _showMessage('You have already joined this group buy', AppColors.info);
    } else {
      _showMessage(
        'This group buy has ended or is not open to shops in your area',
        AppColors.warning,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(
        title: const Text('Group Buy'),
        bottom: TabBar(
          controller: _tabController,
          labelColor: Colors.white,
          unselectedLabelColor: Colors.white70,
          indicatorColor: AppColors.accent,
          tabs: const [
            Tab(text: 'Available Deals'),
            Tab(text: 'My Group Buys'),
          ],
        ),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
          ? _buildError()
          : TabBarView(
              controller: _tabController,
              children: [
                _buildGroupList(_activeGroups, showJoin: true),
                _buildGroupList(_myGroups, showJoin: false),
              ],
            ),
    );
  }

  Widget _buildError() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.cloud_off, size: 48, color: AppColors.textHint),
          const SizedBox(height: 12),
          Text('Unable to load group buys', style: AppTextStyles.body),
          const SizedBox(height: 8),
          ElevatedButton(onPressed: _loadData, child: const Text('Retry')),
        ],
      ),
    );
  }

  Widget _buildGroupList(List<GroupBuy> groups, {required bool showJoin}) {
    if (groups.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.groups_rounded,
              size: 64,
              color: AppColors.primary.withValues(alpha: 0.3),
            ),
            const SizedBox(height: 16),
            Text(
              showJoin
                  ? 'No active group buys yet'
                  : 'You haven\'t joined any group buys',
              style: AppTextStyles.body.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              showJoin
                  ? 'Deals from suppliers will show up here'
                  : 'Join a deal from the Available tab',
              style: AppTextStyles.caption,
            ),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _loadData,
      child: ListView.builder(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
        itemCount: groups.length,
        itemBuilder: (context, index) =>
            _buildGroupCard(groups[index], showJoin: showJoin),
      ),
    );
  }

  Widget _buildGroupCard(GroupBuy group, {required bool showJoin}) {
    final isActive = group.status.toLowerCase() == 'active';
    final daysLeft = group.expiresAt
        ?.difference(DateTime.now())
        .inDays
        .clamp(0, 999);
    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.06),
            blurRadius: 12,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (group.bannerImageUrl != null)
            AspectRatio(
              aspectRatio: 16 / 7,
              child: Image.network(
                group.bannerImageUrl!,
                fit: BoxFit.cover,
                errorBuilder: (_, __, ___) => const SizedBox.shrink(),
              ),
            ),
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: [
                  AppColors.accent.withValues(alpha: 0.12),
                  AppColors.accent.withValues(alpha: 0.04),
                ],
              ),
              borderRadius: const BorderRadius.vertical(
                top: Radius.circular(20),
              ),
            ),
            child: Row(
              children: [
                Container(
                  width: 48,
                  height: 48,
                  decoration: BoxDecoration(
                    color: AppColors.accent.withValues(alpha: 0.15),
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: const Icon(
                    Icons.groups_rounded,
                    color: AppColors.accent,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        group.title,
                        style: AppTextStyles.body.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      if (group.createdByShopName.isNotEmpty)
                        Text(
                          'Started by ${group.createdByShopName}',
                          style: AppTextStyles.caption,
                        ),
                    ],
                  ),
                ),
                _statusChip(isActive, daysLeft),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (group.description.isNotEmpty) ...[
                  Text(group.description, style: AppTextStyles.bodySmall),
                  const SizedBox(height: 10),
                ],
                Row(
                  children: [
                    const Icon(
                      Icons.business_outlined,
                      size: 16,
                      color: AppColors.textHint,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        group.supplierName,
                        style: AppTextStyles.bodySmall.copyWith(
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                    const Icon(
                      Icons.people_outline,
                      size: 16,
                      color: AppColors.textHint,
                    ),
                    const SizedBox(width: 4),
                    Text(
                      '${group.participantCount} shops',
                      style: AppTextStyles.caption,
                    ),
                  ],
                ),
                if (group.areaLabel != null) ...[
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      const Icon(
                        Icons.place_outlined,
                        size: 16,
                        color: AppColors.textHint,
                      ),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          'Open to shops ${group.areaLabel!.replaceFirst('Within', 'within')}',
                          style: AppTextStyles.caption,
                        ),
                      ),
                    ],
                  ),
                ],
                const SizedBox(height: 14),
                ...group.products.map(_buildProductProgress),
                Container(
                  width: double.infinity,
                  margin: const EdgeInsets.only(top: 4),
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: AppColors.info.withValues(alpha: 0.06),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Row(
                    children: [
                      const Icon(
                        Icons.near_me_outlined,
                        size: 17,
                        color: AppColors.info,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Share with nearby shops to reach every product target.',
                          style: AppTextStyles.caption.copyWith(
                            color: AppColors.info,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: () => _shareGroup(group),
                        icon: const Icon(Icons.share_outlined, size: 18),
                        label: const Text('Share'),
                      ),
                    ),
                    if (isActive) ...[
                      const SizedBox(width: 10),
                      Expanded(
                        child: showJoin
                            ? ElevatedButton.icon(
                                onPressed: () => _showJoinDialog(group),
                                icon: const Icon(Icons.group_add, size: 18),
                                label: const Text('Join'),
                              )
                            // Payment is final, so a shop that paid can't leave
                            : group.isPaid
                            ? OutlinedButton.icon(
                                onPressed: null,
                                icon: const Icon(Icons.check_circle, size: 18),
                                label: Text(
                                  'Paid ${_rand(group.myAmountPaid)}',
                                ),
                              )
                            : OutlinedButton.icon(
                                onPressed: () => _leaveGroup(group),
                                style: OutlinedButton.styleFrom(
                                  foregroundColor: AppColors.error,
                                  side: const BorderSide(
                                    color: AppColors.error,
                                  ),
                                ),
                                icon: const Icon(Icons.exit_to_app, size: 18),
                                label: const Text('Leave'),
                              ),
                      ),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _statusChip(bool isActive, int? daysLeft) {
    final label = isActive
        ? daysLeft == null
              ? 'Active'
              : '${daysLeft}d left'
        : 'Closed';
    final color = isActive ? AppColors.accent : AppColors.textHint;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: AppTextStyles.caption.copyWith(
          color: color,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }

  Widget _buildProductProgress(GroupBuyProduct product) {
    final progress = product.progress.clamp(0.0, 1.0);
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.divider),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  '${product.productName} ${product.currentQty}/${product.targetQty}'
                  '${product.discountPct > 0 ? '  \u2022  Save ${product.discountPct}%' : ''}',
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              Text(
                '${product.participantCount} shops',
                style: AppTextStyles.caption,
              ),
            ],
          ),
          const SizedBox(height: 8),
          ClipRRect(
            borderRadius: BorderRadius.circular(4),
            child: LinearProgressIndicator(
              value: progress,
              backgroundColor: AppColors.divider,
              color: product.status == 'completed'
                  ? AppColors.success
                  : AppColors.primary,
              minHeight: 7,
            ),
          ),
        ],
      ),
    );
  }

  // Public advert page served by the gateway, so the link opens without the app or a login.
  String _advertLink(GroupBuy group) {
    final root = ApiService.baseUrl.replaceFirst(RegExp(r'/api/?$'), '');
    return '$root/g/${group.id}';
  }

  Future<void> _shareGroup(GroupBuy group) async {
    final lines = group.products
        .map((p) {
          final saving = p.discountPct > 0 ? ' (save ${p.discountPct}%)' : '';
          return '- ${p.productName}$saving: ${p.currentQty}/${p.targetQty} joined';
        })
        .join('\n');
    final message =
        'Group buy: ${group.title}\n'
        'From ${group.supplierName}\n'
        '${group.areaLabel == null ? '' : 'For shops ${group.areaLabel!.replaceFirst('Within', 'within')}\n'}'
        '$lines\n\n'
        'Join on the SpazaSure app and save together:\n${_advertLink(group)}';
    await Clipboard.setData(ClipboardData(text: message));
    if (!mounted) return;
    _showMessage(
      'Advert copied. Paste it in WhatsApp or your group.',
      AppColors.info,
    );
  }

  Future<void> _showJoinDialog(GroupBuy group) async {
    // The once-off onboarding fee comes before a shop's first group buy
    if (!await OnboardingFeeGate.ensurePaid(context) || !mounted) return;
    final joinableProducts = group.products
        .where(
          (product) =>
              (product.status == 'active' || product.status == 'qualified') &&
              product.remaining > 0,
        )
        .toList();
    if (joinableProducts.isEmpty) {
      _showMessage(
        'Every product in this group buy has reached its target',
        AppColors.info,
      );
      return;
    }
    final selected = <String, bool>{
      for (final product in joinableProducts)
        product.id: joinableProducts.length == 1,
    };
    final controllers = <String, TextEditingController>{
      for (final product in joinableProducts)
        product.id: TextEditingController(
          // One product: start at the whole minimum; several: the shop splits it
          text:
              '${joinableProducts.length == 1 ? group.minTotal.clamp(product.minPerShop, product.remaining) : product.minPerShop}',
        ),
    };
    // The 10-item minimum counts across every product the shop picks
    int selectedTotal() => joinableProducts
        .where((product) => selected[product.id] == true)
        .fold(
          0,
          (sum, product) =>
              sum + (int.tryParse(controllers[product.id]!.text) ?? 0),
        );
    final formKey = GlobalKey<FormState>();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(20),
          ),
          title: const Text('Join Group Buy'),
          content: Form(
            key: formKey,
            child: SizedBox(
              width: double.maxFinite,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      group.minTotal < 10
                          ? 'Only ${group.minTotal} items are left. Take them all to complete this group buy.'
                          : 'Buy at least ${group.minTotal} items in total. '
                                'Mix products if you like, e.g. 3 of one and 7 of another.',
                      style: AppTextStyles.bodySmall,
                    ),
                    const SizedBox(height: 12),
                    ...joinableProducts.map(
                      (product) => Container(
                        margin: const EdgeInsets.only(bottom: 10),
                        decoration: BoxDecoration(
                          border: Border.all(color: AppColors.divider),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Column(
                          children: [
                            CheckboxListTile(
                              value: selected[product.id],
                              activeColor: AppColors.primary,
                              title: Text(product.productName),
                              subtitle: Text(
                                '${product.remaining} still needed',
                              ),
                              onChanged: (value) => setDialogState(
                                () => selected[product.id] = value ?? false,
                              ),
                            ),
                            if (selected[product.id] == true)
                              Padding(
                                padding: const EdgeInsets.fromLTRB(
                                  12,
                                  0,
                                  12,
                                  12,
                                ),
                                child: TextFormField(
                                  controller: controllers[product.id],
                                  keyboardType: TextInputType.number,
                                  onChanged: (_) => setDialogState(() {}),
                                  decoration: InputDecoration(
                                    labelText: 'Quantity',
                                    helperText: 'Up to ${product.remaining}',
                                  ),
                                  validator: (value) {
                                    final quantity = int.tryParse(value ?? '');
                                    if (quantity == null ||
                                        quantity < product.minPerShop) {
                                      return 'At least ${product.minPerShop}';
                                    }
                                    if (quantity > product.remaining) {
                                      return 'Only ${product.remaining} still needed';
                                    }
                                    return null;
                                  },
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                    Text(
                      'Total: ${selectedTotal()} of at least ${group.minTotal} items',
                      style: AppTextStyles.bodySmall.copyWith(
                        fontWeight: FontWeight.w700,
                        color: selectedTotal() >= group.minTotal
                            ? AppColors.success
                            : AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              onPressed: () {
                if (!selected.values.any((value) => value)) {
                  _showMessage(
                    'Select at least one product',
                    AppColors.warning,
                  );
                  return;
                }
                if (formKey.currentState?.validate() != true) return;
                if (selectedTotal() < group.minTotal) {
                  _showMessage(
                    'Buy at least ${group.minTotal} items in total',
                    AppColors.warning,
                  );
                  return;
                }
                Navigator.pop(dialogContext, true);
              },
              child: const Text('Continue'),
            ),
          ],
        ),
      ),
    );
    if (confirmed != true) {
      for (final controller in controllers.values) {
        controller.dispose();
      }
      return;
    }
    final items = joinableProducts
        .where((product) => selected[product.id] == true)
        .map(
          (product) => <String, dynamic>{
            'groupBuyProductId': product.id,
            'quantity': int.parse(controllers[product.id]!.text),
          },
        )
        .toList();
    for (final controller in controllers.values) {
      controller.dispose();
    }

    // Price it with delivery, then the shop chooses how to pay
    GroupBuyQuote quote;
    try {
      quote = await GroupBuyService.quote(group.id, items);
    } catch (error) {
      if (mounted) _showMessage(error.toString(), AppColors.error);
      return;
    }
    if (!mounted) return;
    final method = await _choosePayment(quote);
    if (method == null || !mounted) return;
    await _pay(group, items, method);
  }

  static String _rand(double amount) => 'R${amount.toStringAsFixed(2)}';

  Widget _amountRow(String label, double amount, {bool bold = false}) {
    final style = bold
        ? AppTextStyles.body.copyWith(fontWeight: FontWeight.w800)
        : AppTextStyles.bodySmall;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          Expanded(child: Text(label, style: style)),
          Text(_rand(amount), style: style),
        ],
      ),
    );
  }

  /// Shows what the shop pays and returns 'wallet' or 'paystack', or null when it backs out.
  Future<String?> _choosePayment(GroupBuyQuote quote) {
    final canUseWallet = quote.walletBalance >= quote.total;
    return showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Pay to join', style: AppTextStyles.h3),
              const SizedBox(height: 4),
              Text(
                'Payment is final. If a product doesn\'t reach its target, '
                'its money goes back to your SpazaSure wallet.',
                style: AppTextStyles.caption,
              ),
              const SizedBox(height: 16),
              for (final line in quote.lines)
                _amountRow(
                  '${line.quantity} × ${line.productName}',
                  line.lineTotal,
                ),
              _amountRow('Delivery', quote.deliveryFee),
              const Divider(height: 20),
              _amountRow('Total', quote.total, bold: true),
              const SizedBox(height: 18),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: canUseWallet
                      ? () => Navigator.pop(sheetContext, 'wallet')
                      : null,
                  icon: const Icon(Icons.account_balance_wallet_outlined),
                  label: Text(
                    'Pay from wallet (${_rand(quote.walletBalance)})',
                  ),
                ),
              ),
              if (!canUseWallet)
                Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Text(
                    'Your wallet doesn\'t have enough. Top it up, or pay by card.',
                    style: AppTextStyles.caption,
                  ),
                ),
              const SizedBox(height: 8),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: () => Navigator.pop(sheetContext, 'paystack'),
                  icon: const Icon(Icons.credit_card),
                  label: Text('Pay ${_rand(quote.total)} by card'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _pay(
    GroupBuy group,
    List<Map<String, dynamic>> items,
    String method,
  ) async {
    try {
      final result = await GroupBuyService.join(group.id, items, method);
      if (!mounted) return;
      if (result.checkoutUrl == null) {
        _showMessage(
          result.message ?? 'You joined the group buy!',
          AppColors.success,
        );
        await _loadData();
        return;
      }
      await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => OnboardingFeeCheckoutScreen(
            checkoutUrl: result.checkoutUrl!,
            title: 'Pay for group buy',
          ),
        ),
      );
      if (!mounted) return;
      // Paystack tells SpazaSure a moment after the payment, so check a few times
      var status = 'pending';
      for (var i = 0; i < 5 && status == 'pending'; i++) {
        status = await GroupBuyService.confirmPayment(group.id);
        if (status == 'pending') {
          await Future.delayed(const Duration(seconds: 2));
        }
      }
      if (!mounted) return;
      switch (status) {
        case 'joined':
          _showMessage(
            'Payment received. You joined the group buy!',
            AppColors.success,
          );
        case 'refunded':
          _showMessage(
            'The group buy filled up while you paid, so the money is in your wallet.',
            AppColors.warning,
          );
        default:
          _showMessage(
            'Payment not confirmed yet. If you paid, you will be added shortly; '
            'otherwise tap Join to try again.',
            AppColors.warning,
          );
      }
      await _loadData();
    } catch (error) {
      if (mounted) _showMessage(error.toString(), AppColors.error);
    }
  }

  Future<void> _leaveGroup(GroupBuy group) async {
    try {
      await GroupBuyService.leave(group.id);
      if (!mounted) return;
      _showMessage('You left the group buy', AppColors.info);
      await _loadData();
    } catch (error) {
      if (mounted) _showMessage(error.toString(), AppColors.error);
    }
  }

  void _showMessage(String message, Color color) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: color,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    );
  }
}
