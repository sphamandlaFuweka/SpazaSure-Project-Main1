import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:spazasure_app/core/geo/south_africa.dart';

/// SpazaSure maps: locked to South Africa and drawn in the brand's navy.
abstract final class BrandMap {
  /// South Africa with a little room around its borders.
  static final bounds = LatLngBounds(
    LatLng(SouthAfrica.bounds.south - 0.6, SouthAfrica.bounds.west - 0.6),
    LatLng(SouthAfrica.bounds.north + 0.5, SouthAfrica.bounds.east + 0.7),
  );

  /// Keeps the camera inside South Africa, so the map can't be dragged away from it.
  static final cameraConstraint = CameraConstraint.contain(bounds: bounds);

  /// Zoomed out no further than the whole country.
  static const minZoom = 5.0;

  /// Outside the tiles (never visible inside South Africa): the brand's light blue.
  static const background = Color(0xFFEEF2FF);

  /// Brightness mapped from SpazaSure navy (#25449A) in dark areas to white in light ones:
  /// light land, blue water, navy roads and labels. Same tint as the portal's maps.
  static const _tint = ColorFilter.matrix(<double>[
    0.1818, 0.6116, 0.0617, 0, 37, //
    0.1559, 0.5245, 0.0529, 0, 68, //
    0.0842, 0.2834, 0.0286, 0, 154, //
    0, 0, 0, 1, 0, //
  ]);

  /// OpenStreetMap tiles in the brand tint.
  static TileLayer tiles() => TileLayer(
    urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    userAgentPackageName: 'com.spazasure.spazasure_app',
    tileBuilder: (context, tile, _) =>
        ColorFiltered(colorFilter: _tint, child: tile),
  );
}
