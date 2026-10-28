import { useEffect, useMemo, useState } from 'react';
import { MapContainer, TileLayer, Marker, useMap } from 'react-leaflet';
import L from 'leaflet';
import toast from 'react-hot-toast';
import { MapPin, Phone, MessageCircle, Mail, Navigation, BadgeCheck, ShieldAlert, Search, MapPinned } from 'lucide-react';
import clsx from 'clsx';
import 'leaflet/dist/leaflet.css';
import { adminSpazaOwnersApi, apiErrorMessage } from '../../services/api';
import { openAdminDiditCheck } from '../../services/adminDidit';
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
  identityCheckStatus?: string | null;
  status: string;
  complianceStatus?: string;
  joinedAt?: string;
}

type Filter = 'all' | 'verified' | 'unverified';
type Province = {
  name: string;
  center: [number, number];
};

const GREEN = '#15803d';
const RED = '#e0523c';
const SOUTH_AFRICA_CENTER: [number, number] = [-29.0, 24.0];
const PROVINCES: Province[] = [
  { name: 'Eastern Cape', center: [-32.0, 26.5] },
  { name: 'Free State', center: [-28.5, 26.5] },
  { name: 'Gauteng', center: [-26.0, 28.1] },
  { name: 'KwaZulu-Natal', center: [-28.8, 30.5] },
  { name: 'Limpopo', center: [-23.7, 29.5] },
  { name: 'Mpumalanga', center: [-25.6, 30.8] },
  { name: 'Northern Cape', center: [-29.5, 22.5] },
  { name: 'North West', center: [-26.7, 25.5] },
  { name: 'Western Cape', center: [-33.4, 20.0] },
];

function normalizeProvince(value?: string | null) {
  const normalized = value?.toLowerCase().replace(/[^a-z]/g, '') ?? '';
  return normalized === 'kwazulunatal' ? 'kwazulunatal' : normalized;
}

