import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/address_autocomplete_field.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';
import 'package:spazasure_app/services/address_service.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/services/customer_bulk_service.dart';

/// Where a Bulk Buyer's orders go. It also decides which suppliers they can buy from, since
/// suppliers only deliver (and let customers collect) within their area.
class BulkAddressScreen extends StatefulWidget {
  final BulkAddress? current;

  const BulkAddressScreen({super.key, this.current});

  @override
  State<BulkAddressScreen> createState() => _BulkAddressScreenState();
}

class _BulkAddressScreenState extends State<BulkAddressScreen> {
  late final _address = TextEditingController(text: widget.current?.address ?? '');
  late final _phone = TextEditingController(text: widget.current?.phone ?? '');
  AddressSuggestion? _picked;
  bool _saving = false;

  @override
  void dispose() {
    _address.dispose();
    _phone.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final place = _picked;
    if (place == null) return;
    setState(() => _saving = true);
    try {
      final saved = await CustomerBulkService.saveAddress(place, phone: _phone.text);
      if (mounted) Navigator.pop(context, saved);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e is ApiException ? e.message : 'Could not save the address: $e'),
          backgroundColor: AppColors.error,
        ),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: BrandAppBar(title: const Text('Delivery address')),
    body: ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          'Where should your bulk orders go?',
          style: AppTextStyles.h3,
        ),
        const SizedBox(height: 6),
        Text(
          'We show the suppliers that deliver to this address. If you choose to collect, '
          'you collect from their nearest branch.',
          style: AppTextStyles.bodySmall.copyWith(color: AppColors.textSecondary),
        ),
        const SizedBox(height: 18),
        AddressAutocompleteField(
          controller: _address,
          label: 'Street address',
          confirmed: _picked != null,
          onSelected: (place) => setState(() => _picked = place),
          onEdited: () {
            if (_picked != null) setState(() => _picked = null);
          },
        ),
        const SizedBox(height: 14),
        TextField(
          controller: _phone,
          keyboardType: TextInputType.phone,
          decoration: const InputDecoration(
            labelText: 'Phone for the driver (optional)',
            hintText: 'Uses your account number if left empty',
            prefixIcon: Icon(Icons.phone_outlined),
          ),
        ),
        const SizedBox(height: 24),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: _picked == null || _saving ? null : _save,
            child: Text(_saving ? 'Saving…' : 'Save address'),
          ),
        ),
        if (_picked == null)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              'Pick your address from the suggestions, or pin it on the map.',
              style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
            ),
          ),
      ],
    ),
  );
}
