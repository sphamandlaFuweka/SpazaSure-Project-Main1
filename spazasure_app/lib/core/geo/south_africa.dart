class GeoBox {
  final double south;
  final double west;
  final double north;
  final double east;

  const GeoBox(this.south, this.west, this.north, this.east);

  bool contains(double lat, double lng) =>
      lat >= south && lat <= north && lng >= west && lng <= east;

  double get centerLat => (south + north) / 2;
  double get centerLng => (west + east) / 2;
}

class Province {
  final String name;
  final GeoBox box;
  const Province(this.name, this.box);
}

/// Approximate rectangles; good enough to choose a map viewport, not for borders.
class SouthAfrica {
  static const bounds = GeoBox(-34.9, 16.4, -22.1, 32.9);

  static const provinces = [
    Province('Gauteng', GeoBox(-26.9, 27.2, -25.1, 29.1)),
    Province('KwaZulu-Natal', GeoBox(-31.1, 28.7, -26.85, 32.9)),
    Province('Mpumalanga', GeoBox(-27.2, 28.9, -24.0, 32.1)),
    Province('Limpopo', GeoBox(-25.4, 26.4, -22.1, 31.6)),
    Province('North West', GeoBox(-28.2, 22.5, -24.2, 28.2)),
    Province('Free State', GeoBox(-30.7, 24.2, -26.6, 29.9)),
    Province('Eastern Cape', GeoBox(-34.2, 22.7, -30.2, 30.3)),
    Province('Western Cape', GeoBox(-34.9, 17.8, -30.4, 24.3)),
    Province('Northern Cape', GeoBox(-31.5, 16.4, -26.4, 25.0)),
  ];

  /// Where boxes overlap, the province whose centre is nearest wins.
  static Province? provinceAt(double lat, double lng) {
    Province? best;
    var bestDistance = double.infinity;
    for (final province in provinces) {
      if (!province.box.contains(lat, lng)) continue;
      final dLat = province.box.centerLat - lat;
      final dLng = province.box.centerLng - lng;
      final distance = dLat * dLat + dLng * dLng;
      if (distance < bestDistance) {
        best = province;
        bestDistance = distance;
      }
    }
    return best;
  }
}
