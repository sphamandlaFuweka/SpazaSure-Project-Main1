import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/platform_settings_service.dart';

class CartProvider extends ChangeNotifier {
  final List<CartItem> _items = [];
  PlatformSettings _settings = PlatformSettings.defaults;

  PlatformSettings get settings => _settings;

  /// Use the admin-configured fees so the cart total matches the server's charge.
  void applySettings(PlatformSettings settings) {
    _settings = settings;
    notifyListeners();
  }

  List<CartItem> get items => List.unmodifiable(_items);
  int get count => _items.fold(0, (s, i) => s + i.quantity);
  bool get isEmpty => _items.isEmpty;

  double get subtotal => _items.fold(0, (s, i) => s + i.total);

  double deliveryFee(String option) {
    final perSupplier = switch (option) {
      'express' => _settings.expressDeliveryFee,
      'pickup' => 0.0,
      _ => _settings.standardDeliveryFee,
    };
    return perSupplier * _items.map((item) => item.product.supplierId).toSet().length;
  }

  // Product prices from the server already include SpazaSure's fee, so the total is
  // just the items plus delivery: shops never see a separate fee.
  double total(String option) => subtotal + deliveryFee(option);

  bool add(Product product, int quantity) {
    if (quantity < 1 || product.stockQuantity < quantity) return false;

    final idx = _items.indexWhere((i) => i.product.id == product.id);
    if (idx >= 0) {
      if (_items[idx].quantity + quantity > product.stockQuantity) {
        return false;
      }
      _items[idx].quantity += quantity;
    } else {
      _items.add(CartItem(product: product, quantity: quantity));
    }
    notifyListeners();
    return true;
  }

  void updateQuantity(String productId, int quantity) {
    final idx = _items.indexWhere((i) => i.product.id == productId);
    if (idx < 0) return;
    if (quantity <= 0) {
      _items.removeAt(idx);
    } else {
      final stock = _items[idx].product.stockQuantity;
      if (stock < 1) {
        _items.removeAt(idx);
      } else {
        _items[idx].quantity = quantity > stock ? stock : quantity;
      }
    }
    notifyListeners();
  }

  void remove(String productId) {
    _items.removeWhere((i) => i.product.id == productId);
    notifyListeners();
  }

  void clear() {
    _items.clear();
    notifyListeners();
  }
}
