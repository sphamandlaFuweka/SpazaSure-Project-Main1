import 'package:flutter/foundation.dart';
import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';
import 'package:spazasure_app/services/packaging_text_parser.dart';

/// On-device OCR for product packaging. ML Kit only runs on Android and iOS.
class PackagingOcrService {
  static bool get supported =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS);

  static Future<PackagingScan> scan(String imagePath) async {
    final recognizer = TextRecognizer(script: TextRecognitionScript.latin);
    try {
      final result = await recognizer.processImage(
        InputImage.fromFilePath(imagePath),
      );
      return PackagingTextParser.parse(result.text);
    } finally {
      await recognizer.close();
    }
  }
}
