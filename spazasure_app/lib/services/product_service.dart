import 'api_service.dart';
import 'package:spazasure_app/models/product_info.dart';
import '../models/models.dart';

/// Service to fetch products from the backend marketplace API.
class ProductService {
  /// Fetch products from backend marketplace.
  /// Supports search, category filter, supplier filter, and sorting.
  static Future<List<Product>> getProducts({
    int page = 1,
    int pageSize = 20,
    String? search,
    String? categoryId,
    String? supplierId,
    String sort = 'popular',
  }) async {
    try {
      final params = <String, String>{
        'page': page.toString(),
        'pageSize': pageSize.toString(),
        'sort': sort,
      };
      if (search != null && search.isNotEmpty) params['search'] = search;
      if (categoryId != null) params['categoryId'] = categoryId;
      if (supplierId != null) params['supplierId'] = supplierId;

      final query = params.entries.map((e) => '${e.key}=${e.value}').join('&');
      final res = await ApiService.get('/shop/marketplace/products?$query');
      final rawData = res['data'];
      final items = rawData is Map<String, dynamic>
          ? (rawData['items'] as List<dynamic>? ?? const [])
          : rawData is List<dynamic>
          ? rawData
          : const <dynamic>[];

      return items.map((i) => _mapProduct(i as Map<String, dynamic>)).toList();
    } catch (e) {
      // Rethrow so UI can show proper error
      rethrow;
    }
  }

  /// Fetch every matching product, page by page (the API returns at most 100 at a time).
  /// Only loading the first page made products beyond it disappear from the app.
  static Future<List<Product>> getAllProducts({
    String? search,
    String? categoryId,
    String? supplierId,
    String sort = 'popular',
    int maxPages = 20,
  }) async {
    const pageSize = 100;
    final all = <Product>[];
    final seen = <String>{};
    for (var page = 1; page <= maxPages; page++) {
      final batch = await getProducts(
        page: page,
        pageSize: pageSize,
        search: search,
        categoryId: categoryId,
        supplierId: supplierId,
        sort: sort,
      );
      // Guard against duplicates if the catalogue changes while paging
      all.addAll(batch.where((p) => seen.add(p.id)));
      if (batch.length < pageSize) break;
    }
    return all;
  }

  /// Fetch a single product by ID.
  static Future<Product> getProduct(String id) async {
    final res = await ApiService.get('/shop/marketplace/products/$id');
    return _mapProduct(res['data'] as Map<String, dynamic>);
  }

  /// The supplier's product page information: specifications, nutrition, ingredients,
  /// usage, storage, warnings and how to spot a fake.
  static Future<ProductInfo> getProductInfo(String id) async {
    final res = await ApiService.get('/shop/marketplace/products/$id');
    final data = res['data'];
    final info = data is Map ? data['info'] : null;
    return ProductInfo.fromJson(info is Map ? Map<String, dynamic>.from(info) : null);
  }

  /// Fetch all categories.
  static Future<List<Category>> getCategories() async {
    try {
      final res = await ApiService.get('/shop/marketplace/categories');
      final items = res['data'] as List<dynamic>;
      return items
          .map(
            (i) => Category(
              id: i['id'].toString(),
              name: i['name'] ?? '',
              iconName: _slugToIcon(i['slug'] ?? ''),
              parentId: i['parentId']?.toString(),
              productCount: i['productCount'] ?? 0,
            ),
          )
          .toList();
    } catch (e) {
      rethrow;
    }
  }

  /// Fetch all active suppliers.
  static Future<List<Supplier>> getSuppliers() async {
    try {
      final res = await ApiService.get('/shop/marketplace/suppliers');
      final items = res['data'] as List<dynamic>;
      return items
          .map(
            (i) => Supplier(
              id: i['id'].toString(),
              companyName: i['companyName'] ?? '',
              logoUrl: i['logoUrl'] ?? '',
              tier: i['tier'] ?? 'basic',
              productCount: i['productCount'] ?? 0,
              isVerified: true,
            ),
          )
          .toList();
    } catch (e) {
      rethrow;
    }
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  static Product _mapProduct(Map<String, dynamic> i) {
    // Parse images — stored as JSON string array in backend
    List<String> images = [];
    if (i['images'] != null) {
      if (i['images'] is List) {
        images = (i['images'] as List).map((e) => e.toString()).toList();
      } else if (i['images'] is String) {
        final raw = i['images'] as String;
        if (raw.startsWith('[') && raw.length > 2) {
          // Proper JSON parse for base64 data URIs
          try {
            final decoded = (raw.substring(1, raw.length - 1))
                .split('","')
                .map((s) => s.replaceAll('"', ''))
                .where((s) => s.trim().isNotEmpty)
                .toList();
            images = decoded;
          } catch (_) {}
        }
      }
    }

    return Product(
      id: i['id'].toString(),
      name: i['name'] ?? '',
      description: i['description'] ?? '',
      sku: i['sku'] ?? '',
      price: (i['price'] as num?)?.toDouble() ?? 0,
      imageUrl: images.isNotEmpty ? images.first : '',
      images: images,
      categoryId: i['categoryId']?.toString() ?? '',
      categoryName: i['categoryName'] ?? '',
      supplierId: i['supplierId']?.toString() ?? '',
      supplierName: i['supplierName'] ?? '',
      stockQuantity: (i['stockQty'] as num?)?.toInt() ?? 0,
      minOrderQty: (i['minOrderQty'] as num?)?.toInt() ?? 1,
      // No reviews means no rating — don't invent one
      rating: (i['rating'] as num?)?.toDouble() ?? 0,
      reviewCount: (i['reviewCount'] as num?)?.toInt() ?? 0,
      isAvailable: i['isAvailable'] ?? true,
      isNearby: i['isNearby'] ?? false,
      supplierCity: i['supplierCity'] as String?,
      supplierTier: (i['supplierTier'] as String?)?.toLowerCase(),
      supplierCommissionRate:
          (i['supplierCommissionRate'] as num?)?.toDouble() ?? 5,
    );
  }

  static String _slugToIcon(String slug) {
    final map = {
      'food-beverages': 'shopping_basket',
      'cooking-oil': 'shopping_basket',
      'maize-flour': 'shopping_basket',
      'canned-goods': 'shopping_basket',
      'beverages': 'local_drink',
      'snacks': 'cookie',
      'dairy-eggs': 'local_drink',
      'bread-bakery': 'bakery_dining',
      'spices-condiments': 'shopping_basket',
      'household-cleaning': 'home',
      'laundry': 'home',
      'dishwashing': 'home',
      'cleaning-supplies': 'home',
      'personal-care': 'spa',
      'tobacco-vaping': 'shopping_basket',
      'airtime-data': 'shopping_basket',
      'baby-infant': 'child_care',
      'meat-frozen': 'ac_unit',
      'fresh-produce': 'eco',
      'hot-drinks': 'coffee',
      'fuel-energy': 'local_fire_department',
    };
    return map[slug] ?? 'category';
  }
}
