import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/app_bottom_nav.dart';
import 'package:spazasure_app/features/marketplace/screens/home_screen.dart';
import 'package:spazasure_app/features/orders/screens/orders_screen.dart';
import 'package:spazasure_app/features/cart/screens/cart_screen.dart';
import 'package:spazasure_app/features/compliance/screens/compliance_screen.dart';
import 'package:spazasure_app/features/profile/screens/profile_screen.dart';
import 'package:spazasure_app/providers/cart_provider.dart';

class MainShell extends StatefulWidget {
  const MainShell({super.key});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  int _currentIndex = 0;

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
      body: IndexedStack(index: _currentIndex, children: _screens),
      bottomNavigationBar: AppBottomNav(
        currentIndex: _currentIndex,
        onTap: (index) => setState(() => _currentIndex = index),
        centerIndex: 2,
        items: [
          const AppNavItem(Icons.home_outlined, Icons.home_rounded, 'Home'),
          const AppNavItem(
            Icons.receipt_long_outlined,
            Icons.receipt_long_rounded,
            'Orders',
          ),
          AppNavItem(
            Icons.shopping_cart_outlined,
            Icons.shopping_cart_rounded,
            'Cart',
            badge: cartCount,
          ),
          const AppNavItem(
            Icons.verified_user_outlined,
            Icons.verified_user_rounded,
            'Docs',
          ),
          const AppNavItem(
            Icons.person_outline_rounded,
            Icons.person_rounded,
            'Profile',
          ),
        ],
      ),
    );
  }
}
