import 'package:flutter/material.dart';
import 'package:spazasure_app/core/widgets/app_bottom_nav.dart';
import 'package:spazasure_app/features/customer/screens/customer_shops_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_reports_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_home_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_profile_screen.dart';
import 'package:spazasure_app/features/marketplace/screens/qr_scanner_screen.dart';

class CustomerShell extends StatefulWidget {
  const CustomerShell({super.key});

  @override
  State<CustomerShell> createState() => _CustomerShellState();
}

class _CustomerShellState extends State<CustomerShell> {
  int _index = 0;

  // The scanner is built only while selected so the camera stops on other tabs.
  List<Widget> get _screens => [
    const CustomerHomeScreen(),
    _index == 1
        ? const QrScannerScreen(customerMode: true)
        : const SizedBox.shrink(),
    const CustomerShopsScreen(),
    const CustomerReportsScreen(),
    const CustomerProfileScreen(),
  ];

  static const _items = [
    AppNavItem(Icons.home_outlined, Icons.home_rounded, 'Home'),
    AppNavItem(
      Icons.qr_code_scanner_rounded,
      Icons.qr_code_scanner_rounded,
      'Verify',
    ),
    AppNavItem(Icons.storefront_outlined, Icons.storefront_rounded, 'Shops'),
    AppNavItem(Icons.flag_outlined, Icons.flag_rounded, 'Reports'),
    AppNavItem(Icons.person_outline_rounded, Icons.person_rounded, 'Profile'),
  ];

  @override
  Widget build(BuildContext context) => Scaffold(
    body: IndexedStack(index: _index, children: _screens),
    bottomNavigationBar: AppBottomNav(
      items: _items,
      currentIndex: _index,
      onTap: (index) => setState(() => _index = index),
    ),
  );
}
