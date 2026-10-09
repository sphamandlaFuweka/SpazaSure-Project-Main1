import 'dart:math';

import 'package:geolocator/geolocator.dart';
import 'package:shared_preferences/shared_preferences.dart';

class DeviceFix {
  final double latitude;
  final double longitude;
  const DeviceFix(this.latitude, this.longitude);
}

/// Device-level data sent with scans and reports so repeat or cloned scans can be traced.
class DeviceContext {
  static const _idKey = 'device_install_id';

  /// Random ID created once per install; not tied to any hardware identifier.
  static Future<String> deviceId() async {
    final prefs = await SharedPreferences.getInstance();
    var id = prefs.getString(_idKey);
    if (id == null) {
      final rnd = Random.secure();
      id = List.generate(
        16,
        (_) => rnd.nextInt(256).toRadixString(16).padLeft(2, '0'),
      ).join();
      await prefs.setString(_idKey, id);
    }
    return id;
  }

  /// Current position, or null when location is off or permission is denied.
  static Future<DeviceFix?> location() async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) return null;
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        return null;
      }
      final p = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.low,
          timeLimit: Duration(seconds: 8),
        ),
      );
      return DeviceFix(p.latitude, p.longitude);
    } catch (_) {
      return null;
    }
  }
}
