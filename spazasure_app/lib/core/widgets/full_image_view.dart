import 'package:flutter/material.dart';
import 'package:spazasure_app/core/widgets/brand_app_bar.dart';

/// Shows one image on its own, whole and zoomable, so the text on a poster can be read.
class FullImageView extends StatelessWidget {
  final String url;
  final String title;

  const FullImageView({super.key, required this.url, required this.title});

  static Future<void> open(BuildContext context, String url, String title) =>
      Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => FullImageView(url: url, title: title),
        ),
      );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: BrandAppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: Text(title),
      ),
      body: Column(
        children: [
          Expanded(
            child: InteractiveViewer(
              maxScale: 5,
              child: Center(
                child: Image.network(
                  url,
                  fit: BoxFit.contain,
                  loadingBuilder: (_, child, progress) => progress == null
                      ? child
                      : const CircularProgressIndicator(color: Colors.white),
                  errorBuilder: (_, __, ___) => const Text(
                    'Could not load the picture',
                    style: TextStyle(color: Colors.white70),
                  ),
                ),
              ),
            ),
          ),
          const Padding(
            padding: EdgeInsets.all(16),
            child: Text(
              'Pinch to zoom',
              style: TextStyle(color: Colors.white54),
            ),
          ),
        ],
      ),
    );
  }
}
