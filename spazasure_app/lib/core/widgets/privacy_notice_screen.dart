import 'package:flutter/material.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:webview_flutter/webview_flutter.dart';

/// The privacy notice, shown inside the app so people come straight back to
/// where they were. The page's own "Back to SpazaSure" link closes it too, as
/// does any link off the notice (those open in the browser instead).
class PrivacyNoticeScreen extends StatefulWidget {
  final String url;
  const PrivacyNoticeScreen({super.key, required this.url});

  @override
  State<PrivacyNoticeScreen> createState() => _PrivacyNoticeScreenState();
}

class _PrivacyNoticeScreenState extends State<PrivacyNoticeScreen> {
  late final WebViewController _controller;
  bool _loading = true;
  bool _failed = false;
  bool _closed = false;

  void _set(VoidCallback change) {
    if (mounted) setState(change);
  }

  bool _isNotice(Uri uri) => uri.path.startsWith('/privacy');

  void _close() {
    if (_closed || !mounted) return;
    _closed = true;
    Navigator.of(context).maybePop();
  }

  @override
  void initState() {
    super.initState();
    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setNavigationDelegate(
        NavigationDelegate(
          onPageStarted: (_) => _set(() {
            _loading = true;
            _failed = false;
          }),
          onPageFinished: (_) => _set(() => _loading = false),
          onWebResourceError: (error) {
            if (error.isForMainFrame ?? false) {
              _set(() {
                _loading = false;
                _failed = true;
              });
            }
          },
          onNavigationRequest: (request) {
            final uri = Uri.tryParse(request.url);
            if (uri == null || _isNotice(uri)) {
              return NavigationDecision.navigate;
            }
            final portal = Uri.parse(widget.url).host;
            if (uri.host == portal) {
              // "Back to SpazaSure": back to the app, not the portal's login
              _close();
            } else {
              // Email links, the Information Regulator's site and so on
              launchUrl(uri, mode: LaunchMode.externalApplication);
            }
            return NavigationDecision.prevent;
          },
          // The portal moves between its pages without loading a new one
          onUrlChange: (change) {
            final uri = Uri.tryParse(change.url ?? '');
            if (uri != null && !_isNotice(uri)) _close();
          },
        ),
      )
      ..loadRequest(Uri.parse(widget.url));
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: BrandAppBar(title: const Text('Privacy notice')),
    body: Stack(
      children: [
        WebViewWidget(controller: _controller),
        if (_loading) const LinearProgressIndicator(minHeight: 2),
        if (_failed)
          Positioned.fill(
            child: ColoredBox(
              color: Theme.of(context).scaffoldBackgroundColor,
              child: Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.cloud_off_rounded, size: 44),
                      const SizedBox(height: 12),
                      const Text(
                        'The privacy notice could not be loaded. Check your connection and try again.',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 16),
                      FilledButton(
                        onPressed: () =>
                            _controller.loadRequest(Uri.parse(widget.url)),
                        child: const Text('Try again'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
      ],
    ),
  );
}
