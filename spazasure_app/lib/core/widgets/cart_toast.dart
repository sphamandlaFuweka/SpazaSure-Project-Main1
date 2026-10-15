import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/providers/cart_provider.dart';

/// The small "added to cart" card that floats above the bottom bar while the user shops.
///
/// - One card only: adding more while it is showing updates it in place (no queue of
///   messages), and restarts its short timer.
/// - It hides itself after a few seconds, can be swiped away, and never blocks the
///   rest of the screen.
class CartToast {
  CartToast._();

  static const _visibleFor = Duration(milliseconds: 3200);

  static OverlayEntry? _entry;

  /// The overlay the card lives in; a new app/navigator gets a fresh card
  static OverlayState? _overlay;
  static final _state = ValueNotifier<_ToastData?>(null);
  static Timer? _hideTimer;

  /// Adds [product] to the cart and shows the result. Returns whether it was added.
  static bool add(BuildContext context, Product product, int quantity) {
    final cart = context.read<CartProvider>();
    final inCart = cart.items
        .where((i) => i.product.id == product.id)
        .fold(0, (s, i) => s + i.quantity);
    final added = cart.add(product, quantity);
    if (added) {
      HapticFeedback.selectionClick();
      _show(context, _ToastData(product: product, quantity: quantity));
    } else {
      final left = product.stockQuantity - inCart;
      _show(
        context,
        _ToastData(
          product: product,
          quantity: quantity,
          error: product.stockQuantity <= 0
              ? 'Out of stock right now'
              : left <= 0
              ? 'You already have all ${product.stockQuantity} in your cart'
              : 'Only $left more available',
        ),
      );
    }
    return added;
  }

  static void _show(BuildContext context, _ToastData data) {
    final overlay = Overlay.maybeOf(context, rootOverlay: true);
    if (overlay == null) return;
    final previous = _state.value;
    // Adding the same product again while the card is up: show the running quantity
    _state.value =
        previous != null &&
            previous.error == null &&
            data.error == null &&
            previous.product.id == data.product.id
        ? data.copyWith(
            quantity: previous.quantity + data.quantity,
            bump: previous.bump + 1,
          )
        : data.copyWith(bump: (previous?.bump ?? 0) + 1);

    if (_entry != null && (_overlay != overlay || !_overlay!.mounted)) {
      _remove();
    }
    if (_entry == null) {
      _entry = OverlayEntry(builder: (_) => const _CartToastHost());
      _overlay = overlay;
      overlay.insert(_entry!);
    } else if (!_entry!.mounted) {
      overlay.insert(_entry!);
    }
    _restartTimer();
  }

  static void _restartTimer() {
    _hideTimer?.cancel();
    _hideTimer = Timer(_visibleFor, hide);
  }

  static void hide() {
    _hideTimer?.cancel();
    _state.value = null;
  }

  /// Keep the card up while the user's finger is on it
  static void _hold() => _hideTimer?.cancel();
  static void _release() => _restartTimer();

  static void _remove() {
    if (_entry?.mounted == true) _entry!.remove();
    _entry = null;
    _overlay = null;
  }
}

class _ToastData {
  final Product product;
  final int quantity;
  final String? error;

  /// Changes on every update, to replay the little "bump" animation
  final int bump;

  const _ToastData({
    required this.product,
    required this.quantity,
    this.error,
    this.bump = 0,
  });

  _ToastData copyWith({int? quantity, int? bump}) => _ToastData(
    product: product,
    quantity: quantity ?? this.quantity,
    error: error,
    bump: bump ?? this.bump,
  );
}

class _CartToastHost extends StatelessWidget {
  const _CartToastHost();

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.of(context).padding.bottom;
    return ValueListenableBuilder<_ToastData?>(
      valueListenable: CartToast._state,
      builder: (context, data, _) {
        return Positioned(
          left: 12,
          right: 12,
          // Sits just above the floating bottom navigation bar
          bottom: bottomInset + 92,
          child: AnimatedSwitcher(
            duration: const Duration(milliseconds: 260),
            reverseDuration: const Duration(milliseconds: 200),
            switchInCurve: Curves.easeOutCubic,
            switchOutCurve: Curves.easeInCubic,
            transitionBuilder: (child, animation) => FadeTransition(
              opacity: animation,
              child: SlideTransition(
                position: Tween(
                  begin: const Offset(0, 0.4),
                  end: Offset.zero,
                ).animate(animation),
                child: child,
              ),
            ),
            layoutBuilder: (current, previous) => Stack(
              alignment: Alignment.bottomCenter,
              children: [...previous, ?current],
            ),
            child: data == null
                ? const SizedBox.shrink(key: ValueKey('hidden'))
                : _CartToastCard(key: const ValueKey('card'), data: data),
          ),
        );
      },
    );
  }
}

