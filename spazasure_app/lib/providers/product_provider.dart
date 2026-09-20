import 'package:flutter/material.dart';
import '../services/product_service.dart';
import '../models/models.dart';

/// Central product state provider that keeps the last successful catalogue
/// visible until the user explicitly asks to refresh it.
class ProductProvider extends ChangeNotifier {
  List<Product> _products = [];
  List<Product> _homeProducts = [];
  List<Category> _categories = [];
  List<Supplier> _suppliers = [];
  bool _loading = false;
  String? _error;

  List<Product> get products => _products;
  List<Product> get homeProducts => _homeProducts;
  List<Category> get categories => _categories;
  List<Supplier> get suppliers => _suppliers;
  bool get loading => _loading;
  String? get error => _error;

  /// Refresh all product data from the backend
  Future<void> refreshAll() async {
    // Don't run concurrent refreshes
    if (_loading) return;

    try {
      _error = null;
      // Products are the primary content. Optional filters and supplier
      // metadata must not make the catalog disappear when one endpoint fails.
      final products = await ProductService.getProducts(pageSize: 50);
      _products = products;
      _homeProducts = products.take(10).toList();

      try {
        _categories = await ProductService.getCategories();
      } catch (_) {
        _categories = [];
      }
      try {
        _suppliers = await ProductService.getSuppliers();
      } catch (_) {
        _suppliers = [];
      }
      _error = null;
      notifyListeners();
    } catch (e) {
      _error = 'Unable to load data. Pull down to refresh.';
      notifyListeners();
    }
  }

  /// Initial load — shows loading indicator
  Future<void> loadInitial() async {
    if (_homeProducts.isNotEmpty) return; // Already loaded
    _loading = true;
    notifyListeners();

    await refreshAll();

    _loading = false;
    notifyListeners();
  }

  /// Search/filter products from the backend (used by marketplace)
  Future<List<Product>> searchProducts({
    String? search,
    String? categoryId,
    String? supplierId,
    String sort = 'popular',
    int pageSize = 50,
  }) async {
    try {
      final products = await ProductService.getProducts(
        search: search,
        categoryId: categoryId,
        supplierId: supplierId,
        sort: sort,
        pageSize: pageSize,
      );
      return products;
    } catch (e) {
      rethrow;
    }
  }
}
