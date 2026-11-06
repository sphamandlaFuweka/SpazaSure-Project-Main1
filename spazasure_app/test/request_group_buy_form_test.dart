import 'package:flutter_test/flutter_test.dart';
import 'package:spazasure_app/features/group_buy/screens/request_group_buy_screen.dart';
import 'package:spazasure_app/models/models.dart';

Product _product(double price) => Product(
  id: 'product-1',
  name: 'Maize meal 10kg',
  description: '',
  sku: 'MM10',
  price: price,
  imageUrl: '',
  categoryId: 'category-1',
  categoryName: 'Staples',
  supplierId: 'supplier-1',
  supplierName: 'Bulk Co',
);

String? _problem({
  List<(Product, int)>? lines,
  int? shops = 3,
  DateTime? opensOn,
  DateTime? closesOn,
  String area = 'Soweto',
}) => requestProblem(
  lines: lines ?? [(_product(200), 10)],
  shops: shops,
  opensOn: opensOn ?? DateTime(2026, 12, 1),
  closesOn: closesOn ?? DateTime(2026, 12, 15),
  area: area,
  minShops: 3,
  minSpend: 2000,
);

void main() {
  test('a complete request has no problem', () {
    expect(_problem(), isNull);
  });

  test('needs a product, 10 items and R2,000 per shop', () {
    expect(_problem(lines: []), contains('at least one product'));
    expect(_problem(lines: [(_product(500), 9)]), contains('not less than 10'));
    expect(
      _problem(lines: [(_product(100), 10)]),
      contains('not less than R2000'),
    );
  });

  test('needs the number of shops', () {
    expect(_problem(shops: null), contains('number of shops'));
    expect(_problem(shops: 2), contains('not less than 3'));
  });

  test('closing must come after opening, within 60 days', () {
    expect(
      _problem(closesOn: DateTime(2026, 12, 1)),
      contains('after the opening date'),
    );
    expect(_problem(closesOn: DateTime(2027, 3, 1)), contains('60 days'));
  });

  test('needs an area', () {
    expect(_problem(area: '  '), contains('area'));
  });
}
