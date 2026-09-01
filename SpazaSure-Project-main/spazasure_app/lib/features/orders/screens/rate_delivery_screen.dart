import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/models/models.dart';
import 'package:spazasure_app/services/api_service.dart';

class RateDeliveryScreen extends StatefulWidget {
  const RateDeliveryScreen({super.key});

  @override
  State<RateDeliveryScreen> createState() => _RateDeliveryScreenState();
}

class _RateDeliveryScreenState extends State<RateDeliveryScreen>
    with SingleTickerProviderStateMixin {
  int _deliveryRating = 0;
  int _supplierRating = 0;
  final _commentController = TextEditingController();
  bool _submitted = false;
  late AnimationController _successController;

  @override
  void initState() {
    super.initState();
    _successController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 800),
    );
  }

  @override
  void dispose() {
    _commentController.dispose();
    _successController.dispose();
    super.dispose();
  }

  void _submit() async {
    if (_deliveryRating == 0 || _supplierRating == 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('Please rate both delivery and supplier'),
          backgroundColor: AppColors.warning,
          behavior: SnackBarBehavior.floating,
          shape:
              RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
      );
      return;
    }

    final order = ModalRoute.of(context)?.settings.arguments as Order?;
    if (order == null) return;

    try {
      await ApiService.post('/shop/orders/${order.id}/rate', {
        'deliveryRating': _deliveryRating,
        'supplierRating': _supplierRating,
        'comment': _commentController.text.trim(),
      });
    } catch (_) {
      // Still show success even if API fails — rating is non-critical
    }

    setState(() => _submitted = true);
    _successController.forward();
    await Future.delayed(const Duration(seconds: 3));
    if (!mounted) return;
    Navigator.pushNamedAndRemoveUntil(context, '/home', (r) => false);
  }

  @override
  Widget build(BuildContext context) {
    final order = ModalRoute.of(context)?.settings.arguments as Order?;

    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [Color(0xFF0D3B0F), Color(0xFF1B5E20), Color(0xFF2E7D32)],
          ),
        ),
        child: SafeArea(
          child: _submitted ? _buildSuccess() : _buildForm(order),
        ),
      ),
    );
  }

  Widget _buildForm(Order? order) {
    return Column(
      children: [
        // Header
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          child: Row(
            children: [
              GestureDetector(
                onTap: () => Navigator.pop(context),
                child: Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(14),
                    border: Border.all(
                        color: Colors.white.withValues(alpha: 0.15)),
                  ),
                  child: const Icon(Icons.close_rounded,
                      color: Colors.white, size: 22),
                ),
              ),
              const Spacer(),
              Text('Rate Your Experience',
                  style: GoogleFonts.poppins(
                      fontSize: 18,
                      fontWeight: FontWeight.w600,
                      color: Colors.white)),
              const Spacer(),
              const SizedBox(width: 44),
            ],
          ),
        ),
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Column(
              children: [
                // Order info
                if (order != null)
                  _glassCard(
                    child: Row(
                      children: [
                        Container(
                          width: 48,
                          height: 48,
                          decoration: BoxDecoration(
                            color: AppColors.success.withValues(alpha: 0.2),
                            borderRadius: BorderRadius.circular(14),
                          ),
                          child: const Icon(Icons.check_circle_rounded,
                              color: AppColors.success, size: 26),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Order Delivered!',
                                  style: GoogleFonts.poppins(
                                      fontSize: 16,
                                      fontWeight: FontWeight.w700,
                                      color: Colors.white)),
                              Text(
                                  '${order.orderNumber} • ${order.supplierName}',
                                  style: GoogleFonts.poppins(
                                      fontSize: 12,
                                      color: Colors.white
                                          .withValues(alpha: 0.6))),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ).animate().fadeIn().slideY(begin: 0.1, end: 0),

                const SizedBox(height: 16),

                // Delivery rating
                _glassCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Container(
                            width: 44,
                            height: 44,
                            decoration: BoxDecoration(
                              color: AppColors.info.withValues(alpha: 0.2),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: const Icon(Icons.local_shipping_rounded,
                                color: AppColors.info, size: 22),
                          ),
                          const SizedBox(width: 12),
                          Text('Delivery Rating',
                              style: GoogleFonts.poppins(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w600,
                                  color: Colors.white)),
                        ],
                      ),
                      const SizedBox(height: 16),
                      _buildStarRow(
                        rating: _deliveryRating,
                        onRate: (r) =>
                            setState(() => _deliveryRating = r),
                      ),
                      const SizedBox(height: 8),
                      Center(
                        child: Text(
                          _ratingLabel(_deliveryRating),
                          style: GoogleFonts.poppins(
                              fontSize: 13,
                              color: _deliveryRating > 0
                                  ? AppColors.warning
                                  : Colors.white
                                      .withValues(alpha: 0.4)),
                        ),
                      ),
                    ],
                  ),
                ).animate().fadeIn(delay: 100.ms).slideY(begin: 0.1, end: 0),

                const SizedBox(height: 16),

                // Supplier rating
                _glassCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Container(
                            width: 44,
                            height: 44,
                            decoration: BoxDecoration(
                              color:
                                  AppColors.secondary.withValues(alpha: 0.2),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: const Icon(Icons.business_rounded,
                                color: AppColors.secondary, size: 22),
                          ),
                          const SizedBox(width: 12),
                          Text('Supplier Rating',
                              style: GoogleFonts.poppins(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w600,
                                  color: Colors.white)),
                        ],
                      ),
                      const SizedBox(height: 16),
                      _buildStarRow(
                        rating: _supplierRating,
                        onRate: (r) =>
                            setState(() => _supplierRating = r),
                      ),
                      const SizedBox(height: 8),
                      Center(
                        child: Text(
                          _ratingLabel(_supplierRating),
                          style: GoogleFonts.poppins(
                              fontSize: 13,
                              color: _supplierRating > 0
                                  ? AppColors.warning
                                  : Colors.white
                                      .withValues(alpha: 0.4)),
                        ),
                      ),
                    ],
                  ),
                ).animate().fadeIn(delay: 200.ms).slideY(begin: 0.1, end: 0),

                const SizedBox(height: 16),

                // Comment
                _glassCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Leave a Comment (Optional)',
                          style: GoogleFonts.poppins(
                              fontSize: 14,
                              fontWeight: FontWeight.w600,
                              color: Colors.white)),
                      const SizedBox(height: 12),
                      Container(
                        decoration: BoxDecoration(
                          color: Colors.white.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(14),
                          border: Border.all(
                              color: Colors.white.withValues(alpha: 0.12)),
                        ),
                        child: TextField(
                          controller: _commentController,
                          maxLines: 3,
                          style: GoogleFonts.poppins(
                              color: Colors.white, fontSize: 14),
                          decoration: InputDecoration(
                            hintText:
                                'Share your experience with this order...',
                            hintStyle: GoogleFonts.poppins(
                                color:
                                    Colors.white.withValues(alpha: 0.35),
                                fontSize: 13),
                            border: InputBorder.none,
                            contentPadding: const EdgeInsets.all(16),
                          ),
                        ),
                      ),
                    ],
                  ),
                ).animate().fadeIn(delay: 300.ms).slideY(begin: 0.1, end: 0),

                const SizedBox(height: 24),

                // Submit button
                SizedBox(
                  width: double.infinity,
                  height: 56,
                  child: ElevatedButton.icon(
                    onPressed: _submit,
                    icon: const Icon(Icons.star_rounded, size: 22),
                    label: Text('Submit Rating',
                        style: AppTextStyles.button),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primaryLight,
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(16)),
                      elevation: 0,
                    ),
                  ),
                ).animate().fadeIn(delay: 400.ms),

                const SizedBox(height: 12),

                TextButton(
                  onPressed: () =>
                      Navigator.pushNamedAndRemoveUntil(
                          context, '/home', (r) => false),
                  child: Text('Skip for now',
                      style: AppTextStyles.body
                          .copyWith(color: Colors.white54)),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildStarRow(
      {required int rating, required ValueChanged<int> onRate}) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: List.generate(5, (i) {
        final filled = i < rating;
        return GestureDetector(
          onTap: () => onRate(i + 1),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6),
            child: AnimatedSwitcher(
              duration: 200.ms,
              child: Icon(
                filled ? Icons.star_rounded : Icons.star_outline_rounded,
                key: ValueKey(filled),
                color: filled ? AppColors.warning : Colors.white38,
                size: 40,
              ),
            ),
          ),
        );
      }),
    );
  }

  Widget _buildSuccess() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            // Animated checkmark
            AnimatedBuilder(
              animation: _successController,
              builder: (_, __) => Transform.scale(
                scale: _successController.value,
                child: Container(
                  width: 120,
                  height: 120,
                  decoration: BoxDecoration(
                    color: AppColors.success.withValues(alpha: 0.15),
                    shape: BoxShape.circle,
                    border: Border.all(
                        color: AppColors.success.withValues(alpha: 0.4),
                        width: 3),
                  ),
                  child: const Icon(Icons.star_rounded,
                      color: AppColors.warning, size: 64),
                ),
              ),
            ),
            const SizedBox(height: 28),
            Text('Thank You!',
                style: GoogleFonts.poppins(
                    fontSize: 28,
                    fontWeight: FontWeight.w800,
                    color: Colors.white))
                .animate()
                .fadeIn(delay: 400.ms)
                .slideY(begin: 0.3, end: 0, delay: 400.ms),
            const SizedBox(height: 12),
            Text(
              'Your feedback helps improve the SpazaSure experience for everyone.',
              style: GoogleFonts.poppins(
                  fontSize: 14,
                  color: Colors.white.withValues(alpha: 0.7)),
              textAlign: TextAlign.center,
            ).animate().fadeIn(delay: 500.ms),
            const SizedBox(height: 24),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(5, (i) {
                return Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  child: Icon(
                    i < _deliveryRating
                        ? Icons.star_rounded
                        : Icons.star_outline_rounded,
                    color: i < _deliveryRating
                        ? AppColors.warning
                        : Colors.white24,
                    size: 32,
                  ),
                );
              }),
            ).animate().fadeIn(delay: 600.ms),
            const SizedBox(height: 8),
            Text('Redirecting to home...',
                style: GoogleFonts.poppins(
                    fontSize: 12,
                    color: Colors.white.withValues(alpha: 0.4)))
                .animate()
                .fadeIn(delay: 700.ms),
          ],
        ),
      ),
    );
  }

  Widget _glassCard({required Widget child}) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Colors.white.withValues(alpha: 0.15)),
        boxShadow: [
          BoxShadow(
              color: Colors.black.withValues(alpha: 0.1),
              blurRadius: 16,
              offset: const Offset(0, 6))
        ],
      ),
      child: child,
    );
  }

  String _ratingLabel(int r) {
    switch (r) {
      case 1:
        return 'Poor 😞';
      case 2:
        return 'Fair 😐';
      case 3:
        return 'Good 🙂';
      case 4:
        return 'Very Good 😊';
      case 5:
        return 'Excellent! 🌟';
      default:
        return 'Tap to rate';
    }
  }
}
