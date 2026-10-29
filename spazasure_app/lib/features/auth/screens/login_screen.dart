import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/animated_logo.dart';
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

  // Retailers and customers both sign in on brand blue
  Color get _modeColor => AppColors.primary;

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
  Widget _hero() => Padding(
    padding: const EdgeInsets.only(top: 18),
    child: Column(
      children: [
        Stack(
          alignment: Alignment.center,
          children: [
            // Soft rings behind the logo
            for (final (size, alpha) in const [(190.0, 0.08), (156.0, 0.12)])
              Container(
                width: size,
                height: size,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: Colors.white.withValues(alpha: alpha),
                ),
              ).animate().scaleXY(
                begin: 0.6,
                end: 1,
                duration: 700.ms,
                curve: Curves.easeOutBack,
              ),
            AnimatedLogo(size: 122, delay: 150.ms),
          ],
        ),
        const SizedBox(height: 14),
        BouncyWordmark(
          delay: 1000.ms,
          highlight: AppColors.accent,
          style: GoogleFonts.nunito(
            fontSize: 32,
            fontWeight: FontWeight.w900,
            color: Colors.white,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          'Trusted Supply Chain',
          style: GoogleFonts.nunito(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            letterSpacing: 2,
            color: Colors.white.withValues(alpha: 0.85),
          ),
        ).animate().fadeIn(delay: 1600.ms, duration: 500.ms),
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

                // Brand blue pill button
                Pressable(
                  onTap: _isLoading ? null : _handleSignIn,
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 250),
                    width: double.infinity,
                    height: 56,
                    decoration: BoxDecoration(
                      color: _isLoading
                          ? AppColors.primary.withValues(alpha: 0.6)
                          : AppColors.primary,
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
      height: 50,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(25),
        border: Border.all(color: AppColors.divider),
      ),
      child: LayoutBuilder(
        builder: (context, box) {
          final half = box.maxWidth / 2;
          return Stack(
            children: [
              AnimatedPositioned(
                duration: const Duration(milliseconds: 300),
                curve: Curves.easeOutCubic,
                left: _customerLogin ? half : 0,
                top: 0,
                bottom: 0,
                width: half,
                child: AnimatedContainer(
                  duration: const Duration(milliseconds: 300),
                  decoration: BoxDecoration(
                    color: _modeColor,
                    borderRadius: BorderRadius.circular(21),
                  ),
                ),
              ),
              // Full height, so the labels sit in the middle of the pill
              Positioned.fill(
                child: Row(
                  children: [
                    _modeOption(false, Icons.storefront_outlined, 'Retailer'),
                    _modeOption(true, Icons.person_outline, 'Customer'),
                  ],
                ),
              ),
            ],
          );
        },
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
