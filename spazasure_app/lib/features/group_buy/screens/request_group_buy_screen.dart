import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/services/group_buy_service.dart';
import 'package:spazasure_app/services/platform_settings_service.dart';
import 'package:spazasure_app/services/product_service.dart';
import 'package:spazasure_app/services/profile_service.dart';

/// The items a shop must buy in a group buy, in total. The server checks this too.
const minItemsPerShop = 10;

/// What's missing from a group buy request, or null when it can be sent. The server checks it too.
String? requestProblem({
  required List<(Product, int)> lines,
  required int? shops,
  required DateTime? opensOn,
  required DateTime? closesOn,
  required String area,
  required int minShops,
  required double minSpend,
}) {
  if (lines.isEmpty) return 'Add at least one product.';
  final items = lines.fold(0, (sum, line) => sum + line.$2);
  if (items < minItemsPerShop) {
    return 'Each shop must buy not less than $minItemsPerShop items. '
        'Add ${minItemsPerShop - items} more.';
  }
  final spend = lines.fold(0.0, (sum, line) => sum + line.$1.price * line.$2);
  if (minSpend > 0 && spend < minSpend) {
    return 'Each shop must spend not less than R${minSpend.toStringAsFixed(0)}. '
        'Add R${(minSpend - spend).toStringAsFixed(2)} more.';
  }
  if (shops == null || shops < minShops) {
    return 'Enter the number of shops, not less than $minShops.';
  }
  if (opensOn == null || closesOn == null) {
    return 'Choose the opening and closing dates.';
  }
  if (!closesOn.isAfter(opensOn)) {
    return 'The closing date must be after the opening date.';
  }
  if (closesOn.difference(opensOn).inDays > 60) {
    return 'A group buy can run for 60 days at most.';
  }
  if (area.trim().isEmpty) return 'Tell us the area the deal is for.';
  return null;
}

/// The form a shop fills in to ask SpazaSure for a group buy: products from the marketplace,
/// how much each shop would buy, how many shops, when it runs and where. Closes with true once sent.
class RequestGroupBuyScreen extends StatefulWidget {
  const RequestGroupBuyScreen({super.key});

  @override
  State<RequestGroupBuyScreen> createState() => _RequestGroupBuyScreenState();
}

