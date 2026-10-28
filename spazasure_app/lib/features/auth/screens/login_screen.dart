import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/animated_logo.dart';
import 'package:spazasure_app/core/widgets/brand_pattern.dart';
import 'package:spazasure_app/core/widgets/motion.dart';
import 'package:spazasure_app/providers/auth_provider.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _phoneController = TextEditingController();
  final _phoneFocus = FocusNode();
  final _passwordController = TextEditingController();
  bool _isLoading = false;
  bool _customerLogin = false;
  bool _obscurePassword = true;
  String? _phoneError;

  @override
  void dispose() {
    _phoneController.dispose();
    _phoneFocus.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _handleSignIn() async {
    final phone = _phoneController.text.trim();
    final password = _passwordController.text;
    if (phone.isEmpty) {
      setState(() => _phoneError = 'Enter your phone number');
      _phoneFocus.requestFocus();
      return;
    }
    if (phone.length < 9) {
      setState(() => _phoneError = 'Phone number looks too short');
      _phoneFocus.requestFocus();
      return;
    }
    if (password.length < 8) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Password must be at least 8 characters')),
      );
      return;
    }
    setState(() {
      _phoneError = null;
      _isLoading = true;
    });
    try {
      final auth = context.read<AuthProvider>();
      if (_customerLogin) {
        await auth.loginCustomer(phone, password);
      } else {
        await auth.loginShop(phone, password);
      }
      if (!mounted) return;
      Navigator.pushNamedAndRemoveUntil(
        context,
        _customerLogin ? '/customer-home' : '/home',
        (route) => false,
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.toString()),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
        ),
      );
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  // Retailers sign in on green (suppliers and shops), customers on blue, like the welcome pages
  Color get _modeColor =>
      _customerLogin ? AppColors.primary : AppColors.secondary;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 500),
      curve: Curves.easeOutCubic,
      color: _modeColor,
      child: Scaffold(
        backgroundColor: Colors.transparent,
        body: Stack(
          children: [
            // Soft glows, as on the welcome pages
            Positioned(top: -90, right: -70, child: _glow(280)),
            Positioned(top: 300, left: -110, child: _glow(220)),
            SafeArea(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
                child: Column(
                  children: [
                    _hero(),
                    const SizedBox(height: 18),
                    _loginCard(),
                    const SizedBox(height: 22),
                    _links(),
                  ],
                ),
              ),
            ),
          ],
        ),
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

  /// The logo builds itself on top of a fanned hand of brand cards.
  Widget _hero() => SizedBox(
    width: double.infinity,
    height: 250,
    child: Stack(
      clipBehavior: Clip.none,
      alignment: Alignment.center,
      children: [
        _HeroCards(
          front: _customerLogin ? AppColors.secondary : AppColors.primary,
        ),
        Positioned(top: 30, child: AnimatedLogo(size: 118, delay: 250.ms)),
        Positioned(
          bottom: 10,
          child: BouncyWordmark(
            delay: 1100.ms,
            highlight: AppColors.accent,
            style: GoogleFonts.nunito(
              fontSize: 32,
              fontWeight: FontWeight.w900,
              color: Colors.white,
              shadows: [
                Shadow(
                  color: Colors.black.withValues(alpha: 0.25),
                  blurRadius: 10,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
          ),
        ),
      ],
    ),
  );

  Widget _loginCard() =>
      Container(
            width: double.infinity,
            padding: const EdgeInsets.fromLTRB(22, 22, 22, 20),
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
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Title swaps in when switching who's signing in
                AnimatedSwitcher(
                  duration: const Duration(milliseconds: 300),
                  transitionBuilder: (child, anim) => FadeTransition(
                    opacity: anim,
                    child: SlideTransition(
                      position: Tween(
                        begin: const Offset(0, 0.2),
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
                    key: ValueKey(_customerLogin),
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        _customerLogin
                            ? 'Customer Sign In'
                            : 'Retailer Sign In',
                        style: GoogleFonts.nunito(
                          fontSize: 25,
                          fontWeight: FontWeight.w900,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        _customerLogin
                            ? 'Sign in to verify products and track reports'
                            : 'Enter your shop phone number and password',
                        style: GoogleFonts.nunito(
                          fontSize: 13.5,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 18),
                _modeSwitch(),
                const SizedBox(height: 22),

                Text(
                  'Phone Number',
                  style: GoogleFonts.nunito(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 8),
                AnimatedContainer(
                  duration: const Duration(milliseconds: 200),
                  height: 56,
                  decoration: BoxDecoration(
                    color: AppColors.background,
                    borderRadius: BorderRadius.circular(18),
                    border: Border.all(
                      color: _phoneError != null
                          ? AppColors.error
                          : AppColors.divider,
                      width: _phoneError != null ? 1.6 : 1,
                    ),
                  ),
                  child: Row(
                    children: [
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 14),
                        child: Row(
                          children: [
                            const Text('🇿🇦', style: TextStyle(fontSize: 22)),
                            const SizedBox(width: 8),
                            Text(
                              '+27',
                              style: GoogleFonts.nunito(
                                fontSize: 15,
                                fontWeight: FontWeight.w700,
                                color: AppColors.textPrimary,
                              ),
                            ),
                          ],
                        ),
                      ),
                      Container(width: 1, height: 28, color: AppColors.divider),
                      Expanded(
                        child: TextField(
                          controller: _phoneController,
                          focusNode: _phoneFocus,
                          keyboardType: TextInputType.phone,
                          textInputAction: TextInputAction.done,
                          autofillHints: const [
                            AutofillHints.telephoneNumberNational,
                          ],
                          inputFormatters: [
                            FilteringTextInputFormatter.digitsOnly,
                            LengthLimitingTextInputFormatter(10),
                          ],
                          onChanged: (_) {
                            if (_phoneError != null) {
                              setState(() => _phoneError = null);
                            }
                          },
                          style: GoogleFonts.nunito(
                            fontSize: 16,
                            fontWeight: FontWeight.w600,
                            color: AppColors.textPrimary,
                          ),
                          cursorColor: _modeColor,
                          onSubmitted: (_) => _handleSignIn(),
                          decoration: InputDecoration(
                            hintText: '81 234 5678',
                            hintStyle: GoogleFonts.nunito(
                              color: AppColors.textHint,
                              fontSize: 15,
                            ),
                            border: InputBorder.none,
                            enabledBorder: InputBorder.none,
                            focusedBorder: InputBorder.none,
                            filled: false,
                            contentPadding: const EdgeInsets.symmetric(
                              horizontal: 14,
                              vertical: 16,
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                // A wrong number gives the field a little shake
                if (_phoneError != null)
                  Padding(
                        padding: const EdgeInsets.only(top: 8),
                        child: Row(
                          children: [
                            const Icon(
                              Icons.error_outline_rounded,
                              size: 14,
                              color: AppColors.error,
                            ),
                            const SizedBox(width: 6),
                            Flexible(
                              child: Text(
                                _phoneError!,
                                style: GoogleFonts.nunito(
                                  fontSize: 12,
                                  fontWeight: FontWeight.w700,
                                  color: AppColors.error,
                                ),
                              ),
                            ),
                          ],
                        ),
                      )
                      .animate(key: ValueKey(_phoneError))
                      .shakeX(hz: 5, amount: 4, duration: 350.ms),

                const SizedBox(height: 16),
                TextField(
                  controller: _passwordController,
                  obscureText: _obscurePassword,
                  textInputAction: TextInputAction.done,
                  onSubmitted: (_) => _handleSignIn(),
                  decoration: InputDecoration(
                    labelText: 'Password',
                    prefixIcon: const Icon(Icons.lock_outline),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(18),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(18),
                      borderSide: const BorderSide(color: AppColors.divider),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(18),
                      borderSide: BorderSide(color: _modeColor, width: 1.6),
                    ),
                    filled: true,
                    fillColor: AppColors.background,
                    suffixIcon: IconButton(
                      onPressed: () =>
                          setState(() => _obscurePassword = !_obscurePassword),
                      icon: Icon(
                        _obscurePassword
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  'Forgot your password? Contact SpazaSure support.',
                  style: GoogleFonts.nunito(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 22),

                // Dark pill button, like "Next" on the welcome pages
                Pressable(
                  onTap: _isLoading ? null : _handleSignIn,
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 250),
                    width: double.infinity,
                    height: 56,
                    decoration: BoxDecoration(
                      color: _isLoading
                          ? AppColors.textPrimary.withValues(alpha: 0.6)
                          : AppColors.textPrimary,
                      borderRadius: BorderRadius.circular(28),
                    ),
                    child: Center(
                      child: _isLoading
                          ? const SizedBox(
                              width: 22,
                              height: 22,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.5,
                                valueColor: AlwaysStoppedAnimation<Color>(
                                  Colors.white,
                                ),
                              ),
                            )
                          : Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Text(
                                  'Sign In',
                                  style: GoogleFonts.nunito(
                                    fontSize: 16,
                                    fontWeight: FontWeight.w800,
                                    color: Colors.white,
                                  ),
                                ),
                                const SizedBox(width: 8),
                                const Icon(
                                  Icons.arrow_forward_rounded,
                                  color: Colors.white,
                                  size: 21,
                                ),
                              ],
                            ),
                    ),
                  ),
                ),
              ],
            ),
          )
          .animate()
          .fadeIn(delay: 500.ms, duration: 450.ms)
          .slideY(
            begin: 0.2,
            end: 0,
            delay: 500.ms,
            duration: 550.ms,
            curve: Curves.easeOutCubic,
          );

  /// Retailer / Customer, with a pill that slides to the chosen side.
  Widget _modeSwitch() => Semantics(
    label: 'Sign in as',
    child: Container(
      height: 48,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(24),
      ),
      child: Stack(
        children: [
          AnimatedAlign(
            duration: const Duration(milliseconds: 350),
            curve: Curves.easeOutBack,
            alignment: _customerLogin
                ? Alignment.centerRight
                : Alignment.centerLeft,
            child: FractionallySizedBox(
              widthFactor: 0.5,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 350),
                decoration: BoxDecoration(
                  color: _modeColor,
                  borderRadius: BorderRadius.circular(20),
                  boxShadow: [
                    BoxShadow(
                      color: _modeColor.withValues(alpha: 0.35),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
              ),
            ),
          ),
          Row(
            children: [
              _modeOption(false, Icons.storefront_outlined, 'Retailer'),
              _modeOption(true, Icons.person_outline, 'Customer'),
            ],
          ),
        ],
      ),
    ),
  );

  Widget _modeOption(bool customer, IconData icon, String label) {
    final selected = _customerLogin == customer;
    final color = selected ? Colors.white : AppColors.ink2;
    return Expanded(
      child: Semantics(
        button: true,
        selected: selected,
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: () => setState(() {
            _customerLogin = customer;
            _phoneError = null;
          }),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 18, color: color),
              const SizedBox(width: 6),
              AnimatedDefaultTextStyle(
                duration: const Duration(milliseconds: 250),
                style: GoogleFonts.nunito(
                  fontSize: 14,
                  fontWeight: FontWeight.w800,
                  color: color,
                ),
                child: Text(label),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _links() => Column(
    children: [
      Wrap(
        alignment: WrapAlignment.center,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text(
            "Don't have an account? ",
            style: GoogleFonts.nunito(
              color: Colors.white.withValues(alpha: 0.85),
              fontSize: 14,
            ),
          ),
          InkWell(
            onTap: () => Navigator.pushNamed(context, '/register'),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Text(
                'Register',
                style: GoogleFonts.nunito(
                  color: Colors.white,
                  fontWeight: FontWeight.w900,
                  fontSize: 14,
                  decoration: TextDecoration.underline,
                  decorationColor: Colors.white,
                ),
              ),
            ),
          ),
        ],
      ).animate().fadeIn(delay: 800.ms),
      Pressable(
        onTap: () => Navigator.pushNamed(context, '/customer-register'),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 11),
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.18),
            borderRadius: BorderRadius.circular(22),
            border: Border.all(color: Colors.white.withValues(alpha: 0.3)),
          ),
          child: Text(
            'I am a customer: create an account',
            textAlign: TextAlign.center,
            style: GoogleFonts.nunito(
              color: Colors.white,
              fontSize: 13,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
      ).animate().fadeIn(delay: 900.ms),
      const SizedBox(height: 26),
      Text(
        'Empowering Spaza Shops Across South Africa 🇿🇦',
        style: GoogleFonts.nunito(
          fontSize: 12,
          color: Colors.white.withValues(alpha: 0.75),
        ),
        textAlign: TextAlign.center,
      ).animate().fadeIn(delay: 1000.ms),
    ],
  );
}

/// Three brand cards fanned behind the logo: dealt out from the middle, then
/// floating gently. The front card takes the sign-in colour.
class _HeroCards extends StatefulWidget {
  final Color front;
  const _HeroCards({required this.front});

  @override
  State<_HeroCards> createState() => _HeroCardsState();
}

class _HeroCardsState extends State<_HeroCards> with TickerProviderStateMixin {
  late final AnimationController _deal = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 800),
  );
  late final AnimationController _float = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 2600),
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_deal.isAnimating || _deal.isCompleted) return;
    if (MediaQuery.maybeOf(context)?.disableAnimations ?? false) {
      _deal.value = 1;
    } else {
      _deal.forward();
      _float.repeat(reverse: true);
    }
  }

  @override
  void dispose() {
    _deal.dispose();
    _float.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: Listenable.merge([_deal, _float]),
    builder: (_, __) {
      final bob = sin(_float.value * pi) * 6;
      return Stack(
        clipBehavior: Clip.none,
        alignment: Alignment.center,
        children: [
          _card(
            0,
            AppColors.accent,
            const [AppColors.red, AppColors.primaryDark, Colors.white],
            -0.22,
            const Offset(-58, 6),
            -bob,
          ),
          _card(
            1,
            AppColors.red,
            const [AppColors.accent, AppColors.primaryDark, Colors.white],
            0.2,
            const Offset(58, 6),
            bob,
          ),
          _card(
            2,
            widget.front,
            const [AppColors.accent, AppColors.secondaryLight, Colors.white],
            0,
            const Offset(0, -6),
            -bob * 0.6,
          ),
        ],
      );
    },
  );

  Widget _card(
    int i,
    Color color,
    List<Color> pattern,
    double angle,
    Offset offset,
    double bob,
  ) {
    // Each card starts a little after the one before
    final t = ((_deal.value - i * 0.1) / 0.7).clamp(0.0, 1.0);
    final v = Curves.easeOutBack.transform(t);
    return Opacity(
      opacity: (t * 3).clamp(0.0, 1.0),
      child: Transform.translate(
        offset: offset * v + Offset(0, 40 * (1 - v) + bob),
        child: Transform.rotate(
          angle: angle * v,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 500),
            width: 150,
            height: 190,
            decoration: BoxDecoration(
              color: color,
              borderRadius: BorderRadius.circular(26),
              border: Border.all(color: Colors.white, width: 3),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.2),
                  blurRadius: 22,
                  offset: const Offset(0, 12),
                ),
              ],
            ),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(23),
              child: CustomPaint(painter: BrandPattern(pattern)),
            ),
          ),
        ),
      ),
    );
  }
}
