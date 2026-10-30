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
import 'package:spazasure_app/features/compliance/screens/compliance_screen.dart';
import 'package:spazasure_app/features/profile/screens/profile_screen.dart';
import 'package:spazasure_app/providers/cart_provider.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';

class MainShell extends StatefulWidget {
  const MainShell({super.key});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  int _currentIndex = 0;

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

  final _screens = const [
    HomeScreen(),
    OrdersScreen(),
    CartScreen(),
    ComplianceScreen(),
    ProfileScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    final cartCount = context.watch<CartProvider>().count;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: TabFade(
        index: _currentIndex,
        child: IndexedStack(index: _currentIndex, children: _screens),
      ),
      bottomNavigationBar: AppBottomNav(
        currentIndex: _currentIndex,
        onTap: (index) => setState(() => _currentIndex = index),
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
            Icons.verified_user_outlined,
            Icons.verified_user_rounded,
            tr('nav.docs'),
          ),
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
