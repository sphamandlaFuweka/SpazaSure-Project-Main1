import 'dart:convert';

import 'package:http/http.dart' as http;

class AddressSuggestion {
  final String label;
  final String street;
  final String suburb;
  final String city;
  final String province;
  final String postalCode;
  final double latitude;
  final double longitude;

  const AddressSuggestion({
    required this.label,
    required this.street,
    required this.suburb,
    required this.city,
    required this.province,
    required this.postalCode,
    required this.latitude,
    required this.longitude,
  });

  /// Street line plus suburb, used to fill a single "address" field.
  String get addressLine =>
      [street, suburb].where((p) => p.isNotEmpty).join(', ');

  factory AddressSuggestion.fromNominatim(Map<String, dynamic> json) {
    final a = (json['address'] as Map?)?.cast<String, dynamic>() ?? const {};
    String pick(List<String> keys) {
      for (final k in keys) {
        final v = a[k]?.toString().trim();
        if (v != null && v.isNotEmpty) return v;
      }
      return '';
    }

    final street = [
      pick(['house_number']),
      pick(['road', 'pedestrian', 'residential']),
    ].where((p) => p.isNotEmpty).join(' ');
    final suburb = pick(['suburb', 'neighbourhood', 'quarter', 'township']);
    final city = pick(['city', 'town', 'village', 'municipality', 'county']);
    final province = pick(['state']);
    final postal = pick(['postcode']);

    final label = [
      street,
      suburb,
      city,
      province,
      postal,
    ].where((p) => p.isNotEmpty).join(', ');

    return AddressSuggestion(
      label: label.isEmpty ? (json['display_name']?.toString() ?? '') : label,
      street: street,
      suburb: suburb,
      city: city,
      province: province,
      postalCode: postal,
      latitude: double.parse(json['lat'].toString()),
      longitude: double.parse(json['lon'].toString()),
    );
  }
}

class AddressService {
  /// Autocomplete for South African addresses via OpenStreetMap Nominatim.
  static Future<List<AddressSuggestion>> search(String query) async {
    final q = query.trim();
    if (q.length < 3) return const [];
    final uri = Uri.https('nominatim.openstreetmap.org', '/search', {
      'q': q,
      'format': 'jsonv2',
      'addressdetails': '1',
      'limit': '6',
      'countrycodes': 'za',
    });
    final res = await http
        .get(uri, headers: {'User-Agent': 'SpazaSure-App/1.0'})
        .timeout(const Duration(seconds: 8));
    if (res.statusCode != 200) {
      throw Exception('Address search failed (${res.statusCode}).');
    }
    final data = jsonDecode(res.body) as List;
    return data
        .whereType<Map<String, dynamic>>()
        .map(AddressSuggestion.fromNominatim)
        .where((s) => s.label.isNotEmpty)
        .toList();
  }
}
