/// Product page information from the supplier: specifications, nutrition and serving,
/// ingredients, usage, storage, warnings, and how to tell the genuine product from a fake.
class ProductInfo {
  final String? brand;
  final String? packSize;
  final String? countryOfOrigin;
  final String? manufacturer;
  final String? ingredients;
  final List<String> allergens;
  final String? servingSize;
  final String? servingsPerPack;
  final List<NutritionRow> nutrition;
  final String? usageInstructions;
  final String? storageInstructions;
  final String? warnings;
  final List<SpecificationRow> specifications;
  final List<String> authenticityTips;

  const ProductInfo({
    this.brand,
    this.packSize,
    this.countryOfOrigin,
    this.manufacturer,
    this.ingredients,
    this.allergens = const [],
    this.servingSize,
    this.servingsPerPack,
    this.nutrition = const [],
    this.usageInstructions,
    this.storageInstructions,
    this.warnings,
    this.specifications = const [],
    this.authenticityTips = const [],
  });

  static String? _text(dynamic v) {
    final s = v?.toString().trim();
    return s == null || s.isEmpty ? null : s;
  }

  static List<String> _strings(dynamic v) => v is List
      ? v.map((e) => e?.toString().trim() ?? '').where((e) => e.isNotEmpty).toList()
      : const [];

  factory ProductInfo.fromJson(Map<String, dynamic>? json) {
    if (json == null) return const ProductInfo();
    return ProductInfo(
      brand: _text(json['brand']),
      packSize: _text(json['packSize']),
      countryOfOrigin: _text(json['countryOfOrigin']),
      manufacturer: _text(json['manufacturer']),
      ingredients: _text(json['ingredients']),
      allergens: _strings(json['allergens']),
      servingSize: _text(json['servingSize']),
      servingsPerPack: _text(json['servingsPerPack']),
      nutrition: (json['nutrition'] is List)
          ? (json['nutrition'] as List)
                .whereType<Map>()
                .map((m) => NutritionRow(
                      nutrient: _text(m['nutrient']) ?? '',
                      per100: _text(m['per100']),
                      perServing: _text(m['perServing']),
                    ))
                .where((r) => r.nutrient.isNotEmpty)
                .toList()
          : const [],
      usageInstructions: _text(json['usageInstructions']),
      storageInstructions: _text(json['storageInstructions']),
      warnings: _text(json['warnings']),
      specifications: (json['specifications'] is List)
          ? (json['specifications'] as List)
                .whereType<Map>()
                .map((m) => SpecificationRow(
                      label: _text(m['label']) ?? '',
                      value: _text(m['value']) ?? '',
                    ))
                .where((r) => r.label.isNotEmpty && r.value.isNotEmpty)
                .toList()
          : const [],
      authenticityTips: _strings(json['authenticityTips']),
    );
  }

  /// Brand, pack size, origin, manufacturer and the supplier's specification rows.
  List<SpecificationRow> get facts => [
    if (brand != null) SpecificationRow(label: 'Brand', value: brand!),
    if (packSize != null) SpecificationRow(label: 'Pack size', value: packSize!),
    if (manufacturer != null) SpecificationRow(label: 'Manufacturer', value: manufacturer!),
    if (countryOfOrigin != null) SpecificationRow(label: 'Country of origin', value: countryOfOrigin!),
    ...specifications,
  ];

  bool get isEmpty =>
      facts.isEmpty &&
      ingredients == null &&
      allergens.isEmpty &&
      nutrition.isEmpty &&
      usageInstructions == null &&
      storageInstructions == null &&
      warnings == null &&
      authenticityTips.isEmpty;
}

class NutritionRow {
  final String nutrient;
  final String? per100;
  final String? perServing;
  const NutritionRow({required this.nutrient, this.per100, this.perServing});
}

class SpecificationRow {
  final String label;
  final String value;
  const SpecificationRow({required this.label, required this.value});
}
