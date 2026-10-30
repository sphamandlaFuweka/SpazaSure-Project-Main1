import { TileLayer } from 'react-leaflet';
import type { LatLngBoundsExpression } from 'leaflet';

/** South Africa with a little room around its borders (south-west and north-east corners). */
export const SOUTH_AFRICA_BOUNDS: LatLngBoundsExpression = [[-35.5, 15.8], [-21.6, 33.6]];

/**
 * Props for every MapContainer: SpazaSure only works in South Africa, so the map can't be
 * dragged or zoomed out past it.
 */
export const SOUTH_AFRICA_MAP = {
  maxBounds: SOUTH_AFRICA_BOUNDS,
  maxBoundsViscosity: 1,
  minZoom: 5,
} as const;

/**
 * OpenStreetMap tiles tinted in SpazaSure navy (the `brand-map-tint` filter in index.html,
 * applied by the `.brand-map` class in index.css): light land, blue water, navy text.
 */
export function BrandTileLayer() {
  return (
    <TileLayer
      className="brand-map"
      attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
      url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      bounds={SOUTH_AFRICA_BOUNDS}
    />
  );
}
