import { useEffect, useMemo, useState } from 'react';
import { MapContainer, TileLayer, Marker, useMap } from 'react-leaflet';
import L from 'leaflet';
import toast from 'react-hot-toast';
import { MapPin, Phone, MessageCircle, Mail, Navigation, BadgeCheck, ShieldAlert, Search } from 'lucide-react';
import clsx from 'clsx';
import 'leaflet/dist/leaflet.css';
import { adminSpazaOwnersApi } from '../../services/api';
import PageLoader from '../../components/ui/PageLoader';

interface Shop {
  id: string;
  shopName: string;
  ownerName: string;
  email: string;
  phone: string;
  address: string;
  city: string;
  province: string;
  latitude?: number | null;
  longitude?: number | null;
  isVerified: boolean;
  status: string;
  complianceStatus?: string;
  joinedAt?: string;
}

type Filter = 'all' | 'verified' | 'unverified';

const GREEN = '#16a34a';
const RED = '#dc2626';

function pinIcon(color: string, selected: boolean) {
  const size = selected ? 44 : 34;
  return L.divIcon({
    className: 'shop-pin',
    html: `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="${color}" stroke="white" stroke-width="1.5" style="filter:drop-shadow(0 2px 3px rgba(0,0,0,.35))"><path d="M12 22s7-6.2 7-12a7 7 0 1 0-14 0c0 5.8 7 12 7 12z"/><circle cx="12" cy="10" r="2.6" fill="white" stroke="none"/></svg>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size],
  });
}

function whatsAppNumber(phone: string) {
  const digits = phone.replace(/\D/g, '');
  return digits.startsWith('0') ? `27${digits.slice(1)}` : digits;
}

function FlyTo({ shop }: { shop?: Shop }) {
  const map = useMap();
  useEffect(() => {
    if (shop?.latitude != null && shop.longitude != null) {
      map.flyTo([shop.latitude, shop.longitude], Math.max(map.getZoom(), 13), { duration: 0.6 });
    }
  }, [map, shop]);
  return null;
}

function FitAll({ points }: { points: [number, number][] }) {
  const map = useMap();
  useEffect(() => {
    if (points.length > 0) map.fitBounds(L.latLngBounds(points), { padding: [40, 40], maxZoom: 14 });
  }, [map, points]);
  return null;
}

export default function AdminShopsMapPage() {
  const [shops, setShops] = useState<Shop[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<Filter>('all');
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const load = async () => {
    try {
      const res = await adminSpazaOwnersApi.list({ page: 1, pageSize: 500 });
      setShops((res?.data?.items ?? res?.items ?? []) as Shop[]);
    } catch {
      toast.error('Could not load shops');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return shops.filter((s) =>
      (filter === 'all' || (filter === 'verified' ? s.isVerified : !s.isVerified)) &&
      (!term || [s.shopName, s.ownerName, s.city, s.address].some((v) => v?.toLowerCase().includes(term))));
  }, [shops, filter, search]);

  const pinned = visible.filter((s) => s.latitude != null && s.longitude != null);
  const points = useMemo(() => pinned.map((s) => [s.latitude!, s.longitude!] as [number, number]), [pinned]);
  const selected = shops.find((s) => s.id === selectedId);
  const counts = {
    verified: shops.filter((s) => s.isVerified).length,
    unverified: shops.filter((s) => !s.isVerified).length,
  };

  const verify = async (shop: Shop) => {
    try {
      await adminSpazaOwnersApi.verify(shop.id);
      toast.success(`${shop.shopName} verified`);
      setShops((prev) => prev.map((s) => (s.id === shop.id ? { ...s, isVerified: true, status: 'verified' } : s)));
    } catch {
      toast.error('Verification failed');
    }
  };

  if (loading) return <PageLoader variant="dashboard" />;

  return (
    <div className="p-6 compact:p-4 space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-black text-gray-900">Shops Map</h1>
          <p className="text-sm text-gray-500">
            {counts.verified} verified, {counts.unverified} unverified. Green pins are verified, red pins are not.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {(['all', 'verified', 'unverified'] as const).map((f) => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={clsx(
                'px-4 py-1.5 rounded-full text-sm font-semibold border transition-colors capitalize',
                filter === f ? 'bg-primary text-white border-primary' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
              )}
            >
              {f}
            </button>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-3 gap-4">
        <div className="col-span-2 card overflow-hidden h-[560px] compact:h-[440px]">
          <MapContainer center={[-28.5, 24.7]} zoom={5} style={{ height: '100%', width: '100%' }} scrollWheelZoom>
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <FitAll points={points} />
            <FlyTo shop={selected} />
            {pinned.map((s) => (
              <Marker
                key={s.id}
                position={[s.latitude!, s.longitude!]}
                icon={pinIcon(s.isVerified ? GREEN : RED, s.id === selectedId)}
                eventHandlers={{ click: () => setSelectedId(s.id) }}
              />
            ))}
          </MapContainer>
        </div>

        <div className="card flex flex-col h-[560px] compact:h-[440px] overflow-hidden">
          {selected ? (
            <div className="p-5 overflow-y-auto space-y-3">
              <button onClick={() => setSelectedId(null)} className="text-xs font-semibold text-primary hover:underline">
                Back to list
              </button>
              <div className="flex items-start justify-between gap-2">
                <h2 className="text-lg font-black text-gray-900">{selected.shopName}</h2>
                <span className={clsx(
                  'flex items-center gap-1 text-xs font-bold px-2.5 py-1 rounded-full',
                  selected.isVerified ? 'bg-emerald-50 text-emerald-700' : 'bg-red-50 text-red-700'
                )}>
                  {selected.isVerified ? <BadgeCheck size={13} /> : <ShieldAlert size={13} />}
                  {selected.isVerified ? 'Verified' : 'Unverified'}
                </span>
              </div>
              <dl className="text-sm space-y-1.5">
                <div><dt className="inline text-gray-400">Owner: </dt><dd className="inline font-semibold">{selected.ownerName || '-'}</dd></div>
                <div><dt className="inline text-gray-400">Address: </dt><dd className="inline font-semibold">{[selected.address, selected.city, selected.province].filter(Boolean).join(', ') || '-'}</dd></div>
                <div><dt className="inline text-gray-400">Phone: </dt><dd className="inline font-semibold">{selected.phone || '-'}</dd></div>
                <div><dt className="inline text-gray-400">Email: </dt><dd className="inline font-semibold">{selected.email || '-'}</dd></div>
                <div><dt className="inline text-gray-400">Compliance: </dt><dd className="inline font-semibold capitalize">{selected.complianceStatus || '-'}</dd></div>
                <div><dt className="inline text-gray-400">Status: </dt><dd className="inline font-semibold capitalize">{selected.status}</dd></div>
              </dl>
              <div className="flex flex-wrap gap-2 pt-1">
                {selected.phone && (
                  <a className="btn-primary flex items-center gap-1.5 text-sm" href={`tel:${selected.phone}`}><Phone size={14} /> Call</a>
                )}
                {selected.phone && (
                  <a className="btn-secondary flex items-center gap-1.5 text-sm" target="_blank" rel="noreferrer" href={`https://wa.me/${whatsAppNumber(selected.phone)}`}><MessageCircle size={14} /> WhatsApp</a>
                )}
                {selected.email && (
                  <a className="btn-secondary flex items-center gap-1.5 text-sm" href={`mailto:${selected.email}`}><Mail size={14} /> Email</a>
                )}
                {selected.latitude != null && selected.longitude != null && (
                  <a className="btn-secondary flex items-center gap-1.5 text-sm" target="_blank" rel="noreferrer"
                    href={`https://www.google.com/maps/dir/?api=1&destination=${selected.latitude},${selected.longitude}`}>
                    <Navigation size={14} /> Directions
                  </a>
                )}
              </div>
              {!selected.isVerified && (
                <button onClick={() => verify(selected)} className="w-full btn-primary text-sm">Verify this shop</button>
              )}
            </div>
          ) : (
            <>
              <div className="p-3 border-b border-gray-100">
                <div className="relative">
                  <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                  <input
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Search shop, owner or city"
                    className="w-full pl-9 pr-3 py-2 rounded-xl border border-gray-200 text-sm outline-none focus:border-primary"
                  />
                </div>
                <p className="text-[11px] text-gray-400 mt-2">
                  {pinned.length} of {visible.length} shops have a map location
                </p>
              </div>
              <ul className="flex-1 overflow-y-auto divide-y divide-gray-50">
                {visible.map((s) => (
                  <li key={s.id}>
                    <button onClick={() => setSelectedId(s.id)} className="w-full text-left px-4 py-3 hover:bg-gray-50 flex items-start gap-3">
                      <MapPin size={18} style={{ color: s.isVerified ? GREEN : RED }} className="flex-shrink-0 mt-0.5" />
                      <div className="min-w-0">
                        <p className="font-bold text-sm text-gray-900 truncate">{s.shopName}</p>
                        <p className="text-xs text-gray-400 truncate">{[s.city, s.province].filter(Boolean).join(', ') || s.address || 'No address'}</p>
                        {(s.latitude == null || s.longitude == null) && (
                          <p className="text-[11px] text-amber-600">No map location</p>
                        )}
                      </div>
                    </button>
                  </li>
                ))}
                {visible.length === 0 && <li className="p-6 text-center text-sm text-gray-400">No shops found</li>}
              </ul>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
