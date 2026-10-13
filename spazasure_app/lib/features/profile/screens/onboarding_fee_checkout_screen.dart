import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:webview_flutter/webview_flutter.dart';

class OnboardingFeeCheckoutScreen extends StatefulWidget {
  final String stripeUrl;
  final String title;

  const OnboardingFeeCheckoutScreen({
    required this.stripeUrl,
    this.title = 'Complete payment',
    super.key,
  });

  @override
  State<OnboardingFeeCheckoutScreen> createState() =>
      _OnboardingFeeCheckoutScreenState();
}

class _OnboardingFeeCheckoutScreenState
    extends State<OnboardingFeeCheckoutScreen> {
  late final WebViewController _controller;
  bool _loading = true;
  bool _loadFailed = false;

  @override
  void initState() {
    super.initState();
    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setNavigationDelegate(
        NavigationDelegate(
          onPageStarted: (_) {
            if (mounted) {
              setState(() {
                _loading = true;
                _loadFailed = false;
              });
            }
          },
          onPageFinished: (_) {
            if (mounted) {
              setState(() {
                _loading = false;
                _loadFailed = false;
              });
            }
          },
          onWebResourceError: (error) {
            if (error.isForMainFrame ?? false) {
              if (mounted) {
                setState(() {
                  _loading = false;
                  _loadFailed = true;
                });
              }
            }
          },
          onNavigationRequest: (request) {
            final paymentResult = Uri.tryParse(
              request.url,
            )?.queryParameters['payment'];
            if (paymentResult == 'success' || paymentResult == 'cancelled') {
              Navigator.pop(context, paymentResult == 'success');
              return NavigationDecision.prevent;
            }
            return NavigationDecision.navigate;
          },
        ),
      );
    _loadCheckout();
  }

  Future<void> _loadCheckout() async {
    setState(() {
      _loading = true;
      _loadFailed = false;
    });
    try {
      await _controller.loadRequest(Uri.parse(widget.stripeUrl));
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _loadFailed = true;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(widget.title),
        backgroundColor: AppColors.surface,
        surfaceTintColor: AppColors.surface,
      ),
      body: Column(
        children: [
          Container(
            width: double.infinity,
            color: AppColors.surface,
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 14),
            child: Row(
              children: [
                Container(
                  width: 48,
                  height: 48,
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(14),
                  ),
                  padding: const EdgeInsets.all(5),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: Image.asset(
                      'assets/images/spazasure_logo.jpg',
                      fit: BoxFit.cover,
                    ),
                  ),
                ),
                const SizedBox(width: 13),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'SpazaSure checkout',
                        style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: AppColors.textPrimary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        'Secure payment processed by Stripe',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 7,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.background,
                    borderRadius: BorderRadius.circular(20),
                    border: Border.all(color: AppColors.divider),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(
                        Icons.lock_outline_rounded,
                        size: 14,
                        color: AppColors.success,
                      ),
                      const SizedBox(width: 5),
                      Text(
                        'SECURE',
                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                          color: AppColors.textPrimary,
                          fontWeight: FontWeight.w800,
                          letterSpacing: 0.4,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          if (_loading) const LinearProgressIndicator(minHeight: 2),
          Expanded(
            child: Stack(
              children: [
                WebViewWidget(controller: _controller),
                if (_loadFailed)
                  Positioned.fill(
                    child: ColoredBox(
                      color: AppColors.background,
                      child: Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(
                                Icons.cloud_off_rounded,
                                color: AppColors.textHint,
                                size: 44,
                              ),
                              const SizedBox(height: 12),
                              Text(
                                'Checkout could not be loaded',
                                style: Theme.of(context).textTheme.titleMedium
                                    ?.copyWith(
                                      color: AppColors.textPrimary,
                                      fontWeight: FontWeight.w700,
                                    ),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                'Check your connection and try again. If you already submitted a payment, check its status before trying again.',
                                textAlign: TextAlign.center,
                                style: Theme.of(context).textTheme.bodyMedium
                                    ?.copyWith(color: AppColors.textSecondary),
                              ),
                              const SizedBox(height: 18),
                              FilledButton.icon(
                                onPressed: _loadCheckout,
                                icon: const Icon(Icons.refresh_rounded),
                                label: const Text('Try again'),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
