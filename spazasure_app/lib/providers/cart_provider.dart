import 'package:flutter/material.dart';
import '../models/models.dart';

class CartProvider extends ChangeNotifier {
  final List<CartItem> _items = [];

  List<CartItem> get items => List.unmodifiable(_items);
  int get count => _items.fold(0, (s, i) => s + i.quantity);
  bool get isEmpty => _items.isEmpty;

  double get subtotal => _items.fold(0, (s, i) => s + i.total);

  double deliveryFee(String option) {
    final perSupplier = switch (option) {
      'express' => 250.0,
      'pickup' => 0.0,
      _ => 150.0,
    };
    return perSupplier * _items.map((item) => item.product.supplierId).toSet().length;
  }

  double platformFee() {
    final subtotals = <String, double>{};
    final rates = <String, double>{};
    for (final item in _items) {
      final supplierId = item.product.supplierId;
      subtotals.update(
        supplierId,
        (value) => value + item.total,
        ifAbsent: () => item.total,
      );
      rates[supplierId] = item.product.supplierCommissionRate;
    }
    return subtotals.entries.fold(0, (sum, entry) {
      final fee = entry.value * (rates[entry.key] ?? 5) / 100;
      return sum + double.parse(fee.toStringAsFixed(2));
    });
  }

  double total(String option) {
    final sub = subtotal;
    return sub + deliveryFee(option) + platformFee();
  }

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
