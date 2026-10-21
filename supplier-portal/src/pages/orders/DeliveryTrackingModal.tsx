import { useEffect, useState } from 'react';
import { MapContainer, TileLayer, Marker, Popup, Polyline, useMap } from 'react-leaflet';
import L from 'leaflet';
import { Modal } from '../../components/ui';
import type { Order } from '../../types';
import { MapPin, Navigation, Loader2, Check } from 'lucide-react';
import clsx from 'clsx';
import { profileApi } from '../../services/api';
import { useAuthStore } from '../../store/authStore';
import { formatCurrency } from '../../utils';
// Fix default marker icon issue with Leaflet + bundlers
import 'leaflet/dist/leaflet.css';

interface Props {
  order: Order;
  onClose: () => void;
}

type LatLng = [number, number];

// Custom marker icons using SVG
function createIcon(color: string, iconSvg: string): L.DivIcon {
  return L.divIcon({
    className: 'custom-marker',
    html: `
      <div style="
        width: 40px; height: 40px; border-radius: 50%;
        background: ${color}; border: 3px solid white;
        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
        display: flex; align-items: center; justify-content: center;
      ">
        ${iconSvg}
      </div>
    `,
    iconSize: [40, 40],
    iconAnchor: [20, 20],
  });
}

const supplierIcon = createIcon('#3b82f6',
  '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M3 21h18"/><path d="M5 21V7l8-4v18"/><path d="M19 21V11l-6-4"/><path d="M9 9h1"/><path d="M9 13h1"/><path d="M9 17h1"/></svg>'
);

const shopIcon = createIcon('#f59e0b',
  '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>'
);

function FitBounds({ points }: { points: LatLng[] }) {
  const map = useMap();
  useEffect(() => {
    if (points.length >= 2) {
      map.fitBounds(L.latLngBounds(points.map((p) => L.latLng(p[0], p[1]))), { padding: [50, 50] });
    } else if (points.length === 1) {
      map.setView(points[0], 14);
    }
  }, [map, points]);
  return null;
}

/** Straight-line distance in km (haversine) */
function distanceKm(a: LatLng, b: LatLng) {
  const toRad = (d: number) => (d * Math.PI) / 180;
  const dLat = toRad(b[0] - a[0]);
  const dLng = toRad(b[1] - a[1]);
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(a[0])) * Math.cos(toRad(b[0])) * Math.sin(dLng / 2) ** 2;
  return 6371 * 2 * Math.asin(Math.sqrt(h));
}

const STEPS = [
  { status: 'pending',    label: 'Placed' },
  { status: 'processing', label: 'Preparing' },
  { status: 'dispatched', label: 'Dispatched' },
  { status: 'delivered',  label: 'Delivered' },
];

const STATUS_MESSAGE: Record<string, { text: string; color: string; bg: string }> = {
  pending:    { text: 'Awaiting your confirmation',            color: 'text-amber-600',   bg: 'bg-amber-50 border-amber-200' },
  confirmed:  { text: 'Confirmed — ready to be prepared',      color: 'text-blue-600',    bg: 'bg-blue-50 border-blue-200' },
  processing: { text: 'Being packed and prepared for dispatch', color: 'text-violet-600',  bg: 'bg-violet-50 border-violet-200' },
  ready:      { text: 'Packed: waiting for SpazaSure to collect', color: 'text-sky-600',  bg: 'bg-sky-50 border-sky-200' },
  dispatched: { text: 'Collected by SpazaSure, out for delivery', color: 'text-green-600',   bg: 'bg-green-50 border-green-200' },
  delivered:  { text: 'Delivered',                             color: 'text-emerald-600', bg: 'bg-emerald-50 border-emerald-200' },
  cancelled:  { text: 'This order was cancelled',              color: 'text-gray-600',    bg: 'bg-gray-50 border-gray-200' },
};

