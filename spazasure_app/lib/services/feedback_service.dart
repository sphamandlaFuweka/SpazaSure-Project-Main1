import 'package:flutter/foundation.dart';
import 'package:package_info_plus/package_info_plus.dart';

import 'api_service.dart';

/// Sends tester feedback (bug, idea, question or praise) to the SpazaSure team.
class FeedbackService {
  final String basePath;

  const FeedbackService(this.basePath);

  static const shop = FeedbackService('/shop/profile/feedback');
  static const customer = FeedbackService('/customer/profile/feedback');

  static const types = {
    'bug': "Something's not working",
    'idea': 'An idea',
    'question': 'A question',
    'praise': 'Something I like',
  };

  /// Sends it with the app version and platform, so the team can reproduce problems.
  Future<void> send({
    required String type,
    required String message,
    int? rating,
    String? screen,
    List<int>? screenshotBytes,
    String? screenshotName,
  }) async {
    var version = '';
    try {
      final info = await PackageInfo.fromPlatform();
      version = '${info.version}+${info.buildNumber}';
    } catch (_) {
      // Not available in tests or on some platforms; the feedback still matters
    }
    await ApiService.sendForm(
      basePath,
      fields: {
        'type': type,
        'message': message,
        if (rating != null) 'rating': '$rating',
        if (screen != null) 'screen': screen,
        'source': 'mobile_app',
        if (version.isNotEmpty) 'appVersion': version,
        'platform': kIsWeb ? 'web' : defaultTargetPlatform.name,
      },
      fileField: 'screenshot',
      fileBytes: screenshotBytes,
      fileName: screenshotName,
    );
  }
}
