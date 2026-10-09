import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/screens/safety_basics_screen.dart';
import 'package:spazasure_app/features/marketplace/screens/qr_scanner_screen.dart';
import 'package:spazasure_app/features/notifications/screens/report_screen.dart';

/// Opens the Kwazi chatbot as a popup over the current screen.
Future<void> showKwaziChat(BuildContext context) => showModalBottomSheet<void>(
  context: context,
  isScrollControlled: true,
  useSafeArea: true,
  backgroundColor: Colors.transparent,
  builder: (_) => const _KwaziChatSheet(),
);

class _Faq {
  final String label;
  final String question;
  final String answer;
  final List<String> keywords;
  final bool offerReport;

  const _Faq(
    this.label,
    this.question,
    this.answer,
    this.keywords, {
    this.offerReport = false,
  });
}

const _greeting =
    'Sawubona! I\u2019m Kwazi, your Authenticity Guide. Spotted a bargain that feels too good to be true, or want to check if your grocery haul is the real deal? Ask me anything about product safety, spot-checking fakes, or how to use our scanner!';

const _faqs = [
  _Faq(
    'Fake food or oil?',
    'How do I know if cooking oil or packaged food is fake?',
    'Check the cap and the seal first! Genuine cooking oil bottles have tight, unbroken plastic rings and smooth cap threading. Look closely at the print quality of the label. Fakes often have blurry text, missing batch numbers, or smear easily if a drop of water touches them. When in doubt, scan the barcode with this app!',
    ['oil', 'food', 'packaged', 'seal', 'cap', 'fake food'],
  ),
  _Faq(
    'Red warning',
    'What should I do if a scanned product shows a Red Warning?',
    'Do not use or consume it! A red warning means the barcode is unrecognised or flagged as suspicious. Please leave the item alone if you\u2019re in-store, or set it aside if you bought it. Tap \u201cReport this product\u201d below to send us the store location and photos so our safety team can investigate.',
    ['red', 'warning', 'flagged', 'unverified', 'scan result', 'suspicious'],
    offerReport: true,
  ),
  _Faq(
    'Fake cosmetics',
    'Why is buying counterfeit cosmetics or soap dangerous?',
    'Unlike food, which you digest immediately, fake cosmetics and soaps are absorbed directly through your skin. Counterfeit lotions and creams often contain industrial chemicals, banned bleaching agents, or harmful bacteria that cause severe rashes, burns, and long-term skin damage. Never risk your skin for a cheap knockoff!',
    ['cosmetic', 'soap', 'lotion', 'cream', 'skin', 'toothpaste'],
  ),
  _Faq(
    'Cheap price',
    'The price is super cheap. Does that automatically mean it\u2019s fake?',
    'Not always. It could be expired-closeout stock or a clearance sale. However, if a high-end brand is selling for a fraction of its normal price at an unauthorised flea market or random pop-up shop, it\u2019s likely counterfeit or stolen stock. Protect your peace and check the batch details!',
    ['cheap', 'price', 'discount', 'bargain', 'too good'],
  ),
  _Faq(
    'Batch number',
    'What is a \u2018Batch Number\u2019 and where do I find it?',
    'A batch number is a code assigned by the manufacturer to a specific production run. It helps track when and where the item was made. You\u2019ll usually find it stamped near the expiry date, printed on the bottom of cans, or along the crimp of tubes and sachets. If a product has no batch number at all, steer clear!',
    ['batch', 'lot', 'code number'],
  ),
  _Faq(
    'Report anonymously',
    'Can I report a suspicious shop or market vendor anonymously?',
    'Yes, absolutely! Your safety and privacy come first. When you use the report feature in the app, you can choose to submit your findings completely anonymously. Every report helps us protect the wider community.',
    ['anonymous', 'anonymously', 'vendor', 'privacy'],
    offerReport: true,
  ),
];

class _Reply {
  final String label;
  final IconData? icon;
  final VoidCallback onTap;

  const _Reply(this.label, this.onTap, {this.icon});
}