export default function DeliveryTrackingModal({ order, onClose }: Props) {
  const { user } = useAuthStore();
  const [supplierCoords, setSupplierCoords] = useState<LatLng | null>(null);
  const [supplierCity, setSupplierCity] = useState<string>('');
  const [loadingOrigin, setLoadingOrigin] = useState(true);

  // The supplier's saved business location is the delivery origin
  useEffect(() => {
    let cancelled = false;
    profileApi.get()
      .then((p) => {
        if (cancelled) return;
        if (p.latitude != null && p.longitude != null) setSupplierCoords([p.latitude, p.longitude]);
        setSupplierCity([p.city, p.province].filter(Boolean).join(', '));
      })
      .catch(() => {})
      .finally(() => { if (!cancelled) setLoadingOrigin(false); });
    return () => { cancelled = true; };
  }, []);

  const shopCoords: LatLng | null = order.deliveryLatitude != null && order.deliveryLongitude != null
    ? [order.deliveryLatitude, order.deliveryLongitude]
    : null;
  const points = [supplierCoords, shopCoords].filter(Boolean) as LatLng[];
  const straightKm = supplierCoords && shopCoords ? distanceKm(supplierCoords, shopCoords) : null;
  const km = order.deliveryDistanceKm ?? straightKm;

  const msg = STATUS_MESSAGE[order.status] ?? { text: `Status: ${order.status}`, color: 'text-gray-600', bg: 'bg-gray-50 border-gray-200' };
  const stepStatus = order.status === 'confirmed' ? 'pending' : order.status;
  const currentStep = STEPS.findIndex((s) => s.status === stepStatus);

  const directionsUrl = shopCoords
    ? `https://www.google.com/maps/dir/?api=1&destination=${shopCoords[0]},${shopCoords[1]}${supplierCoords ? `&origin=${supplierCoords[0]},${supplierCoords[1]}` : ''}`
    : null;

  return (
    <Modal title={`Delivery — ${order.orderNumber}`} onClose={onClose} size="xl">
      <div className="p-0">
        {/* Map */}
        <div className="h-[300px] sm:h-[380px] w-full relative bg-gray-100">
          {loadingOrigin ? (
            <div className="absolute inset-0 flex items-center justify-center">
              <Loader2 size={28} className="animate-spin text-gray-300" />
            </div>
          ) : points.length === 0 ? (
            <div className="absolute inset-0 flex flex-col items-center justify-center text-center px-6">
              <MapPin size={32} className="text-gray-300 mb-2" />
              <p className="text-sm font-semibold text-gray-600">No map location for this order</p>
              <p className="text-xs text-gray-400 mt-1 max-w-sm">
                The shop didn't share a delivery pin and your business location isn't set. Add your location under Profile to see it here.
              </p>
            </div>
          ) : (
            <MapContainer center={points[0]} zoom={13} style={{ height: '100%', width: '100%' }} scrollWheelZoom>
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
              <FitBounds points={points} />

              {supplierCoords && shopCoords && (
                <Polyline
                  positions={[supplierCoords, shopCoords]}
                  pathOptions={{ color: '#16a34a', weight: 3, opacity: 0.7, dashArray: order.status === 'delivered' ? undefined : '8 10' }}
                />
              )}

              {supplierCoords && (
                <Marker position={supplierCoords} icon={supplierIcon}>
                  <Popup>
                    <div className="text-center">
                      <p className="font-bold text-sm">{user?.companyName ?? 'Your business'}</p>
                      <p className="text-xs text-gray-500">Origin</p>
                    </div>
                  </Popup>
                </Marker>
              )}

              {shopCoords && (
                <Marker position={shopCoords} icon={shopIcon}>
                  <Popup>
                    <div className="text-center">
                      <p className="font-bold text-sm">{order.shopName}</p>
                      <p className="text-xs text-gray-500">{order.shopAddress}</p>
                    </div>
                  </Popup>
                </Marker>
              )}
            </MapContainer>
          )}
        </div>

        {/* Info Panel */}
        <div className="p-5 space-y-4">
          <div className={clsx('flex items-center gap-3 px-4 py-3 rounded-xl border', msg.bg)}>
            <Navigation size={16} className={msg.color} />
            <p className={clsx('text-sm font-semibold', msg.color)}>{msg.text}</p>
          </div>

          {/* Progress steps */}
          {order.status !== 'cancelled' && (
            <ol className="flex items-center" aria-label="Delivery progress">
              {STEPS.map((step, i) => {
                const done = i <= currentStep;
                return (
                  <li key={step.status} className="flex-1 flex items-center last:flex-none">
                    <div className="flex flex-col items-center gap-1">
                      <span className={clsx('w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold',
                        done ? 'bg-primary text-white' : 'bg-gray-100 text-gray-400')}>
                        {done ? <Check size={14} /> : i + 1}
                      </span>
                      <span className={clsx('text-[11px] font-semibold whitespace-nowrap', done ? 'text-primary' : 'text-gray-400')}>{step.label}</span>
                    </div>
                    {i < STEPS.length - 1 && (
                      <span className={clsx('flex-1 h-0.5 mx-2 mb-5 rounded', i < currentStep ? 'bg-primary' : 'bg-gray-200')} />
                    )}
                  </li>
                );
              })}
            </ol>
          )}

          {/* Route Info */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="flex items-start gap-3 p-3 bg-blue-50/60 rounded-xl border border-blue-100">
              <span className="w-3 h-3 mt-1 rounded-full bg-blue-500 ring-2 ring-blue-200 flex-shrink-0" />
              <div className="min-w-0">
                <p className="text-[10px] font-bold text-blue-600 uppercase">From</p>
                <p className="text-sm font-semibold text-gray-900 mt-0.5 truncate">{user?.companyName ?? 'Your business'}</p>
                <p className="text-xs text-gray-500">{supplierCity || (supplierCoords ? 'Saved business location' : 'Location not set')}</p>
              </div>
            </div>
            <div className="flex items-start gap-3 p-3 bg-amber-50/60 rounded-xl border border-amber-100">
              <span className="w-3 h-3 mt-1 rounded-full bg-amber-500 ring-2 ring-amber-200 flex-shrink-0" />
              <div className="min-w-0">
                <p className="text-[10px] font-bold text-amber-600 uppercase">To</p>
                <p className="text-sm font-semibold text-gray-900 mt-0.5 truncate">{order.shopName}</p>
                <p className="text-xs text-gray-500">{order.shopAddress || (shopCoords ? 'Pinned location' : 'Address not provided')}</p>
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3 text-xs text-gray-500 border-t border-gray-100 pt-3">
            <span>
              {order.items.length} item{order.items.length !== 1 ? 's' : ''} • {formatCurrency(order.total)}
              {km != null && <> • {km.toFixed(1)} km{order.deliveryDistanceKm == null ? ' (straight line)' : ''}</>}
            </span>
            {directionsUrl && (
              <a href={directionsUrl} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1.5 font-semibold text-primary hover:underline">
                <Navigation size={12} /> Open directions
              </a>
            )}
          </div>
        </div>
      </div>
    </Modal>
  );
}
