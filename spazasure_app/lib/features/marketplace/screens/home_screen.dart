import 'package:flutter/material.dart';
import 'package:spazasure_app/core/widgets/cart_toast.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/product_card.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/providers/product_provider.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/group_buy_service.dart';
import 'package:spazasure_app/core/widgets/notification_popup_host.dart';
import 'package:spazasure_app/services/notification_service.dart';
import 'package:spazasure_app/services/order_service.dart';
import 'package:spazasure_app/services/profile_service.dart';
import 'package:spazasure_app/models/models.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen>
    with SingleTickerProviderStateMixin, WidgetsBindingObserver {
  bool _walletVisible = false;
  List<Order> _activeOrders = [];
  String? _error;

  // Dashboard stats from real data
  int _orderCount = 0;
  String _complianceStatus = '--';
  String _rating = '--';
  double? _walletBalance;
  int _unreadNotifications = 0;
  GroupBuy? _featuredGroup;
  @override
  void initState() {
    super.initState();
    NotificationPopupHost.unreadCount.addListener(_syncUnread);
    WidgetsBinding.instance.addObserver(this);
    _loadData();
  }

  @override
  void dispose() {
    NotificationPopupHost.unreadCount.removeListener(_syncUnread);
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _loadOrders();
    }
  }

  Future<void> _loadData() async {
    setState(() => _error = null);
    try {
      // Products are now managed by ProductProvider (auto-refreshes)
      final productProvider = context.read<ProductProvider>();
      if (productProvider.homeProducts.isEmpty) {
        await productProvider.loadInitial();
      }

      await Future.wait([
        _loadOrders(),
        _loadWalletBalance(),
        _loadUnreadCount(),
        _loadFeaturedGroup(),
      ]);

      // Load profile for compliance and rating
      try {
        final profile = await ProfileService.getProfile();
        if (!mounted) return;
        setState(() {
          _complianceStatus = profile.complianceStatus == 'green'
              ? '100%'
              : profile.complianceStatus == 'orange'
              ? '75%'
              : profile.complianceStatus == 'incomplete'
              ? '0%'
              : profile.complianceStatus;
          _rating = profile.ratingAvg > 0
              ? '${profile.ratingAvg.toStringAsFixed(1)}★'
              : 'New';
        });
      } catch (_) {}
    } catch (e) {
      if (mounted) {
        setState(() => _error = 'Unable to load data. Pull down to refresh.');
      }
    }
  }

  Future<void> _loadFeaturedGroup() async {
    try {
      final groups = await GroupBuyService.getGroupBuys('active');
      if (!mounted) return;
      final withPoster = groups.where((g) => g.bannerImageUrl != null);
      setState(
        () => _featuredGroup = withPoster.isEmpty ? null : withPoster.first,
      );
    } catch (_) {}
  }

  /// The pop-up host polls for notifications; keep the bell dot in step with it
  void _syncUnread() {
    if (!mounted) return;
    setState(
      () => _unreadNotifications = NotificationPopupHost.unreadCount.value,
    );
  }

  Future<void> _loadUnreadCount() async {
    final count = await NotificationService.getUnreadCount();
    if (mounted) setState(() => _unreadNotifications = count);
  }

  Future<void> _loadWalletBalance() async {
    try {
      final res = await ApiService.get('/shop/wallet');
      final data = res['data'] as Map<String, dynamic>;
      if (!mounted) return;
      setState(
        () => _walletBalance = (data['balance'] as num?)?.toDouble() ?? 0,
      );
    } catch (_) {
      if (mounted) setState(() => _walletBalance = null);
    }
  }

  Future<void> _loadOrders() async {
    try {
      final orders = await OrderService.getOrders();
      if (!mounted) return;
      setState(() {
        _activeOrders = orders
            .where((o) => o.status != 'delivered' && o.status != 'cancelled')
            .toList();
        _orderCount = _activeOrders.length;
      });
    } catch (_) {}
  }

  Future<void> _onRefresh() async {
    // Pull-to-refresh: force fresh data from backend
    setState(() => _error = null);
    try {
      await context.read<ProductProvider>().refreshAll();
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Unable to load data. Pull down to refresh.');
      }
    }
    await Future.wait([
      _loadOrders(),
      _loadWalletBalance(),
      _loadUnreadCount(),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    final productProvider = context.watch<ProductProvider>();
    final products = productProvider.homeProducts;

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FF),
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _onRefresh,
          color: AppColors.primary,
          child: CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(
              parent: BouncingScrollPhysics(),
            ),
            slivers: [
              SliverToBoxAdapter(child: _buildHeader()),
              if (_error != null)
                SliverToBoxAdapter(child: _buildErrorBanner(_error!)),
              SliverToBoxAdapter(child: _buildWalletCard()),
              SliverToBoxAdapter(child: _buildKpiRow()),
              SliverToBoxAdapter(child: _buildActiveOrder()),
              SliverToBoxAdapter(child: _buildQuickActions()),
              SliverToBoxAdapter(
                child: _buildSectionTitle(
                  'Shop by Category',
                  onTap: () => Navigator.pushNamed(context, '/marketplace'),
                ),
              ),
              SliverToBoxAdapter(child: _buildCategories()),
              SliverToBoxAdapter(child: _buildPromoBanner()),
              SliverToBoxAdapter(
                child: _buildSectionTitle(
                  'Popular Products',
                  onTap: () => Navigator.pushNamed(context, '/marketplace'),
                ),
              ),
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                sliver: productProvider.loading && products.isEmpty
                    ? const SliverToBoxAdapter(
                        child: Center(
                          child: Padding(
                            padding: EdgeInsets.all(32),
                            child: CircularProgressIndicator(),
                          ),
                        ),
                      )
                    : SliverGrid(
                        gridDelegate:
                            const SliverGridDelegateWithFixedCrossAxisCount(
                              crossAxisCount: 3,
                              mainAxisExtent: ProductCard.gridHeight,
                              crossAxisSpacing: 8,
                              mainAxisSpacing: 8,
                            ),
                        delegate: SliverChildBuilderDelegate((context, index) {
                          final product = products[index];
                          return ProductCard(
                            product: product,
                            onTap: () => Navigator.pushNamed(
                              context,
                              '/product',
                              arguments: product,
                            ),
                            onAddToCart: () =>
                                CartToast.add(context, product, 1),
                          );
                        }, childCount: products.length),
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  // ─── PIECE 1: HEADER ────────────────────────────────────────────────────────
  Widget _buildHeader() {
    final auth = context.watch<AuthProvider>();
    final shopName = auth.session?.shopName ?? 'My Spaza Shop';
    final initials = shopName.isNotEmpty
        ? shopName
              .split(' ')
              .where((w) => w.isNotEmpty)
              .take(2)
              .map((w) => w[0].toUpperCase())
              .join()
        : 'SP';

    // Light header (greeting + round icon buttons) so the blue wallet card is the hero
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Row(
        children: [
          // Avatar: the profile picture, or the shop's initials until one is uploaded
          ValueListenableBuilder<String?>(
            valueListenable: ProfileService.photoUrl,
            builder: (context, photoUrl, _) {
              final url = ApiService.resolveUploadUrl(photoUrl);
              final initialsText = Center(
                child: Text(
                  initials,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w900,
                    fontSize: 16,
                  ),
                ),
              );
              return Container(
                width: 50,
                height: 50,
                clipBehavior: Clip.antiAlias,
                decoration: BoxDecoration(
                  gradient: AppColors.heroGradient,
                  shape: BoxShape.circle,
                  border: Border.all(color: Colors.white, width: 3),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.primary.withValues(alpha: 0.25),
                      blurRadius: 12,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: url == null
                    ? initialsText
                    : Image.network(
                        url,
                        fit: BoxFit.cover,
                        width: 50,
                        height: 50,
                        errorBuilder: (_, __, ___) => initialsText,
                      ),
              );
            },
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _getGreeting(),
                  style: AppTextStyles.bodySmall.copyWith(
                    color: AppColors.ink3,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                Text(
                  shopName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.h3.copyWith(fontWeight: FontWeight.w900),
                ),
                Row(
                  children: [
                    Container(
                      width: 7,
                      height: 7,
                      decoration: const BoxDecoration(
                        color: AppColors.secondary,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: 5),
                    Text(
                      'Shop active',
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.secondary,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          _headerButton(
            icon: Icons.search_rounded,
            label: 'Search products',
            onTap: () => Navigator.pushNamed(context, '/marketplace'),
          ),
          const SizedBox(width: 8),
          _headerButton(
            icon: Icons.forum_outlined,
            label: 'Messages and alerts',
            showDot: _unreadNotifications > 0,
            onTap: () async {
              await Navigator.pushNamed(context, '/inbox');
              _loadUnreadCount();
            },
          ),
        ],
      ),
    ).animate().fadeIn(duration: 400.ms);
  }

  Widget _headerButton({
    required IconData icon,
    required String label,
    required VoidCallback onTap,
    bool showDot = false,
  }) {
    return Semantics(
      button: true,
      label: label,
      child: Material(
        color: Colors.white,
        shape: const CircleBorder(
          side: BorderSide(color: AppColors.primaryTint),
        ),
        child: InkWell(
          customBorder: const CircleBorder(),
          onTap: onTap,
          child: SizedBox(
            width: 46,
            height: 46,
            child: Stack(
              alignment: Alignment.center,
              children: [
                Icon(icon, color: AppColors.primary, size: 22),
                if (showDot)
                  Positioned(
                    right: 11,
                    top: 11,
                    child: Container(
                      width: 9,
                      height: 9,
                      decoration: BoxDecoration(
                        color: AppColors.red,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.white, width: 1.5),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  // ─── PIECE 2: PREMIUM WALLET CARD ───────────────────────────────────────────
  Widget _buildWalletCard() {
    return GestureDetector(
          onTap: () => Navigator.pushNamed(context, '/wallet'),
          child: Container(
            margin: const EdgeInsets.fromLTRB(16, 16, 16, 4),
            decoration: BoxDecoration(
              gradient: const LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  Color(0xFF1A3278),
                  Color(0xFF25449A),
                  Color(0xFF3F5DB0),
                ],
                stops: [0.0, 0.5, 1.0],
              ),
              borderRadius: BorderRadius.circular(24),
              boxShadow: [
                BoxShadow(
                  color: const Color(0xFF25449A).withValues(alpha: 0.4),
                  blurRadius: 24,
                  offset: const Offset(0, 10),
                ),
              ],
            ),
            child: Stack(
              children: [
                // Decorative circles
                Positioned(
                  right: -30,
                  top: -30,
                  child: _decorCircle(120, 0.06),
                ),
                Positioned(
                  right: 40,
                  bottom: -40,
                  child: _decorCircle(100, 0.05),
                ),
                Positioned(
                  left: -20,
                  bottom: -20,
                  child: _decorCircle(80, 0.04),
                ),
                // Card content
                Padding(
                  padding: const EdgeInsets.all(22),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: Colors.white.withValues(alpha: 0.15),
                                  borderRadius: BorderRadius.circular(10),
                                ),
                                child: const Icon(
                                  Icons.account_balance_wallet_rounded,
                                  color: Colors.white,
                                  size: 18,
                                ),
                              ),
                              const SizedBox(width: 8),
                              const Text(
                                'SpazaSure Wallet',
                                style: TextStyle(
                                  color: Colors.white70,
                                  fontSize: 13,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                            ],
                          ),
                          GestureDetector(
                            onTap: () => setState(
                              () => _walletVisible = !_walletVisible,
                            ),
                            child: Icon(
                              _walletVisible
                                  ? Icons.visibility_off_outlined
                                  : Icons.visibility_outlined,
                              color: Colors.white60,
                              size: 18,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 16),
                      Text(
                        !_walletVisible
                            ? 'R ••••••'
                            : _walletBalance == null
                            ? 'R --'
                            : 'R ${_walletBalance!.toStringAsFixed(2)}',
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 32,
                          fontWeight: FontWeight.w800,
                          letterSpacing: -0.5,
                        ),
                      ),
                      const SizedBox(height: 4),
                      const Text(
                        'Available Balance',
                        style: TextStyle(color: Colors.white54, fontSize: 12),
                      ),
                      const SizedBox(height: 16),
                      Row(
                        children: [
                          _walletAction(Icons.add_rounded, 'Top Up'),
                          const SizedBox(width: 10),
                          _walletAction(Icons.send_rounded, 'Transfer'),
                          const SizedBox(width: 10),
                          _walletAction(Icons.history_rounded, 'History'),
                          const Spacer(),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 12,
                              vertical: 6,
                            ),
                            decoration: BoxDecoration(
                              color: AppColors.accent,
                              borderRadius: BorderRadius.circular(20),
                            ),
                            child: const Row(
                              children: [
                                Text(
                                  'Manage',
                                  style: TextStyle(
                                    color: AppColors.primary,
                                    fontSize: 12,
                                    fontWeight: FontWeight.w800,
                                  ),
                                ),
                                SizedBox(width: 4),
                                Icon(
                                  Icons.arrow_forward_rounded,
                                  color: AppColors.primary,
                                  size: 14,
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        )
        .animate()
        .fadeIn(delay: 100.ms)
        .slideY(begin: 0.08, end: 0, delay: 100.ms, duration: 400.ms);
  }

  Widget _buildErrorBanner(String message) => Container(
    margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
    decoration: BoxDecoration(
      color: AppColors.error.withValues(alpha: 0.08),
      borderRadius: BorderRadius.circular(12),
      border: Border.all(color: AppColors.error.withValues(alpha: 0.3)),
    ),
    child: Row(
      children: [
        const Icon(Icons.cloud_off_rounded, color: AppColors.error, size: 18),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            message,
            style: AppTextStyles.bodySmall.copyWith(color: AppColors.error),
          ),
        ),
        TextButton(onPressed: _loadData, child: const Text('Retry')),
      ],
    ),
  );

  Widget _decorCircle(double size, double opacity) => Container(
    width: size,
    height: size,
    decoration: BoxDecoration(
      color: Colors.white.withValues(alpha: opacity),
      shape: BoxShape.circle,
    ),
  );

  Widget _walletAction(IconData icon, String label) => Column(
    children: [
      Container(
        width: 36,
        height: 36,
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: 0.15),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Icon(icon, color: Colors.white, size: 16),
      ),
      const SizedBox(height: 4),
      Text(label, style: const TextStyle(color: Colors.white60, fontSize: 10)),
    ],
  );

  // ─── PIECE 3: KPI STATS ROW ──────────────────────────────────────────────────
  Widget _buildKpiRow() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 4),
      child: Row(
        children: [
          Expanded(
            child: _KpiCard(
              value: '$_orderCount',
              label: 'Active Orders',
              color: AppColors.info,
              icon: Icons.receipt_long_rounded,
              trend: _orderCount > 0 ? 'In progress' : 'No orders',
              onTap: () => Navigator.pushNamed(context, '/orders'),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: _KpiCard(
              value: _complianceStatus,
              label: 'Compliance',
              color: AppColors.warning,
              icon: Icons.verified_user_rounded,
              trend: _complianceStatus == '100%' ? 'Complete' : 'Upload docs',
              onTap: () => Navigator.pushNamed(context, '/compliance'),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: _KpiCard(
              value: _rating,
              label: 'Rating',
              color: AppColors.success,
              icon: Icons.star_rounded,
              trend: _rating == 'New' || _rating == '--'
                  ? 'No ratings yet'
                  : 'From shop reviews',
              onTap: () => Navigator.pushNamed(context, '/orders'),
            ),
          ),
        ],
      ),
    ).animate().fadeIn(delay: 200.ms);
  }

  // ─── PIECE 4: ACTIVE ORDER TRACKER ──────────────────────────────────────────
  Widget _buildActiveOrder() {
    if (_activeOrders.isEmpty) return const SizedBox.shrink();
    final order = _activeOrders.first;
    final statusColor = _orderStatusColor(order.status);
    return GestureDetector(
          onTap: () => Navigator.pushNamed(context, '/orders'),
          child: Container(
            margin: const EdgeInsets.fromLTRB(16, 14, 16, 4),
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(20),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.05),
                  blurRadius: 12,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.all(8),
                          decoration: BoxDecoration(
                            color: AppColors.info.withValues(alpha: 0.1),
                            borderRadius: BorderRadius.circular(10),
                          ),
                          child: const Icon(
                            Icons.local_shipping_rounded,
                            color: AppColors.info,
                            size: 18,
                          ),
                        ),
                        const SizedBox(width: 10),
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Active Order',
                              style: AppTextStyles.caption.copyWith(
                                color: AppColors.textSecondary,
                              ),
                            ),
                            Text(
                              order.orderNumber,
                              style: AppTextStyles.body.copyWith(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: statusColor.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Text(
                        _orderStatusLabel(order.status),
                        style: TextStyle(
                          color: statusColor,
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                // Progress steps
                _buildOrderProgress(order.status),
                const SizedBox(height: 12),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      '${order.supplierName} • ${order.items.length} items',
                      style: AppTextStyles.caption,
                    ),
                    Text(
                      'R ${order.total.toStringAsFixed(2)}',
                      style: AppTextStyles.body.copyWith(
                        color: AppColors.primary,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        )
        .animate()
        .fadeIn(delay: 250.ms)
        .slideY(begin: 0.05, end: 0, delay: 250.ms, duration: 350.ms);
  }

  String _orderStatusLabel(String status) => switch (status) {
    'pending' => 'Awaiting Confirmation',
    'confirmed' => 'Confirmed',
    'processing' => 'Being Packed',
    'dispatched' => 'On the Way',
    _ => status.isEmpty ? 'In Progress' : status,
  };

  Color _orderStatusColor(String status) => switch (status) {
    'dispatched' => AppColors.statusDispatched,
    'confirmed' || 'processing' => AppColors.info,
    _ => AppColors.textSecondary,
  };

  Widget _buildOrderProgress(String status) {
    final steps = ['Confirmed', 'Processing', 'Dispatched', 'Delivered'];
    // -1 = still pending, so no step is complete yet
    final currentStep = steps.indexWhere((s) => s.toLowerCase() == status);
    return Row(
      children: List.generate(steps.length, (i) {
        final isDone = i <= currentStep;
        final isCurrent = i == currentStep;
        return Expanded(
          child: Row(
            children: [
              Column(
                children: [
                  AnimatedContainer(
                    duration: const Duration(milliseconds: 300),
                    width: isCurrent ? 28 : 22,
                    height: isCurrent ? 28 : 22,
                    decoration: BoxDecoration(
                      color: isDone ? AppColors.primary : AppColors.divider,
                      shape: BoxShape.circle,
                      boxShadow: isCurrent
                          ? [
                              BoxShadow(
                                color: AppColors.primary.withValues(alpha: 0.4),
                                blurRadius: 8,
                              ),
                            ]
                          : [],
                    ),
                    child: Icon(
                      isDone ? Icons.check_rounded : Icons.circle,
                      color: Colors.white,
                      size: isCurrent ? 14 : 10,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    steps[i],
                    style: TextStyle(
                      fontSize: 9,
                      color: isDone ? AppColors.primary : AppColors.textHint,
                      fontWeight: isCurrent ? FontWeight.w700 : FontWeight.w400,
                    ),
                  ),
                ],
              ),
              if (i < steps.length - 1)
                Expanded(
                  child: Container(
                    height: 2,
                    margin: const EdgeInsets.only(bottom: 18),
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: i < currentStep
                            ? [AppColors.primary, AppColors.primary]
                            : [AppColors.primary, AppColors.divider],
                      ),
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ),
            ],
          ),
        );
      }),
    );
  }

  // ─── PIECE 5: QUICK ACTIONS ──────────────────────────────────────────────────
  Widget _buildQuickActions() {
    final actions = [
      {
        'icon': Icons.groups_rounded,
        'label': 'Group Buy',
        'color': AppColors.accent,
        'route': '/group-buy',
        'badge': null,
      },
      {
        'icon': Icons.qr_code_scanner_rounded,
        'label': 'Scan QR',
        'color': AppColors.info,
        'route': '/qr-scanner',
        'badge': null,
      },
      {
        'icon': Icons.description_outlined,
        'label': 'Compliance',
        'color': AppColors.warning,
        'route': '/compliance',
        'badge': _complianceStatus == '100%' || _complianceStatus == '--'
            ? null
            : '!',
      },
      {
        'icon': Icons.school_rounded,
        'label': 'Academy',
        'color': AppColors.primary,
        'route': '/academy',
        'badge': null,
      },
      {
        'icon': Icons.local_shipping_outlined,
        'label': 'Track Order',
        'color': AppColors.secondary,
        'route': '/orders',
        'badge': null,
      },
    ];
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 4),
      child: Row(
        children: actions
            .map(
              (a) => Expanded(
                child: GestureDetector(
                  onTap: () =>
                      Navigator.pushNamed(context, a['route'] as String),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: Column(
                      children: [
                        Stack(
                          clipBehavior: Clip.none,
                          children: [
                            Container(
                              width: 56,
                              height: 56,
                              decoration: BoxDecoration(
                                color: Colors.white,
                                borderRadius: BorderRadius.circular(16),
                                boxShadow: [
                                  BoxShadow(
                                    color: (a['color'] as Color).withValues(
                                      alpha: 0.15,
                                    ),
                                    blurRadius: 10,
                                    offset: const Offset(0, 4),
                                  ),
                                ],
                              ),
                              child: Icon(
                                a['icon'] as IconData,
                                color: a['color'] as Color,
                                size: 26,
                              ),
                            ),
                            if (a['badge'] != null)
                              Positioned(
                                right: -4,
                                top: -4,
                                child: Container(
                                  padding: const EdgeInsets.all(4),
                                  decoration: const BoxDecoration(
                                    color: AppColors.error,
                                    shape: BoxShape.circle,
                                  ),
                                  child: Text(
                                    a['badge'] as String,
                                    style: const TextStyle(
                                      color: Colors.white,
                                      fontSize: 9,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                ),
                              ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        Text(
                          a['label'] as String,
                          style: AppTextStyles.caption.copyWith(
                            fontWeight: FontWeight.w600,
                            color: AppColors.textSecondary,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            )
            .toList(),
      ),
    ).animate().fadeIn(delay: 300.ms);
  }

  // ─── PIECE 6: CATEGORIES ─────────────────────────────────────────────────────
  Widget _buildCategories() {
    final categories = context.read<ProductProvider>().categories;
    final colors = [
      AppColors.primary,
      AppColors.secondary,
      AppColors.accent,
      AppColors.info,
      const Color(0xFF8E24AA),
      AppColors.error,
      const Color(0xFF00ACC1),
      const Color(0xFF6D4C41),
    ];
    final icons = {
      'shopping_basket': Icons.shopping_basket,
      'local_drink': Icons.local_drink,
      'cookie': Icons.cookie,
      'spa': Icons.spa,
      'home': Icons.home,
      'child_care': Icons.child_care,
      'ac_unit': Icons.ac_unit,
      'bakery_dining': Icons.bakery_dining,
      'eco': Icons.eco,
      'coffee': Icons.coffee,
      'local_fire_department': Icons.local_fire_department,
    };
    return SizedBox(
      height: 100,
      child: ListView.builder(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
        itemCount: categories.length,
        itemBuilder: (context, index) {
          final cat = categories[index];
          final color = colors[index % colors.length];
          return Padding(
            padding: const EdgeInsets.only(right: 14),
            child: GestureDetector(
              onTap: () => Navigator.pushNamed(
                context,
                '/marketplace',
                arguments: cat.id,
              ),
              child: Column(
                children: [
                  Container(
                    width: 54,
                    height: 54,
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(16),
                      boxShadow: [
                        BoxShadow(
                          color: color.withValues(alpha: 0.2),
                          blurRadius: 8,
                          offset: const Offset(0, 3),
                        ),
                      ],
                    ),
                    child: Icon(
                      icons[cat.iconName] ?? Icons.category,
                      color: color,
                      size: 26,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    cat.name,
                    style: AppTextStyles.caption.copyWith(
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  // ─── PIECE 7: PROMO BANNER ───────────────────────────────────────────────────
  Widget _buildPromoBanner() {
    final poster = _featuredGroup?.bannerImageUrl;
    final hasPoster = poster != null;
    return GestureDetector(
      onTap: () => Navigator.pushNamed(context, '/group-buy'),
      child: Container(
        margin: const EdgeInsets.fromLTRB(16, 8, 16, 16),
        height: 130,
        clipBehavior: Clip.antiAlias,
        decoration: BoxDecoration(
          gradient: const LinearGradient(
            colors: [Color(0xFFE8A510), Color(0xFFF8B217), Color(0xFFFAC44B)],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
          borderRadius: BorderRadius.circular(20),
          boxShadow: [
            BoxShadow(
              color: AppColors.accent.withValues(alpha: 0.35),
              blurRadius: 16,
              offset: const Offset(0, 6),
            ),
          ],
        ),
        child: Stack(
          children: [
            if (hasPoster) ...[
              Positioned.fill(
                child: Image.network(
                  poster,
                  fit: BoxFit.cover,
                  errorBuilder: (_, __, ___) => const SizedBox.shrink(),
                ),
              ),
              Positioned.fill(
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      colors: [
                        Colors.black.withValues(alpha: 0.65),
                        Colors.black.withValues(alpha: 0.1),
                      ],
                    ),
                  ),
                ),
              ),
            ] else ...[
              Positioned(right: -20, top: -20, child: _decorCircle(110, 0.12)),
              Positioned(right: 30, bottom: -30, child: _decorCircle(80, 0.1)),
            ],
            Padding(
              padding: const EdgeInsets.all(20),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 10,
                            vertical: 3,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.primary.withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: const Text(
                            '🔥 LIMITED TIME',
                            style: TextStyle(
                              color: AppColors.primary,
                              fontSize: 10,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          hasPoster ? _featuredGroup!.title : 'Save up to 20%',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            color: hasPoster ? Colors.white : AppColors.primary,
                            fontSize: 20,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        Text(
                          'Join a group order now',
                          style: TextStyle(
                            color: hasPoster
                                ? Colors.white70
                                : AppColors.primaryDark,
                            fontSize: 13,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 10,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.primary,
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: const Text(
                      'Join Now',
                      style: TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                        fontSize: 13,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    ).animate().fadeIn(delay: 350.ms);
  }

  Widget _buildSectionTitle(String title, {VoidCallback? onTap}) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 10),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(title, style: AppTextStyles.h3),
          GestureDetector(
            onTap: onTap,
            child: Text(
              'See All',
              style: AppTextStyles.body.copyWith(
                color: AppColors.primary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _getGreeting() {
    final hour = DateTime.now().hour;
    if (hour < 12) return 'Good Morning 🌅';
    if (hour < 17) return 'Good Afternoon ☀️';
    return 'Good Evening 🌙';
  }
}

// ─── KPI CARD WIDGET ──────────────────────────────────────────────────────────
class _KpiCard extends StatelessWidget {
  final String value, label, trend;
  final Color color;
  final IconData icon;
  final VoidCallback? onTap;

  const _KpiCard({
    required this.value,
    required this.label,
    required this.color,
    required this.icon,
    required this.trend,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(18),
          boxShadow: [
            BoxShadow(
              color: color.withValues(alpha: 0.12),
              blurRadius: 12,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(icon, color: color, size: 18),
            ),
            const SizedBox(height: 10),
            Text(
              value,
              style: TextStyle(
                color: color,
                fontSize: 18,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 2),
            Text(
              label,
              style: AppTextStyles.caption.copyWith(
                fontWeight: FontWeight.w500,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              trend,
              style: TextStyle(
                color: color.withValues(alpha: 0.8),
                fontSize: 10,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
