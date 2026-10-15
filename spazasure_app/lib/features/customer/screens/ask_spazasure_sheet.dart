import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/marketplace/screens/qr_scanner_screen.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';
import 'package:url_launcher/url_launcher.dart';

/// "Ask SpazaSure": answers questions about SpazaSure as a business (who we are, what we
/// offer, where we operate, how to reach us). Product authenticity questions are Kwazi's job.
Future<void> showAskSpazaSure(BuildContext context) =>
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => const _AskSpazaSureSheet(),
    );

const _phone = '+27743811256';
const _phoneLabel = '074 381 1256';
const _email = 'info@spazasure.co.za';

enum _Link { scanner, verifyShop, report, call, email, website }

class _Topic {
  final String chip;
  final IconData icon;
  final String question;
  final String answer;
  final List<String> keywords;
  final List<_Link> links;

  const _Topic(
    this.chip,
    this.icon,
    this.question,
    this.answer,
    this.keywords, [
    this.links = const [],
  ]);
}

// Facts from spazasure.co.za and the features in this app. Prices are not quoted here
// because they can change; they are shown in the app where they apply.
const _topics = [
  _Topic(
    'What is SpazaSure?',
    Icons.storefront_rounded,
    'What is SpazaSure?',
    'SpazaSure is a South African platform for township retail. We connect spaza shops to '
        'verified suppliers, bulk buying power and simple digital tools, and we help shoppers '
        'make sure what they buy is real and safe.\n\nOur mission: to build safer, smarter and '
        'more profitable township retail.',
    [
      'what is',
      'about',
      'who are',
      'spazasure',
      'company',
      'business',
      'mission',
    ],
  ),
  _Topic(
    'For shoppers',
    Icons.shopping_bag_rounded,
    'What can SpazaSure do for me as a shopper?',
    'As a shopper you can:\n'
        '• Scan a product to check it is authentic and see its details\n'
        '• Check if a shop is SpazaSure Verified (look for the green sticker)\n'
        '• Report fake, expired or unsafe goods, anonymously if you like\n'
        '• Get consumer safety alerts\n'
        '• Earn reward points for scanning',
    ['shopper', 'customer', 'buy', 'offer', 'for me', 'what can'],
    [_Link.scanner, _Link.verifyShop],
  ),
  _Topic(
    'For spaza shops',
    Icons.store_mall_directory_rounded,
    'How does SpazaSure help spaza shop owners?',
    'Spaza shop owners can order stock from verified suppliers in the app, track deliveries, and '
        'download invoices and receipts. We combine orders from many shops so you get '
        'wholesale-style prices through group buying. You also get monthly spending reports, '
        'product authenticity scanning, and a SpazaSure Verified sticker once your documents are approved.',
    [
      'shop owner',
      'spaza',
      'retailer',
      'my shop',
      'stock',
      'order',
      'group buy',
      'bulk',
    ],
  ),
  _Topic(
    'For suppliers',
    Icons.local_shipping_rounded,
    'I am a supplier. How do I work with SpazaSure?',
    'Suppliers and wholesalers list their products, receive bulk orders from many spaza shops at '
        'once, and track sales on the supplier dashboard at portal.spazasure.co.za. Verified '
        'suppliers get a verified supplier badge that shops can trust.',
    [
      'supplier',
      'wholesaler',
      'sell',
      'list my products',
      'manufacturer',
      'distributor',
    ],
    [_Link.website],
  ),
  _Topic(
    'Where do you operate?',
    Icons.map_rounded,
    'Where does SpazaSure operate?',
    'SpazaSure is based in Durban and serves township retail across South Africa. Shops and '
        'suppliers from any province can join, and you can find SpazaSure shops near you on '
        'the shop map in this app.',
    [
      'where',
      'operate',
      'area',
      'province',
      'city',
      'durban',
      'located',
      'location',
      'near me',
    ],
  ),
  _Topic(
    'Is it safe to trust a shop?',
    Icons.verified_rounded,
    'How do I know a shop is SpazaSure Verified?',
    'Verified shops display a green SpazaSure Verified sticker with a QR code and shop number. '
        'Scan the QR code or type the shop number in the app to check it. Verified means SpazaSure '
        'has checked the shop\'s business permit, health certificate and owner ID.',
    ['verified', 'sticker', 'trust', 'shop number', 'legit', 'genuine shop'],
    [_Link.verifyShop],
  ),
  _Topic(
    'Report a problem',
    Icons.flag_rounded,
    'How do I report a problem?',
    'Tap "Report a concern" to tell us about fake, expired or unsafe goods, unfair prices or an '
        'unhygienic shop. You can stay anonymous. Our team reviews every report within 48 hours.',
    ['report', 'complain', 'complaint', 'problem', 'fake', 'expired', 'unsafe'],
    [_Link.report],
  ),
  _Topic(
    'Our story',
    Icons.auto_stories_rounded,
    'How did SpazaSure start?',
    'SpazaSure grew out of Authentic8, launched in 2023 to fight counterfeit goods in township '
        'retail. Authentic8 received the SAB Foundation Social Innovation Award. SpazaSure is run '
        'by GEOTX (Pty) Ltd and works with partners including Imali Wealth Group and Further.',
    [
      'story',
      'history',
      'start',
      'founded',
      'authentic8',
      'sab',
      'partner',
      'geotx',
      'owner',
    ],
  ),
  _Topic(
    'Contact us',
    Icons.support_agent_rounded,
    'How can I contact SpazaSure?',
    'Call us on $_phoneLabel or email $_email. We are based in Durban, South Africa.',
    ['contact', 'call', 'phone', 'email', 'talk', 'human', 'support', 'help'],
    [_Link.call, _Link.email],
  ),
];

