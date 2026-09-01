import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:pin_code_fields/pin_code_fields.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';

class OtpScreen extends StatefulWidget {
  const OtpScreen({super.key});

  @override
  State<OtpScreen> createState() => _OtpScreenState();
}

class _OtpScreenState extends State<OtpScreen> {
  String _otp = '';
  bool _isLoading = false;
  int _resendTimer = 30;
  final _pinController = TextEditingController();
  bool _autoFilled = false;
  Map<String, dynamic>? _savedArgs;

  @override
  void initState() {
    super.initState();
    _startTimer();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Save args on first access (they can be lost on web after rebuild)
    if (_savedArgs == null) {
      _savedArgs = (ModalRoute.of(context)?.settings.arguments as Map<String, dynamic>?) ?? {};
      print('[OTP] Args received: phone=${_savedArgs!['phone']}, purpose=${_savedArgs!['purpose']}, hasOtp=${_savedArgs!.containsKey('otp')}');
    }
    // Auto-fill OTP if provided (QA/dev mode)
    if (!_autoFilled) {
      final autoOtp = _savedArgs!['otp'] as String?;
      if (autoOtp != null && autoOtp.length == 6) {
        _autoFilled = true;
        Future.delayed(const Duration(milliseconds: 500), () {
          if (mounted) {
            _pinController.text = autoOtp;
            setState(() => _otp = autoOtp);
          }
        });
      }
    }
  }

  @override
  void dispose() {
    _pinController.dispose();
    super.dispose();
  }

  void _startTimer() {
    Future.doWhile(() async {
      await Future.delayed(const Duration(seconds: 1));
      if (!mounted) return false;
      if (_resendTimer > 0) {
        setState(() => _resendTimer--);
        return true;
      }
      return false;
    });
  }

  Map<String, dynamic> _args(BuildContext context) => _savedArgs ?? {};

