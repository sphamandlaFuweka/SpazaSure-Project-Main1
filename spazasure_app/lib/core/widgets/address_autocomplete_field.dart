import 'dart:async';

import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/map_pin_picker_screen.dart';
import 'package:spazasure_app/services/address_service.dart';

/// Text field that suggests real South African addresses as the user types.
/// The address only counts as valid once a suggestion is picked, or once the user
/// pins it on the map (for places search can't find), so it can always be mapped.
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

  /// Closest match from the last search, so the map picker opens near it
  LatLng? _nearest;

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
        if (results.isNotEmpty) {
          _nearest = LatLng(results.first.latitude, results.first.longitude);
        }
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

  Future<void> _pinOnMap() async {
    FocusScope.of(context).unfocus();
    final picked = await MapPinPickerScreen.open(
      context,
      initial: _nearest,
      typedAddress: widget.controller.text,
    );
    if (picked == null || !mounted) return;
    _pick(picked);
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
          style: const TextStyle(color: AppColors.textPrimary, fontSize: 16),
          cursorColor: AppColors.primary,
          decoration: InputDecoration(
            labelText: widget.label,
            hintText: widget.hint,
            labelStyle: const TextStyle(color: AppColors.textPrimary),
            floatingLabelStyle: const TextStyle(color: AppColors.primary),
            hintStyle: const TextStyle(color: AppColors.textSecondary),
            fillColor: AppColors.surface,
            filled: true,
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: AppColors.divider),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(
                color: AppColors.primary,
                width: 1.5,
              ),
            ),
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
                const Divider(height: 1),
                ListTile(
                  dense: true,
                  leading: const Icon(
                    Icons.push_pin_outlined,
                    size: 20,
                    color: AppColors.primary,
                  ),
                  title: const Text(
                    'Not listed? Pin it on the map',
                    style: TextStyle(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  onTap: _pinOnMap,
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
                    ? 'No matches. Add the suburb or town, or pin it on the map.'
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
        if (_suggestions.isEmpty)
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              onPressed: _pinOnMap,
              icon: const Icon(Icons.push_pin_outlined, size: 18),
              label: Text(
                widget.confirmed
                    ? 'Adjust the pin on the map'
                    : 'Pin it on the map instead',
              ),
              style: TextButton.styleFrom(
                foregroundColor: AppColors.primary,
                backgroundColor: _searched && !widget.confirmed
                    ? AppColors.primaryTint
                    : null,
                padding: const EdgeInsets.symmetric(horizontal: 10),
              ),
            ),
          ),
      ],
    );
  }
}
