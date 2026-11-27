import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:in_app_update/in_app_update.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';

class PlayStoreUpdatePrompt extends StatefulWidget {
  const PlayStoreUpdatePrompt({
    required this.navigatorKey,
    required this.child,
    super.key,
  });

  final GlobalKey<NavigatorState> navigatorKey;
  final Widget child;

  @override
  State<PlayStoreUpdatePrompt> createState() => _PlayStoreUpdatePromptState();
}

class _PlayStoreUpdatePromptState extends State<PlayStoreUpdatePrompt> {
  StreamSubscription<InstallStatus>? _updateSubscription;
  bool _completingFlexibleUpdate = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      unawaited(_checkForUpdate());
    });
  }

  @override
  void dispose() {
    unawaited(_stopListeningForUpdate());
    super.dispose();
  }

  Future<void> _checkForUpdate() async {
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) return;

    await Future<void>.delayed(const Duration(seconds: 4));
    if (!mounted) return;

    try {
      final update = await InAppUpdate.checkForUpdate();
      if (update.updateAvailability != UpdateAvailability.updateAvailable ||
          (!update.flexibleUpdateAllowed && !update.immediateUpdateAllowed)) {
        return;
      }

      final navigator = widget.navigatorKey.currentState;
      if (navigator == null || !navigator.mounted) return;
      final shouldUpdate = await showDialog<bool>(
        context: navigator.context,
        builder: (context) => AlertDialog(
          title: Text(tr('update.available.title')),
          content: Text(tr('update.available.message')),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: Text(tr('update.action.later')),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: Text(tr('update.action.install')),
            ),
          ],
        ),
      );
      if (shouldUpdate != true || !mounted) return;

      if (update.flexibleUpdateAllowed) {
        await _startFlexibleUpdate();
      } else if (update.immediateUpdateAllowed) {
        await _startImmediateUpdate();
      }
    } on PlatformException catch (error) {
      debugPrint('Play Store update check failed: $error');
    } on MissingPluginException catch (error) {
      debugPrint('Play Store update check is unavailable: $error');
    }
  }

  Future<void> _startFlexibleUpdate() async {
    _updateSubscription = InAppUpdate.installUpdateListener.listen(
      (status) {
        if (status == InstallStatus.downloaded) {
          unawaited(_completeFlexibleUpdate());
        } else if (status == InstallStatus.failed) {
          unawaited(_stopListeningForUpdate());
          _showUpdateFailure();
        }
      },
      onError: (Object error, StackTrace stackTrace) {
        debugPrint('Play Store update download failed: $error');
        unawaited(_stopListeningForUpdate());
        _showUpdateFailure();
      },
    );

    try {
      final result = await InAppUpdate.startFlexibleUpdate();
      if (result != AppUpdateResult.success) {
        await _stopListeningForUpdate();
        if (result == AppUpdateResult.inAppUpdateFailed) {
          _showUpdateFailure();
        }
      }
    } on PlatformException catch (error) {
      debugPrint('Could not start Play Store update: $error');
      await _stopListeningForUpdate();
      _showUpdateFailure();
    } on MissingPluginException catch (error) {
      debugPrint('Play Store update is unavailable: $error');
      await _stopListeningForUpdate();
      _showUpdateFailure();
    }
  }

  Future<void> _completeFlexibleUpdate() async {
    if (_completingFlexibleUpdate) return;
    _completingFlexibleUpdate = true;
    try {
      await InAppUpdate.completeFlexibleUpdate();
    } on PlatformException catch (error) {
      debugPrint('Could not install Play Store update: $error');
      _showUpdateFailure();
    } on MissingPluginException catch (error) {
      debugPrint('Play Store update is unavailable: $error');
      _showUpdateFailure();
    } finally {
      await _stopListeningForUpdate();
      _completingFlexibleUpdate = false;
    }
  }

  Future<void> _startImmediateUpdate() async {
    try {
      final result = await InAppUpdate.performImmediateUpdate();
      if (result == AppUpdateResult.inAppUpdateFailed) {
        _showUpdateFailure();
      }
    } on PlatformException catch (error) {
      debugPrint('Could not start Play Store update: $error');
      _showUpdateFailure();
    } on MissingPluginException catch (error) {
      debugPrint('Play Store update is unavailable: $error');
      _showUpdateFailure();
    }
  }

  Future<void> _stopListeningForUpdate() async {
    final subscription = _updateSubscription;
    _updateSubscription = null;
    await subscription?.cancel();
  }

  void _showUpdateFailure() {
    if (!mounted) return;
    final context = widget.navigatorKey.currentContext;
    if (context == null) return;
    ScaffoldMessenger.maybeOf(
      context,
    )?.showSnackBar(SnackBar(content: Text(tr('update.error'))));
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
