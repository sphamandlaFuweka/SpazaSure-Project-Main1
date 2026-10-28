import 'dart:async';

import 'package:flutter/material.dart';
import 'package:spazasure_app/core/widgets/motion.dart';
import 'package:spazasure_app/core/widgets/cart_toast.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/product_card.dart';
import 'package:spazasure_app/providers/product_provider.dart';
import 'package:spazasure_app/services/product_service.dart';
import 'package:spazasure_app/models/models.dart';

class MarketplaceScreen extends StatefulWidget {
  const MarketplaceScreen({super.key});

  @override
  State<MarketplaceScreen> createState() => _MarketplaceScreenState();
}

class _MarketplaceScreenState extends State<MarketplaceScreen> {
  final _searchController = TextEditingController();

  /// Waits for a pause in typing before searching
  Timer? _searchDebounce;

  /// Only the newest request may update the list; older, slower responses are ignored
  int _requestId = 0;
  String? _selectedCategory;
  String? _selectedSupplier;
  String _sortBy = 'popular';
  List<Product> _filteredProducts = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() => _loading = true);
    await _filterProducts();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final catId = ModalRoute.of(context)?.settings.arguments as String?;
    if (catId != null && _selectedCategory == null) {
      _selectedCategory = catId;
      _filterProducts();
    }
  }

  @override
  void dispose() {
    _searchDebounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _onSearchChanged() {
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 350), _filterProducts);
  }

  Future<void> _filterProducts() async {
    final requestId = ++_requestId;
    setState(() => _loading = true);
    try {
      final products = await ProductService.getAllProducts(
        search: _searchController.text.trim().isNotEmpty
            ? _searchController.text.trim()
            : null,
        categoryId: _selectedCategory,
        supplierId: _selectedSupplier,
        sort: _sortBy,
      );
      // A newer search started while this one was loading: its results win
      if (!mounted || requestId != _requestId) return;
      setState(() {
        // A transient empty server response must not wipe an already visible
        // catalogue. Empty is still shown normally on a first load or after
        // an explicit search/filter produces no matches.
        final hasFilter = _searchController.text.isNotEmpty ||
            _selectedCategory != null ||
            _selectedSupplier != null;
        if (products.isNotEmpty || _filteredProducts.isEmpty || hasFilter) {
          _filteredProducts = products;
        }
        _loading = false;
      });
    } catch (e) {
      if (mounted && requestId == _requestId) setState(() => _loading = false);
    }
  }

  Future<void> _onRefresh() async {
    // Pull-to-refresh: force fresh data from backend
    await context.read<ProductProvider>().refreshAll();
    await _filterProducts();
  }

  @override
  Widget build(BuildContext context) {
    final productProvider = context.watch<ProductProvider>();
    final categories = productProvider.categories;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Marketplace'),
        backgroundColor: AppColors.surface,
        actions: [
          IconButton(
            icon: const Icon(Icons.filter_list),
            onPressed: _showFilterSheet,
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _onRefresh,
        color: AppColors.primary,
        child: Column(
          children: [
            // Search
            Container(
              color: AppColors.surface,
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
              child: TextField(
                controller: _searchController,
                onChanged: (_) => _onSearchChanged(),
                decoration: InputDecoration(
                  hintText: 'Search products...',
                  prefixIcon: const Icon(
                    Icons.search,
                    color: AppColors.textHint,
                  ),
                  suffixIcon: _searchController.text.isNotEmpty
                      ? IconButton(
                          icon: const Icon(Icons.clear, size: 20),
                          onPressed: () {
                            _searchController.clear();
                            _filterProducts();
                          },
                        )
                      : null,
                ),
              ),
            ),
            // Category chips
            SizedBox(
              height: 50,
              child: ListView.builder(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 12),
                itemCount: categories.length + 1,
                itemBuilder: (context, index) {
                  if (index == 0) {
                    return Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      child: FilterChip(
                        label: const Text('All'),
                        selected: _selectedCategory == null,
                        onSelected: (_) {
                          _selectedCategory = null;
                          _filterProducts();
                        },
                        selectedColor: AppColors.primary.withValues(
                          alpha: 0.15,
                        ),
                        checkmarkColor: AppColors.primary,
                      ),
                    );
                  }
                  final cat = categories[index - 1];
                  return Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: FilterChip(
                      label: Text(cat.name),
                      selected: _selectedCategory == cat.id,
                      onSelected: (_) {
                        _selectedCategory = _selectedCategory == cat.id
                            ? null
                            : cat.id;
                        _filterProducts();
                      },
                      selectedColor: AppColors.primary.withValues(alpha: 0.15),
                      checkmarkColor: AppColors.primary,
                    ),
                  );
                },
              ),
            ),
            // An active supplier filter, so it can't silently hide products
            if (_selectedSupplier != null)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 4),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: InputChip(
                    avatar: const Icon(Icons.storefront_outlined, size: 18),
                    label: Text(
                      'Supplier: ${productProvider.suppliers.where((s) => s.id == _selectedSupplier).map((s) => s.companyName).firstOrNull ?? 'selected'}',
                    ),
                    onDeleted: () {
                      setState(() => _selectedSupplier = null);
                      _filterProducts();
                    },
                  ),
                ),
              ),
            // Results count & sort
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    '${_filteredProducts.length} products',
                    style: AppTextStyles.bodySmall,
                  ),
                  GestureDetector(
                    onTap: _showSortSheet,
                    child: Row(
                      children: [
                        const Icon(
                          Icons.sort,
                          size: 18,
                          color: AppColors.textSecondary,
                        ),
                        const SizedBox(width: 4),
                        Text(
                          'Sort',
                          style: AppTextStyles.bodySmall.copyWith(
                            color: AppColors.primary,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            // Products grid
            Expanded(
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : _filteredProducts.isEmpty
                  ? ListView(
                      // Wrap in ListView so RefreshIndicator works when empty
                      children: [
                        const SizedBox(height: 100),
                        Icon(
                          Icons.search_off,
                          size: 64,
                          color: AppColors.textHint,
                        ),
                        const SizedBox(height: 16),
                        Center(
                          child: Text(
                            'No products found',
                            style: AppTextStyles.subtitle,
                          ),
                        ),
                        Center(
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 32),
                            child: Text(
                              'Try adjusting your search or filters. Only suppliers in your province, close to your shop, are shown.',
                              style: AppTextStyles.bodySmall,
                              textAlign: TextAlign.center,
                            ),
                          ),
                        ),
                      ],
                    )
                  : GridView.builder(
                      padding: const EdgeInsets.all(12),
                      gridDelegate:
                          const SliverGridDelegateWithFixedCrossAxisCount(
                            crossAxisCount: 3,
                            mainAxisExtent: ProductCard.gridHeight,
                            crossAxisSpacing: 8,
                            mainAxisSpacing: 8,
                          ),
                      itemCount: _filteredProducts.length,
                      itemBuilder: (context, index) {
                        final product = _filteredProducts[index];
                        return Entrance(
                          index: index,
                          child: ProductCard(
                            product: product,
                            onTap: () => Navigator.pushNamed(
                              context,
                              '/product',
                              arguments: product,
                            ),
                            onAddToCart: () => CartToast.add(context, product, 1),
                          ),
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }

  void _showSortSheet() {
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) => Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Sort By', style: AppTextStyles.h3),
            const SizedBox(height: 16),
            ...['popular', 'price_low', 'price_high', 'rating'].map((opt) {
              final labels = {
                'popular': 'Most Popular',
                'price_low': 'Price: Low to High',
                'price_high': 'Price: High to Low',
                'rating': 'Highest Rated',
              };
              return ListTile(
                title: Text(labels[opt]!),
                trailing: _sortBy == opt
                    ? const Icon(Icons.check, color: AppColors.primary)
                    : null,
                onTap: () {
                  _sortBy = opt;
                  _filterProducts();
                  Navigator.pop(context);
                },
              );
            }),
          ],
        ),
      ),
    );
  }

  /// Category and supplier filters. Choices apply when the shop taps "Show products".
  void _showFilterSheet() {
    final productProvider = context.read<ProductProvider>();
    final categories = productProvider.categories;
    final suppliers = productProvider.suppliers;
    var category = _selectedCategory;
    var supplier = _selectedSupplier;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) => StatefulBuilder(
        builder: (sheetContext, setSheet) => DraggableScrollableSheet(
          initialChildSize: 0.6,
          minChildSize: 0.4,
          maxChildSize: 0.9,
          expand: false,
          builder: (_, scrollController) => Padding(
            padding: const EdgeInsets.fromLTRB(24, 24, 24, 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text('Filters', style: AppTextStyles.h3),
                    TextButton(
                      onPressed: () => setSheet(() {
                        category = null;
                        supplier = null;
                      }),
                      child: const Text('Clear all'),
                    ),
                  ],
                ),
                Expanded(
                  child: ListView(
                    controller: scrollController,
                    children: [
                      const SizedBox(height: 8),
                      Text('Categories', style: AppTextStyles.subtitle),
                      const SizedBox(height: 12),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          for (final cat in categories)
                            FilterChip(
                              label: Text(
                                cat.productCount > 0 ? '${cat.name} (${cat.productCount})' : cat.name,
                              ),
                              selected: category == cat.id,
                              onSelected: (_) => setSheet(
                                () => category = category == cat.id ? null : cat.id,
                              ),
                            ),
                        ],
                      ),
                      const SizedBox(height: 24),
                      Text('Suppliers', style: AppTextStyles.subtitle),
                      const SizedBox(height: 12),
                      if (suppliers.isEmpty)
                        Text('No suppliers near your shop yet.', style: AppTextStyles.caption)
                      else
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: [
                            for (final s in suppliers)
                              FilterChip(
                                label: Text(s.companyName),
                                selected: supplier == s.id,
                                onSelected: (_) => setSheet(
                                  () => supplier = supplier == s.id ? null : s.id,
                                ),
                              ),
                          ],
                        ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: () {
                      Navigator.pop(sheetContext);
                      setState(() {
                        _selectedCategory = category;
                        _selectedSupplier = supplier;
                      });
                      _filterProducts();
                    },
                    child: const Text('Show products'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
