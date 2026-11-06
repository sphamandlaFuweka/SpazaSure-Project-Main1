import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/group_buy_service.dart';

/// Lets a shop ask SpazaSure to arrange a group buy. Closes with true once the request is sent.
class RequestGroupBuySheet extends StatefulWidget {
  /// Replaces sending to the server in tests.
  final Future<void> Function(String productName, int? quantity, String? note)?
  send;

  const RequestGroupBuySheet({super.key, this.send});

  static Future<bool?> show(BuildContext context) => showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: AppColors.surface,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
    ),
    builder: (_) => const RequestGroupBuySheet(),
  );

  @override
  State<RequestGroupBuySheet> createState() => _RequestGroupBuySheetState();
}

class _RequestGroupBuySheetState extends State<RequestGroupBuySheet> {
  final _productController = TextEditingController();
  final _quantityController = TextEditingController();
  final _noteController = TextEditingController();
  bool _sending = false;
  String? _error;

  @override
  void dispose() {
    _productController.dispose();
    _quantityController.dispose();
    _noteController.dispose();
    super.dispose();
  }

  bool get _canSend => !_sending && _productController.text.trim().isNotEmpty;

  Future<void> _submit() async {
    final product = _productController.text.trim();
    final quantity = int.tryParse(_quantityController.text.trim());
    final note = _noteController.text.trim();
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      final send = widget.send;
      if (send != null) {
        await send(product, quantity, note.isEmpty ? null : note);
      } else {
        await GroupBuyService.createRequest(
          productName: product,
          quantity: quantity,
          note: note.isEmpty ? null : note,
        );
      }
      if (mounted) Navigator.pop(context, true);
    } catch (error) {
      if (mounted) {
        setState(() {
          _sending = false;
          _error = error.toString();
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => Padding(
    padding: EdgeInsets.fromLTRB(
      20,
      20,
      20,
      20 + MediaQuery.of(context).viewInsets.bottom,
    ),
    child: SingleChildScrollView(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Request a group buy', style: AppTextStyles.h3),
          const SizedBox(height: 4),
          Text(
            'Tell us what you want to buy together with other shops. '
            'We will try to arrange it with suppliers.',
            style: AppTextStyles.bodySmall,
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _productController,
            maxLength: 120,
            textCapitalization: TextCapitalization.sentences,
            onChanged: (_) => setState(() {}),
            decoration: const InputDecoration(
              labelText: 'Product',
              hintText: 'e.g. Maize meal 10kg',
              counterText: '',
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _quantityController,
            keyboardType: TextInputType.number,
            maxLength: 6,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: const InputDecoration(
              labelText: 'How many would you buy? (optional)',
              counterText: '',
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _noteController,
            maxLength: 500,
            maxLines: 3,
            minLines: 2,
            textCapitalization: TextCapitalization.sentences,
            decoration: const InputDecoration(
              labelText: 'Anything else? (optional)',
              hintText: 'Brand, supplier or area',
              counterText: '',
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: 10),
            Text(
              _error!,
              style: AppTextStyles.caption.copyWith(color: AppColors.error),
            ),
          ],
          const SizedBox(height: 16),
          ElevatedButton(
            onPressed: _canSend ? _submit : null,
            child: Text(_sending ? 'Sending...' : 'Send request'),
          ),
        ],
      ),
    ),
  );
}
