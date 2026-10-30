import 'package:app_links/app_links.dart';
import 'package:flutter/material.dart';

/// Opens the app from a shared group buy page ("Open in the SpazaSure app").
/// The link is `spazasure://group-buy/<id>`. It waits until a shop is signed in and on its
/// home screen, so a link tapped on the login screen still opens the deal after sign-in.
class DeepLinkService {
  static GlobalKey<NavigatorState>? _navigatorKey;
  static String? _pendingGroupBuyId;
  static bool _shopShellOpen = false;

  static void init(GlobalKey<NavigatorState> navigatorKey) {
    _navigatorKey = navigatorKey;
    // Emits the link that launched the app as well as ones tapped while it is running
    AppLinks().uriLinkStream.listen(_handle, onError: (_) {});
  }

  /// The group buy id in `spazasure://group-buy/<id>`, or in `https://…/g/<id>` (the page's own address).
  static String? groupBuyIdFrom(Uri uri) {
    final segments = uri.pathSegments.where((s) => s.isNotEmpty).toList();
    if (uri.scheme == 'spazasure' && uri.host == 'group-buy') {
      return segments.isEmpty ? null : segments.first;
    }
    if (segments.length == 2 && segments.first == 'g') return segments[1];
    return null;
  }

  static void _handle(Uri uri) {
    final id = groupBuyIdFrom(uri);
    if (id == null) return;
    _pendingGroupBuyId = id;
    if (_shopShellOpen) _openPending();
  }

  /// Called by the shop's home shell once it is on screen.
  static void shopShellOpened() {
    _shopShellOpen = true;
    _openPending();
  }

  static void shopShellClosed() => _shopShellOpen = false;

  static void _openPending() {
    final id = _pendingGroupBuyId;
    final navigator = _navigatorKey?.currentState;
    if (id == null || navigator == null) return;
    _pendingGroupBuyId = null;
    navigator.pushNamed('/group-buy', arguments: id);
  }
}
