import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

class NewsScreen extends StatefulWidget {
  const NewsScreen({super.key});

  @override
  State<NewsScreen> createState() => _NewsScreenState();
}

class _NewsScreenState extends State<NewsScreen> {
  final TextEditingController _searchController = TextEditingController();
  String _query = '';

  List<_NewsArticle> get _filteredArticles {
    final query = _query.trim().toLowerCase();
    if (query.isEmpty) return _articles;
    return _articles
        .where(
          (article) =>
              article.category.toLowerCase().contains(query) ||
              article.date.toLowerCase().contains(query) ||
              article.title.toLowerCase().contains(query) ||
              article.summary.toLowerCase().contains(query) ||
              article.source.toLowerCase().contains(query),
        )
        .toList();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: AppColors.background,
    appBar: BrandAppBar(title: Text(tr('nav.news'))),
    body: Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(18, 18, 18, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Know what’s behind the label.',
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                'A manually curated archive of South African coverage on '
                'illicit trade, counterfeit goods and food safety. Not a live '
                'news feed.',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: AppColors.textSecondary,
                  height: 1.4,
                ),
              ),
              const SizedBox(height: 14),
              TextField(
                controller: _searchController,
                onChanged: (value) => setState(() => _query = value),
                decoration: InputDecoration(
                  hintText: 'Search coverage',
                  prefixIcon: const Icon(Icons.search_rounded),
                  suffixIcon: _query.isEmpty
                      ? null
                      : IconButton(
                          tooltip: 'Clear search',
                          icon: const Icon(Icons.close_rounded),
                          onPressed: () {
                            _searchController.clear();
                            setState(() => _query = '');
                          },
                        ),
                  filled: true,
                  fillColor: AppColors.surface,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(16),
                    borderSide: BorderSide.none,
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(16),
                    borderSide: const BorderSide(color: AppColors.primaryTint),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(16),
                    borderSide: const BorderSide(color: AppColors.primary),
                  ),
                ),
              ),
            ],
          ),
        ),
        Expanded(
          child: _filteredArticles.isEmpty
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Text(
                      'No coverage matches “${_searchController.text}”.',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                )
              : ListView(
                  padding: const EdgeInsets.fromLTRB(18, 0, 18, 24),
                  children: [
                    const _ContextNote(),
                    const SizedBox(height: 12),
                    for (final article in _filteredArticles) ...[
                      _NewsArticleCard(article: article),
                      const SizedBox(height: 12),
                    ],
                    const _SafetyNote(),
                  ],
                ),
        ),
      ],
    ),
  );
}

class _NewsArticleCard extends StatelessWidget {
  final _NewsArticle article;

  const _NewsArticleCard({required this.article});

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(16),
    decoration: BoxDecoration(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(20),
      border: Border.all(color: AppColors.primaryTint),
      boxShadow: [
        BoxShadow(
          color: AppColors.primary.withValues(alpha: 0.04),
          blurRadius: 12,
          offset: const Offset(0, 4),
        ),
      ],
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Wrap(
          spacing: 8,
          runSpacing: 6,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
              decoration: BoxDecoration(
                color: AppColors.primaryTint,
                borderRadius: BorderRadius.circular(20),
              ),
              child: Text(
                article.category,
                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
            Text(
              article.date,
              style: Theme.of(
                context,
              ).textTheme.labelMedium?.copyWith(color: AppColors.ink2),
            ),
          ],
        ),
        const SizedBox(height: 10),
        Text(
          article.title,
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
            color: AppColors.textPrimary,
            fontWeight: FontWeight.w900,
            height: 1.25,
          ),
        ),
        const SizedBox(height: 8),
        Text(
          article.summary,
          style: Theme.of(
            context,
          ).textTheme.bodyMedium?.copyWith(color: AppColors.ink, height: 1.5),
        ),
        const SizedBox(height: 12),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Icon(Icons.article_outlined, size: 16, color: AppColors.ink3),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                'Source: ${article.source}',
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: AppColors.ink2,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
        ),
      ],
    ),
  );
}

class _ContextNote extends StatelessWidget {
  const _ContextNote();

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: AppColors.accentTint,
      borderRadius: BorderRadius.circular(16),
      border: Border.all(color: AppColors.accent.withValues(alpha: 0.35)),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(
          Icons.info_outline_rounded,
          color: AppColors.primary,
          size: 20,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            'Figures come from different dates and provinces. Early Gauteng '
            'and later national updates have different scopes and should not '
            'be combined.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: AppColors.textPrimary,
              height: 1.45,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ],
    ),
  );
}

