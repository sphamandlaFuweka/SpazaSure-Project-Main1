import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/services/api_service.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// A reference photo of the real product.
class GalleryPhoto {
  final String url;
  final String label;
  final String? source;

  const GalleryPhoto(this.url, this.label, {this.source});

  /// The verify response's `gallery` (open databases), or else the supplier's own
  /// product photos in `images` (SpazaSure products).
  static List<GalleryPhoto> fromProduct(Map<String, dynamic> p) {
    final gallery = (p['gallery'] as List?)?.whereType<Map>().toList() ?? [];
    if (gallery.isNotEmpty) {
      return [
        for (final g in gallery)
          if ('${g['url'] ?? ''}'.isNotEmpty)
            GalleryPhoto(
              '${g['url']}',
              '${g['label'] ?? 'Product photo'}',
              source: g['source']?.toString(),
            ),
      ];
    }
    final images =
        (p['images'] as List?)
            ?.map((e) => '$e'.trim())
            .where((e) => e.isNotEmpty && e != 'null')
            .toList() ??
        [];
    return [
      for (var i = 0; i < images.length; i++)
        GalleryPhoto(
          images[i],
          images.length == 1 ? 'Supplier photo' : 'Supplier photo ${i + 1}',
          source: 'SpazaSure supplier',
        ),
    ];
  }
}

/// Shows a product image stored as a web link, a /uploads path on our server, or a
/// data: URI (suppliers' uploads can be any of these).
class ProductImage extends StatelessWidget {
  final String url;
  final BoxFit fit;

  const ProductImage(this.url, {super.key, this.fit = BoxFit.cover});

  @override
  Widget build(BuildContext context) {
    final placeholder = Container(
      color: AppColors.surface,
      alignment: Alignment.center,
      child: Icon(
        Icons.image_not_supported_outlined,
        color: AppColors.textHint,
      ),
    );
    if (url.startsWith('data:image')) {
      final comma = url.indexOf(',');
      try {
        return Image.memory(
          base64Decode(url.substring(comma + 1)),
          fit: fit,
          errorBuilder: (_, __, ___) => placeholder,
        );
      } catch (_) {
        return placeholder;
      }
    }
    final resolved = ApiService.resolveUploadUrl(url);
    if (resolved == null) return placeholder;
    return Image.network(
      resolved,
      fit: fit,
      errorBuilder: (_, __, ___) => placeholder,
      loadingBuilder: (context, child, progress) => progress == null
          ? child
          : Container(
              color: AppColors.surface,
              alignment: Alignment.center,
              child: const SizedBox(
                width: 22,
                height: 22,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
    );
  }
}

/// "Photos of the real product": a strip of reference photos, tap to zoom, plus the
/// button that starts comparing them with the customer's own pack.
class ProductGalleryCard extends StatelessWidget {
  final List<GalleryPhoto> photos;
  final VoidCallback? onCompare;

  const ProductGalleryCard({super.key, required this.photos, this.onCompare});

  @override
  Widget build(BuildContext context) {
    final sources = photos.map((p) => p.source).whereType<String>().toSet();
    return Material(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(16),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(
                  Icons.photo_library_outlined,
                  color: AppColors.primary,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    photos.isEmpty
                        ? 'Check your pack'
                        : 'Photos of the real product',
                    style: AppTextStyles.body.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              photos.isEmpty
                  ? 'No photos of this product are on record yet. You can still check the text printed on your pack.'
                  : 'Is the pack in your hand the same? Compare the logo, colours, spelling, size and where the barcode sits.',
              style: AppTextStyles.caption.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            if (photos.isNotEmpty) ...[
              const SizedBox(height: 10),
              SizedBox(
                height: 132,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  itemCount: photos.length,
                  separatorBuilder: (_, __) => const SizedBox(width: 10),
                  itemBuilder: (context, i) => _thumb(context, i),
                ),
              ),
              if (sources.isNotEmpty)
                Text(
                  'Photos: ${sources.join(', ')}',
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textHint,
                    fontSize: 11,
                  ),
                ),
            ],
            if (onCompare != null) ...[
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: onCompare,
                  icon: const Icon(Icons.compare_rounded),
                  label: Text(
                    photos.isEmpty
                        ? 'Check the text on my pack'
                        : 'Compare with my pack',
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _thumb(BuildContext context, int i) {
    return GestureDetector(
      onTap: () => Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => PhotoViewerScreen(photos: photos, initialIndex: i),
        ),
      ),
      child: SizedBox(
        width: 96,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(10),
              child: SizedBox(
                width: 96,
                height: 100,
                child: ProductImage(photos[i].url),
              ),
            ),
            const SizedBox(height: 4),
            Text(
              photos[i].label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppTextStyles.caption.copyWith(fontSize: 11),
            ),
          ],
        ),
      ),
    );
  }
}

/// Full-screen photos: swipe between them, pinch to zoom.
class PhotoViewerScreen extends StatefulWidget {
  final List<GalleryPhoto> photos;
  final int initialIndex;

  const PhotoViewerScreen({
    super.key,
    required this.photos,
    this.initialIndex = 0,
  });

  @override
  State<PhotoViewerScreen> createState() => _PhotoViewerScreenState();
}

class _PhotoViewerScreenState extends State<PhotoViewerScreen> {
  late final _controller = PageController(initialPage: widget.initialIndex);
  late int _index = widget.initialIndex;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final photo = widget.photos[_index];
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: BrandAppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: Text(photo.label),
      ),
      body: Column(
        children: [
          Expanded(
            child: PageView.builder(
              controller: _controller,
              itemCount: widget.photos.length,
              onPageChanged: (i) => setState(() => _index = i),
              itemBuilder: (_, i) => InteractiveViewer(
                maxScale: 5,
                child: Center(
                  child: ProductImage(
                    widget.photos[i].url,
                    fit: BoxFit.contain,
                  ),
                ),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Text(
              [
                '${_index + 1} of ${widget.photos.length}',
                if (photo.source != null) photo.source!,
                'Pinch to zoom',
              ].join('  ·  '),
              style: const TextStyle(color: Colors.white70, fontSize: 12),
            ),
          ),
        ],
      ),
    );
  }
}
