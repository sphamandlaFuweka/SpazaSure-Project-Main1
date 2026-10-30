import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/group_buy_service.dart';

/// What the shop chose in the join sheet: the quantities and how it pays ('wallet' or 'paystack').
class GroupBuyJoinChoice {
  final List<Map<String, dynamic>> items;
  final String paymentMethod;

  const GroupBuyJoinChoice(this.items, this.paymentMethod);
}

/// One sheet to join a group buy: quantities per product, the total with delivery, and how to
/// pay. Problems (too few items, no shop location, out of stock) show inside the sheet.
class JoinGroupBuySheet extends StatefulWidget {
  final GroupBuy group;
  final List<GroupBuyProduct> products;

  const JoinGroupBuySheet({
    super.key,
    required this.group,
    required this.products,
  });

  static Future<GroupBuyJoinChoice?> show(
    BuildContext context,
    GroupBuy group,
    List<GroupBuyProduct> products,
  ) => showModalBottomSheet<GroupBuyJoinChoice>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: AppColors.surface,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
    ),
    builder: (_) => JoinGroupBuySheet(group: group, products: products),
  );

  @override
  State<JoinGroupBuySheet> createState() => _JoinGroupBuySheetState();
}

class _JoinGroupBuySheetState extends State<JoinGroupBuySheet> {
  late final Map<String, TextEditingController> _quantities;
  GroupBuyQuote? _quote;
  String? _error;
  bool _quoting = false;
  Timer? _debounce;

  @override
  void initState() {
    super.initState();
    final single = widget.products.length == 1;
    _quantities = {
      for (final product in widget.products)
        product.id: TextEditingController(
          // One product: start at the whole minimum; several: the shop splits it
          text: single
              ? '${widget.group.minTotal.clamp(product.minPerShop, product.remaining)}'
              : '0',
        ),
    };
    if (single) _refreshQuote();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    for (final controller in _quantities.values) {
      controller.dispose();
    }
    super.dispose();
  }

  int _qty(GroupBuyProduct product) =>
      int.tryParse(_quantities[product.id]!.text) ?? 0;

  int get _totalItems =>
      widget.products.fold(0, (sum, product) => sum + _qty(product));

  List<Map<String, dynamic>> get _items => [
    for (final product in widget.products)
      if (_qty(product) > 0)
        {'groupBuyProductId': product.id, 'quantity': _qty(product)},
  ];

  /// A problem the shop can fix here, before asking the server.
  String? get _localProblem {
    for (final product in widget.products) {
      final qty = _qty(product);
      if (qty > product.remaining) {
        return 'Only ${product.remaining} of ${product.productName} are still needed.';
      }
      if (qty > 0 && qty < product.minPerShop) {
        return 'The minimum for ${product.productName} is ${product.minPerShop}.';
      }
    }
    if (_totalItems < widget.group.minTotal) {
      return widget.group.minTotal < 10
          ? 'Only ${widget.group.minTotal} items are left. Take them all.'
          : 'Choose at least ${widget.group.minTotal} items in total (any mix).';
    }
    return null;
  }

  void _changed() {
    setState(() {
      _quote = null;
      _error = null;
    });
    _debounce?.cancel();
    if (_localProblem == null) {
      _debounce = Timer(const Duration(milliseconds: 400), _refreshQuote);
    }
  }

  void _step(GroupBuyProduct product, int by) {
    final next = (_qty(product) + by).clamp(0, product.remaining);
    _quantities[product.id]!.text = '$next';
    _changed();
  }

