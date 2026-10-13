import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/providers/cart_provider.dart';

Product _product({int minimum = 10, int stock = 30}) => Product(
  id: 'product-1',
  name: 'Test product',
  description: '',
  sku: 'TEST-1',
  price: 12.5,
  imageUrl: '',
  categoryId: 'category-1',
  categoryName: 'Test',
  supplierId: 'supplier-1',
  supplierName: 'Test supplier',
  stockQuantity: stock,
  minOrderQty: minimum,
);

void main() {
  group('CartProvider quantities', () {
    test('allows quantities below the supplier minimum', () {
      final cart = CartProvider();
      final product = _product();

      expect(cart.add(product, 1), isTrue);
      expect(cart.items.single.quantity, 1);

      cart.updateQuantity(product.id, 4);
      expect(cart.items.single.quantity, 4);
    });

    test('prevents adding more than available stock', () {
      final cart = CartProvider();
      final product = _product(stock: 5);

      expect(cart.add(product, 4), isTrue);
      expect(cart.add(product, 2), isFalse);
      expect(cart.items.single.quantity, 4);
    });

    test('caps edited cart quantities at available stock', () {
      final cart = CartProvider();
      final product = _product(stock: 5);

      expect(cart.add(product, 1), isTrue);
      cart.updateQuantity(product.id, 8);

      expect(cart.items.single.quantity, 5);
    });
  });
}
