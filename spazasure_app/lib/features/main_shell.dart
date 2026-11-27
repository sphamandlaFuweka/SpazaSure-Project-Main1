import 'package:spazasure_app/core/widgets/privacy_widgets.dart';
import 'package:spazasure_app/services/privacy_service.dart';
import 'package:spazasure_app/services/deep_link_service.dart';
import 'package:flutter/material.dart';
import 'package:spazasure_app/core/widgets/motion.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/app_bottom_nav.dart';
import 'package:spazasure_app/features/marketplace/screens/home_screen.dart';
import 'package:spazasure_app/features/orders/screens/orders_screen.dart';
import 'package:spazasure_app/features/cart/screens/cart_screen.dart';
import 'package:spazasure_app/features/group_buy/screens/group_buy_screen.dart';
import 'package:spazasure_app/features/profile/screens/profile_screen.dart';
import 'package:spazasure_app/features/news/screens/news_screen.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';

class MainShell extends StatefulWidget {
  const MainShell({super.key});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  int _currentIndex = 0;
  bool _newsVisited = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ensurePrivacyConsent(context, PrivacyService.shop);
      // A shared group buy link tapped before sign-in opens now
      DeepLinkService.shopShellOpened();
    });
  }

  @override
  void dispose() {
    DeepLinkService.shopShellClosed();
    super.dispose();
  }

  List<Widget> get _screens => [
    HomeScreen(),
    OrdersScreen(),
    CartScreen(),
    GroupBuyScreen(),
    _newsVisited ? const NewsScreen() : const SizedBox.shrink(),
    ProfileScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    final cartCount = context.watch<CartProvider>().badgeCount;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: TabFade(
        index: _currentIndex,
        child: IndexedStack(index: _currentIndex, children: _screens),
      ),
      bottomNavigationBar: AppBottomNav(
        currentIndex: _currentIndex,
        onTap: (index) => setState(() {
          _currentIndex = index;
          if (index == 4) _newsVisited = true;
        }),
        centerIndex: 2,
        items: [
          AppNavItem(Icons.home_outlined, Icons.home_rounded, tr('nav.home')),
          AppNavItem(
            Icons.receipt_long_outlined,
            Icons.receipt_long_rounded,
            tr('nav.orders'),
          ),
          AppNavItem(
            Icons.shopping_cart_outlined,
            Icons.shopping_cart_rounded,
            tr('nav.cart'),
            badge: cartCount,
          ),
          AppNavItem(
            Icons.groups_outlined,
            Icons.groups_rounded,
            tr('shop.groupBuys'),
          ),
          AppNavItem(Icons.newspaper_outlined, Icons.newspaper, tr('nav.news')),
          AppNavItem(
            Icons.person_outline_rounded,
            Icons.person_rounded,
            tr('nav.profile'),
          ),
        ],
      ),
    );
  }
}