  /// Delivery and the total come from the server, which also checks stock and the shop's location.
  Future<void> _refreshQuote() async {
    if (_localProblem != null) return;
    final items = _items;
    setState(() => _quoting = true);
    try {
      final quote = await GroupBuyService.quote(widget.group.id, items);
      // Ignore answers for quantities the shop has since changed
      if (!mounted || _items.toString() != items.toString()) return;
      setState(() {
        _quote = quote;
        _error = null;
      });
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _quoting = false);
    }
  }

  void _pay(String method) =>
      Navigator.pop(context, GroupBuyJoinChoice(_items, method));

  static String _rand(double amount) => 'R${amount.toStringAsFixed(2)}';

  @override
  Widget build(BuildContext context) {
    final problem = _error ?? _localProblem;
    final quote = _quote;
    final canUseWallet = quote != null && quote.walletBalance >= quote.total;

    return Padding(
      // Stay above the keyboard while typing a quantity
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxHeight: MediaQuery.of(context).size.height * 0.85,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 8, 4),
              child: Row(
                children: [
                  Expanded(
                    child: Text('Join group buy', style: AppTextStyles.h3),
                  ),
                  IconButton(
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Text(
                widget.group.minTotal < 10
                    ? 'Only ${widget.group.minTotal} items are left. Take them all to complete it.'
                    : 'At least ${widget.group.minTotal} items in total, any mix, e.g. 3 of one and 7 of another.',
                style: AppTextStyles.caption,
              ),
            ),
            const SizedBox(height: 8),
            Flexible(
              child: ListView.separated(
                shrinkWrap: true,
                padding: const EdgeInsets.symmetric(horizontal: 20),
                itemCount: widget.products.length,
                separatorBuilder: (_, __) => const Divider(height: 1),
                itemBuilder: (_, index) => _productRow(widget.products[index]),
              ),
            ),
            Container(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
              decoration: BoxDecoration(
                color: AppColors.surface,
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.06),
                    blurRadius: 8,
                    offset: const Offset(0, -2),
                  ),
                ],
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (problem != null)
                    Container(
                      margin: const EdgeInsets.only(bottom: 10),
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: AppColors.error.withValues(alpha: 0.08),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Icon(
                            Icons.info_outline,
                            size: 18,
                            color: AppColors.error,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              problem,
                              style: AppTextStyles.bodySmall.copyWith(
                                color: AppColors.error,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  _summaryRow(
                    '$_totalItems items',
                    quote == null ? '—' : _rand(quote.itemsTotal),
                  ),
                  _summaryRow(
                    'Delivery to your shop',
                    _quoting
                        ? 'Working out…'
                        : quote == null
                        ? '—'
                        : _rand(quote.deliveryFee),
                  ),
                  const Divider(height: 16),
                  _summaryRow(
                    'Total',
                    quote == null ? '—' : _rand(quote.total),
                    bold: true,
                  ),
                  const SizedBox(height: 12),
                  ElevatedButton.icon(
                    onPressed: canUseWallet ? () => _pay('wallet') : null,
                    icon: const Icon(Icons.account_balance_wallet_outlined),
                    label: Text(
                      quote == null
                          ? 'Pay from wallet'
                          : 'Pay from wallet (${_rand(quote.walletBalance)})',
                    ),
                  ),
                  const SizedBox(height: 6),
                  OutlinedButton.icon(
                    onPressed: quote == null ? null : () => _pay('paystack'),
                    icon: const Icon(Icons.credit_card),
                    label: Text(
                      quote == null
                          ? 'Pay by card'
                          : 'Pay ${_rand(quote.total)} by card',
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    'Payment is final. If a product doesn\'t reach its target, its money goes back to your wallet.',
                    style: AppTextStyles.caption,
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _summaryRow(String label, String value, {bool bold = false}) {
    final style = bold
        ? AppTextStyles.body.copyWith(fontWeight: FontWeight.w800)
        : AppTextStyles.bodySmall;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          Expanded(child: Text(label, style: style)),
          Text(value, style: style),
        ],
      ),
    );
  }

  Widget _productRow(GroupBuyProduct product) {
    final qty = _qty(product);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 10),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  product.productName,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
                ),
                Text(
                  '${_rand(product.discountPrice)} each • ${product.remaining} still needed',
                  style: AppTextStyles.caption,
                ),
              ],
            ),
          ),
          IconButton(
            visualDensity: VisualDensity.compact,
            onPressed: qty > 0 ? () => _step(product, -1) : null,
            icon: const Icon(Icons.remove_circle_outline),
          ),
          SizedBox(
            width: 44,
            child: TextField(
              controller: _quantities[product.id],
              textAlign: TextAlign.center,
              keyboardType: TextInputType.number,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: const InputDecoration(
                isDense: true,
                contentPadding: EdgeInsets.symmetric(vertical: 8),
              ),
              onChanged: (_) => _changed(),
            ),
          ),
          IconButton(
            visualDensity: VisualDensity.compact,
            onPressed: qty < product.remaining ? () => _step(product, 1) : null,
            icon: const Icon(Icons.add_circle_outline),
          ),
        ],
      ),
    );
  }
}
