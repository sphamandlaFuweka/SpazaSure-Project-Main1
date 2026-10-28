import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/academy/widgets/academy_widgets.dart';
import 'package:spazasure_app/services/shop_academy_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// The stock book: every product on the shop's shelves, with low-stock and expiry warnings.
class StockTab extends StatefulWidget {
  const StockTab({super.key});

  @override
  State<StockTab> createState() => _StockTabState();
}

class _StockTabState extends State<StockTab>
    with AutomaticKeepAliveClientMixin {
  bool _loading = true;
  bool _failed = false;
  Map<String, dynamic> _summary = {};
  List<Map<String, dynamic>> _items = [];
  String _filter = 'all';
  String _search = '';

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _failed = false;
    });
    try {
      final data = await ShopAcademyService.stock();
      if (!mounted) return;
      setState(() {
        _summary = (data['summary'] as Map?)?.cast<String, dynamic>() ?? {};
        _items = ((data['items'] as List?) ?? []).cast<Map<String, dynamic>>();
      });
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  List<Map<String, dynamic>> get _visible => _items.where((i) {
    if (_filter == 'low' && i['isLow'] != true) return false;
    if (_filter == 'expiring' && i['expiresSoon'] != true) return false;
    if (_search.isEmpty) return true;
    final q = _search.toLowerCase();
    return '${i['name']} ${i['category'] ?? ''} ${i['barcode'] ?? ''}'
        .toLowerCase()
        .contains(q);
  }).toList();

  Future<void> _importOrders() async {
    try {
      final message = await ShopAcademyService.importOrders();
      if (!mounted) return;
      showAcademyMessage(context, message);
      _load();
    } catch (e) {
      if (mounted) showAcademyError(context, e);
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_failed) return AcademyLoadError(onRetry: _load);

    final visible = _visible;
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.only(top: 12, bottom: 100),
        children: [
          AcademyCard(
            child: Column(
              children: [
                Row(
                  children: [
                    Expanded(
                      child: AcademyStat(
                        ShopAcademyService.rand(_summary['valueAtCost']),
                        'Stock value (cost)',
                      ),
                    ),
                    Expanded(
                      child: AcademyStat(
                        ShopAcademyService.rand(_summary['valueAtSelling']),
                        'At selling price',
                        color: AppColors.success,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: AcademyStat(
                        '${_summary['items'] ?? 0}',
                        'Products',
                      ),
                    ),
                    Expanded(
                      child: AcademyStat(
                        '${_summary['lowStock'] ?? 0}',
                        'Running low',
                        color: AppColors.red,
                      ),
                    ),
                    Expanded(
                      child: AcademyStat(
                        '${_summary['expiringSoon'] ?? 0}',
                        'Expiring soon',
                        color: AppColors.accentDark,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
            child: Row(
              children: [
                Expanded(
                  child: FilledButton.icon(
                    onPressed: () => _openItemForm(),
                    icon: const Icon(Icons.add_rounded),
                    label: const Text('Add product'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: _importOrders,
                    icon: const Icon(Icons.local_shipping_outlined, size: 18),
                    label: const Text(
                      'Book in deliveries',
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: TextField(
              decoration: academyInput(
                'Search products',
              ).copyWith(prefixIcon: const Icon(Icons.search_rounded)),
              onChanged: (v) => setState(() => _search = v.trim()),
            ),
          ),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: Row(
              children: [
                for (final f in const [
                  ('all', 'All'),
                  ('low', 'Running low'),
                  ('expiring', 'Expiring soon'),
                ])
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(f.$2),
                      selected: _filter == f.$1,
                      onSelected: (_) => setState(() => _filter = f.$1),
                    ),
                  ),
              ],
            ),
          ),
          if (_items.isEmpty)
            _emptyState()
          else if (visible.isEmpty)
            Padding(
              padding: const EdgeInsets.all(32),
              child: Text(
                'No products match.',
                style: AppTextStyles.body,
                textAlign: TextAlign.center,
              ),
            )
          else
            ...visible.map(_itemTile),
        ],
      ),
    );
  }

  Widget _emptyState() => AcademyCard(
    child: Column(
      children: [
        const Icon(
          Icons.inventory_2_outlined,
          size: 44,
          color: AppColors.primary,
        ),
        const SizedBox(height: 10),
        Text(
          'Start your stock book',
          style: AppTextStyles.subtitle.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 6),
        Text(
          'Tap "Book in deliveries" to add everything you bought on SpazaSure, then add the products you buy elsewhere.',
          style: AppTextStyles.bodySmall,
          textAlign: TextAlign.center,
        ),
      ],
    ),
  );

  Widget _itemTile(Map<String, dynamic> item) {
    final low = item['isLow'] == true;
    final expiring = item['expiresSoon'] == true;
    final markup = (item['markupPct'] as num?)?.toDouble();
    return AcademyCard(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: InkWell(
        onTap: () => _openItem(item),
        child: Row(
          children: [
            Container(
              width: 46,
              height: 46,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: low ? AppColors.redTint : AppColors.primaryTint,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Text(
                '${item['quantity']}',
                style: AppTextStyles.subtitle.copyWith(
                  fontWeight: FontWeight.w800,
                  color: low ? AppColors.red : AppColors.primary,
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item['name'] ?? '',
                    style: AppTextStyles.body.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'Cost ${ShopAcademyService.rand(item['costPrice'])} · Sell ${ShopAcademyService.rand(item['sellingPrice'])}'
                    '${markup != null ? ' · ${markup.toStringAsFixed(0)}% markup' : ''}',
                    style: AppTextStyles.caption.copyWith(
                      color: AppColors.textHint,
                    ),
                  ),
                  if (low || expiring)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Wrap(
                        spacing: 6,
                        children: [
                          if (low)
                            _tag(
                              'Reorder (min ${item['reorderLevel']})',
                              AppColors.red,
                            ),
                          if (expiring)
                            _tag(
                              'Expires ${_date(item['expiryDate'])}',
                              AppColors.accentDark,
                            ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right_rounded, color: AppColors.textHint),
          ],
        ),
      ),
    );
  }

  Widget _tag(String text, Color color) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.12),
      borderRadius: BorderRadius.circular(20),
    ),
    child: Text(
      text,
      style: AppTextStyles.caption.copyWith(
        color: color,
        fontWeight: FontWeight.w700,
        fontSize: 11,
      ),
    ),
  );

  static String _date(dynamic iso) {
    final d = DateTime.tryParse('${iso ?? ''}');
    return d == null ? '' : DateFormat('d MMM yyyy').format(d);
  }

  // ── Item actions ──

  Future<void> _openItem(Map<String, dynamic> item) async {
    final action = await showModalBottomSheet<String>(
      context: context,
      showDragHandle: true,
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(item['name'] ?? '', style: AppTextStyles.h3),
              const SizedBox(height: 4),
              Text(
                '${item['quantity']} ${item['unit'] ?? 'unit'}(s) on hand · sold ${item['soldLast30Days'] ?? 0} in the last 30 days',
                style: AppTextStyles.bodySmall,
              ),
              const SizedBox(height: 16),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  _actionChip(
                    ctx,
                    'received',
                    Icons.add_box_outlined,
                    'Stock received',
                  ),
                  _actionChip(ctx, 'sold', Icons.point_of_sale_rounded, 'Sold'),
                  _actionChip(
                    ctx,
                    'count',
                    Icons.fact_check_outlined,
                    'Stock count',
                  ),
                  _actionChip(
                    ctx,
                    'loss',
                    Icons.remove_circle_outline,
                    'Loss / own use',
                  ),
                  _actionChip(ctx, 'history', Icons.history_rounded, 'History'),
                  _actionChip(ctx, 'edit', Icons.edit_outlined, 'Edit'),
                  _actionChip(ctx, 'remove', Icons.delete_outline, 'Remove'),
                ],
              ),
            ],
          ),
        ),
      ),
    );
    if (!mounted || action == null) return;
    switch (action) {
      case 'edit':
        _openItemForm(item: item);
      case 'history':
        _showHistory(item);
      case 'remove':
        _remove(item);
      default:
        _recordMovement(item, action);
    }
  }

  Widget _actionChip(
    BuildContext ctx,
    String value,
    IconData icon,
    String label,
  ) => ActionChip(
    avatar: Icon(
      icon,
      size: 18,
      color: value == 'remove' ? AppColors.red : AppColors.primary,
    ),
    label: Text(label),
    onPressed: () => Navigator.pop(ctx, value),
  );

  Future<void> _recordMovement(Map<String, dynamic> item, String action) async {
    var type = action == 'loss' ? 'spoiled' : action;
    final qty = TextEditingController();
    final price = TextEditingController(
      text: action == 'received'
          ? '${item['costPrice'] ?? ''}'
          : action == 'sold'
          ? '${item['sellingPrice'] ?? ''}'
          : '',
    );
    final note = TextEditingController();
    var addToCashBook = action == 'received';

    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setSheet) => Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            0,
            20,
            MediaQuery.of(ctx).viewInsets.bottom + 20,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                '${ShopAcademyService.movementLabels[type] ?? type}: ${item['name']}',
                style: AppTextStyles.h3,
              ),
              const SizedBox(height: 6),
              if (type == 'count')
                Text(
                  'Count what is on the shelf. Your stock book says ${item['quantity']}; any difference is recorded.',
                  style: AppTextStyles.bodySmall,
                ),
              if (action == 'loss') ...[
                const SizedBox(height: 6),
                SegmentedButton<String>(
                  segments: const [
                    ButtonSegment(value: 'spoiled', label: Text('Expired')),
                    ButtonSegment(value: 'stolen', label: Text('Missing')),
                    ButtonSegment(value: 'own_use', label: Text('Own use')),
                  ],
                  selected: {type},
                  onSelectionChanged: (s) => setSheet(() => type = s.first),
                ),
              ],
              const SizedBox(height: 12),
              TextField(
                controller: qty,
                autofocus: true,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                decoration: academyInput(
                  type == 'count' ? 'Quantity counted' : 'Quantity',
                ),
              ),
              if (type == 'received' || type == 'sold') ...[
                const SizedBox(height: 10),
                TextField(
                  controller: price,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: academyInput(
                    type == 'received' ? 'Cost per unit' : 'Price per unit',
                    prefix: 'R ',
                  ),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  value: addToCashBook,
                  onChanged: (v) => setSheet(() => addToCashBook = v),
                  title: Text(
                    type == 'received'
                        ? 'Add the cost to my cash book'
                        : 'Add the takings to my cash book',
                  ),
                ),
              ],
              const SizedBox(height: 10),
              TextField(
                controller: note,
                decoration: academyInput('Note (optional)'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => Navigator.pop(ctx, true),
                child: const Text('Save'),
              ),
            ],
          ),
        ),
      ),
    );
    if (saved != true || !mounted) return;
    final quantity = int.tryParse(qty.text);
    if (quantity == null) {
      showAcademyError(context, 'none', 'Enter a quantity.');
      return;
    }
    try {
      final message = await ShopAcademyService.addMovement(
        item['id'],
        type: type,
        quantity: quantity,
        unitCost: type == 'received' ? parseAmount(price.text) : null,
        unitPrice: type == 'sold' ? parseAmount(price.text) : null,
        note: note.text,
        addToCashBook: addToCashBook && (type == 'received' || type == 'sold'),
      );
      if (!mounted) return;
      showAcademyMessage(context, message);
      _load();
    } catch (e) {
      if (mounted) showAcademyError(context, e);
    }
  }

  Future<void> _showHistory(Map<String, dynamic> item) async {
    List<Map<String, dynamic>> moves;
    try {
      moves = await ShopAcademyService.movements(item['id']);
    } catch (e) {
      if (mounted) showAcademyError(context, e);
      return;
    }
    if (!mounted) return;
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (ctx) => DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.6,
        builder: (ctx, scroll) => ListView(
          controller: scroll,
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
          children: [
            Text('History: ${item['name']}', style: AppTextStyles.h3),
            const SizedBox(height: 8),
            if (moves.isEmpty)
              Text('Nothing recorded yet.', style: AppTextStyles.bodySmall),
            for (final m in moves)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(
                  ShopAcademyService.movementLabels[m['type']] ??
                      '${m['type']}',
                ),
                subtitle: Text(
                  '${_date(m['occurredOn'])}${m['note'] != null ? ' · ${m['note']}' : ''}',
                ),
                trailing: Text(
                  '${(m['quantity'] as num) > 0 ? '+' : ''}${m['quantity']}',
                  style: AppTextStyles.subtitle.copyWith(
                    fontWeight: FontWeight.w800,
                    color: (m['quantity'] as num) > 0
                        ? AppColors.success
                        : AppColors.red,
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Future<void> _remove(Map<String, dynamic> item) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Remove product?'),
        content: Text(
          '${item['name']} will be removed from your stock book. Past records stay in your reports.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Remove'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await ShopAcademyService.removeItem(item['id']);
      _load();
    } catch (e) {
      if (mounted) showAcademyError(context, e);
    }
  }

  Future<void> _openItemForm({Map<String, dynamic>? item}) async {
    final saved = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => StockItemForm(item: item)),
    );
    if (saved == true) _load();
  }
}

/// Add or edit a product in the stock book.
class StockItemForm extends StatefulWidget {
  final Map<String, dynamic>? item;

  const StockItemForm({super.key, this.item});

  @override
  State<StockItemForm> createState() => _StockItemFormState();
}

class _StockItemFormState extends State<StockItemForm> {
  late final _name = TextEditingController(text: widget.item?['name'] ?? '');
  late final _category = TextEditingController(
    text: widget.item?['category'] ?? '',
  );
  late final _unit = TextEditingController(
    text: widget.item?['unit'] ?? 'unit',
  );
  late final _barcode = TextEditingController(
    text: widget.item?['barcode'] ?? '',
  );
  late final _cost = TextEditingController(
    text: _num(widget.item?['costPrice']),
  );
  late final _price = TextEditingController(
    text: _num(widget.item?['sellingPrice']),
  );
  late final _qty = TextEditingController(
    text: '${widget.item?['quantity'] ?? ''}',
  );
  late final _reorder = TextEditingController(
    text: '${widget.item?['reorderLevel'] ?? 5}',
  );
  late DateTime? _expiry = DateTime.tryParse(
    '${widget.item?['expiryDate'] ?? ''}',
  );
  bool _saving = false;

  bool get _editing => widget.item != null;

  static String _num(dynamic v) =>
      v == null ? '' : (v as num).toStringAsFixed(2);

  String get _markup {
    final cost = parseAmount(_cost.text);
    final price = parseAmount(_price.text);
    if (cost == null || price == null || cost <= 0) return '';
    return 'Markup ${((price - cost) / cost * 100).toStringAsFixed(0)}% · profit ${ShopAcademyService.rand(price - cost)} per unit';
  }

  Future<void> _save() async {
    final cost = parseAmount(_cost.text) ?? 0;
    final price = parseAmount(_price.text) ?? 0;
    if (_name.text.trim().isEmpty) {
      showAcademyError(context, 'none', 'Enter the product name.');
      return;
    }
    final body = {
      'name': _name.text.trim(),
      'category': _category.text.trim(),
      'unit': _unit.text.trim(),
      'barcode': _barcode.text.trim(),
      'costPrice': cost,
      'sellingPrice': price,
      'quantity': int.tryParse(_qty.text) ?? 0,
      'reorderLevel': int.tryParse(_reorder.text) ?? 0,
      'expiryDate': _expiry == null ? null : ShopAcademyService.day(_expiry!),
    };
    setState(() => _saving = true);
    try {
      if (_editing) {
        await ShopAcademyService.updateItem(widget.item!['id'], body);
      } else {
        await ShopAcademyService.addItem(body);
      }
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) showAcademyError(context, e);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: BrandAppBar(
        title: Text(_editing ? 'Edit product' : 'Add product'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          TextField(
            controller: _name,
            decoration: academyInput(
              'Product name',
              hint: 'e.g. White bread 700g',
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _category,
                  decoration: academyInput('Category', hint: 'e.g. Bread'),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: TextField(
                  controller: _unit,
                  decoration: academyInput('Unit', hint: 'loaf, can, pack'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _cost,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: academyInput('Cost price', prefix: 'R '),
                  onChanged: (_) => setState(() {}),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: TextField(
                  controller: _price,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: academyInput('Selling price', prefix: 'R '),
                  onChanged: (_) => setState(() {}),
                ),
              ),
            ],
          ),
          if (_markup.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 6, left: 4),
              child: Text(
                _markup,
                style: AppTextStyles.caption.copyWith(color: AppColors.success),
              ),
            ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _qty,
                  enabled: !_editing,
                  keyboardType: TextInputType.number,
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                  decoration: academyInput(
                    _editing ? 'On hand (use Stock count)' : 'Quantity on hand',
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: TextField(
                  controller: _reorder,
                  keyboardType: TextInputType.number,
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                  decoration: academyInput('Reorder at'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          TextField(
            controller: _barcode,
            decoration: academyInput('Barcode (optional)'),
          ),
          const SizedBox(height: 10),
          ListTile(
            tileColor: AppColors.surface,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
            leading: const Icon(Icons.event_outlined),
            title: Text(
              _expiry == null
                  ? 'Expiry date (optional)'
                  : 'Expires ${DateFormat('d MMM yyyy').format(_expiry!)}',
            ),
            trailing: _expiry == null
                ? null
                : IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => setState(() => _expiry = null),
                  ),
            onTap: () async {
              final picked = await showDatePicker(
                context: context,
                initialDate:
                    _expiry ?? DateTime.now().add(const Duration(days: 30)),
                firstDate: DateTime.now().subtract(const Duration(days: 365)),
                lastDate: DateTime.now().add(const Duration(days: 365 * 5)),
              );
              if (picked != null) setState(() => _expiry = picked);
            },
          ),
          const SizedBox(height: 20),
          FilledButton(
            onPressed: _saving ? null : _save,
            child: Text(_saving ? 'Saving…' : 'Save product'),
          ),
        ],
      ),
    );
  }
}
