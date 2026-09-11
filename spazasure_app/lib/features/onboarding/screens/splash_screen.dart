import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:spazasure_app/services/auth_service.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> with TickerProviderStateMixin {
  late AnimationController _pulseController;
  late AnimationController _particleController;
  late AnimationController _progressController;

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(vsync: this, duration: const Duration(seconds: 2))..repeat(reverse: true);
    _particleController = AnimationController(vsync: this, duration: const Duration(seconds: 4))..repeat();

    // Progress bar fills over 3.5 seconds then navigates
    _progressController = AnimationController(vsync: this, duration: const Duration(milliseconds: 3500));
    _progressController.forward();
    _progressController.addStatusListener((status) async {
      if (status == AnimationStatus.completed && mounted) {
        final loggedIn = await AuthService.isLoggedIn();
        if (!mounted) return;
        Navigator.pushReplacementNamed(
            context, loggedIn ? '/home' : '/onboarding');
      }
    });
  }

  @override
  void dispose() {
    _pulseController.dispose();
    _particleController.dispose();
    _progressController.dispose();
    super.dispose();
  }

  String _loadingLabel(double value) {
    if (value < 0.3) return 'Initialising...';
    if (value < 0.6) return 'Loading marketplace...';
    if (value < 0.85) return 'Securing connection...';
    return 'Almost ready...';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [Color(0xFF0D3B0F), Color(0xFF1B5E20), Color(0xFF2E7D32), Color(0xFF1B5E20)],
            stops: [0.0, 0.3, 0.7, 1.0],
          ),
        ),
        child: Stack(
          children: [
            // Animated floating particles
            ...List.generate(20, (i) => _FloatingParticle(index: i, controller: _particleController)),

            // Glowing rings behind logo
            Center(
              child: AnimatedBuilder(
                animation: _pulseController,
                builder: (_, __) => Container(
                  width: 200 + (_pulseController.value * 40),
                  height: 200 + (_pulseController.value * 40),
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: Colors.white.withValues(alpha: 0.05 + _pulseController.value * 0.05),
                      width: 1.5,
                    ),
                  ),
                ),
              ),
            ),
            Center(
              child: AnimatedBuilder(
                animation: _pulseController,
                builder: (_, __) => Container(
                  width: 260 + (_pulseController.value * 30),
                  height: 260 + (_pulseController.value * 30),
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: Colors.white.withValues(alpha: 0.03 + _pulseController.value * 0.03),
                      width: 1,
                    ),
                  ),
                ),
              ),
            ),

            // Main content
            Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  // Logo container with glow
                  Container(
                    width: 130,
                    height: 130,
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(32),
                      boxShadow: [
                        BoxShadow(color: const Color(0xFF4CAF50).withValues(alpha: 0.4), blurRadius: 40, spreadRadius: 5),
                        BoxShadow(color: Colors.white.withValues(alpha: 0.2), blurRadius: 20, spreadRadius: 2),
                      ],
                    ),
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(32),
                      child: Image.asset(
                        'assets/images/spazasure_logo.jpg',
                        fit: BoxFit.cover,
                      ),
                    ),
                  )
                      .animate()
                      .scale(begin: const Offset(0.3, 0.3), end: const Offset(1, 1), duration: 800.ms, curve: Curves.elasticOut)
                      .fadeIn(duration: 600.ms),

                  const SizedBox(height: 28),

                  // App name with shimmer
                  Text(
                    'SpazaSure',
                    style: GoogleFonts.poppins(
                      fontSize: 38,
                      fontWeight: FontWeight.w800,
                      color: Colors.white,
                      letterSpacing: 2,
                      shadows: [
                        Shadow(color: Colors.black.withValues(alpha: 0.3), blurRadius: 10, offset: const Offset(0, 4)),
                      ],
                    ),
                  )
                      .animate()
                      .fadeIn(delay: 400.ms, duration: 600.ms)
                      .slideY(begin: 0.3, end: 0, delay: 400.ms, duration: 600.ms, curve: Curves.easeOut)
                      .shimmer(delay: 1200.ms, duration: 1800.ms, color: Colors.white.withValues(alpha: 0.3)),

                  const SizedBox(height: 10),

                  Text(
                    'Trusted Supply Chain',
                    style: GoogleFonts.poppins(
                      fontSize: 15,
                      fontWeight: FontWeight.w400,
                      color: Colors.white.withValues(alpha: 0.8),
                      letterSpacing: 3,
                    ),
                  )
                      .animate()
                      .fadeIn(delay: 800.ms, duration: 600.ms)
                      .slideY(begin: 0.5, end: 0, delay: 800.ms, duration: 600.ms),

                  const SizedBox(height: 60),

                  // Loading indicator — fills visibly over 3.5s
                  AnimatedBuilder(
                    animation: _progressController,
                    builder: (_, __) => Column(
                      children: [
                        SizedBox(
                          width: 200,
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(10),
                            child: LinearProgressIndicator(
                              value: _progressController.value,
                              backgroundColor: Colors.white.withValues(alpha: 0.15),
                              valueColor: const AlwaysStoppedAnimation<Color>(Color(0xFF4CAF50)),
                              minHeight: 5,
                            ),
                          ),
                        ),
                        const SizedBox(height: 12),
                        Text(
                          _loadingLabel(_progressController.value),
                          style: GoogleFonts.poppins(
                            fontSize: 11,
                            color: Colors.white.withValues(alpha: 0.55),
                            letterSpacing: 0.5,
                          ),
                        ),
                      ],
                    ),
                  ).animate().fadeIn(delay: 1000.ms, duration: 500.ms),
                ],
              ),
            ),

            // Bottom tagline
            Positioned(
              bottom: 50,
              left: 0,
              right: 0,
              child: Text(
                'Empowering Spaza Shops Across South Africa 🇿🇦',
                textAlign: TextAlign.center,
                style: GoogleFonts.poppins(fontSize: 12, color: Colors.white.withValues(alpha: 0.5), letterSpacing: 0.5),
              ).animate().fadeIn(delay: 1500.ms, duration: 800.ms),
            ),
          ],
        ),
      ),
    );
  }
}

class _FloatingParticle extends StatelessWidget {
  final int index;
  final AnimationController controller;
  const _FloatingParticle({required this.index, required this.controller});

  @override
  Widget build(BuildContext context) {
    final random = Random(index);
    final size = random.nextDouble() * 6 + 2;
    final startX = random.nextDouble() * MediaQuery.of(context).size.width;
    final startY = random.nextDouble() * MediaQuery.of(context).size.height;
    final dx = (random.nextDouble() - 0.5) * 80;
    final dy = (random.nextDouble() - 0.5) * 80;

    return AnimatedBuilder(
      animation: controller,
      builder: (_, __) {
        final t = (controller.value + index / 20) % 1.0;
        return Positioned(
          left: startX + dx * sin(t * 2 * pi),
          top: startY + dy * cos(t * 2 * pi),
          child: Container(
            width: size,
            height: size,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: Colors.white.withValues(alpha: 0.1 + 0.15 * sin(t * pi)),
            ),
          ),
        );
      },
    );
  }
}
