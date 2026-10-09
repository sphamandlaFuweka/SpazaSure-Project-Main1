import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/customer_shop_service.dart';

class CustomerShopDetailScreen extends StatefulWidget {
  final CustomerShop shop;

  const CustomerShopDetailScreen({required this.shop, super.key});

  @override
  State<CustomerShopDetailScreen> createState() =>
      _CustomerShopDetailScreenState();
}

class _CustomerShopDetailScreenState extends State<CustomerShopDetailScreen> {
  List<Map<String, dynamic>> _reviews = [];
  bool _loadingReviews = true;

  CustomerShop get shop => widget.shop;

  @override
  void initState() {
    super.initState();
    _loadReviews();
  }

  Future<void> _loadReviews() async {
    try {
      final reviews = await CustomerShopService.reviews(shop.id);
      if (mounted) setState(() => _reviews = reviews);
    } finally {
      if (mounted) setState(() => _loadingReviews = false);
    }
  }

  Future<void> _writeReview() async {
    var rating = 5;
    final commentController = TextEditingController();
    final saved = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Review shop'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<int>(
                initialValue: rating,
                items: [1, 2, 3, 4, 5]
                    .map(
                      (value) => DropdownMenuItem(
                        value: value,
                        child: Text('$value stars'),
                      ),
                    )
                    .toList(),
                onChanged: (value) => setDialogState(() => rating = value ?? 5),
              ),
              TextField(
                controller: commentController,
                decoration: const InputDecoration(
                  labelText: 'Comment (optional)',
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () async {
                await CustomerShopService.review(
                  shop.id,
                  rating,
                  commentController.text,
                );
                if (context.mounted) Navigator.pop(context, true);
              },
              child: const Text('Save'),
            ),
          ],
        ),
      ),
    );
    commentController.dispose();
    if (saved == true) _loadReviews();
  }

  Future<void> _openDirections(BuildContext context) async {
    if (shop.latitude == null || shop.longitude == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('This shop has no map location yet.')),
      );
      return;
    }
    final uri = Uri.parse(
      'https://www.google.com/maps/dir/?api=1&destination=${shop.latitude},${shop.longitude}',
    );
    if (!await launchUrl(uri, mode: LaunchMode.externalApplication) &&
        context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Unable to open map directions.')),
      );
    }
  }

  Future<void> _launch(Uri uri, String failMessage) async {
    final ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!ok && mounted) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(failMessage)));
    }
  }

  String get _whatsAppNumber {
    final digits = shop.phone.replaceAll(RegExp(r'\D'), '');
    if (digits.startsWith('0')) return '27${digits.substring(1)}';
    return digits;
  }

  Widget _detailRow(IconData icon, String label, String value) {
    if (value.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: AppColors.textHint),
          const SizedBox(width: 10),
          Text(
            '$label: ',
            style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
          ),
          Expanded(
            child: Text(
              value,
              style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final statusColor = shop.isVerified ? AppColors.success : AppColors.error;
    final location = [
      shop.address,
      shop.city,
      shop.province,
      shop.postalCode,
    ].where((value) => value.isNotEmpty).join(', ');
    final registered =
        shop.registeredAt?.toIso8601String().substring(0, 10) ?? '';
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Shop profile')),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Container(
            height: 150,
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: .1),
              borderRadius: BorderRadius.circular(22),
            ),
            child: Icon(Icons.storefront_rounded, size: 72, color: statusColor),
          ),
          const SizedBox(height: 20),
          Text(shop.name, style: AppTextStyles.h1),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: .1),
              borderRadius: BorderRadius.circular(14),
            ),
            child: Row(
              children: [
                Icon(
                  shop.isVerified
                      ? Icons.verified_rounded
                      : Icons.warning_amber_rounded,
                  color: statusColor,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    shop.isVerified
                        ? 'Verified by SpazaSure'
                        : 'Registered but not yet verified by SpazaSure',
                    style: AppTextStyles.body.copyWith(
                      fontWeight: FontWeight.w700,
                      color: statusColor,
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          _detailRow(Icons.person_outline, 'Owner', shop.ownerName),
          _detailRow(Icons.place_outlined, 'Address', location),
          _detailRow(Icons.call_outlined, 'Phone', shop.phone),
          _detailRow(Icons.email_outlined, 'Email', shop.email),
          _detailRow(
            Icons.fact_check_outlined,
            'Compliance',
            shop.complianceStatus,
          ),
          _detailRow(Icons.event_outlined, 'Registered', registered),
          _detailRow(
            Icons.star_outline_rounded,
            'Rating',
            shop.rating > 0
                ? '${shop.rating.toStringAsFixed(1)} stars (${shop.ratingCount} reviews)'
                : 'No reviews yet',
          ),
          const SizedBox(height: 12),
          Text('Contact this shop', style: AppTextStyles.h3),
          const SizedBox(height: 8),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: [
              if (shop.phone.isNotEmpty)
                FilledButton.icon(
                  onPressed: () => _launch(
                    Uri(scheme: 'tel', path: shop.phone),
                    'Unable to start a call.',
                  ),
                  icon: const Icon(Icons.call_rounded),
                  label: const Text('Call'),
                ),
              if (shop.phone.isNotEmpty)
                FilledButton.tonalIcon(
                  onPressed: () => _launch(
                    Uri.parse('https://wa.me/$_whatsAppNumber'),
                    'Unable to open WhatsApp.',
                  ),
                  icon: const Icon(Icons.chat_rounded),
                  label: const Text('WhatsApp'),
                ),
              if (shop.phone.isNotEmpty)
                OutlinedButton.icon(
                  onPressed: () => _launch(
                    Uri(scheme: 'sms', path: shop.phone),
                    'Unable to open messages.',
                  ),
                  icon: const Icon(Icons.sms_outlined),
                  label: const Text('SMS'),
                ),
              if (shop.email.isNotEmpty)
                OutlinedButton.icon(
                  onPressed: () => _launch(
                    Uri(scheme: 'mailto', path: shop.email),
                    'Unable to open email.',
                  ),
                  icon: const Icon(Icons.email_outlined),
                  label: const Text('Email'),
                ),
              OutlinedButton.icon(
                onPressed: () => _openDirections(context),
                icon: const Icon(Icons.directions_rounded),
                label: const Text('Directions'),
              ),
            ],
          ),
          const SizedBox(height: 24),
          FilledButton.tonalIcon(
            onPressed: _writeReview,
            icon: const Icon(Icons.rate_review_rounded),
            label: const Text('Write a review'),
          ),
          const SizedBox(height: 16),
          Text('Reviews', style: AppTextStyles.h3),
          const SizedBox(height: 8),
          if (_loadingReviews) const Center(child: CircularProgressIndicator()),
          if (!_loadingReviews && _reviews.isEmpty)
            const Card(
              child: ListTile(
                leading: Icon(
                  Icons.rate_review_rounded,
                  color: AppColors.primary,
                ),
                title: Text('No reviews yet'),
              ),
            ),
          ..._reviews.map(
            (review) => Card(
              child: ListTile(
                title: Text('${review['rating'] ?? 0} stars'),
                subtitle: Text(review['comment']?.toString() ?? ''),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