class _RequestGroupBuyScreenState extends State<RequestGroupBuyScreen> {
  final _lines = <_Line>[];
  final _shopsController = TextEditingController();
  final _areaController = TextEditingController();
  final _discountController = TextEditingController();
  final _noteController = TextEditingController();
  DateTime? _opensOn;
  DateTime? _closesOn;
  double _minSpend = PlatformSettings.defaults.minOrderAmount;
  int _minShops = PlatformSettings.defaults.groupBuyMinShops;
  bool _sending = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    PlatformSettingsService.fetch().then((settings) {
      if (mounted) {
        setState(() {
          _minSpend = settings.minOrderAmount;
          _minShops = settings.groupBuyMinShops;
        });
      }
    });
    // Starts from the shop's own area; the shop can change it
    ProfileService.getProfile()
        .then((profile) {
          if (!mounted || _areaController.text.isNotEmpty) return;
          setState(() {
            _areaController.text = [
              profile.city,
              profile.province,
            ].where((part) => part.isNotEmpty).join(', ');
          });
        })
        .catchError((_) {});
  }

  @override
  void dispose() {
    for (final line in _lines) {
      line.quantity.dispose();
    }
    _shopsController.dispose();
    _areaController.dispose();
    _discountController.dispose();
    _noteController.dispose();
    super.dispose();
  }

  int get _totalItems => _lines.fold(0, (sum, line) => sum + line.qty);

  double get _spend =>
      _lines.fold(0, (sum, line) => sum + line.product.price * line.qty);

  String? get _problem => requestProblem(
    lines: [for (final line in _lines) (line.product, line.qty)],
    shops: int.tryParse(_shopsController.text),
    opensOn: _opensOn,
    closesOn: _closesOn,
    area: _areaController.text,
    minShops: _minShops,
    minSpend: _minSpend,
  );

  Future<void> _addProduct() async {
    final product = await showModalBottomSheet<Product>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      backgroundColor: AppColors.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (_) => _ProductPicker(
        // A deal is with one supplier, so the first product decides it
        supplierId: _lines.isEmpty ? null : _lines.first.product.supplierId,
        chosen: {for (final line in _lines) line.product.id},
      ),
    );
    if (product == null || !mounted) return;
    setState(() {
      _lines.add(
        _Line(
          product,
          // The first product starts at the minimum, so the form opens on a valid amount
          TextEditingController(text: _lines.isEmpty ? '$minItemsPerShop' : ''),
        ),
      );
    });
  }

  void _removeLine(_Line line) => setState(() {
    _lines.remove(line);
    line.quantity.dispose();
  });

  Future<void> _pickDate({required bool opening}) async {
    final today = DateUtils.dateOnly(DateTime.now());
    final first = opening ? today : (_opensOn ?? today).add(_oneDay);
    final current = opening ? _opensOn : _closesOn;
    final picked = await showDatePicker(
      context: context,
      initialDate: current != null && !current.isBefore(first)
          ? current
          : first,
      firstDate: first,
      lastDate: today.add(const Duration(days: 365)),
    );
    if (picked == null) return;
    setState(() {
      if (opening) {
        _opensOn = picked;
        if (_closesOn != null && !_closesOn!.isAfter(picked)) _closesOn = null;
      } else {
        _closesOn = picked;
      }
    });
  }

  static const _oneDay = Duration(days: 1);

  Future<void> _submit() async {
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      await GroupBuyService.createRequest(
        items: [
          for (final line in _lines)
            {'productId': line.product.id, 'quantity': line.qty},
        ],
        shopsWanted: int.parse(_shopsController.text),
        opensOn: _opensOn!,
        closesOn: _closesOn!,
        area: _areaController.text.trim(),
        targetDiscountPct: int.tryParse(_discountController.text),
        note: _noteController.text.trim().isEmpty
            ? null
            : _noteController.text.trim(),
      );
      if (mounted) Navigator.pop(context, true);
    } catch (error) {
      if (mounted) {
        setState(() {
          _sending = false;
          _error = error.toString();
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final problem = _error ?? _problem;
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(title: const Text('Request a group buy')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          _section(
            'Products',
            'Choose from the marketplace. A group buy is with one supplier.',
            [
              for (final line in _lines) _lineRow(line),
              OutlinedButton.icon(
                onPressed: _addProduct,
                icon: const Icon(Icons.add_rounded),
                label: const Text('Add a product'),
              ),
            ],
          ),
          const SizedBox(height: 12),
          _section(
            'How many per shop',
            'Not less than $minItemsPerShop items per shop and '
                'R${_minSpend.toStringAsFixed(0)} per shop. You can mix products.',
            [
              Text(
                '$_totalItems items per shop, about '
                'R${_spend.toStringAsFixed(2)}',
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w700),
              ),
            ],
          ),
          const SizedBox(height: 12),
          _section('Number of shops', 'Not less than $_minShops shops.', [
            TextField(
              controller: _shopsController,
              keyboardType: TextInputType.number,
              maxLength: 4,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              onChanged: (_) => setState(() {}),
              decoration: const InputDecoration(
                labelText: 'How many shops should join?',
                counterText: '',
              ),
            ),
          ]),
          const SizedBox(height: 12),
          _section(
            'When it runs',
            'When the deal should open and close. Up to 60 days.',
            [
              _dateRow('Opening date', _opensOn, opening: true),
              const SizedBox(height: 8),
              _dateRow('Closing date', _closesOn, opening: false),
            ],
          ),
          const SizedBox(height: 12),
          _section('More details', 'This helps us arrange the deal.', [
            TextField(
              controller: _areaController,
              maxLength: 120,
              textCapitalization: TextCapitalization.words,
              onChanged: (_) => setState(() {}),
              decoration: const InputDecoration(
                labelText: 'Area for the deal',
                hintText: 'e.g. Soweto, Gauteng',
                counterText: '',
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _discountController,
              keyboardType: TextInputType.number,
              maxLength: 2,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: const InputDecoration(
                labelText: 'Discount you hope for (optional)',
                suffixText: '%',
                counterText: '',
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _noteController,
              maxLength: 500,
              minLines: 2,
              maxLines: 4,
              textCapitalization: TextCapitalization.sentences,
              decoration: const InputDecoration(
                labelText: 'Anything else? (optional)',
                counterText: '',
              ),
            ),
          ]),
          if (problem != null) ...[
            const SizedBox(height: 12),
            Text(
              problem,
              style: AppTextStyles.bodySmall.copyWith(
                color: _error != null
                    ? AppColors.error
                    : AppColors.textSecondary,
              ),
            ),
          ],
          const SizedBox(height: 16),
          ElevatedButton(
            onPressed: _sending || _problem != null ? null : _submit,
            child: Text(_sending ? 'Sending...' : 'Send request'),
          ),
        ],
      ),
    );
  }

  Widget _section(String title, String hint, List<Widget> children) =>
      Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.divider),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(title, style: AppTextStyles.subtitle),
            const SizedBox(height: 2),
            Text(hint, style: AppTextStyles.caption),
            const SizedBox(height: 12),
            ...children,
          ],
        ),
      );

  Widget _lineRow(_Line line) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                line.product.name,
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
              ),
              Text(
                '${line.product.supplierName} • '
                'R${line.product.price.toStringAsFixed(2)}',
                style: AppTextStyles.caption,
              ),
            ],
          ),
        ),
        const SizedBox(width: 8),
        SizedBox(
          width: 72,
          child: TextField(
            controller: line.quantity,
            keyboardType: TextInputType.number,
            textAlign: TextAlign.center,
            maxLength: 6,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            onChanged: (_) => setState(() {}),
            decoration: const InputDecoration(
              labelText: 'Qty',
              counterText: '',
              isDense: true,
            ),
          ),
        ),
        IconButton(
          tooltip: 'Remove ${line.product.name}',
          onPressed: () => _removeLine(line),
          icon: const Icon(Icons.close_rounded),
        ),
      ],
    ),
  );

  Widget _dateRow(String label, DateTime? date, {required bool opening}) =>
      OutlinedButton.icon(
        onPressed: () => _pickDate(opening: opening),
        icon: const Icon(Icons.calendar_today_outlined, size: 18),
        label: Text(
          date == null
              ? label
              : '$label: ${DateFormat('d MMM yyyy').format(date)}',
        ),
      );
}

