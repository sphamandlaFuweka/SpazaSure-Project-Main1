import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:webview_flutter/webview_flutter.dart';
import 'package:webview_flutter_android/webview_flutter_android.dart';
import 'package:webview_flutter_wkwebview/webview_flutter_wkwebview.dart';

/// Didit's identity check (ID photo and selfie) inside the app, so the owner never leaves
/// SpazaSure. When Didit finishes it opens [doneUrl]; the screen closes and returns true.
class IdentityCheckScreen extends StatefulWidget {
  final String checkUrl;
  final String doneUrl;

  const IdentityCheckScreen({super.key, required this.checkUrl, required this.doneUrl});

  @override
  State<IdentityCheckScreen> createState() => _IdentityCheckScreenState();
}

class _IdentityCheckScreenState extends State<IdentityCheckScreen> {
  late final WebViewController _controller;
  bool _loading = true;
  bool _loadFailed = false;
  bool _cameraDenied = false;
  bool _closed = false;

  @override
  void initState() {
    super.initState();
    PlatformWebViewControllerCreationParams params = const PlatformWebViewControllerCreationParams();
    if (WebViewPlatform.instance is WebKitWebViewPlatform) {
      // iOS: the camera preview must play inside the page, without a tap.
      params = WebKitWebViewControllerCreationParams(
        allowsInlineMediaPlayback: true,
        mediaTypesRequiringUserAction: const <PlaybackMediaTypes>{},
      );
    }
    _controller = WebViewController.fromPlatformCreationParams(
      params,
      // Didit asks for the camera to photograph the ID and take the selfie.
      onPermissionRequest: (request) => request.grant(),
    )
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setNavigationDelegate(
        NavigationDelegate(
          onPageStarted: (_) => _setLoading(true),
          onPageFinished: (_) => _setLoading(false),
          onWebResourceError: (error) {
            if ((error.isForMainFrame ?? false) && mounted) {
              setState(() {
                _loading = false;
                _loadFailed = true;
              });
            }
          },
          onNavigationRequest: (request) {
            if (_isDone(request.url)) {
              _finish(true);
              return NavigationDecision.prevent;
            }
            // Only Didit's own pages load here.
            final host = Uri.tryParse(request.url)?.host ?? '';
            return host == 'didit.me' || host.endsWith('.didit.me')
                ? NavigationDecision.navigate
                : NavigationDecision.prevent;
          },
        ),
      );
    if (_controller.platform is AndroidWebViewController) {
      (_controller.platform as AndroidWebViewController).setMediaPlaybackRequiresUserGesture(false);
    }
    _start();
  }

  bool _isDone(String url) {
    final done = Uri.parse(widget.doneUrl);
    final target = Uri.tryParse(url);
    return target != null && target.host == done.host && target.path.startsWith(done.path);
  }

  void _setLoading(bool loading) {
    if (mounted && !_closed) setState(() => _loading = loading);
  }

  void _finish(bool completed) {
    if (_closed || !mounted) return;
    _closed = true;
    Navigator.pop(context, completed);
  }

  Future<void> _start() async {
    // Android needs the app's own camera permission before the page can use the camera.
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      final status = await Permission.camera.request();
      if (!status.isGranted) {
        if (mounted) {
          setState(() {
            _cameraDenied = true;
            _loading = false;
          });
        }
        return;
      }
    }
    if (mounted) {
      setState(() {
        _cameraDenied = false;
        _loadFailed = false;
        _loading = true;
      });
    }
    await _controller.loadRequest(Uri.parse(widget.checkUrl));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Verify your identity'),
        leading: IconButton(
          icon: const Icon(Icons.close),
          tooltip: 'Close',
          onPressed: () => _finish(false),
        ),
      ),
      body: _cameraDenied
          ? _message(
              Icons.no_photography_outlined,
              'SpazaSure needs your camera to photograph your ID and take a selfie.',
              actions: [
                FilledButton(onPressed: _start, child: const Text('Try again')),
                TextButton(onPressed: openAppSettings, child: const Text('Open settings')),
              ],
            )
          : _loadFailed
              ? _message(
                  Icons.wifi_off_outlined,
                  'The identity check could not load. Check your connection and try again.',
                  actions: [FilledButton(onPressed: _start, child: const Text('Try again'))],
                )
              : Stack(
                  children: [
                    WebViewWidget(controller: _controller),
                    if (_loading) const LinearProgressIndicator(color: AppColors.primary),
                  ],
                ),
    );
  }

  Widget _message(IconData icon, String text, {required List<Widget> actions}) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 48, color: AppColors.textSecondary),
              const SizedBox(height: 12),
              Text(text, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              ...actions,
            ],
          ),
        ),
      );
}
