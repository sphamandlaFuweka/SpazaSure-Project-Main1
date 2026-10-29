import 'dart:convert';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/privacy_notice_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/privacy_service.dart';

/// The SpazaSure privacy notice (POPIA s18), published on the portal.
const privacyNoticeUrl = 'https://portal.spazasure.co.za/privacy';

/// Opens the notice inside the app; ?app=1 makes its back link say "Back to the SpazaSure app".
Future<void> openPrivacyNotice(BuildContext context) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) =>
            const PrivacyNoticeScreen(url: '$privacyNoticeUrl?app=1'),
      ),
    );

/// Asks people who haven't accepted the current privacy notice (e.g. they registered before it
/// existed, or it has changed) to accept it, or sign out. Call once after sign-in.
Future<void> ensurePrivacyConsent(
  BuildContext context,
  PrivacyService service,
) async {
  try {
    if (await service.hasAcceptedNotice()) return;
  } catch (_) {
    return; // offline or server trouble: ask next time rather than block the app
  }
  if (!context.mounted) return;
  final agreed = await showDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder: (ctx) => PopScope(
      canPop: false,
      child: AlertDialog(
        title: const Text('Our privacy notice'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'SpazaSure has a privacy notice explaining how we handle your personal information, '
              'including identity checks by our partner Didit, which take place outside South Africa. '
              'Please read it and agree to keep using SpazaSure.',
              style: TextStyle(fontSize: 13),
            ),
            TextButton(
              style: TextButton.styleFrom(padding: EdgeInsets.zero),
              onPressed: () => openPrivacyNotice(context),
              child: const Text('Read the privacy notice'),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Sign out'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('I agree'),
          ),
        ],
      ),
    ),
  );
  if (!context.mounted) return;
  if (agreed == true) {
    try {
      await service.acceptNotice();
    } catch (_) {
      // Not recorded; they'll be asked again next time.
    }
    return;
  }
  await context.read<AuthProvider>().logout();
  if (context.mounted) {
    Navigator.of(context).pushNamedAndRemoveUntil('/login', (_) => false);
  }
}

/// The consent people must give to register. The server records it with the notice version.
class PrivacyConsentTile extends StatelessWidget {
  final bool value;
  final ValueChanged<bool> onChanged;

  /// Shop owners also agree to the Didit identity check, which runs outside South Africa.
  final bool includesIdentityCheck;

  const PrivacyConsentTile({
    super.key,
    required this.value,
    required this.onChanged,
    this.includesIdentityCheck = false,
  });

  @override
  Widget build(BuildContext context) {
    return CheckboxListTile(
      contentPadding: EdgeInsets.zero,
      controlAffinity: ListTileControlAffinity.leading,
      activeColor: AppColors.primary,
      value: value,
      onChanged: (checked) => onChanged(checked ?? false),
      title: Text(
        includesIdentityCheck
            ? 'I have read the privacy notice and agree that SpazaSure may process my information as it describes, '
                  'including checking my identity through its partner Didit, outside South Africa. *'
            : 'I have read the privacy notice and agree that SpazaSure may process my information as it describes. *',
        style: const TextStyle(fontSize: 13),
      ),
      subtitle: Align(
        alignment: Alignment.centerLeft,
        child: TextButton(
          style: TextButton.styleFrom(
            padding: EdgeInsets.zero,
            minimumSize: const Size(0, 32),
          ),
          onPressed: () => openPrivacyNotice(context),
          child: const Text('Read the privacy notice'),
        ),
      ),
    );
  }
}

/// "Your privacy" on the profile: download a copy of your data, or ask for the account to be deleted.
class PrivacySection extends StatefulWidget {
  final PrivacyService service;

  /// What deleting removes, in the person's terms (shop owner or customer).
  final String deletionExplanation;

  const PrivacySection({
    super.key,
    required this.service,
    required this.deletionExplanation,
  });

  @override
  State<PrivacySection> createState() => _PrivacySectionState();
}