class _SafetyNote extends StatelessWidget {
  const _SafetyNote();

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(16),
    decoration: BoxDecoration(
      color: AppColors.secondaryTint,
      borderRadius: BorderRadius.circular(18),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(
          Icons.health_and_safety_outlined,
          color: AppColors.secondary,
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            'Unsure about a product? Don’t consume anything you suspect is '
            'unsafe. Keep its packaging and proof of purchase if safe, and '
            'seek medical help if someone becomes unwell.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: AppColors.textPrimary,
              height: 1.45,
            ),
          ),
        ),
      ],
    ),
  );
}

class _NewsArticle {
  final String category;
  final String date;
  final String title;
  final String summary;
  final String source;

  const _NewsArticle({
    required this.category,
    required this.date,
    required this.title,
    required this.summary,
    required this.source,
  });
}

const _articles = <_NewsArticle>[
  _NewsArticle(
    category: 'Research report',
    date: '3 September 2026',
    title: 'ECMX study estimates illicit trade across 12 sectors',
    summary:
        'A Your Choice Has Power release summarizes an Econometrix estimate '
        'of R280 billion in illicit trade across 12 sectors. This is an '
        'industry-commissioned estimate of illicit trade broadly, not an '
        'official measure or a counterfeit-goods-only figure.',
    source: 'Your Choice Has Power; Econometrix',
  ),
  _NewsArticle(
    category: 'South African video report',
    date: '17 July 2026',
    title: 'SABC revisits spaza-shop enforcement after the food-safety probe',
    summary:
        'SABC News interviews Vusumuzi Dlamini about spaza-shop operators '
        'and enforcement systems in connection with the 2024 food-poisoning '
        'incidents.',
    source: 'SABC News',
  ),
  _NewsArticle(
    category: 'Policy update',
    date: '15 May 2026',
    title: 'South Africa bans agricultural remedies containing terbufos',
    summary:
        'A prohibition published in the Government Gazette on 8 May bars '
        'the sale, use and other handling of agricultural remedies containing '
        'terbufos. Reporting notes that the exact exposure pathway in the '
        '2024 Naledi deaths remained uncertain.',
    source: 'Mail & Guardian; Department of Health advisory',
  ),
  _NewsArticle(
    category: 'Fact-check',
    date: '19 February 2025',
    title: 'Africa Check examines viral claims about more child deaths',
    summary:
        'Africa Check reviewed social-media claims of additional spaza-shop '
        'poisoning deaths and found them to be false. Verify urgent health '
        'claims before sharing them.',
    source: 'Africa Check',
  ),
  _NewsArticle(
    category: 'Analysis',
    date: '5 February 2025',
    title: 'Researchers assess South Africa’s response to food poisoning',
    summary:
        'Researchers argue that enforcement and registration alone cannot '
        'address the crisis. Their analysis also points to environmental '
        'health capacity and underlying local conditions.',
    source: 'Gauteng City-Region Observatory; The Conversation',
  ),
  _NewsArticle(
    category: 'Counterfeit goods',
    date: '20 January 2025',
    title: 'News24 reports counterfeit-condom concerns in spaza shops',
    summary:
        'This report covers a counterfeit-goods concern distinct from the '
        'food-borne illness investigations. Product authenticity and food '
        'safety are related consumer concerns, but they are not the same issue.',
    source: 'News24',
  ),
  _NewsArticle(
    category: 'Official update',
    date: '15 November 2024',
    title: 'National address sets out the 2024 food-borne illness response',
    summary:
        'The address reported 890 food-borne illness incidents across '
        'provinces since September and at least 22 child deaths. It said tests '
        'linked six Naledi deaths to terbufos and found traces on a snack '
        'packet; later reporting noted that the precise exposure pathway '
        'remained unclear.',
    source: 'The Presidency; SABC News; Mail & Guardian',
  ),
  _NewsArticle(
    category: 'Early provincial tally',
    date: '11 November 2024',
    title: 'Gauteng reports an early tally of food-borne illness deaths',
    summary:
        'SAnews reported preliminary Gauteng figures of 23 deaths and at '
        'least 441 poisoning cases. These were an early provincial update, '
        'different in date and scope from later national figures.',
    source: 'SAnews',
  ),
  _NewsArticle(
    category: 'South African video report',
    date: '6 November 2024',
    title: 'SABC reports on spaza-shop compliance inspections',
    summary:
        'This report covers authorities inspecting shops and checking '
        'compliance during the 2024 food-poisoning response.',
    source: 'SABC News',
  ),
  _NewsArticle(
    category: 'News report & video',
    date: '21 October 2024',
    title: 'SABC reports on the early investigation and health response',
    summary:
        'Health authorities were investigating suspected cases and had '
        'deployed environmental-health practitioners. The report includes '
        'an interview with the Health Minister.',
    source: 'SABC News',
  ),
];