class _Message {
  final String text;
  final bool fromUser;
  final List<_Link> links;
  const _Message(this.text, {this.fromUser = false, this.links = const []});
}

class _AskSpazaSureSheet extends StatefulWidget {
  const _AskSpazaSureSheet();

  @override
  State<_AskSpazaSureSheet> createState() => _AskSpazaSureSheetState();
}

class _AskSpazaSureSheetState extends State<_AskSpazaSureSheet> {
  final _input = TextEditingController();
  final _scroll = ScrollController();
  final _messages = <_Message>[
    const _Message(
      'Hi, welcome to SpazaSure! I can tell you who we are, what we offer shoppers, spaza shops '
      'and suppliers, where we operate and how to reach us. What would you like to know?',
    ),
  ];
  bool _typing = false;

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _scrollToEnd() => WidgetsBinding.instance.addPostFrameCallback((_) {
    if (_scroll.hasClients) {
      _scroll.animateTo(
        _scroll.position.maxScrollExtent,
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      );
    }
  });

  Future<void> _ask(String question, [_Topic? topic]) async {
    final match = topic ?? _match(question);
    setState(() {
      _messages.add(_Message(question, fromUser: true));
      _typing = true;
    });
    _scrollToEnd();
    await Future<void>.delayed(const Duration(milliseconds: 550));
    if (!mounted) return;
    setState(() {
      _typing = false;
      _messages.add(
        match == null
            ? const _Message(
                'I\'m not sure about that one. Try one of the topics below, or contact our team '
                'directly and they\'ll help you.',
                links: [_Link.call, _Link.email],
              )
            : _Message(match.answer, links: match.links),
      );
    });
    _scrollToEnd();
  }

  /// Best topic for a typed question: the one with the most keyword hits.
  _Topic? _match(String question) {
    final q = question.toLowerCase();
    _Topic? best;
    var bestScore = 0;
    for (final t in _topics) {
      final score = t.keywords.where(q.contains).length;
      if (score > bestScore) {
        best = t;
        bestScore = score;
      }
    }
    return best;
  }

  void _submit() {
    final text = _input.text.trim();
    if (text.isEmpty) return;
    _input.clear();
    _ask(text);
  }

  Future<void> _open(_Link link) async {
    // Close the sheet, then navigate with the screen underneath it
    final navigator = Navigator.of(context);
    switch (link) {
      case _Link.scanner:
        navigator.pop();
        await navigator.push(
          MaterialPageRoute(
            builder: (_) => const QrScannerScreen(customerMode: true),
          ),
        );
      case _Link.verifyShop:
        navigator.pop();
        await navigator.pushNamed('/verify-shop');
      case _Link.report:
        navigator.pop();
        await navigator.push(
          MaterialPageRoute(builder: (_) => const ReportScreen()),
        );
      case _Link.call:
        await launchUrl(Uri(scheme: 'tel', path: _phone));
      case _Link.email:
        await launchUrl(
          Uri(
            scheme: 'mailto',
            path: _email,
            query: 'subject=SpazaSure question',
          ),
        );
      case _Link.website:
        await launchUrl(
          Uri.parse('https://portal.spazasure.co.za'),
          mode: LaunchMode.externalApplication,
        );
    }
  }

  static (String, IconData) _linkLabel(_Link link) => switch (link) {
    _Link.scanner => ('Scan a product', Icons.qr_code_scanner_rounded),
    _Link.verifyShop => ('Verify a shop', Icons.verified_rounded),
    _Link.report => ('Report a concern', Icons.flag_rounded),
    _Link.call => ('Call $_phoneLabel', Icons.call_rounded),
    _Link.email => ('Email us', Icons.mail_rounded),
    _Link.website => ('Open supplier portal', Icons.open_in_new_rounded),
  };

