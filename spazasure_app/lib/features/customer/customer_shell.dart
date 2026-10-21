import 'package:spazasure_app/core/widgets/privacy_widgets.dart';
import 'package:spazasure_app/services/privacy_service.dart';
import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
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

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback(
      (_) => ensurePrivacyConsent(context, PrivacyService.customer),
    );
  }

  // The scanner is built only while selected so the camera stops on other tabs.
  List<Widget> get _screens => [
    const CustomerHomeScreen(),
    const CustomerShopsScreen(),
    _index == 2
        ? const QrScannerScreen(customerMode: true)
        : const SizedBox.shrink(),
    const CustomerReportsScreen(),
    const CustomerProfileScreen(),
  ];

  static const _items = [
    AppNavItem(Icons.home_outlined, Icons.home_rounded, 'Home'),
    AppNavItem(Icons.storefront_outlined, Icons.storefront_rounded, 'Shops'),
    // Centre action: scan a product to verify it
    AppNavItem(
      Icons.qr_code_scanner_rounded,
      Icons.qr_code_scanner_rounded,
      'Verify',
    ),
    AppNavItem(Icons.flag_outlined, Icons.flag_rounded, 'Reports'),
    AppNavItem(Icons.person_outline_rounded, Icons.person_rounded, 'Profile'),
  ];

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    body: IndexedStack(index: _index, children: _screens),
    bottomNavigationBar: AppBottomNav(
      items: _items,
      currentIndex: _index,
      onTap: (index) => setState(() => _index = index),
      centerIndex: 2,
    ),
  );
}
