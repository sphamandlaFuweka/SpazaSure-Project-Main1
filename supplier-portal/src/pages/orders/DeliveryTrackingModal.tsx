import { useEffect, useRef, useState } from 'react';
import { MapContainer, TileLayer, Marker, Popup, Polyline, useMap } from 'react-leaflet';
import L from 'leaflet';
import { Modal } from '../../components/ui';
import type { Order } from '../../types';
import { MapPin, Truck, Store, Navigation } from 'lucide-react';

// Fix default marker icon issue with Leaflet + bundlers
import 'leaflet/dist/leaflet.css';

interface Props {
  order: Order;
  onClose: () => void;
}

// Supplier location (Johannesburg CBD area - this would come from backend in production)
const SUPPLIER_COORDS: [number, number] = [-26.1952, 28.0348];

// Simulated driver position along the route
function getDriverPosition(supplier: [number, number], shop: [number, number], progress: number): [number, number] {
  return [
    supplier[0] + (shop[0] - supplier[0]) * progress,
    supplier[1] + (shop[1] - supplier[1]) * progress,
  ];
}

// Geocode shop address to coordinates (simulated - in production use a geocoding API)
function getShopCoords(shopAddress: string): [number, number] {
  // Use a hash of the address to generate consistent but varied coordinates around Johannesburg
  let hash = 0;
  for (let i = 0; i < shopAddress.length; i++) {
    hash = ((hash << 5) - hash) + shopAddress.charCodeAt(i);
    hash |= 0;
  }
  const latOffset = (Math.abs(hash % 100) - 50) * 0.0003;
  const lngOffset = (Math.abs((hash >> 8) % 100) - 50) * 0.0003;
  return [-26.2041 + latOffset, 28.0473 + lngOffset];
}

// Delivery progress based on order status
function getDeliveryProgress(status: string): number {
  switch (status) {
    case 'processing': return 0.0;
    case 'dispatched': return 0.55;
    case 'delivered': return 1.0;
    default: return 0.0;
  }
}

