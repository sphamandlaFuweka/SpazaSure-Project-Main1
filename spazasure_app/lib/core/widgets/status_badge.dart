import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';

class StatusBadge extends StatelessWidget {
  final String status;

  const StatusBadge({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: _color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        _label,
        style: AppTextStyles.caption.copyWith(
          color: _color,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }

  String get _label => status.replaceAll('_', ' ').toUpperCase();

  Color get _color => switch (status) {
        'pending' => AppColors.statusPending,
        'confirmed' => AppColors.statusConfirmed,
        'processing' => AppColors.statusProcessing,
        'dispatched' => AppColors.statusDispatched,
        'delivered' => AppColors.statusDelivered,
        'cancelled' => AppColors.statusCancelled,
        'disputed' => AppColors.error,
        'approved' => AppColors.success,
        'rejected' => AppColors.error,
        'verified' => AppColors.success,
        _ => AppColors.textSecondary,
      };
}

