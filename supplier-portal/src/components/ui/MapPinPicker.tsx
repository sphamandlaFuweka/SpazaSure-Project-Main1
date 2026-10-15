import { useEffect, useRef, useState } from 'react';
import { MapContainer, TileLayer, useMapEvents } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { MapPin, Check, Loader2, Hand } from 'lucide-react';
import { Modal } from './index';
import { reverseAddress, type AddressSuggestion } from '../../services/address';

/** Johannesburg, if we know nothing better */
const FALLBACK: [number, number] = [-26.2041, 28.0473];

function CenterTracker({ onMove, onMoving }: { onMove: (lat: number, lng: number) => void; onMoving: () => void }) {
  const map = useMapEvents({
    movestart: onMoving,
    moveend: () => { const c = map.getCenter(); onMove(c.lat, c.lng); },
  });
  return null;
}

/**
 * Place an address exactly on the map when search can't find it (common for township stands).
 * The pin stays in the centre; the map moves underneath it.
 */
export default function MapPinPicker({ initial, typedAddress, onPick, onClose }: {
  initial?: [number, number];
  typedAddress?: string;
  onPick: (place: AddressSuggestion) => void;
  onClose: () => void;
}) {
  const [center, setCenter] = useState<[number, number]>(initial ?? FALLBACK);
  const [place, setPlace] = useState<AddressSuggestion | null>(null);
  const [moving, setMoving] = useState(false);
  const [looking, setLooking] = useState(false);
  const request = useRef(0);

  const lookUp = async (lat: number, lng: number) => {
    const id = ++request.current;
    setLooking(true);
    try {
      const found = await reverseAddress(lat, lng);
      if (id === request.current) setPlace(found);
    } catch {
      if (id === request.current) setPlace(null);
    } finally {
      if (id === request.current) setLooking(false);
    }
  };

  useEffect(() => { lookUp(center[0], center[1]); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const confirm = () => {
    const typed = typedAddress?.trim() ?? '';
    // Keep the exact pin, and the street the user typed if the map only knows the area
    const street = typed && (!place || !place.street) ? typed : place?.street ?? typed;
    const label = [street, place?.suburb, place?.city, place?.province, place?.postalCode].filter(Boolean).join(', ');
    onPick({
      label, street,
      suburb: place?.suburb ?? '', city: place?.city ?? '', province: place?.province ?? '', postalCode: place?.postalCode ?? '',
      latitude: center[0], longitude: center[1],
    });
  };

  return (
    <Modal title="Pin the exact location" onClose={onClose} size="lg">
      <div className="p-4 space-y-3">
        <p className="flex items-center gap-2 text-sm text-ink-2">
          <Hand size={15} className="text-brand-blue" /> Drag the map so the pin sits on the entrance of the place.
        </p>
        <div className="relative h-[360px] rounded-2xl overflow-hidden border border-brand-blue-light">
          <MapContainer center={center} zoom={initial ? 17 : 12} style={{ height: '100%', width: '100%' }} scrollWheelZoom>
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <CenterTracker
              onMoving={() => setMoving(true)}
              onMove={(lat, lng) => { setMoving(false); setCenter([lat, lng]); lookUp(lat, lng); }}
            />
          </MapContainer>
          <div className="pointer-events-none absolute inset-0 z-[400] flex items-center justify-center">
            <MapPin size={44} className={`-mt-10 text-brand-red drop-shadow-lg transition-transform ${moving ? 'scale-110' : ''}`} fill="#fff" />
          </div>
        </div>
        <div className="flex flex-col sm:flex-row sm:items-center gap-3">
          <p className="flex-1 text-sm font-semibold text-gray-900 flex items-center gap-2 min-h-[20px]">
            {moving || looking ? <><Loader2 size={14} className="animate-spin text-ink-3" /> Finding this place...</> : (place?.label || 'Location selected')}
          </p>
          <div className="flex gap-2 justify-end">
            <button type="button" onClick={onClose} className="btn-secondary">Cancel</button>
            <button type="button" onClick={confirm} disabled={moving} className="btn-primary"><Check size={14} /> Use this location</button>
          </div>
        </div>
      </div>
    </Modal>
  );
}