class _Message {
  final String text;
  final bool fromUser;
  final Color? accent;
  final List<_Action> actions;

  const _Message(
    this.text, {
    this.fromUser = false,
    this.accent,
    this.actions = const [],
  });
}

class _Action {
  final String label;
  final IconData icon;
  final Color? color;
  final VoidCallback onTap;

  const _Action(this.label, this.icon, this.onTap, {this.color});
}

class _KwaziChatSheet extends StatefulWidget {
  const _KwaziChatSheet();

  @override
  State<_KwaziChatSheet> createState() => _KwaziChatSheetState();
}

class _KwaziChatSheetState extends State<_KwaziChatSheet> {
  final _input = TextEditingController();
  final _scroll = ScrollController();
  final _messages = <_Message>[];
  List<_Reply> _replies = [];
  bool _typing = false;

  // Guided check state.
  String _category = '';
  int _redFlags = 0;
  final _flagNotes = <String>[];

  @override
  void initState() {
    super.initState();
    _botSay(_Message(_greeting), then: _mainMenu, delay: 500);
  }

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _scrollToEnd() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) {
        _scroll.animateTo(
          _scroll.position.maxScrollExtent,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> _botSay(
    _Message message, {
    VoidCallback? then,
    int delay = 700,
  }) async {
    setState(() {
      _typing = true;
      _replies = [];
    });
    _scrollToEnd();
    await Future<void>.delayed(Duration(milliseconds: delay));
    if (!mounted) return;
    setState(() {
      _typing = false;
      _messages.add(message);
    });
    _scrollToEnd();
    then?.call();
  }

  void _userSays(String text) {
    setState(() {
      _messages.add(_Message(text, fromUser: true));
      _replies = [];
    });
    _scrollToEnd();
  }

  void _setReplies(List<_Reply> replies) {
    setState(() => _replies = replies);
    _scrollToEnd();
  }

  void _mainMenu() => _setReplies([
    _Reply('Check a product', _startCheck, icon: Icons.fact_check_rounded),
    _Reply('Scan a barcode', _openScanner, icon: Icons.qr_code_scanner_rounded),
    for (final faq in _faqs)
      _Reply(faq.label, () => _answer(faq, echo: faq.question)),
    _Reply('Safety basics', _openSafety, icon: Icons.school_rounded),
  ]);

  void _afterAnswer() => _setReplies([
    _Reply('Check a product', _startCheck, icon: Icons.fact_check_rounded),
    _Reply('Ask something else', () {
      _userSays('Ask something else');
      _botSay(
        const _Message('Of course! What would you like to know?'),
        then: _mainMenu,
        delay: 400,
      );
    }),
  ]);

  void _answer(_Faq faq, {required String echo}) {
    _userSays(echo);
    _botSay(
      _Message(faq.answer, actions: [if (faq.offerReport) _reportAction()]),
      then: _afterAnswer,
    );
  }

  _Action _reportAction() => _Action(
    'Report this product',
    Icons.flag_rounded,
    () => Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => const ReportScreen()),
    ),
    color: AppColors.error,
  );

  void _openScanner() {
    final nav = Navigator.of(context)..pop();
    nav.push(
      MaterialPageRoute(
        builder: (_) => const QrScannerScreen(customerMode: true),
      ),
    );
  }

  void _openSafety() {
    final nav = Navigator.of(context)..pop();
    nav.push(MaterialPageRoute(builder: (_) => const SafetyBasicsScreen()));
  }

  // Free-text questions are matched to the closest FAQ by keyword.
  void _askFreeText(String text) {
    final trimmed = text.trim();
    if (trimmed.isEmpty || _typing) return;
    _input.clear();
    _userSays(trimmed);
    final q = trimmed.toLowerCase();
    _Faq? best;
    var bestScore = 0;
    for (final faq in _faqs) {
      final score = faq.keywords.where(q.contains).length;
      if (score > bestScore) {
        best = faq;
        bestScore = score;
      }
    }
    if (best != null) {
      _botSay(
        _Message(best.answer, actions: [if (best.offerReport) _reportAction()]),
        then: _afterAnswer,
      );
    } else {
      _botSay(
        _Message(
          'I\u2019m not sure about that one yet. Pick a topic below, or report it and our safety team will take a look.',
          actions: [_reportAction()],
        ),
        then: _mainMenu,
      );
    }
  }

  // Guided check: five quick questions, then a verdict.
  void _startCheck() {
    _category = '';
    _redFlags = 0;
    _flagNotes.clear();
    _userSays('Check a product');
    _botSay(
      const _Message(
        'Let\u2019s inspect it together. What kind of product is it?',
      ),
      then: () => _setReplies([
        _Reply('Food or drink', () => _pickCategory('Food or drink')),
        _Reply('Cosmetics or soap', () => _pickCategory('Cosmetics or soap')),
        _Reply('Household cleaner', () => _pickCategory('Household cleaner')),
      ]),
    );
  }

  void _pickCategory(String category) {
    _category = category;
    _userSays(category);
    _ask(
      'How does the packaging and print look? Think logos, colours and spelling.',
      good: 'Crisp and clear',
      bad: 'Blurry or misspelt',
      badNote: 'poor print quality on the packaging',
      next: _askSeal,
    );
  }

  void _askSeal() => _ask(
    'Is the seal or shrink-wrap intact?',
    good: 'Intact',
    bad: 'Broken or glued',
    badNote: 'the seal looks broken, double-layered or glued',
    next: _askLabel,
  );

  void _askLabel() => _ask(
    'Are the batch number and expiry date clearly printed?',
    good: 'Yes, both',
    bad: 'Missing or smudged',
    badNote: 'the batch number or expiry date is missing or smudged',
    next: _askPrice,
  );

  void _askPrice() => _ask(
    'How is the price compared with other shops?',
    good: 'About normal',
    bad: 'Much cheaper',
    badNote: 'the price is far below normal retail value',
    next: _verdict,
  );

  void _ask(
    String question, {
    required String good,
    required String bad,
    required String badNote,
    required VoidCallback next,
  }) {
    _botSay(
      _Message(question),
      then: () => _setReplies([
        _Reply(good, () {
          _userSays(good);
          next();
        }, icon: Icons.check_circle_outline_rounded),
        _Reply(bad, () {
          _userSays(bad);
          _redFlags++;
          _flagNotes.add(badNote);
          next();
        }, icon: Icons.error_outline_rounded),
      ]),
    );
  }

  void _verdict() {
    final tip = switch (_category) {
      'Cosmetics or soap' =>
        'Fake cosmetics are absorbed through your skin, so stop using it if you feel any irritation.',
      'Household cleaner' =>
        'Diluted or fake cleaners may not sanitise, and can irritate skin. Do not mix it with other products.',
      _ =>
        'Fake food can contain unsafe ingredients. Do not eat or drink it if you are unsure.',
    };
    final (title, color, body) = _redFlags >= 2
        ? (
            'High concern',
            AppColors.error,
            'I found $_redFlags warning signs: ${_flagNotes.join('; ')}. Please do not buy or use this product.',
          )
        : _redFlags == 1
        ? (
            'Be careful',
            AppColors.warning,
            'One warning sign: ${_flagNotes.first}. This does not prove it is fake, but scan it before you buy.',
          )
        : (
            'No obvious red flags',
            AppColors.success,
            'It passes the physical checks. A scan can still confirm the barcode and batch.',
          );
    _botSay(
      _Message(
        '$title\n$body\n\n$tip',
        accent: color,
        actions: [
          _Action(
            'Scan the barcode',
            Icons.qr_code_scanner_rounded,
            _openScanner,
          ),
          if (_redFlags >= 1) _reportAction(),
        ],
      ),
      then: _afterAnswer,
      delay: 900,
    );
  }

  @override
  Widget build(BuildContext context) {
    final inset = MediaQuery.of(context).viewInsets.bottom;
    return Padding(
      padding: EdgeInsets.only(bottom: inset),
      child: DraggableScrollableSheet(
        initialChildSize: 0.88,
        minChildSize: 0.5,
        maxChildSize: 0.95,
        expand: false,
        builder: (_, __) => Container(
          decoration: const BoxDecoration(
            color: AppColors.background,
            borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
          ),
          child: Column(
            children: [
              _header(),
              Expanded(
                child: ListView.builder(
                  controller: _scroll,
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
                  itemCount: _messages.length + (_typing ? 1 : 0),
                  itemBuilder: (_, i) => i == _messages.length
                      ? _typingBubble()
                      : _bubble(_messages[i]),
                ),
              ),
              if (_replies.isNotEmpty)
                ConstrainedBox(
                  constraints: const BoxConstraints(maxHeight: 130),
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 4),
                    child: Align(
                      alignment: Alignment.centerLeft,
                      child: Wrap(
                        spacing: 8,
                        runSpacing: 6,
                        children: [
                          for (final r in _replies)
                            ActionChip(
                              avatar: r.icon == null
                                  ? null
                                  : Icon(r.icon, size: 18),
                              label: Text(r.label),
                              onPressed: r.onTap,
                            ),
                        ],
                      ),
                    ),
                  ),
                ),
              _inputBar(),
            ],
          ),
        ),
      ),
    );
  }

  Widget _header() => Container(
    padding: const EdgeInsets.fromLTRB(16, 12, 8, 12),
    decoration: const BoxDecoration(
      color: AppColors.primary,
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    child: Row(
      children: [
        const CircleAvatar(
          backgroundColor: Colors.white,
          child: Icon(Icons.smart_toy_rounded, color: AppColors.primary),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Kwazi',
                style: AppTextStyles.subtitle.copyWith(color: Colors.white),
              ),
              Text(
                'Authenticity Guide',
                style: AppTextStyles.caption.copyWith(color: Colors.white70),
              ),
            ],
          ),
        ),
        IconButton(
          tooltip: 'Close',
          onPressed: () => Navigator.pop(context),
          icon: const Icon(Icons.close_rounded, color: Colors.white),
        ),
      ],
    ),
  );

  Widget _inputBar() => SafeArea(
    top: false,
    child: Padding(
      padding: const EdgeInsets.fromLTRB(12, 4, 12, 8),
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: _input,
              textInputAction: TextInputAction.send,
              onSubmitted: _askFreeText,
              decoration: const InputDecoration(
                hintText: 'Ask Kwazi about a product',
              ),
            ),
          ),
          const SizedBox(width: 8),
          IconButton.filled(
            tooltip: 'Send',
            onPressed: _typing ? null : () => _askFreeText(_input.text),
            icon: const Icon(Icons.send_rounded),
          ),
        ],
      ),
    ),
  );

  Widget _typingBubble() => Align(
    alignment: Alignment.centerLeft,
    child: Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
      ),
      child: const SizedBox(
        width: 36,
        height: 12,
        child: LinearProgressIndicator(
          minHeight: 4,
          backgroundColor: Colors.transparent,
        ),
      ),
    ),
  );

  Widget _bubble(_Message m) {
    final accent = m.accent;
    return Align(
      alignment: m.fromUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.82,
        ),
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: m.fromUser
              ? AppColors.primary
              : accent?.withValues(alpha: 0.1) ?? AppColors.surface,
          borderRadius: BorderRadius.circular(16),
          border: accent == null
              ? null
              : Border.all(color: accent.withValues(alpha: 0.4)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              m.text,
              style: AppTextStyles.body.copyWith(
                color: m.fromUser ? Colors.white : AppColors.textPrimary,
              ),
            ),
            for (final a in m.actions) ...[
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: a.onTap,
                  style: a.color == null
                      ? null
                      : FilledButton.styleFrom(backgroundColor: a.color),
                  icon: Icon(a.icon, size: 18),
                  label: Text(a.label),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
