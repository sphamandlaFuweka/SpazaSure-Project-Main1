import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'core/theme/app_theme.dart';
import 'providers/auth_provider.dart';
import 'providers/cart_provider.dart';
import 'providers/product_provider.dart';
import 'features/onboarding/screens/splash_screen.dart';
import 'features/onboarding/screens/onboarding_screen.dart';
import 'features/auth/screens/login_screen.dart';
import 'features/auth/screens/otp_screen.dart';
import 'features/auth/screens/register_screen.dart';
import 'features/main_shell.dart';
import 'features/marketplace/screens/marketplace_screen.dart';
import 'features/marketplace/screens/product_detail_screen.dart';
import 'features/cart/screens/cart_screen.dart';
import 'features/orders/screens/orders_screen.dart';
import 'features/orders/screens/order_detail_screen.dart';
import 'features/delivery/screens/delivery_tracking_screen.dart';
import 'features/compliance/screens/compliance_screen.dart';
import 'features/notifications/screens/notifications_screen.dart';
import 'features/notifications/screens/report_screen.dart';
import 'features/wallet/screens/wallet_screen.dart';
import 'features/group_buy/screens/group_buy_screen.dart';
import 'features/marketplace/screens/qr_scanner_screen.dart';
import 'features/orders/screens/rate_delivery_screen.dart';
import 'features/orders/screens/receipt_screen.dart';
import 'core/widgets/logo_page_route.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  SystemChrome.setSystemUIOverlayStyle(const SystemUiOverlayStyle(
    statusBarColor: Colors.transparent,
    statusBarIconBrightness: Brightness.dark,
  ));
  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AuthProvider()..init()),
        ChangeNotifierProvider(create: (_) => CartProvider()),
        ChangeNotifierProvider(create: (_) => ProductProvider()..loadInitial()),
      ],
      child: const SpazaSureApp(),
    ),
  );
}

class SpazaSureApp extends StatelessWidget {
  const SpazaSureApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'SpazaSure',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      initialRoute: '/splash',
      onGenerateRoute: (settings) {
        final pages = <String, Widget Function()>{
          '/splash':            () => const SplashScreen(),
          '/onboarding':        () => const OnboardingScreen(),
          '/login':             () => const LoginScreen(),
          '/otp':               () => const OtpScreen(),
          '/register':          () => const RegisterScreen(),
          '/home':              () => const MainShell(),
          '/marketplace':       () => const MarketplaceScreen(),
          '/product':           () => const ProductDetailScreen(),
          '/cart':              () => const CartScreen(),
          '/orders':            () => const OrdersScreen(),
          '/order-detail':      () => const OrderDetailScreen(),
          '/delivery-tracking': () => const DeliveryTrackingScreen(),
          '/compliance':        () => const ComplianceScreen(),
          '/notifications':     () => const NotificationsScreen(),
          '/wallet':            () => const WalletScreen(),
          '/group-buy':         () => const GroupBuyScreen(),
          '/qr-scanner':        () => const QrScannerScreen(),
          '/report':            () => const ReportScreen(),
          '/rate-delivery':     () => const RateDeliveryScreen(),
          '/receipt':           () => const ReceiptScreen(),
        };
        final builder = pages[settings.name];
        if (builder == null) return null;
        if (settings.name == '/splash') {
          return MaterialPageRoute(builder: (_) => builder(), settings: settings);
        }
        return LogoPageRoute.logoRoute(builder(), settings: settings);
      },
    );
  }
}