class _PrivacySectionState extends State<PrivacySection> {
  DeletionRequest? _request;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _loadRequest();
  }

  Future<void> _loadRequest() async {
    try {
      final request = await widget.service.deletionRequest();
      if (mounted) setState(() => _request = request);
    } catch (_) {
      // The buttons still work; the status just isn't shown.
    }
  }

  void _show(String message, {bool error = false}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: error ? AppColors.error : null,
      ),
    );
  }

  Future<void> _download() async {
    setState(() => _busy = true);
    try {
      final data = await widget.service.myData();
      final bytes = utf8.encode(
        const JsonEncoder.withIndent('  ').convert(data),
      );
      final date = DateTime.now().toIso8601String().substring(0, 10);
      final saved = await FilePicker.platform.saveFile(
        dialogTitle: 'Save your SpazaSure data',
        fileName: 'spazasure-my-data-$date.json',
        bytes: bytes,
      );
      if (saved != null) _show('Your data was saved.');
    } catch (e) {
      _show(
        e is ApiException
            ? e.message
            : 'Could not download your data. Try again.',
        error: true,
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _requestDeletion() async {
    final reasonController = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete your account?'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              widget.deletionExplanation,
              style: const TextStyle(fontSize: 13),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: reasonController,
              maxLength: 1000,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: 'Why are you leaving? (optional)',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Keep my account'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.error),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Request deletion'),
          ),
        ],
      ),
    );
    final reason = reasonController.text.trim();
    reasonController.dispose();
    if (confirmed != true) return;

    setState(() => _busy = true);
    try {
      _show(await widget.service.requestDeletion(reason));
      await _loadRequest();
    } catch (e) {
      _show(
        e is ApiException
            ? e.message
            : 'Could not send your request. Try again.',
        error: true,
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _cancel() async {
    setState(() => _busy = true);
    try {
      await widget.service.cancelDeletion();
      _show('Your deletion request was cancelled.');
      await _loadRequest();
    } catch (e) {
      _show(
        e is ApiException ? e.message : 'Could not cancel the request.',
        error: true,
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final request = _request;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Padding(
          padding: EdgeInsets.fromLTRB(4, 8, 4, 4),
          child: Text(
            'Your privacy',
            style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
          ),
        ),
        ListTile(
          contentPadding: const EdgeInsets.symmetric(horizontal: 4),
          leading: const Icon(Icons.policy_outlined),
          title: const Text('Privacy notice'),
          subtitle: const Text('How SpazaSure handles your information'),
          onTap: () => openPrivacyNotice(context),
        ),
        ListTile(
          contentPadding: const EdgeInsets.symmetric(horizontal: 4),
          leading: const Icon(Icons.download_outlined),
          title: const Text('Download my data'),
          subtitle: const Text('A copy of everything we hold about you'),
          enabled: !_busy,
          onTap: _download,
        ),
        if (request?.isPending == true)
          Container(
            margin: const EdgeInsets.symmetric(vertical: 6),
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.warning.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              children: [
                const Expanded(
                  child: Text(
                    "You asked for your account to be deleted. We'll let you know when it's done.",
                    style: TextStyle(fontSize: 12),
                  ),
                ),
                TextButton(
                  onPressed: _busy ? null : _cancel,
                  child: const Text('Cancel'),
                ),
              ],
            ),
          )
        else ...[
          if (request?.status == 'rejected' && request?.decisionNote != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
              child: Text(
                "We couldn't delete your account yet: ${request!.decisionNote}",
                style: const TextStyle(fontSize: 12, color: AppColors.error),
              ),
            ),
          ListTile(
            contentPadding: const EdgeInsets.symmetric(horizontal: 4),
            leading: const Icon(
              Icons.person_off_outlined,
              color: AppColors.error,
            ),
            title: const Text(
              'Delete my account',
              style: TextStyle(color: AppColors.error),
            ),
            subtitle: const Text('Erase your personal information'),
            enabled: !_busy,
            onTap: _requestDeletion,
          ),
        ],
      ],
    );
  }
}
