import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:smooth_page_indicator/smooth_page_indicator.dart';

class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key});

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> with TickerProviderStateMixin {
  final _controller = PageController();
  int _currentPage = 0;
  late AnimationController _bgController;

  static const _pages = [
    _PageData(Icons.storefront_rounded, 'Order from Verified Suppliers', 'Browse products from trusted, verified suppliers.\nGet wholesale prices delivered to your shop.', Color(0xFF4CAF50)),
    _PageData(Icons.verified_user_rounded, 'Stay Compliant', 'Upload your permits and documents.\nWe help you stay compliant and verified.', Color(0xFF00897B)),
    _PageData(Icons.local_shipping_rounded, 'Track Deliveries', 'Real-time delivery tracking.\nKnow exactly when your stock arrives.', Color(0xFFF57C00)),
    _PageData(Icons.qr_code_scanner_rounded, 'Authentic Products', 'Every product has a QR code.\nYour customers can verify authenticity.', Color(0xFF1565C0)),
  ];

  @override
  void initState() {
    super.initState();
    _bgController = AnimationController(vsync: this, duration: const Duration(seconds: 10))..repeat();
  }

  @override
  void dispose() {
    _bgController.dispose();
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Stack(
        children: [
          // Animated gradient background
          AnimatedBuilder(
            animation: _bgController,
            builder: (_, __) => Container(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment(sin(_bgController.value * 2 * pi) * 0.5, -1),
                  end: Alignment(-sin(_bgController.value * 2 * pi) * 0.5, 1),
                  colors: const [Color(0xFF0D3B0F), Color(0xFF1B5E20), Color(0xFF2E7D32)],
                ),
              ),
            ),
          ),

          SafeArea(
            child: Column(
              children: [
                // Skip button
                Align(
                  alignment: Alignment.topRight,
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: GestureDetector(
                      onTap: () => Navigator.pushReplacementNamed(context, '/login'),
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
                        decoration: BoxDecoration(
                          color: Colors.white.withValues(alpha: 0.1),
                          borderRadius: BorderRadius.circular(20),
                          border: Border.all(color: Colors.white.withValues(alpha: 0.15)),
                        ),
                        child: Text('Skip', style: GoogleFonts.poppins(color: Colors.white.withValues(alpha: 0.8), fontSize: 14, fontWeight: FontWeight.w500)),
                      ),
                    ),
                  ),
                ).animate().fadeIn(delay: 300.ms),

                // Pages
                Expanded(
                  child: PageView.builder(
                    controller: _controller,
                    itemCount: _pages.length,
                    onPageChanged: (i) => setState(() => _currentPage = i),
                    itemBuilder: (_, i) => _OnboardingPage(data: _pages[i]),
                  ),
                ),

                // Indicator
                SmoothPageIndicator(
                  controller: _controller,
                  count: _pages.length,
                  effect: ExpandingDotsEffect(
                    activeDotColor: Colors.white,
                    dotColor: Colors.white.withValues(alpha: 0.25),
                    dotHeight: 8,
                    dotWidth: 8,
                    expansionFactor: 4,
                    spacing: 6,
                  ),
                ),

                const SizedBox(height: 36),

                // Button
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 28),
                  child: GestureDetector(
                    onTap: () {
                      if (_currentPage == _pages.length - 1) {
                        Navigator.pushReplacementNamed(context, '/login');
                      } else {
                        _controller.nextPage(duration: 400.ms, curve: Curves.easeOutCubic);
                      }
                    },
                    child: Container(
                      width: double.infinity,
                      height: 56,
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(16),
                        boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.15), blurRadius: 20, offset: const Offset(0, 8))],
                      ),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Text(
                            _currentPage == _pages.length - 1 ? 'Get Started' : 'Next',
                            style: GoogleFonts.poppins(color: const Color(0xFF1B5E20), fontSize: 16, fontWeight: FontWeight.w700),
                          ),
                          const SizedBox(width: 8),
                          Icon(
                            _currentPage == _pages.length - 1 ? Icons.rocket_launch_rounded : Icons.arrow_forward_rounded,
                            color: const Color(0xFF1B5E20),
                            size: 22,
                          ),
                        ],
                      ),
                    ),
                  ),
                ).animate().fadeIn(delay: 500.ms).slideY(begin: 0.3, end: 0, delay: 500.ms),

                const SizedBox(height: 40),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PageData {
  final IconData icon;
  final String title;
  final String description;
  final Color color;
  const _PageData(this.icon, this.title, this.description, this.color);
}

class _OnboardingPage extends StatelessWidget {
  final _PageData data;
  const _OnboardingPage({required this.data});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 36),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          // Icon with animated rings
          Stack(
            alignment: Alignment.center,
            children: [
              Container(
                width: 200,
                height: 200,
                decoration: BoxDecoration(shape: BoxShape.circle, color: data.color.withValues(alpha: 0.08)),
              ),
              Container(
                width: 160,
                height: 160,
                decoration: BoxDecoration(shape: BoxShape.circle, color: data.color.withValues(alpha: 0.12)),
              ),
              Container(
                width: 120,
                height: 120,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  gradient: LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [data.color, data.color.withValues(alpha: 0.7)]),
                  boxShadow: [BoxShadow(color: data.color.withValues(alpha: 0.4), blurRadius: 30, spreadRadius: 5)],
                ),
                child: Icon(data.icon, size: 56, color: Colors.white),
              ),
            ],
          )
              .animate()
              .scale(begin: const Offset(0.7, 0.7), duration: 600.ms, curve: Curves.elasticOut)
              .fadeIn(duration: 400.ms),

          const SizedBox(height: 48),

          Text(
            data.title,
            style: GoogleFonts.poppins(fontSize: 26, fontWeight: FontWeight.w700, color: Colors.white, height: 1.2),
            textAlign: TextAlign.center,
          ).animate().fadeIn(delay: 200.ms).slideY(begin: 0.2, end: 0, delay: 200.ms),

          const SizedBox(height: 16),

          Text(
            data.description,
            style: GoogleFonts.poppins(fontSize: 15, color: Colors.white.withValues(alpha: 0.7), height: 1.6),
            textAlign: TextAlign.center,
          ).animate().fadeIn(delay: 400.ms).slideY(begin: 0.2, end: 0, delay: 400.ms),
        ],
      ),
    );
  }
}