class _Line {
  final Product product;
  final TextEditingController quantity;

  _Line(this.product, this.quantity);

  int get qty => int.tryParse(quantity.text) ?? 0;
}

/// Searches the marketplace for a product to add to the request.
class _ProductPicker extends StatefulWidget {
  final String? supplierId;
  final Set<String> chosen;

  const _ProductPicker({required this.supplierId, required this.chosen});

  @override
  State<_ProductPicker> createState() => _ProductPickerState();
}

class _ProductPickerState extends State<_ProductPicker> {
  List<Product> _products = [];
  bool _loading = true;
  String? _error;
  Timer? _debounce;
  String _search = '';

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    final search = _search;
    setState(() => _loading = true);
    try {
      final products = await ProductService.getProducts(
        pageSize: 30,
        search: search,
        supplierId: widget.supplierId,
      );
      // A newer search started while this one was loading: its results win
      if (!mounted || search != _search) return;
      setState(() {
        _products = products
            .where((product) => !widget.chosen.contains(product.id))
            .toList();
        _error = null;
        _loading = false;
      });
    } catch (error) {
      if (mounted && search == _search) {
        setState(() {
          _error = error.toString();
          _loading = false;
        });
      }
    }
  }

  void _changed(String value) {
    _search = value.trim();
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), _load);
  }

  @override
  Widget build(BuildContext context) => Padding(
    padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
    child: SizedBox(
      height: MediaQuery.of(context).size.height * 0.75,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
            child: TextField(
              autofocus: true,
              onChanged: _changed,
              decoration: InputDecoration(
                prefixIcon: const Icon(Icons.search),
                hintText: widget.supplierId == null
                    ? 'Search products'
                    : 'Search this supplier\'s products',
              ),
            ),
          ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _error != null || _products.isEmpty
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(
                        _error ?? 'No products found',
                        textAlign: TextAlign.center,
                        style: AppTextStyles.bodySmall,
                      ),
                    ),
                  )
                : ListView.separated(
                    itemCount: _products.length,
                    separatorBuilder: (_, __) => const Divider(height: 1),
                    itemBuilder: (_, index) {
                      final product = _products[index];
                      return ListTile(
                        title: Text(product.name),
                        subtitle: Text(
                          '${product.supplierName} • '
                          'R${product.price.toStringAsFixed(2)}',
                        ),
                        trailing: const Icon(Icons.add_rounded),
                        onTap: () => Navigator.pop(context, product),
                      );
                    },
                  ),
          ),
        ],
      ),
    ),
  );
}
