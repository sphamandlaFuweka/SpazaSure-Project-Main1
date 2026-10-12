import 'package:flutter/material.dart';
import 'package:webview_flutter/webview_flutter.dart';

class OnboardingFeeCheckoutScreen extends StatefulWidget {
  final String stripeUrl;

  const OnboardingFeeCheckoutScreen({required this.stripeUrl, super.key});

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
      )
      ..loadRequest(Uri.parse(widget.stripeUrl));
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
