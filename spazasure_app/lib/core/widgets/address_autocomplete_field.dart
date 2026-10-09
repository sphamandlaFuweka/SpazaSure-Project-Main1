import 'dart:async';

import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/services/address_service.dart';

/// Text field that suggests real South African addresses as the user types.
/// The address only counts as valid once a suggestion is picked.
class AddressAutocompleteField extends StatefulWidget {
  final TextEditingController controller;
  final String label;
  final String hint;
  final ValueChanged<AddressSuggestion> onSelected;
  final VoidCallback onEdited;
  final bool confirmed;

  const AddressAutocompleteField({
    super.key,
    required this.controller,
    required this.onSelected,
    required this.onEdited,
    required this.confirmed,
    this.label = 'Physical Address',
    this.hint = 'Start typing the street address',
  });

  @override
  State<AddressAutocompleteField> createState() =>
      _AddressAutocompleteFieldState();
}

class _AddressAutocompleteFieldState extends State<AddressAutocompleteField> {
  Timer? _debounce;
  List<AddressSuggestion> _suggestions = [];
  bool _loading = false;
  String? _error;
  bool _searched = false;

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  void _onChanged(String value) {
    widget.onEdited();
    _debounce?.cancel();
    if (value.trim().length < 3) {
      setState(() {
        _suggestions = [];
        _searched = false;
      });
      return;
    }
    _debounce = Timer(const Duration(milliseconds: 600), () => _search(value));
  }

  Future<void> _search(String value) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await AddressService.search(value);
      if (!mounted || widget.controller.text != value) return;
      setState(() {
        _suggestions = results;
        _searched = true;
      });
    } catch (_) {
      if (!mounted) return;
      setState(
        () => _error = 'Could not search addresses. Check your connection.',
      );
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _pick(AddressSuggestion s) {
    widget.controller.text = s.label;
    setState(() {
      _suggestions = [];
      _searched = false;
    });
    FocusScope.of(context).unfocus();
    widget.onSelected(s);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextField(
          controller: widget.controller,
          onChanged: _onChanged,
          textInputAction: TextInputAction.search,
          keyboardType: TextInputType.streetAddress,
          maxLines: null,
          decoration: InputDecoration(
            labelText: widget.label,
            hintText: widget.hint,
            prefixIcon: const Icon(Icons.location_on_outlined),
            suffixIcon: _loading
                ? const Padding(
                    padding: EdgeInsets.all(14),
                    child: SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  )
                : widget.confirmed
                ? const Icon(Icons.check_circle, color: AppColors.success)
                : null,
          ),
        ),
        if (_suggestions.isNotEmpty)
          Container(
            margin: const EdgeInsets.only(top: 6),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: const Color(0xFFE0E0E0)),
            ),
            child: Column(
              children: [
                for (final s in _suggestions)
                  ListTile(
                    dense: true,
                    leading: const Icon(Icons.place_outlined, size: 20),
                    title: Text(s.label),
                    onTap: () => _pick(s),
                  ),
              ],
            ),
          ),
        Padding(
          padding: const EdgeInsets.only(top: 6, left: 4),
          child: Text(
            _error ??
                (widget.confirmed
                    ? 'Address confirmed on the map'
                    : _searched && _suggestions.isEmpty
                    ? 'No matches. Try adding the suburb or city.'
                    : 'Choose a suggestion to confirm your address'),
            style: TextStyle(
              fontSize: 12,
              color: _error != null
                  ? AppColors.error
                  : widget.confirmed
                  ? AppColors.success
                  : AppColors.textSecondary,
            ),
          ),
        ),
      ],
    );
  }
}
