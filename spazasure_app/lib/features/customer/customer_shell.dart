import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/screens/customer_shops_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_reports_screen.dart';
import 'package:spazasure_app/features/marketplace/screens/home_screen.dart';
import 'package:spazasure_app/features/marketplace/screens/qr_scanner_screen.dart';
import 'package:spazasure_app/features/notifications/screens/notifications_screen.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';
import 'package:spazasure_app/features/profile/screens/profile_screen.dart';

class CustomerShell extends StatefulWidget {
  const CustomerShell({super.key});

  @override
  State<CustomerShell> createState() => _CustomerShellState();
}

class _CustomerShellState extends State<CustomerShell> {
  int _index = 0;

  final _screens = const [
    HomeScreen(),
    QrScannerScreen(),
    CustomerShopsScreen(),
    CustomerReportsScreen(),
    ProfileScreen(),
  ];

  static const _items = [
    (Icons.home_outlined, Icons.home, 'Home'),
    (Icons.qr_code_scanner_outlined, Icons.qr_code_scanner, 'Verify'),
    (Icons.storefront_outlined, Icons.storefront, 'Shops'),
    (Icons.report_problem_outlined, Icons.report_problem, 'Reports'),
    (Icons.person_outline, Icons.person, 'Profile'),
  ];

  @override
  Widget build(BuildContext context) => Scaffold(
    body: IndexedStack(index: _index, children: _screens),
    bottomNavigationBar: NavigationBar(
      selectedIndex: _index,
      onDestinationSelected: (index) => setState(() => _index = index),
      backgroundColor: AppColors.surface,
      indicatorColor: AppColors.primary.withValues(alpha: .12),
      destinations: _items
          .map(
            (item) => NavigationDestination(
              icon: Icon(item.$1),
              selectedIcon: Icon(item.$2, color: AppColors.primary),
              label: item.$3,
            ),
          )
          .toList(),
    ),
  );
}