  @override
  Widget build(BuildContext context) {
    final height = MediaQuery.of(context).size.height * 0.88;
    return Padding(
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: Container(
        height: height,
        decoration: const BoxDecoration(
          color: AppColors.background,
          borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
        ),
        child: Column(
          children: [
            _header(),
            Expanded(
              child: ListView(
                controller: _scroll,
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                children: [
                  for (final m in _messages) _bubble(m),
                  if (_typing) _typingBubble(),
                ],
              ),
            ),
            _chips(),
            _composer(),
          ],
        ),
      ),
    );
  }

  Widget _header() => Container(
    padding: const EdgeInsets.fromLTRB(16, 10, 8, 12),
    decoration: const BoxDecoration(
      color: AppColors.surface,
      borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      border: Border(bottom: BorderSide(color: AppColors.divider)),
    ),
    child: Column(
      children: [
        Container(
          width: 40,
          height: 4,
          decoration: BoxDecoration(
            color: AppColors.divider,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        const SizedBox(height: 10),
        Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(14),
              child: Image.asset(
                'assets/images/spazasure_logo.jpg',
                width: 44,
                height: 44,
                fit: BoxFit.cover,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Ask SpazaSure', style: AppTextStyles.h3),
                  Text(
                    'About us, our services and how to reach us',
                    style: AppTextStyles.caption.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            IconButton(
              tooltip: 'Close',
              onPressed: () => Navigator.pop(context),
              icon: const Icon(Icons.close_rounded),
            ),
          ],
        ),
      ],
    ),
  );

  Widget _bubble(_Message m) {
    return Align(
      alignment: m.fromUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.82,
        ),
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: m.fromUser ? AppColors.primary : AppColors.surface,
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(18),
            topRight: const Radius.circular(18),
            bottomLeft: Radius.circular(m.fromUser ? 18 : 4),
            bottomRight: Radius.circular(m.fromUser ? 4 : 18),
          ),
          border: m.fromUser ? null : Border.all(color: AppColors.divider),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              m.text,
              style: AppTextStyles.body.copyWith(
                color: m.fromUser ? Colors.white : AppColors.textPrimary,
                height: 1.4,
              ),
            ),
            if (m.links.isNotEmpty) ...[
              const SizedBox(height: 10),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final link in m.links)
                    Builder(
                      builder: (_) {
                        final (label, icon) = _linkLabel(link);
                        return OutlinedButton.icon(
                          onPressed: () => _open(link),
                          icon: Icon(icon, size: 16),
                          label: Text(label),
                          style: OutlinedButton.styleFrom(
                            foregroundColor: AppColors.primary,
                            side: const BorderSide(
                              color: AppColors.primaryTint,
                            ),
                            visualDensity: VisualDensity.compact,
                          ),
                        );
                      },
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _typingBubble() => Align(
    alignment: Alignment.centerLeft,
    child: Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.divider),
      ),
      child: Text(
        'Typing...',
        style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
      ),
    ),
  );

  Widget _chips() => SizedBox(
    height: 46,
    child: ListView.separated(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      itemCount: _topics.length,
      separatorBuilder: (_, _) => const SizedBox(width: 8),
      itemBuilder: (_, i) {
        final t = _topics[i];
        return ActionChip(
          avatar: Icon(t.icon, size: 16, color: AppColors.primary),
          label: Text(t.chip),
          onPressed: _typing ? null : () => _ask(t.question, t),
          backgroundColor: AppColors.surface,
          side: const BorderSide(color: AppColors.primaryTint),
          labelStyle: AppTextStyles.bodySmall.copyWith(
            fontWeight: FontWeight.w700,
            color: AppColors.primary,
          ),
        );
      },
    ),
  );

  Widget _composer() => Container(
    padding: const EdgeInsets.fromLTRB(12, 8, 8, 8),
    color: AppColors.surface,
    child: SafeArea(
      top: false,
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: _input,
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => _submit(),
              decoration: InputDecoration(
                hintText: 'Ask about SpazaSure',
                filled: true,
                fillColor: AppColors.background,
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: 16,
                  vertical: 10,
                ),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(24),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
          ),
          const SizedBox(width: 6),
          IconButton.filled(
            tooltip: 'Send',
            onPressed: _submit,
            style: IconButton.styleFrom(backgroundColor: AppColors.primary),
            icon: const Icon(Icons.send_rounded, color: Colors.white),
          ),
        ],
      ),
    ),
  );
}
