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

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: AppBar(title: const Text('Shop profile')),
    body: ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Container(
          height: 150,
          decoration: BoxDecoration(
            color: AppColors.primary.withValues(alpha: .1),
            borderRadius: BorderRadius.circular(22),
          ),
          child: const Icon(
            Icons.storefront,
            size: 72,
            color: AppColors.primary,
          ),
        ),
        const SizedBox(height: 20),
        Text(shop.name, style: AppTextStyles.h1),
        const SizedBox(height: 8),
        Text(
          [
            shop.address,
            shop.city,
            shop.province,
          ].where((value) => value.isNotEmpty).join(', '),
          style: AppTextStyles.body,
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            const Icon(Icons.verified_user, color: AppColors.success, size: 20),
            const SizedBox(width: 8),
            Text(
              'Compliance: ${shop.complianceStatus}',
              style: AppTextStyles.body,
            ),
            const Spacer(),
            Text(
              shop.rating > 0
                  ? '${shop.rating.toStringAsFixed(1)} stars'
                  : 'No reviews',
              style: AppTextStyles.subtitle,
            ),
          ],
        ),
        const SizedBox(height: 24),
        FilledButton.icon(
          onPressed: () => _openDirections(context),
          icon: const Icon(Icons.directions),
          label: const Text('Get directions'),
        ),
        const SizedBox(height: 12),
        OutlinedButton.icon(
          onPressed: () => ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text(
                'Messaging will be available after shop messaging is enabled.',
              ),
            ),
          ),
          icon: const Icon(Icons.chat_bubble_outline),
          label: const Text('Message shop'),
        ),
        const SizedBox(height: 24),
        FilledButton.tonalIcon(
          onPressed: _writeReview,
          icon: const Icon(Icons.rate_review),
          label: const Text('Write a review'),
        ),
        Text('Reviews', style: AppTextStyles.h3),
        const SizedBox(height: 8),
        if (_loadingReviews) const Center(child: CircularProgressIndicator()),
        if (!_loadingReviews && _reviews.isEmpty)
          const Card(
            child: ListTile(
              leading: Icon(
                Icons.rate_review_outlined,
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