function pinIcon(color: string, selected: boolean) {
  const size = selected ? 44 : 34;
  return L.divIcon({
    className: 'shop-pin',
    html: `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="${color}" stroke="white" stroke-width="1.5" style="filter:drop-shadow(0 2px 3px rgba(0,0,0,.35))"><path d="M12 22s7-6.2 7-12a7 7 0 1 0-14 0c0 5.8 7 12 7 12z"/><g stroke="none"><path d="M8.3 6.8h7.4l.9 2.3H7.4z" fill="white"/><path d="M8.2 9.6h7.6v3.9H8.2z" fill="white"/><path d="M11.1 11.1h1.8v2.4h-1.8z" fill="${color}"/></g></svg>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size],
  });
}

function whatsAppNumber(phone: string) {
  const digits = phone.replace(/\D/g, '');
  return digits.startsWith('0') ? `27${digits.slice(1)}` : digits;
}

function MapViewport({
  points,
  province,
  selectedShop,
}: {
  points: [number, number][];
  province?: Province;
  selectedShop?: Shop;
}) {
  const map = useMap();
  useEffect(() => {
    if (selectedShop?.latitude != null && selectedShop.longitude != null) {
      map.flyTo([selectedShop.latitude, selectedShop.longitude], Math.max(map.getZoom(), 13), { duration: 0.6 });
    } else if (points.length > 0) {
      map.flyToBounds(L.latLngBounds(points), { padding: [48, 48], maxZoom: province ? 11 : 8, duration: 0.7 });
    } else if (province) {
      map.flyTo(province.center, 7, { duration: 0.7 });
    } else {
      map.flyTo(SOUTH_AFRICA_CENTER, 5, { duration: 0.7 });
    }
  }, [map, points, province, selectedShop]);
  return null;
}

export default function AdminShopsMapPage() {
  const [shops, setShops] = useState<Shop[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<Filter>('all');
  const [provinceName, setProvinceName] = useState('');
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
    const selectedProvince = normalizeProvince(provinceName);
    return shops.filter((s) =>
      (filter === 'all' || (filter === 'verified' ? s.isVerified : !s.isVerified)) &&
      (!selectedProvince || normalizeProvince(s.province) === selectedProvince) &&
      (!term || [s.shopName, s.ownerName, s.city, s.address, s.province].some((v) => v?.toLowerCase().includes(term))));
  }, [shops, filter, provinceName, search]);

  const pinned = visible.filter((s) => s.latitude != null && s.longitude != null);
  const points = useMemo(() => pinned.map((s) => [s.latitude!, s.longitude!] as [number, number]), [pinned]);
  const selected = visible.find((s) => s.id === selectedId);
  const selectedProvince = PROVINCES.find((p) => p.name === provinceName);

  const verify = async (shop: Shop) => {
    if (shop.identityCheckStatus !== 'Approved') {
      const result = await openAdminDiditCheck('shop', shop.id, shop.shopName);
      if (result) {
        setShops((prev) => prev.map((item) => item.id === shop.id
          ? { ...item, identityCheckStatus: result }
          : item));
      }
      return;
    }
    try {
      await adminSpazaOwnersApi.verify(shop.id);
      toast.success(`${shop.shopName} verified`);
      setShops((prev) => prev.map((s) => (s.id === shop.id ? { ...s, isVerified: true, status: 'verified' } : s)));
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Verification failed'));
    }
  };

  if (loading) return <PageLoader variant="dashboard" />;

  return (
    <div className="p-6 compact:p-4 space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-black text-gray-900">Shops Map</h1>
          <p className="text-sm text-gray-500">
            Explore registered shops by province. Green pins are verified; coral pins need verification.
          </p>
        </div>
        <div className="flex flex-wrap items-end gap-3">
          <label className="block">
            <span className="mb-1 block text-[11px] font-bold uppercase tracking-wider text-gray-400">Province</span>
            <select
              value={provinceName}
              onChange={(e) => {
                setProvinceName(e.target.value);
                setSelectedId(null);
              }}
              className="select min-w-[190px] py-2.5"
              aria-label="Filter shops by province"
            >
              <option value="">All South Africa</option>
              {PROVINCES.map((province) => <option key={province.name} value={province.name}>{province.name}</option>)}
            </select>
          </label>
          <div className="flex items-center gap-1 rounded-xl border border-gray-100 bg-white p-1 shadow-sm">
            {(['all', 'verified', 'unverified'] as const).map((f) => (
              <button
                key={f}
                onClick={() => {
                  setFilter(f);
                  setSelectedId(null);
                }}
                className={clsx(
                  'rounded-lg px-3 py-2 text-xs font-bold capitalize transition-colors',
                  filter === f ? 'bg-primary text-white shadow-sm' : 'text-gray-500 hover:bg-primary-50 hover:text-primary'
                )}
              >
                {f}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <div className="shops-map relative overflow-hidden rounded-2xl border border-primary-100 bg-primary-50 shadow-card xl:col-span-2 h-[560px] compact:h-[440px]">
          <MapContainer center={SOUTH_AFRICA_CENTER} zoom={5} style={{ height: '100%', width: '100%' }} scrollWheelZoom>
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <MapViewport points={points} province={selectedProvince} selectedShop={selected} />
            {pinned.map((s) => (
              <Marker
                key={s.id}
                position={[s.latitude!, s.longitude!]}
                icon={pinIcon(s.isVerified ? GREEN : RED, s.id === selectedId)}
                eventHandlers={{ click: () => setSelectedId(s.id) }}
              />
            ))}
          </MapContainer>
          <div className="pointer-events-none absolute left-4 top-4 z-[400] flex items-center gap-3 rounded-xl border border-white/80 bg-white/95 px-3.5 py-2.5 shadow-lg shadow-slate-900/10 backdrop-blur">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-primary-50 text-primary">
              <MapPinned size={18} />
            </span>
            <div>
              <p className="text-xs font-extrabold text-gray-900">{provinceName || 'South Africa'}</p>
              <p className="text-[10px] font-semibold text-gray-400">{pinned.length} mapped · {visible.length} shops</p>
            </div>
          </div>
          <div className="pointer-events-none absolute bottom-4 left-4 z-[400] flex items-center gap-4 rounded-xl border border-white/80 bg-white/95 px-3.5 py-2.5 shadow-lg shadow-slate-900/10 backdrop-blur">
            <span className="inline-flex items-center gap-2 text-[11px] font-bold text-gray-600"><i className="h-2.5 w-2.5 rounded-full bg-primary" /> Verified</span>
            <span className="inline-flex items-center gap-2 text-[11px] font-bold text-gray-600"><i className="h-2.5 w-2.5 rounded-full bg-[#e0523c]" /> Unverified</span>
          </div>
        </div>

        <div className="card flex h-[560px] compact:h-[440px] flex-col overflow-hidden">
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
                <button onClick={() => verify(selected)} className="w-full btn-primary text-sm">
                  {selected.identityCheckStatus === 'Approved' ? 'Verify this shop' : 'Verify with Didit'}
                </button>
              )}
            </div>
          ) : (
            <>
              <div className="border-b border-gray-100 bg-gradient-to-r from-primary-50/70 to-white p-3">
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
