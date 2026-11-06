import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/brand_pattern.dart';

/// The app's top bar in the welcome pages' style: SpazaSure blue with the faint
/// brand pattern, white text and rounded bottom corners. A screen that passes
/// its own [backgroundColor] (the camera screens, say) keeps it, without the pattern.
class BrandAppBar extends AppBar {
  BrandAppBar({
    super.key,
    super.leading,
    super.automaticallyImplyLeading,
    super.title,
    super.actions,
    Widget? flexibleSpace,
    PreferredSizeWidget? bottom,
    double? elevation,
    super.scrolledUnderElevation,
    super.shadowColor,
    super.surfaceTintColor,
    ShapeBorder? shape,
    Color? backgroundColor,
    Color? foregroundColor,
    IconThemeData? iconTheme,
    IconThemeData? actionsIconTheme,
    super.primary,
    super.centerTitle,
    super.titleSpacing,
    super.toolbarOpacity,
    super.bottomOpacity,
    super.toolbarHeight,
    super.leadingWidth,
    super.toolbarTextStyle,
    TextStyle? titleTextStyle,
    SystemUiOverlayStyle? systemOverlayStyle,
  }) : super(
         backgroundColor: backgroundColor ?? AppColors.primary,
         foregroundColor:
             foregroundColor ?? (backgroundColor == null ? Colors.white : null),
         elevation: elevation ?? 0,
         flexibleSpace:
             flexibleSpace ??
             (backgroundColor == null ? const _BarPattern() : null),
         bottom: bottom == null || backgroundColor != null
             ? bottom
             : _OnBrand(bottom),
         shape:
             shape ??
             (backgroundColor == null
                 ? const RoundedRectangleBorder(
                     borderRadius: BorderRadius.vertical(
                       bottom: Radius.circular(24),
                     ),
                   )
                 : null),
         iconTheme:
             iconTheme ??
             (backgroundColor == null
                 ? const IconThemeData(color: Colors.white)
                 : null),
         actionsIconTheme:
             actionsIconTheme ??
             (backgroundColor == null
                 ? const IconThemeData(color: Colors.white)
                 : null),
         titleTextStyle:
             titleTextStyle ??
             (backgroundColor == null
                 ? GoogleFonts.nunito(
                     fontSize: 20,
                     fontWeight: FontWeight.w900,
                     color: Colors.white,
                   )
                 : null),
         systemOverlayStyle:
             systemOverlayStyle ??
             (backgroundColor == null ? SystemUiOverlayStyle.light : null),
       );
}

class _BarPattern extends StatelessWidget {
  const _BarPattern();

  @override
  Widget build(BuildContext context) => const SizedBox.expand(
    child: DecoratedBox(
      decoration: BoxDecoration(
        image: brandPatternImage,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(24)),
      ),
    ),
  );
}

/// Tabs under a brand bar: white labels and a yellow underline instead of blue on blue.
class _OnBrand extends StatelessWidget implements PreferredSizeWidget {
  final PreferredSizeWidget child;
  const _OnBrand(this.child);

  @override
  Size get preferredSize => child.preferredSize;

  @override
  Widget build(BuildContext context) => Theme(
    data: Theme.of(context).copyWith(
      tabBarTheme: TabBarThemeData(
        labelColor: Colors.white,
        unselectedLabelColor: Colors.white.withValues(alpha: 0.7),
        indicatorColor: AppColors.accent,
        dividerColor: Colors.transparent,
        labelStyle: GoogleFonts.nunito(fontWeight: FontWeight.w800),
      ),
    ),
    child: DefaultTextStyle.merge(
      style: const TextStyle(color: Colors.white),
      child: child,
    ),
  );
}
