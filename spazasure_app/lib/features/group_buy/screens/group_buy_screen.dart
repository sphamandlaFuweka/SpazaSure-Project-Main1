import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/group_buy_service.dart';

class GroupBuyScreen extends StatefulWidget {
  const GroupBuyScreen({super.key});

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
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Group Buy'),
        bottom: TabBar(
          controller: _tabController,
          labelColor: AppColors.primary,
          unselectedLabelColor: AppColors.textHint,
          indicatorColor: AppColors.primary,
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
        itemBuilder: (context, index) => _buildGroupCard(
          groups[index],
          showJoin: showJoin,
        ).animate().fadeIn(delay: (index * 80).ms).slideY(begin: 0.05),
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
    final joinableProducts = group.products
        .where(
          (product) =>
              product.status == 'active' || product.status == 'qualified',
        )
        .toList();
    if (joinableProducts.isEmpty) {
      _showMessage('This group buy has no products available', AppColors.error);
      return;
    }
    final selected = <String, bool>{
      for (final product in joinableProducts) product.id: false,
    };
    final controllers = <String, TextEditingController>{
      for (final product in joinableProducts)
        product.id: TextEditingController(text: '${product.minOrderQty}'),
    };
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
                      'Select products and enter your quantities.',
                      style: AppTextStyles.bodySmall,
                    ),
                    const SizedBox(height: 12),
                    ...group.products.map(
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
                                '${product.currentQty}/${product.targetQty} • min ${product.minOrderQty}',
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
                                  decoration: const InputDecoration(
                                    labelText: 'Quantity',
                                  ),
                                  validator: (value) {
                                    final quantity = int.tryParse(value ?? '');
                                    if (quantity == null ||
                                        quantity < product.minOrderQty) {
                                      return 'Minimum ${product.minOrderQty}';
                                    }
                                    return null;
                                  },
                                ),
                              ),
                          ],
                        ),
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
                Navigator.pop(dialogContext, true);
              },
              child: const Text('Join'),
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
    final items = group.products
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
    try {
      await GroupBuyService.join(group.id, items);
      if (!mounted) return;
      _showMessage('You joined the selected products!', AppColors.success);
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