  Future<void> _verify(BuildContext context) async {
    if (_isLoading) return;
    // Use pin controller text as source of truth
    final otpToVerify = _pinController.text.isNotEmpty ? _pinController.text : _otp;
    if (otpToVerify.length != 6) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('Please enter the full 6-digit code'),
          backgroundColor: AppColors.warning,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
      );
      return;
    }

    final args = _args(context);
    final phone = args['phone'] as String? ?? '';
    final purpose = args['purpose'] as String? ?? 'login';
    print('[OTP] Verifying: phone=$phone, purpose=$purpose, otp=$otpToVerify');

    if (phone.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('Phone number missing. Please go back and try again.'),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
      );
      return;
    }

    setState(() => _isLoading = true);
    try {
      if (purpose == 'login') {
        await context.read<AuthProvider>().verifyLogin(phone, otpToVerify);
      } else {
        await context.read<AuthProvider>().verifyRegister(
          phone: phone,
          otp: otpToVerify,
          fullName: args['fullName'] as String? ?? '',
          shopName: args['shopName'] as String? ?? '',
          address: args['address'] as String? ?? '',
          idNumber: args['idNumber'] as String?,
          documents: args['documents'] as Map<String, dynamic>?,
        );
      }
      if (!mounted) return;
      Navigator.pushNamedAndRemoveUntil(context, '/home', (r) => false);
    } catch (e) {
      if (!mounted) return;
      setState(() => _isLoading = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.toString()),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
      );
    }
  }

  Future<void> _resend(BuildContext context) async {
    final args = _args(context);
    final phone = args['phone'] as String? ?? '';
    final purpose = args['purpose'] as String? ?? 'login';
    try {
      String? newOtp;
      if (purpose == 'login') {
        newOtp = await context.read<AuthProvider>().sendLoginOtp(phone);
      } else {
        newOtp = await context.read<AuthProvider>().sendRegisterOtp(phone);
      }
      setState(() => _resendTimer = 30);
      _startTimer();

      // Auto-fill resent OTP
      if (newOtp != null && newOtp.length == 6 && mounted) {
        _pinController.text = newOtp;
        setState(() => _otp = newOtp!);
      }

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('OTP resent successfully'),
          backgroundColor: AppColors.success,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(e.toString()),
          backgroundColor: AppColors.error,
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final args = _args(context);
    final phone = args['phone'] as String? ?? '';
    final hasOtp = _otp.length == 6;

    return Scaffold(
      body: Container(
        width: double.infinity,
        height: double.infinity,
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [
              Color(0xFF0A2E0C),
              Color(0xFF144417),
              Color(0xFF1B5E20),
            ],
          ),
        ),
        child: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 28),
            child: Column(
              children: [
                const SizedBox(height: 30),

                // ── Back button ──
                Align(
                  alignment: Alignment.centerLeft,
                  child: GestureDetector(
                    onTap: () => Navigator.pop(context),
                    child: Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: Colors.white.withOpacity(0.1),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: Colors.white.withOpacity(0.2)),
                      ),
                      child: const Icon(Icons.arrow_back_rounded, color: Colors.white, size: 20),
                    ),
                  ),
                ).animate().fadeIn(duration: 400.ms),

                const SizedBox(height: 30),

                // ── Icon ──
                Container(
                  width: 80,
                  height: 80,
                  decoration: BoxDecoration(
                    color: Colors.white,
                    shape: BoxShape.circle,
                    boxShadow: [
                      BoxShadow(
                        color: const Color(0xFF4CAF50).withOpacity(0.3),
                        blurRadius: 30,
                        spreadRadius: 5,
                      ),
                    ],
                  ),
                  child: const Icon(
                    Icons.verified_user_rounded,
                    color: AppColors.primary,
                    size: 38,
                  ),
                ).animate().fadeIn(duration: 600.ms).scale(
                    begin: const Offset(0.8, 0.8),
                    end: const Offset(1, 1),
                    duration: 600.ms,
                    curve: Curves.easeOutBack),

                const SizedBox(height: 20),

                // ── Title ──
                Text(
                  'Verification',
                  style: GoogleFonts.poppins(
                    fontSize: 26,
                    fontWeight: FontWeight.w700,
                    color: Colors.white,
                  ),
                ).animate().fadeIn(delay: 200.ms),

                const SizedBox(height: 6),

                Text(
                  phone.isNotEmpty
                      ? 'Code sent to $phone'
                      : 'Enter the code we sent you',
                  style: GoogleFonts.poppins(
                    fontSize: 13,
                    color: const Color(0xFF81C784),
                    letterSpacing: 0.3,
                  ),
                ).animate().fadeIn(delay: 300.ms),

                const SizedBox(height: 40),

                // ── OTP Card ──
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(24),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(24),
                    boxShadow: [
                      BoxShadow(
                        color: Colors.black.withOpacity(0.15),
                        blurRadius: 30,
                        offset: const Offset(0, 12),
                      ),
                    ],
                  ),
                  child: Column(
                    children: [
                      // Card title
                      Text(
                        'Enter 6-Digit Code',
                        style: GoogleFonts.poppins(
                          fontSize: 18,
                          fontWeight: FontWeight.w700,
                          color: const Color(0xFF1A1A1A),
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        _otp.length == 6
                            ? 'Code filled — tap Verify to continue'
                            : 'Check your SMS messages',
                        style: GoogleFonts.poppins(
                          fontSize: 13,
                          color: _otp.length == 6
                              ? AppColors.primary
                              : const Color(0xFF757575),
                          fontWeight: _otp.length == 6
                              ? FontWeight.w600
                              : FontWeight.w400,
                        ),
                      ),

                      const SizedBox(height: 28),

                      // ── PIN Input ──
                      PinCodeTextField(
                        appContext: context,
                        length: 6,
                        controller: _pinController,
                        onChanged: (v) {
                          setState(() => _otp = v);
                        },
                        onCompleted: (_) => _verify(context),
                        pinTheme: PinTheme(
                          shape: PinCodeFieldShape.box,
                          borderRadius: BorderRadius.circular(12),
                          fieldHeight: 54,
                          fieldWidth: 44,
                          activeFillColor: const Color(0xFFF0F9F0),
                          inactiveFillColor: const Color(0xFFF5F5F5),
                          selectedFillColor: const Color(0xFFE8F5E9),
                          activeColor: AppColors.primary,
                          inactiveColor: const Color(0xFFE0E0E0),
                          selectedColor: AppColors.primaryLight,
                        ),
                        enableActiveFill: true,
                        keyboardType: TextInputType.number,
                        animationType: AnimationType.scale,
                        textStyle: GoogleFonts.poppins(
                          color: const Color(0xFF1A1A1A),
                          fontSize: 22,
                          fontWeight: FontWeight.w700,
                        ),
                        cursorColor: AppColors.primary,
                      ),

                      const SizedBox(height: 20),

                      // ── Resend section ──
                      _resendTimer > 0
                          ? Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                const Icon(Icons.timer_outlined,
                                    size: 16, color: Color(0xFF9E9E9E)),
                                const SizedBox(width: 6),
                                Text(
                                  'Resend in ${_resendTimer}s',
                                  style: GoogleFonts.poppins(
                                    color: const Color(0xFF9E9E9E),
                                    fontSize: 13,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                              ],
                            )
                          : GestureDetector(
                              onTap: () => _resend(context),
                              child: Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  const Icon(Icons.refresh_rounded,
                                      size: 18, color: AppColors.primary),
                                  const SizedBox(width: 6),
                                  Text(
                                    'Resend Code',
                                    style: GoogleFonts.poppins(
                                      color: AppColors.primary,
                                      fontWeight: FontWeight.w600,
                                      fontSize: 14,
                                    ),
                                  ),
                                ],
                              ),
                            ),

                      const SizedBox(height: 28),

                      // ── Verify Button ──
                      SizedBox(
                        width: double.infinity,
                        height: 54,
                        child: ElevatedButton(
                          onPressed: _isLoading ? null : () => _verify(context),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: hasOtp
                                ? AppColors.primary
                                : const Color(0xFF9E9E9E),
                            disabledBackgroundColor: AppColors.primary.withOpacity(0.5),
                            foregroundColor: Colors.white,
                            elevation: hasOtp ? 4 : 0,
                            shadowColor: AppColors.primary.withOpacity(0.3),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(14),
                            ),
                          ),
                          child: _isLoading
                              ? const SizedBox(
                                  width: 22,
                                  height: 22,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2.5,
                                    valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
                                  ),
                                )
                              : Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Text(
                                      'Verify & Continue',
                                      style: GoogleFonts.poppins(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                    const SizedBox(width: 8),
                                    const Icon(Icons.arrow_forward_rounded, size: 20),
                                  ],
                                ),
                        ),
                      ),

                      // ── Dev hint ──
                      if (_otp.length < 6) ...[
                        const SizedBox(height: 16),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                          decoration: BoxDecoration(
                            color: const Color(0xFFFFF3E0),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(Icons.lightbulb_outline, size: 16, color: Color(0xFFE65100)),
                              const SizedBox(width: 6),
                              Text(
                                'OTP will auto-fill from server',
                                style: GoogleFonts.poppins(
                                  fontSize: 12,
                                  fontWeight: FontWeight.w600,
                                  color: const Color(0xFFE65100),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ],
                  ),
                )
                    .animate()
                    .fadeIn(delay: 400.ms, duration: 500.ms)
                    .slideY(begin: 0.1, end: 0, delay: 400.ms, duration: 500.ms),

                const SizedBox(height: 32),

                // ── Security note ──
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.shield_outlined,
                        size: 14, color: Colors.white.withOpacity(0.5)),
                    const SizedBox(width: 6),
                    Text(
                      'Your code expires in 5 minutes',
                      style: GoogleFonts.poppins(
                        fontSize: 11,
                        color: Colors.white.withOpacity(0.5),
                      ),
                    ),
                  ],
                ).animate().fadeIn(delay: 600.ms),

                const SizedBox(height: 40),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
