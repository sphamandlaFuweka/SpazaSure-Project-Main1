import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:spazasure_app/services/profile_service.dart';
import 'package:webview_flutter/webview_flutter.dart';

class OnboardingFeeCheckoutScreen extends StatefulWidget {
  final OnboardingCheckout checkout;

  const OnboardingFeeCheckoutScreen({required this.checkout, super.key});

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
            if (request.url == widget.checkout.returnUrl) {
              Navigator.pop(context, true);
              return NavigationDecision.prevent;
            }
            if (request.url == widget.checkout.cancelUrl) {
              Navigator.pop(context, false);
              return NavigationDecision.prevent;
            }
            return NavigationDecision.navigate;
          },
        ),
      )
      ..loadHtmlString(_paymentForm(widget.checkout));
  }

  static String _escape(String value) => const HtmlEscape().convert(value);

  static String _paymentForm(OnboardingCheckout checkout) {
    final action = _escape(checkout.actionUrl ?? '');
    final fields = checkout.fields.entries
        .map(
          (entry) =>
              '<input type="hidden" name="${_escape(entry.key)}" value="${_escape(entry.value)}">',
        )
        .join();
    return '''<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1"></head>
<body style="font-family:sans-serif;text-align:center;padding:32px"><p>Opening secure PayFast checkout…</p>
<form id="payment" method="post" action="$action">$fields</form>
<script>document.getElementById('payment').submit();</script></body></html>''';
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
