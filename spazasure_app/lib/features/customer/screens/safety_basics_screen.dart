import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/screens/kwazi_chat_sheet.dart';

class _SafetyPage {
  final IconData icon;
  final Color color;
  final String title;
  final String? subtitle;
  final List<(String, String)> points;

  const _SafetyPage(
    this.icon,
    this.color,
    this.title, {
    this.subtitle,
    this.points = const [],
  });
}

const _pages = [
  _SafetyPage(
    Icons.shield_rounded,
    AppColors.primary,
    'Protecting Your Home, One Scan at a Time',
    subtitle:
        'Fast-moving consumer goods (FMCG) like groceries, cosmetics, and household cleaners are prime targets for counterfeiters. Learn how to spot fake products and protect your family from toxic or substandard ingredients.',
  ),
  _SafetyPage(
    Icons.search_rounded,
    AppColors.secondary,
    'The 4-Step Physical Inspection',
    subtitle:
        'Before you even scan a barcode, use your senses to inspect the physical product on the shelf.',
    points: [
      (
        '1. Packaging & print quality',
        'Look for pixelated logos, faded colours, spelling mistakes, or uneven sealing. Genuine FMCG brands invest heavily in crisp, high-definition packaging.',
      ),
      (
        '2. Seals and tamper evidence',
        'Check if security tape, shrink-wrap, or foil safety seals are broken, double-layered, or hastily glued.',
      ),
      (
        '3. Critical labelling data',
        'Legitimate products must clearly display a batch or lot number, production and best-before or expiry dates, and the manufacturer\'s physical address and consumer care line.',
      ),
      (
        '4. Pricing realism',
        'If a premium washing powder, luxury perfume, or branded beverage is priced 50% lower than standard retail value, treat it as a major red flag.',
      ),
    ],
  ),
  _SafetyPage(
    Icons.qr_code_scanner_rounded,
    AppColors.info,
    'Understanding Barcodes & Digital Scanning',
    subtitle:
        'Use the app to scan the product\'s barcode or secure QR code. Our system cross-references product databases to check whether the item is legitimate, gray-market, or flagged as counterfeit.',
    points: [
      (
        'Green status: Verified',
        'The item matches official production records.',
      ),
      (
        'Red status: Unverified or flagged',
        'This barcode is unrecognised, unregistered, or has been reported multiple times. Do not consume or use it.',
      ),
    ],
  ),
  _SafetyPage(
    Icons.warning_amber_rounded,
    AppColors.error,
    'Category-Specific Risks',
    points: [
      (
        'Food & beverages',
        'Counterfeit sodas, cooking oils, and baby formula can contain dangerous chemical substitutes or unhygienic ingredients that cause severe food poisoning.',
      ),
      (
        'Cosmetics & personal care',
        'Fake lotions, soaps, and toothpastes frequently contain harmful industrial chemicals or heavy metals that cause severe skin rashes and chemical burns.',
      ),
      (
        'Household cleaning agents',
        'Diluted or fake bleaches and detergents fail to sanitise, leaving homes vulnerable to bacteria and causing corrosive skin irritation.',
      ),
    ],
  ),
];

class SafetyBasicsScreen extends StatefulWidget {
  const SafetyBasicsScreen({super.key});

  @override
  State<SafetyBasicsScreen> createState() => _SafetyBasicsScreenState();
}

class _SafetyBasicsScreenState extends State<SafetyBasicsScreen> {
  final _controller = PageController();
  int _index = 0;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _next() {
    if (_index == _pages.length - 1) {
      Navigator.pop(context);
    } else {
      _controller.nextPage(
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final last = _index == _pages.length - 1;
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Safety Basics')),
      body: Column(
        children: [
          Expanded(
            child: PageView.builder(
              controller: _controller,
              itemCount: _pages.length,
              onPageChanged: (i) => setState(() => _index = i),
              itemBuilder: (_, i) => _buildPage(_pages[i]),
            ),
          ),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              for (var i = 0; i < _pages.length; i++)
                AnimatedContainer(
                  duration: const Duration(milliseconds: 200),
                  margin: const EdgeInsets.symmetric(horizontal: 3),
                  width: i == _index ? 22 : 8,
                  height: 8,
                  decoration: BoxDecoration(
                    color: i == _index
                        ? AppColors.primary
                        : AppColors.textHint.withValues(alpha: 0.4),
                    borderRadius: BorderRadius.circular(4),
                  ),
                ),
            ],
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
            child: Row(
              children: [
                if (last)
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => showKwaziChat(context),
                      icon: const Icon(Icons.smart_toy_rounded),
                      label: const Text('Ask Kwazi'),
                    ),
                  ),
                if (last) const SizedBox(width: 12),
                Expanded(
                  child: FilledButton(
                    onPressed: _next,
                    child: Text(
                      _index == 0
                          ? 'Start the Quick Check Guide'
                          : last
                          ? 'Done'
                          : 'Next',
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPage(_SafetyPage page) => SingleChildScrollView(
    padding: const EdgeInsets.all(24),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Center(
          child: Container(
            width: 84,
            height: 84,
            decoration: BoxDecoration(
              color: page.color.withValues(alpha: 0.12),
              shape: BoxShape.circle,
            ),
            child: Icon(page.icon, size: 44, color: page.color),
          ),
        ),
        const SizedBox(height: 20),
        Text(page.title, style: AppTextStyles.h1),
        if (page.subtitle != null) ...[
          const SizedBox(height: 10),
          Text(page.subtitle!, style: AppTextStyles.body),
        ],
        const SizedBox(height: 16),
        for (final (title, body) in page.points)
          Container(
            width: double.infinity,
            margin: const EdgeInsets.only(bottom: 12),
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(16),
              border: Border(left: BorderSide(color: page.color, width: 4)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: AppTextStyles.body.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  body,
                  style: AppTextStyles.bodySmall.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
      ],
    ),
  );
}
