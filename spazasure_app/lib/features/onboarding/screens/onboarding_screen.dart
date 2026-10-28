import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:smooth_page_indicator/smooth_page_indicator.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/motion.dart';

/// The first screen a new user sees. Each message has its own brand colour: the
/// background blends between them as you swipe, and a stack of bold, patterned
/// cards deals the next one to the front.
class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key});

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen>
    with SingleTickerProviderStateMixin {
  final _controller = PageController();
  double _page = 0;
  late final AnimationController _float = AnimationController(
    vsync: this,
    duration: const Duration(seconds: 3),
  )..repeat(reverse: true);

  static const _pages = [
    _PageData(
      Icons.storefront_rounded,
      'Order from Verified Suppliers',
      'Browse products from trusted, verified suppliers.\nGet wholesale prices delivered to your shop.',
      'Wholesale',
      AppColors.secondary,
      [AppColors.accent, AppColors.primaryDark, Colors.white],
    ),
    _PageData(
      Icons.verified_user_rounded,
      'Stay Compliant',
      'Upload your permits and documents.\nWe help you stay compliant and verified.',
      'Verified',
      AppColors.primary,
      [AppColors.secondaryLight, AppColors.accent, Colors.white],
    ),
    _PageData(
      Icons.local_shipping_rounded,
      'Track Deliveries',
      'Real-time delivery tracking.\nKnow exactly when your stock arrives.',
      'On its way',
      AppColors.accentDark,
      [AppColors.red, AppColors.primaryDark, Colors.white],
    ),
    _PageData(
      Icons.qr_code_scanner_rounded,
      'Authentic Products',
      'Every product has a QR code.\nYour customers can verify authenticity.',
      'Genuine',
      AppColors.red,
      [AppColors.accent, AppColors.primaryDark, Colors.white],
    ),
  ];

  int get _current => _page.round().clamp(0, _pages.length - 1);
  bool get _isLast => _current == _pages.length - 1;

  @override
  void initState() {
    super.initState();
    _controller.addListener(
      () => setState(() => _page = _controller.page ?? 0),
    );
  }

  @override
  void dispose() {
    _float.dispose();
    _controller.dispose();
    super.dispose();
  }

  void _start() => Navigator.pushReplacementNamed(context, '/login');

  void _next() => _isLast
      ? _start()
      : _controller.nextPage(duration: 500.ms, curve: Curves.easeOutCubic);

  Color get _background {
    final i = _page.floor().clamp(0, _pages.length - 1);
    final j = (i + 1).clamp(0, _pages.length - 1);
    return Color.lerp(_pages[i].color, _pages[j].color, _page - i)!;
  }

  @override
  Widget build(BuildContext context) {
    final page = _pages[_current];
    return Scaffold(
      backgroundColor: _background,
      body: Stack(
        children: [
          // Soft glows that drift with the swipe
          Positioned(
            top: -80 - _page * 20,
            right: -60 + _page * 30,
            child: _glow(260),
          ),
          Positioned(
            top: 260 + _page * 15,
            left: -90 + _page * 10,
            child: _glow(200),
          ),

          SafeArea(
            child: Column(
              children: [
                _topBar(),
                // The card stack, driven by the swipe
                Expanded(
                  child: Stack(
                    children: [
                      LayoutBuilder(
                        builder: (context, box) => AnimatedBuilder(
                          animation: _float,
                          builder: (_, __) => _CardStack(
                            pages: _pages,
                            page: _page,
                            float: _float.value,
                            size: box.biggest,
                          ),
                        ),
                      ),
                      // Swiping over the cards moves the pages
                      PageView.builder(
                        controller: _controller,
                        itemCount: _pages.length,
                        itemBuilder: (_, __) => const SizedBox.expand(),
                      ),
                    ],
                  ),
                ),
                _bottomCard(page),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _glow(double size) => IgnorePointer(
    child: Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: RadialGradient(
          colors: [
            Colors.white.withValues(alpha: 0.22),
            Colors.white.withValues(alpha: 0),
          ],
        ),
      ),
    ),
  );

  Widget _topBar() => Padding(
    padding: const EdgeInsets.fromLTRB(20, 12, 16, 0),
    child: Row(
      children: [
        // Brand badge, like a shop sign
        Flexible(
          child: Container(
            padding: const EdgeInsets.fromLTRB(6, 6, 14, 6),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(18),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.12),
                  blurRadius: 16,
                  offset: const Offset(0, 6),
                ),
              ],
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: Image.asset(
                    'assets/images/spazasure_logo.png',
                    width: 34,
                    height: 34,
                    fit: BoxFit.cover,
                  ),
                ),
                const SizedBox(width: 8),
                Flexible(
                  child: Text(
                    'SpazaSure',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: GoogleFonts.nunito(
                      fontSize: 17,
                      fontWeight: FontWeight.w900,
                      color: AppColors.textPrimary,
                    ),
                  ),
                ),
              ],
            ),
          ).animate().fadeIn(duration: 400.ms).slideY(begin: -0.4, end: 0),
        ),
        const Spacer(),
        if (!_isLast)
          Pressable(
            onTap: _start,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 10),
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.18),
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: Colors.white.withValues(alpha: 0.3)),
              ),
              child: Text(
                'Skip',
                style: GoogleFonts.nunito(
                  color: Colors.white,
                  fontSize: 14,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ).animate().fadeIn(delay: 300.ms),
      ],
    ),
  );

  Widget _bottomCard(_PageData page) => GestureDetector(
    // The white card can be swiped too
    onHorizontalDragEnd: (d) {
      final v = d.primaryVelocity ?? 0;
      if (v < -200) _next();
      if (v > 200 && _current > 0) {
        _controller.previousPage(duration: 500.ms, curve: Curves.easeOutCubic);
      }
    },
    child:
        Container(
              width: double.infinity,
              margin: const EdgeInsets.fromLTRB(16, 0, 16, 16),
              padding: const EdgeInsets.fromLTRB(24, 22, 24, 20),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(32),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.18),
                    blurRadius: 30,
                    offset: const Offset(0, 12),
                  ),
                ],
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      // Step pill in the page's colour
                      AnimatedContainer(
                        duration: 300.ms,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 5,
                        ),
                        decoration: BoxDecoration(
                          color: page.color.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          '${_current + 1} of ${_pages.length}',
                          style: GoogleFonts.nunito(
                            fontSize: 12,
                            fontWeight: FontWeight.w800,
                            color: page.color,
                          ),
                        ),
                      ),
                      const Spacer(),
                      SmoothPageIndicator(
                        controller: _controller,
                        count: _pages.length,
                        effect: ExpandingDotsEffect(
                          activeDotColor: page.color,
                          dotColor: AppColors.divider,
                          dotHeight: 8,
                          dotWidth: 8,
                          expansionFactor: 3.5,
                          spacing: 5,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  // Title and text swap in as the page changes
                  AnimatedSwitcher(
                    duration: 350.ms,
                    transitionBuilder: (child, anim) => FadeTransition(
                      opacity: anim,
                      child: SlideTransition(
                        position: Tween(
                          begin: const Offset(0, 0.15),
                          end: Offset.zero,
                        ).animate(anim),
                        child: child,
                      ),
                    ),
                    layoutBuilder: (current, previous) => Stack(
                      alignment: Alignment.topLeft,
                      children: [...previous, if (current != null) current],
                    ),
                    child: Column(
                      key: ValueKey(_current),
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          page.title,
                          style: GoogleFonts.nunito(
                            fontSize: 25,
                            fontWeight: FontWeight.w900,
                            color: AppColors.textPrimary,
                            height: 1.15,
                          ),
                        ),
                        const SizedBox(height: 10),
                        Text(
                          page.description,
                          style: GoogleFonts.nunito(
                            fontSize: 14.5,
                            color: AppColors.textSecondary,
                            height: 1.5,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 22),
                  Pressable(
                    onTap: _next,
                    child: AnimatedContainer(
                      duration: 300.ms,
                      width: double.infinity,
                      height: 56,
                      decoration: BoxDecoration(
                        color: _isLast ? page.color : AppColors.textPrimary,
                        borderRadius: BorderRadius.circular(28),
                      ),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Text(
                            _isLast ? 'Get Started' : 'Next',
                            style: GoogleFonts.nunito(
                              color: Colors.white,
                              fontSize: 16,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          const SizedBox(width: 8),
                          Icon(
                            _isLast
                                ? Icons.rocket_launch_rounded
                                : Icons.arrow_forward_rounded,
                            color: Colors.white,
                            size: 21,
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            )
            .animate()
            .fadeIn(delay: 200.ms, duration: 400.ms)
            .slideY(begin: 0.25, end: 0, curve: Curves.easeOutCubic),
  );
}

class _PageData {
  final IconData icon;
  final String title;
  final String description;
  final String tag;
  final Color color;

  /// Colours for the card's pattern.
  final List<Color> pattern;

  const _PageData(
    this.icon,
    this.title,
    this.description,
    this.tag,
    this.color,
    this.pattern,
  );
}

/// One card per page, stacked like a hand of cards. The current page's card is
/// in front; the next ones peek out behind it, and swiping throws the front
/// card off to the side.
class _CardStack extends StatelessWidget {
  final List<_PageData> pages;
  final double page;
  final double float;
  final Size size;

  const _CardStack({
    required this.pages,
    required this.page,
    required this.float,
    required this.size,
  });

  @override
  Widget build(BuildContext context) {
    final width = min(size.width * 0.66, 270.0);
    final height = min(width * 1.25, size.height * 0.7);
    final bob = sin(float * pi) * 8;

    // Farthest back first; cards already thrown away go on top
    final order = List.generate(pages.length, (i) => i)
      ..sort((a, b) {
        final da = a - page, db = b - page;
        if (da < 0 && db >= 0) return 1;
        if (db < 0 && da >= 0) return -1;
        return db.compareTo(da);
      });

    return SizedBox.fromSize(
      size: size,
      child: Stack(
        alignment: Alignment.center,
        children: [for (final i in order) _card(i, width, height, bob)],
      ),
    );
  }

  Widget _card(int i, double width, double height, double bob) {
    final d = i - page;
    double dx = 0, dy = 0, scale = 1, angle = 0, opacity = 1;
    if (d >= 0) {
      // Waiting behind: smaller, higher and slightly fanned
      dy = -d * 30;
      scale = 1 - d * 0.08;
      angle = d * 0.06 * (i.isEven ? 1 : -1);
      opacity = (1 - (d - 2).clamp(0, 1)).toDouble();
    } else {
      // Thrown off to the left with a spin
      dx = d * size.width * 0.9;
      dy = d * -40;
      angle = d * 0.45;
      opacity = (1 + d).clamp(0, 1).toDouble();
    }
    final front = d.abs() < 0.5;
    return Opacity(
      opacity: opacity,
      child: Transform.translate(
        offset: Offset(dx, dy + (front ? bob : 0) + size.height * 0.08),
        child: Transform.rotate(
          angle: angle,
          child: Transform.scale(
            scale: scale,
            child: _PatternCard(data: pages[i], width: width, height: height),
          ),
        ),
      ),
    );
  }
}

class _PatternCard extends StatelessWidget {
  final _PageData data;
  final double width;
  final double height;

  const _PatternCard({
    required this.data,
    required this.width,
    required this.height,
  });

  @override
  Widget build(BuildContext context) => Container(
    width: width,
    height: height,
    decoration: BoxDecoration(
      color: data.color,
      borderRadius: BorderRadius.circular(30),
      border: Border.all(color: Colors.white, width: 4),
      boxShadow: [
        BoxShadow(
          color: Colors.black.withValues(alpha: 0.22),
          blurRadius: 28,
          offset: const Offset(0, 16),
        ),
      ],
    ),
    child: ClipRRect(
      borderRadius: BorderRadius.circular(26),
      child: Stack(
        children: [
          Positioned.fill(
            child: CustomPaint(painter: _PatternPainter(data.pattern)),
          ),
          // The page's icon on a white disc
          Center(
            child: Container(
              width: width * 0.42,
              height: width * 0.42,
              decoration: BoxDecoration(
                color: Colors.white,
                shape: BoxShape.circle,
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.18),
                    blurRadius: 20,
                    offset: const Offset(0, 10),
                  ),
                ],
              ),
              child: Icon(data.icon, size: width * 0.2, color: data.color),
            ),
          ),
          Positioned(
            left: 18,
            bottom: 18,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(14),
              ),
              child: Text(
                data.tag,
                style: GoogleFonts.nunito(
                  fontSize: 13,
                  fontWeight: FontWeight.w900,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

/// Bold, flat shapes: a big quarter circle, a leaf, rings and a dot.
class _PatternPainter extends CustomPainter {
  final List<Color> colors;
  const _PatternPainter(this.colors);

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    final fill = Paint()..style = PaintingStyle.fill;

    // Big quarter circle in the bottom right
    fill.color = colors[0];
    canvas.drawCircle(Offset(w * 1.05, h * 1.02), w * 0.62, fill);

    // Leaf shape across the top left
    fill.color = colors[1];
    final leaf = Path()
      ..moveTo(-w * 0.1, h * 0.38)
      ..quadraticBezierTo(w * 0.1, -h * 0.05, w * 0.62, -h * 0.02)
      ..quadraticBezierTo(w * 0.32, h * 0.3, -w * 0.1, h * 0.38)
      ..close();
    canvas.drawPath(leaf, fill);

    // Rings with a dot, like a button
    final ring = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3
      ..color = colors[2].withValues(alpha: 0.9);
    canvas.drawCircle(Offset(w * 0.82, h * 0.16), w * 0.08, ring);
    fill.color = colors[2];
    canvas.drawCircle(Offset(w * 0.82, h * 0.16), w * 0.03, fill);
    canvas.drawCircle(Offset(w * 0.16, h * 0.7), w * 0.12, ring);

    // Scattered small dots
    fill.color = colors[2].withValues(alpha: 0.55);
    for (final p in const [
      Offset(0.62, 0.3),
      Offset(0.3, 0.86),
      Offset(0.9, 0.55),
    ]) {
      canvas.drawCircle(Offset(w * p.dx, h * p.dy), 4, fill);
    }
  }

  @override
  bool shouldRepaint(_PatternPainter old) => old.colors != colors;
}