class _CartToastCard extends StatelessWidget {
  const _CartToastCard({super.key, required this.data});

  final _ToastData data;

  @override
  Widget build(BuildContext context) {
    final failed = data.error != null;
    final cart = context.watch<CartProvider>();
    return Dismissible(
      key: ValueKey('cart-toast-${data.bump}'),
      direction: DismissDirection.down,
      onDismissed: (_) {
        CartToast.hide();
        CartToast._remove();
      },
      child: Listener(
        onPointerDown: (_) => CartToast._hold(),
        onPointerUp: (_) => CartToast._release(),
        onPointerCancel: (_) => CartToast._release(),
        child: Material(
          color: failed ? AppColors.redTint : AppColors.surface,
          elevation: 12,
          shadowColor: Colors.black38,
          borderRadius: BorderRadius.circular(18),
          child: Container(
            padding: const EdgeInsets.fromLTRB(8, 8, 8, 8),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(18),
              border: Border.all(
                color: failed
                    ? AppColors.error.withValues(alpha: 0.4)
                    : AppColors.divider,
              ),
            ),
            child: Row(
              children: [
                _thumbnail(failed),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Icon(
                            failed
                                ? Icons.error_outline_rounded
                                : Icons.check_circle_rounded,
                            size: 15,
                            color: failed ? AppColors.error : AppColors.success,
                          ),
                          const SizedBox(width: 4),
                          Flexible(
                            child: Text(
                              failed ? 'Couldn\'t add' : 'Added to cart',
                              style: AppTextStyles.caption.copyWith(
                                fontWeight: FontWeight.w800,
                                color: failed
                                    ? AppColors.error
                                    : AppColors.success,
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 2),
                      Text(
                        failed
                            ? data.error!
                            : '${data.quantity} × ${data.product.name}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTextStyles.bodySmall.copyWith(
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      if (!failed)
                        TweenAnimationBuilder<double>(
                          key: ValueKey(data.bump),
                          tween: Tween(begin: 1.12, end: 1),
                          duration: const Duration(milliseconds: 320),
                          curve: Curves.easeOutBack,
                          builder: (_, scale, child) => Transform.scale(
                            scale: scale,
                            alignment: Alignment.centerLeft,
                            child: child,
                          ),
                          child: Text(
                            '${cart.count} item${cart.count == 1 ? '' : 's'} · R${cart.subtotal.toStringAsFixed(2)}',
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textSecondary,
                            ),
                          ),
                        )
                      else
                        Text(
                          data.product.name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: AppTextStyles.caption.copyWith(
                            color: AppColors.textSecondary,
                          ),
                        ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                if (!failed || cart.count > 0)
                  FilledButton(
                    onPressed: () {
                      CartToast.hide();
                      Navigator.of(
                        context,
                        rootNavigator: true,
                      ).pushNamed('/cart');
                    },
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      padding: const EdgeInsets.symmetric(horizontal: 14),
                      minimumSize: const Size(0, 40),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: const Text(
                      'View cart',
                      style: TextStyle(fontWeight: FontWeight.w800),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _thumbnail(bool failed) {
    final url = data.product.imageUrl;
    Widget? image;
    if (url.startsWith('http')) {
      image = Image.network(
        url,
        fit: BoxFit.cover,
        errorBuilder: (_, _, _) => const SizedBox.shrink(),
      );
    } else if (url.startsWith('data:image') && url.contains(',')) {
      try {
        image = Image.memory(
          base64Decode(url.substring(url.indexOf(',') + 1)),
          fit: BoxFit.cover,
        );
      } catch (_) {}
    }
    return Container(
      width: 48,
      height: 48,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: failed ? Colors.white : AppColors.primaryTint,
        borderRadius: BorderRadius.circular(12),
      ),
      child:
          image ??
          Icon(
            Icons.shopping_basket_rounded,
            color: failed ? AppColors.error : AppColors.primary,
            size: 24,
          ),
    );
  }
}
