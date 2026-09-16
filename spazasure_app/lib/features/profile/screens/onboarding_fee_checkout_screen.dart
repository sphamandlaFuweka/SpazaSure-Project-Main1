import 'package:flutter/material.dart';
import 'package:spazasure_app/services/profile_service.dart';
import 'package:webview_flutter/webview_flutter.dart';

class OnboardingFeeCheckoutScreen extends StatefulWidget {
  final OnboardingCheckout? checkout;
  final String? stripeUrl;

  const OnboardingFeeCheckoutScreen({this.checkout, this.stripeUrl, super.key});

  @override
  State<OnboardingFeeCheckoutScreen> createState() =>
      _OnboardingFeeCheckoutScreenState();
}

class _OnboardingFeeCheckoutScreenState
    extends State<OnboardingFeeCheckoutScreen> {
  late final WebViewController _controller;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setNavigationDelegate(
        NavigationDelegate(
          onPageFinished: (_) {
            if (mounted) setState(() => _loading = false);
          },
          onNavigationRequest: (request) {
            if (widget.checkout?.returnUrl != null &&
                request.url == widget.checkout!.returnUrl) {
              Navigator.pop(context, true);
              return NavigationDecision.prevent;
            }
            if (widget.checkout?.cancelUrl != null &&
                request.url == widget.checkout!.cancelUrl) {
              Navigator.pop(context, false);
              return NavigationDecision.prevent;
            }
            return NavigationDecision.navigate;
          },
        ),
      )
      ..loadRequest(
        Uri.parse(widget.stripeUrl ?? widget.checkout?.actionUrl ?? ''),
      );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Pay onboarding fee')),
      body: Stack(
        children: [
          WebViewWidget(controller: _controller),
          if (_loading) const Center(child: CircularProgressIndicator()),
        ],
      ),
    );
  }
}
