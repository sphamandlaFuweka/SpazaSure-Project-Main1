import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:smooth_page_indicator/smooth_page_indicator.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/brand_pattern.dart';
import 'package:spazasure_app/core/widgets/motion.dart';

/// The first screen a new user sees: a few simple pages on brand blue, each
/// with a glowing icon, a title and a line or two of text.
class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key});

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  final _controller = PageController();
  int _current = 0;

  static const _pages = [
    _PageData(
      Icons.storefront_rounded,
      'Order from Verified Suppliers',
      'Browse products from trusted, verified suppliers.\nGet wholesale prices delivered to your shop.',
    ),
    _PageData(
      Icons.verified_user_rounded,
      'Stay Compliant',
      'Upload your permits and documents.\nWe help you stay compliant and verified.',
    ),
    _PageData(
      Icons.local_shipping_rounded,
      'Track Deliveries',
      'Real-time delivery tracking.\nKnow exactly when your stock arrives.',
    ),
    _PageData(
      Icons.qr_code_scanner_rounded,
      'Authentic Products',
      'Every product has a QR code.\nYour customers can verify authenticity.',
    ),
  ];

  bool get _isLast => _current == _pages.length - 1;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _start() => Navigator.pushReplacementNamed(context, '/login');

  void _next() => _isLast
      ? _start()
      : _controller.nextPage(duration: 400.ms, curve: Curves.easeOutCubic);

  @override
  Widget build(BuildContext context) => Scaffold(
    body: Container(
      decoration: const BoxDecoration(
        image: brandPatternImage,
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [AppColors.primary, AppColors.primaryDark],
        ),
      ),
      child: SafeArea(
        child: Column(
          children: [
            _topBar(),
            Expanded(
              child: PageView.builder(
                controller: _controller,
                itemCount: _pages.length,
                onPageChanged: (i) => setState(() => _current = i),
                itemBuilder: (_, i) => _PageBody(data: _pages[i]),
              ),
            ),
            SmoothPageIndicator(
              controller: _controller,
              count: _pages.length,
              effect: ExpandingDotsEffect(
                activeDotColor: Colors.white,
                dotColor: Colors.white.withValues(alpha: 0.35),
                dotHeight: 6,
                dotWidth: 6,
                expansionFactor: 4,
                spacing: 6,
              ),
            ),
            const SizedBox(height: 32),
            _button(),
          ],
        ),
      ),
    ),
  );

  Widget _topBar() => Padding(
    padding: const EdgeInsets.fromLTRB(20, 12, 16, 0),
    child: SizedBox(
      height: 44,
      child: Align(
        alignment: Alignment.centerRight,
        child: AnimatedOpacity(
          duration: 250.ms,
          opacity: _isLast ? 0 : 1,
          child: IgnorePointer(
            ignoring: _isLast,
            child: Pressable(
              onTap: _start,
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 18,
                  vertical: 10,
                ),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(
                    color: Colors.white.withValues(alpha: 0.25),
                  ),
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
            ),
          ),
        ),
      ),
    ),
  );

  Widget _button() => Padding(
    padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
    child: Pressable(
      onTap: _next,
      child: Container(
        width: double.infinity,
        height: 56,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.15),
              blurRadius: 20,
              offset: const Offset(0, 8),
            ),
          ],
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              _isLast ? 'Get Started' : 'Next',
              style: GoogleFonts.nunito(
                color: AppColors.primary,
                fontSize: 16,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(width: 8),
            Icon(
              _isLast
                  ? Icons.rocket_launch_rounded
                  : Icons.arrow_forward_rounded,
              color: AppColors.primary,
              size: 20,
            ),
          ],
        ),
      ),
    ),
  );
}

class _PageData {
  final IconData icon;
  final String title;
  final String description;

  const _PageData(this.icon, this.title, this.description);
}

/// One page: the icon in a soft white halo, then the title and text.
class _PageBody extends StatelessWidget {
  final _PageData data;

  const _PageBody({required this.data});

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(horizontal: 32),
    child: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Container(
          width: 170,
          height: 170,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            gradient: RadialGradient(
              colors: [
                Colors.white.withValues(alpha: 0.08),
                Colors.white.withValues(alpha: 0),
              ],
            ),
          ),
          child: Container(
            width: 96,
            height: 96,
            decoration: BoxDecoration(
              color: Colors.white,
              shape: BoxShape.circle,
              boxShadow: [
                BoxShadow(
                  color: Colors.white.withValues(alpha: 0.1),
                  blurRadius: 30,
                  spreadRadius: 6,
                ),
              ],
            ),
            child: Icon(data.icon, size: 46, color: AppColors.primary),
          ),
        ).animate().scaleXY(
          begin: 0.8,
          end: 1,
          duration: 500.ms,
          curve: Curves.easeOutBack,
        ),
        const SizedBox(height: 40),
        Text(
          data.title,
          textAlign: TextAlign.center,
          style: GoogleFonts.nunito(
            fontSize: 24,
            fontWeight: FontWeight.w800,
            color: Colors.white,
          ),
        ),
        const SizedBox(height: 12),
        Text(
          data.description,
          textAlign: TextAlign.center,
          style: GoogleFonts.nunito(
            fontSize: 14.5,
            color: Colors.white.withValues(alpha: 0.85),
            height: 1.6,
          ),
        ),
      ],
    ).animate().fadeIn(duration: 400.ms),
  );
}
