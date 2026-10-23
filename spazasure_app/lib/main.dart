import 'package:flutter/material.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'core/theme/app_theme.dart';
import 'core/i18n/app_language.dart';
import 'providers/auth_provider.dart';
import 'providers/cart_provider.dart';
import 'providers/product_provider.dart';
import 'features/onboarding/screens/splash_screen.dart';
import 'features/onboarding/screens/onboarding_screen.dart';
import 'features/auth/screens/login_screen.dart';
import 'features/auth/screens/register_screen.dart';
import 'features/auth/screens/customer_register_screen.dart';
import 'features/main_shell.dart';
import 'features/customer/customer_shell.dart';
import 'features/marketplace/screens/marketplace_screen.dart';
import 'features/marketplace/screens/product_detail_screen.dart';
import 'features/cart/screens/cart_screen.dart';
import 'features/orders/screens/orders_screen.dart';
import 'features/orders/screens/order_detail_screen.dart';
import 'features/delivery/screens/delivery_tracking_screen.dart';
import 'features/compliance/screens/compliance_screen.dart';
import 'core/widgets/notification_popup_host.dart';
import 'features/chat/screens/inbox_screen.dart';
import 'features/shop_verification/my_sticker_screen.dart';
import 'features/shop_verification/shop_verify_screen.dart';
import 'features/notifications/screens/notifications_screen.dart';
import 'features/notifications/screens/report_screen.dart';
import 'features/wallet/screens/wallet_screen.dart';
import 'features/academy/screens/academy_screen.dart';
import 'features/group_buy/screens/group_buy_screen.dart';
import 'features/marketplace/screens/qr_scanner_screen.dart';
import 'features/orders/screens/rate_delivery_screen.dart';
import 'features/orders/screens/receipt_screen.dart';
import 'core/widgets/logo_page_route.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  SystemChrome.setSystemUIOverlayStyle(
    const SystemUiOverlayStyle(
      statusBarColor: Colors.transparent,
      statusBarIconBrightness: Brightness.dark,
    ),
  );
  AppLanguage.load();
  final auth = AuthProvider()..init();
  // When a session can't be renewed, sign out and go back to login instead of leaving
  // every screen failing while the app still looks signed in.
  var expiring = false;
  ApiService.onSessionExpired = () async {
    if (expiring || auth.session == null) return;
    expiring = true;
    try {
      await auth.sessionExpired();
      final nav = appNavigatorKey.currentState;
      if (nav == null || !nav.mounted) return;
      final messenger = ScaffoldMessenger.maybeOf(nav.context);
      nav.pushNamedAndRemoveUntil('/login', (_) => false);
      messenger?.showSnackBar(
        const SnackBar(
          content: Text('Your session has ended. Please sign in again.'),
        ),
      );
    } finally {
      expiring = false;
    }
  };

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: auth),
        ChangeNotifierProvider(create: (_) => CartProvider()),
        ChangeNotifierProvider(create: (_) => ProductProvider()..loadInitial()),
      ],
      child: const SpazaSureApp(),
    ),
  );
}

/// Lets app-wide overlays (e.g. notification pop-ups) open screens.
final appNavigatorKey = GlobalKey<NavigatorState>();

class SpazaSureApp extends StatelessWidget {
  const SpazaSureApp({super.key});

  @override
  Widget build(BuildContext context) {
    // Every screen reads its text through tr(), so the whole app rebuilds in the new language
    return ValueListenableBuilder<String>(
      valueListenable: AppLanguage.code,
      builder: (context, _, __) => _app(context),
    );
  }

  Widget _app(BuildContext context) {
    return MaterialApp(
      title: 'SpazaSure',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      navigatorKey: appNavigatorKey,
      // New notifications pop up over every screen while someone is signed in
      builder: (context, child) => NotificationPopupHost(
        navigatorKey: appNavigatorKey,
        child: child ?? const SizedBox.shrink(),
      ),
      initialRoute: '/splash',
      onGenerateRoute: (settings) {
        final pages = <String, Widget Function()>{
          '/splash': () => const SplashScreen(),
          '/onboarding': () => const OnboardingScreen(),
          '/login': () => const LoginScreen(),
          '/register': () => const RegisterScreen(),
          '/customer-register': () => const CustomerRegisterScreen(),
          '/home': () => const MainShell(),
          '/customer-home': () => const CustomerShell(),
          '/marketplace': () => const MarketplaceScreen(),
          '/product': () => const ProductDetailScreen(),
          '/cart': () => const CartScreen(),
          '/orders': () => const OrdersScreen(),
          '/order-detail': () => const OrderDetailScreen(),
          '/delivery-tracking': () => const DeliveryTrackingScreen(),
          '/compliance': () => const ComplianceScreen(),
          '/notifications': () => const NotificationsScreen(),
          '/wallet': () => const WalletScreen(),
          '/academy': () => const AcademyScreen(),
          '/group-buy': () => const GroupBuyScreen(),
          '/qr-scanner': () => const QrScannerScreen(),
          '/report': () => const ReportScreen(),
          '/rate-delivery': () => const RateDeliveryScreen(),
          '/receipt': () => const ReceiptScreen(),
          '/verify-shop': () => const ShopVerifyScreen(),
          '/my-sticker': () => const MyStickerScreen(),
          '/inbox': () => Builder(
            builder: (ctx) {
              // Wait for the saved session so customers don't get the shop's inbox
              final auth = ctx.watch<AuthProvider>();
              if (!auth.initialized) {
                return const Scaffold(
                  body: Center(child: CircularProgressIndicator()),
                );
              }
              final asShop = auth.session?.role != 'customer';
              final arguments = settings.arguments;
              final chatId = arguments is String
                  ? arguments
                  : arguments is Map
                  ? arguments['chatId']?.toString()
                  : null;
              return InboxScreen(
                key: ValueKey(asShop),
                asShop: asShop,
                initialChatId: chatId,
              );
            },
          ),
        };
        final builder = pages[settings.name];
        if (builder == null) return null;
        if (settings.name == '/splash') {
          return MaterialPageRoute(
            builder: (_) => builder(),
            settings: settings,
          );
        }
        return LogoPageRoute.logoRoute(builder(), settings: settings);
      },
    );
  }
}