// Custom marker icons using SVG
function createIcon(color: string, iconSvg: string): L.DivIcon {
  return L.divIcon({
    className: 'custom-marker',
    html: `
      <div style="
        width: 40px; height: 40px; border-radius: 50%;
        background: ${color}; border: 3px solid white;
        box-shadow: 0 3px 10px ${color}66;
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

const driverIcon = createIcon('#16a34a',
  '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><rect x="1" y="3" width="15" height="13" rx="2"/><polygon points="16 8 20 8 23 11 23 16 16 16 16 8"/><circle cx="5.5" cy="18.5" r="2.5"/><circle cx="18.5" cy="18.5" r="2.5"/></svg>'
);

const shopIcon = createIcon('#f59e0b',
  '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>'
);

// Component to fit bounds to markers
function FitBounds({ points }: { points: [number, number][] }) {
  const map = useMap();
  useEffect(() => {
    if (points.length >= 2) {
      const bounds = L.latLngBounds(points.map(p => L.latLng(p[0], p[1])));
      map.fitBounds(bounds, { padding: [50, 50] });
    }
  }, [map, points]);
  return null;
}

// Animated driver marker that moves along the route
function AnimatedDriver({ from, to, status }: { from: [number, number]; to: [number, number]; status: string }) {
  const [progress, setProgress] = useState(getDeliveryProgress(status));

  useEffect(() => {
    if (status !== 'dispatched') return;
    // Simulate movement for dispatched orders
    const interval = setInterval(() => {
      setProgress(prev => {
        const next = prev + 0.003;
        return next >= 0.95 ? 0.55 : next; // Loop around for demo
      });
    }, 200);
    return () => clearInterval(interval);
  }, [status]);

  const pos = getDriverPosition(from, to, progress);

  if (status === 'pending' || status === 'confirmed') return null;

  return (
    <Marker position={pos} icon={driverIcon}>
      <Popup>
        <div className="text-center">
          <p className="font-bold text-sm">Delivery Driver</p>
          <p className="text-xs text-gray-500">
            {status === 'dispatched' ? 'In transit' : status === 'delivered' ? 'Delivered' : 'Preparing'}
          </p>
        </div>
      </Popup>
    </Marker>
  );
}

export default function DeliveryTrackingModal({ order, onClose }: Props) {
  const shopCoords = getShopCoords(order.shopAddress || order.shopName);
  const supplierCoords = SUPPLIER_COORDS;
  const progress = getDeliveryProgress(order.status);
  const driverPos = getDriverPosition(supplierCoords, shopCoords, progress);

  // Route points (in production, use a routing API for real road directions)
  const routePoints: [number, number][] = [
    supplierCoords,
    // Add intermediate points for a more realistic route
    [
      supplierCoords[0] + (shopCoords[0] - supplierCoords[0]) * 0.3,
      supplierCoords[1] + (shopCoords[1] - supplierCoords[1]) * 0.2,
    ],
    [
      supplierCoords[0] + (shopCoords[0] - supplierCoords[0]) * 0.6,
      supplierCoords[1] + (shopCoords[1] - supplierCoords[1]) * 0.7,
    ],
    shopCoords,
  ];

  const statusMessage = () => {
    switch (order.status) {
      case 'pending': return { text: 'Awaiting confirmation', color: 'text-amber-600', bg: 'bg-amber-50 border-amber-200' };
      case 'confirmed': return { text: 'Order confirmed, preparing for dispatch', color: 'text-blue-600', bg: 'bg-blue-50 border-blue-200' };
      case 'processing': return { text: 'Order being packed and prepared', color: 'text-violet-600', bg: 'bg-violet-50 border-violet-200' };
      case 'dispatched': return { text: 'Driver is on the way to the spaza shop', color: 'text-green-600', bg: 'bg-green-50 border-green-200' };
      case 'delivered': return { text: 'Order delivered successfully', color: 'text-emerald-600', bg: 'bg-emerald-50 border-emerald-200' };
      default: return { text: 'Order status unknown', color: 'text-gray-600', bg: 'bg-gray-50 border-gray-200' };
    }
  };

  const msg = statusMessage();

  return (
    <Modal title={`Track Delivery — ${order.orderNumber}`} onClose={onClose} size="xl">
      <div className="p-0">
        {/* Map */}
        <div className="h-[400px] w-full relative">
          <MapContainer
            center={driverPos}
            zoom={14}
            style={{ height: '100%', width: '100%' }}
            scrollWheelZoom={true}
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />

            <FitBounds points={[supplierCoords, shopCoords]} />

            {/* Route line - completed portion */}
            <Polyline
              positions={routePoints}
              pathOptions={{ color: '#16a34a', weight: 4, opacity: 0.8, dashArray: order.status === 'dispatched' ? undefined : '8 12' }}
            />

            {/* Route line - remaining portion (lighter) */}
            {order.status === 'dispatched' && (
              <Polyline
                positions={[driverPos, ...routePoints.slice(2)]}
                pathOptions={{ color: '#16a34a', weight: 3, opacity: 0.3, dashArray: '8 12' }}
              />
            )}

            {/* Supplier marker */}
            <Marker position={supplierCoords} icon={supplierIcon}>
              <Popup>
                <div className="text-center">
                  <p className="font-bold text-sm">Supplier Warehouse</p>
                  <p className="text-xs text-gray-500">Origin</p>
                </div>
              </Popup>
            </Marker>

            {/* Shop destination marker */}
            <Marker position={shopCoords} icon={shopIcon}>
              <Popup>
                <div className="text-center">
                  <p className="font-bold text-sm">{order.shopName}</p>
                  <p className="text-xs text-gray-500">{order.shopAddress}</p>
                </div>
              </Popup>
            </Marker>

            {/* Animated driver */}
            <AnimatedDriver from={supplierCoords} to={shopCoords} status={order.status} />
          </MapContainer>
        </div>

        {/* Info Panel */}
        <div className="p-5 space-y-4">
          {/* Status Banner */}
          <div className={`flex items-center gap-3 px-4 py-3 rounded-xl border ${msg.bg}`}>
            <Navigation size={16} className={msg.color} />
            <p className={`text-sm font-semibold ${msg.color}`}>{msg.text}</p>
          </div>

          {/* Route Info */}
          <div className="grid grid-cols-2 gap-4">
            {/* From */}
            <div className="flex items-start gap-3 p-3 bg-blue-50/60 rounded-xl border border-blue-100">
              <div className="w-8 h-8 bg-blue-500 rounded-lg flex items-center justify-center flex-shrink-0">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 21h18"/><path d="M5 21V7l8-4v18"/><path d="M19 21V11l-6-4"/></svg>
              </div>
              <div>
                <p className="text-[10px] font-bold text-blue-600 uppercase">From: Supplier</p>
                <p className="text-sm font-semibold text-gray-900 mt-0.5">Your Warehouse</p>
                <p className="text-xs text-gray-500">Johannesburg</p>
              </div>
            </div>

            {/* To */}
            <div className="flex items-start gap-3 p-3 bg-amber-50/60 rounded-xl border border-amber-100">
              <div className="w-8 h-8 bg-amber-500 rounded-lg flex items-center justify-center flex-shrink-0">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>
              </div>
              <div>
                <p className="text-[10px] font-bold text-amber-600 uppercase">To: Spaza Shop</p>
                <p className="text-sm font-semibold text-gray-900 mt-0.5">{order.shopName}</p>
                <p className="text-xs text-gray-500">{order.shopAddress || 'Address on file'}</p>
              </div>
            </div>
          </div>

          {/* Legend */}
          <div className="flex items-center justify-center gap-5 pt-2">
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-blue-500 ring-2 ring-blue-200" />
              <span className="text-xs text-gray-500 font-medium">Supplier</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-green-500 ring-2 ring-green-200" />
              <span className="text-xs text-gray-500 font-medium">Driver</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded-full bg-amber-500 ring-2 ring-amber-200" />
              <span className="text-xs text-gray-500 font-medium">Spaza Shop</span>
            </div>
          </div>

          {/* Order Details Quick Info */}
          <div className="flex items-center justify-between text-xs text-gray-400 border-t border-gray-100 pt-3">
            <span>{order.items.length} item{order.items.length !== 1 ? 's' : ''} • R{order.total.toLocaleString()}</span>
            <span className="capitalize">{order.deliveryOption} delivery</span>
          </div>
        </div>
      </div>
    </Modal>
  );
}
