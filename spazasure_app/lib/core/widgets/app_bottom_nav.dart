import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';

class AppNavItem {
  final IconData icon;
  final IconData activeIcon;
  final String label;
  final int badge;
  final Color? badgeColor;

  const AppNavItem(
    this.icon,
    this.activeIcon,
    this.label, {
    this.badge = 0,
    this.badgeColor,
  });
}

/// Floating pill navigation bar. The item at [centerIndex] is shown as a raised
/// brand-yellow action button (the app's main action, e.g. Cart or Scan).
class AppBottomNav extends StatelessWidget {
  final List<AppNavItem> items;
  final int currentIndex;
  final ValueChanged<int> onTap;
  final int? centerIndex;

  const AppBottomNav({
    super.key,
    required this.items,
    required this.currentIndex,
    required this.onTap,
    this.centerIndex,
  });

  @override
  Widget build(BuildContext context) => SafeArea(
    minimum: const EdgeInsets.only(bottom: 10),
    child: Padding(
      padding: const EdgeInsets.fromLTRB(14, 0, 14, 0),
      child: SizedBox(
        height: 84,
        child: Stack(
          clipBehavior: Clip.none,
          alignment: Alignment.bottomCenter,
          children: [
            Container(
              height: 70,
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(28),
                border: Border.all(color: AppColors.primaryTint),
                boxShadow: [
                  BoxShadow(
                    color: AppColors.primary.withValues(alpha: 0.14),
                    blurRadius: 30,
                    offset: const Offset(0, 10),
                  ),
                ],
              ),
              child: Row(
                children: [
                  for (var i = 0; i < items.length; i++)
                    Expanded(
                      child: i == centerIndex
                          ? const SizedBox.shrink()
                          : _item(i, items[i]),
                    ),
                ],
              ),
            ),
            if (centerIndex != null) _centerButton(centerIndex!),
          ],
        ),
      ),
    ),
  );

  Widget _item(int index, AppNavItem item) {
    final isActive = currentIndex == index;
    final color = isActive ? AppColors.primary : AppColors.ink3;
    return Semantics(
      button: true,
      selected: isActive,
      label: item.label,
      child: InkWell(
        onTap: () => onTap(index),
        borderRadius: BorderRadius.circular(20),
        child: SizedBox(
          height: 70,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              AnimatedScale(
                scale: isActive ? 1.15 : 1,
                duration: const Duration(milliseconds: 260),
                curve: Curves.easeOutBack,
                child: _withBadge(
                  Icon(
                    isActive ? item.activeIcon : item.icon,
                    color: color,
                    size: 24,
                  ),
                  item,
                ),
              ),
              const SizedBox(height: 3),
              Text(
                item.label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: AppTextStyles.caption.copyWith(
                  color: color,
                  fontSize: 11,
                  fontWeight: isActive ? FontWeight.w800 : FontWeight.w600,
                ),
              ),
              const SizedBox(height: 3),
              AnimatedContainer(
                duration: const Duration(milliseconds: 200),
                width: isActive ? 18 : 0,
                height: 3,
                decoration: BoxDecoration(
                  color: AppColors.accent,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _centerButton(int index) {
    final item = items[index];
    final isActive = currentIndex == index;
    return Positioned(
      top: 0,
      child: Semantics(
        button: true,
        selected: isActive,
        label: item.label,
        child: GestureDetector(
          onTap: () => onTap(index),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              AnimatedContainer(
                duration: const Duration(milliseconds: 200),
                width: 60,
                height: 60,
                decoration: BoxDecoration(
                  color: isActive ? AppColors.primary : AppColors.accent,
                  shape: BoxShape.circle,
                  border: Border.all(color: Colors.white, width: 4),
                  boxShadow: [
                    BoxShadow(
                      color: (isActive ? AppColors.primary : AppColors.accent)
                          .withValues(alpha: 0.45),
                      blurRadius: 18,
                      offset: const Offset(0, 8),
                    ),
                  ],
                ),
                child: _withBadge(
                  Icon(
                    isActive ? item.activeIcon : item.icon,
                    color: isActive ? Colors.white : AppColors.primary,
                    size: 26,
                  ),
                  item,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                item.label,
                style: AppTextStyles.caption.copyWith(
                  color: AppColors.primary,
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _withBadge(Widget icon, AppNavItem item) {
    if (item.badge <= 0) return icon;
    return Stack(
      clipBehavior: Clip.none,
      alignment: Alignment.center,
      children: [
        icon,
        Positioned(
          right: -8,
          top: -6,
          child:
              Container(
                    constraints: const BoxConstraints(minWidth: 18),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 5,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: item.badgeColor ?? AppColors.error,
                      borderRadius: BorderRadius.circular(10),
                      border: Border.all(color: Colors.white, width: 1.5),
                    ),
                    child: Text(
                      item.badge > 99 ? '99+' : '${item.badge}',
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 10,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  )
                  // A new key per count replays the bounce each time it changes
                  .animate(key: ValueKey(item.badge))
                  .scaleXY(
                    begin: 0.4,
                    end: 1,
                    duration: 450.ms,
                    curve: Curves.elasticOut,
                  ),
        ),
      ],
    );
  }
}
