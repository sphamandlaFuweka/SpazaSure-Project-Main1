import { useEffect, useMemo, useState } from 'react';
import { MapContainer, Marker, Polyline, Tooltip, useMap } from 'react-leaflet';
import { BrandTileLayer, SOUTH_AFRICA_MAP } from '../../components/ui/BrandMap';
import L from 'leaflet';
import toast from 'react-hot-toast';
import { MapPin, Phone, MessageCircle, Mail, Navigation, BadgeCheck, ShieldAlert, Search, MapPinned, Warehouse } from 'lucide-react';
import clsx from 'clsx';
import 'leaflet/dist/leaflet.css';
import { adminSpazaOwnersApi, adminSuppliersApi, apiErrorMessage, type SupplierLocation } from '../../services/api';
import { openAdminDiditCheck } from '../../services/adminDidit';
import PageLoader from '../../components/ui/PageLoader';
import ShopSticker from '../../components/ui/ShopSticker';

interface Shop {
  id: string;
  shopNumber?: string | null;
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

const NAVY = '#25449A';

/** A navy square pin with a warehouse, for supplier head offices and branches. */
function supplierIcon(highlighted: boolean) {
  const size = highlighted ? 34 : 26;
  return L.divIcon({
    className: 'shop-pin',
    html: `<svg width="${size}" height="${size}" viewBox="0 0 24 24" style="filter:drop-shadow(0 2px 3px rgba(0,0,0,.35))"><rect x="2" y="2" width="20" height="20" rx="6" fill="${NAVY}" stroke="white" stroke-width="1.5"/><path d="M6 11.5 12 7l6 4.5V17H6z" fill="white"/><path d="M9 13h6v4H9z" fill="${NAVY}"/><path d="M9 14.4h6M9 15.8h6" stroke="white" stroke-width=".7"/></svg>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
  });
}

/** Straight-line distance in km (the same measure the server uses for supplier reach). */
function distanceKm(lat1: number, lng1: number, lat2: number, lng2: number) {
  const rad = Math.PI / 180;
  const dLat = (lat2 - lat1) * rad;
  const dLng = (lng2 - lng1) * rad;
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(lat1 * rad) * Math.cos(lat2 * rad) * Math.sin(dLng / 2) ** 2;
  return 6371 * 2 * Math.asin(Math.sqrt(a));
}

/** A supplier's closest location to a shop, with the distance. */
interface NearbySupplier { location: SupplierLocation; km: number; inReach: boolean }

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
  const [suppliers, setSuppliers] = useState<SupplierLocation[]>([]);
  const [reachKm, setReachKm] = useState(50);
  const [showSuppliers, setShowSuppliers] = useState(true);
  /** The supplier place under the mouse: a line and its distance are drawn to the selected shop */
  const [hovered, setHovered] = useState<SupplierLocation | null>(null);

  const load = async () => {
    try {
      const res = await adminSpazaOwnersApi.list({ page: 1, pageSize: 500 });
      setShops((res?.data?.items ?? res?.items ?? []) as Shop[]);
    } catch {
      toast.error('Could not load shops');
    } finally {
      setLoading(false);
    }
    // Suppliers are extra: the map still works with shops alone
    adminSuppliersApi.locations()
      .then((res) => { setSuppliers(res.locations ?? []); setReachKm(res.reachKm ?? 50); })
      .catch(() => {});
  };

  useEffect(() => { load(); }, []);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    // Shop numbers match however they're typed: "SS482913", "ss 482 913", "482913"
    const compact = term.replace(/[^a-z0-9]/g, '');
    const selectedProvince = normalizeProvince(provinceName);
    return shops.filter((s) =>
      (filter === 'all' || (filter === 'verified' ? s.isVerified : !s.isVerified)) &&
      (!selectedProvince || normalizeProvince(s.province) === selectedProvince) &&
      (!term ||
        [s.shopName, s.ownerName, s.city, s.address, s.province].some((v) => v?.toLowerCase().includes(term)) ||
        (compact.length > 0 && !!s.shopNumber?.toLowerCase().includes(compact))));
  }, [shops, filter, provinceName, search]);

  const pinned = visible.filter((s) => s.latitude != null && s.longitude != null);
  const points = useMemo(() => pinned.map((s) => [s.latitude!, s.longitude!] as [number, number]), [pinned]);
  const selected = visible.find((s) => s.id === selectedId);
  const selectedProvince = PROVINCES.find((p) => p.name === provinceName);

  // Suppliers in the chosen province (or all), and for a selected shop each supplier's nearest place
  const visibleSuppliers = useMemo(() => {
    const province = normalizeProvince(provinceName);
    return suppliers.filter((l) => !province || normalizeProvince(l.province) === province);
  }, [suppliers, provinceName]);
  const nearby = useMemo((): NearbySupplier[] => {
    if (selected?.latitude == null || selected.longitude == null) return [];
    const best = new Map<string, NearbySupplier>();
    for (const location of suppliers) {
      const km = distanceKm(selected.latitude, selected.longitude, location.latitude, location.longitude);
      const sameProvince = !selected.province || !location.province ||
        normalizeProvince(selected.province) === normalizeProvince(location.province);
      const current = best.get(location.supplierId);
      if (!current || km < current.km) best.set(location.supplierId, { location, km, inReach: sameProvince && km <= reachKm });
    }
    return [...best.values()].sort((a, b) => a.km - b.km);
  }, [selected, suppliers, reachKm]);
  const lines = nearby.filter((n) => n.inReach).slice(0, 5);

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
            Explore registered shops by province. Green pins are verified; coral pins need verification. Navy squares are suppliers: pick a shop to see how far each supplier is.
          </p>
        </div>
        <div className="flex flex-wrap items-end gap-3">
          <label className="flex items-center gap-2 rounded-xl border border-gray-100 bg-white px-3 py-2.5 text-xs font-bold text-gray-600 shadow-sm cursor-pointer">
            <input type="checkbox" checked={showSuppliers} onChange={(e) => setShowSuppliers(e.target.checked)} />
            <Warehouse size={14} className="text-primary" /> Show suppliers
          </label>
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
          <MapContainer center={SOUTH_AFRICA_CENTER} zoom={5} {...SOUTH_AFRICA_MAP} style={{ height: '100%', width: '100%' }} scrollWheelZoom>
            <BrandTileLayer />
            <MapViewport points={points} province={selectedProvince} selectedShop={selected} />
            {pinned.map((s) => (
              <Marker
                key={s.id}
                position={[s.latitude!, s.longitude!]}
                icon={pinIcon(s.isVerified ? GREEN : RED, s.id === selectedId)}
                eventHandlers={{ click: () => setSelectedId(s.id) }}
              />
            ))}
            {/* Dashed lines from the selected shop to the suppliers that can deliver to it, with the distance */}
            {selected?.latitude != null && selected.longitude != null && lines.map((n) => (
              <Polyline
                key={`line-${n.location.supplierId}`}
                positions={[[selected.latitude!, selected.longitude!], [n.location.latitude, n.location.longitude]]}
                pathOptions={{ color: NAVY, weight: 2, dashArray: '6 6', opacity: 0.8 }}
              >
                <Tooltip permanent direction="center" className="!text-[10px] !font-bold">{n.km.toFixed(1)} km</Tooltip>
              </Polyline>
            ))}
            {/* Hovering any supplier draws its line too, even when it's out of reach */}
            {selected?.latitude != null && selected.longitude != null && hovered &&
              !lines.some((n) => n.location === hovered) && (
              <Polyline
                positions={[[selected.latitude, selected.longitude], [hovered.latitude, hovered.longitude]]}
                pathOptions={{ color: RED, weight: 2, dashArray: '4 6', opacity: 0.9 }}
              >
                <Tooltip permanent direction="center" className="!text-[10px] !font-bold">
                  {distanceKm(selected.latitude, selected.longitude, hovered.latitude, hovered.longitude).toFixed(1)} km
                </Tooltip>
              </Polyline>
            )}
            {showSuppliers && visibleSuppliers.map((l) => (
              <Marker
                key={`${l.supplierId}-${l.branchId ?? 'hq'}`}
                position={[l.latitude, l.longitude]}
                icon={supplierIcon(hovered === l || lines.some((n) => n.location === l))}
                zIndexOffset={-100}
                eventHandlers={{ mouseover: () => setHovered(l), mouseout: () => setHovered(null) }}
              >
                <Tooltip direction="top" offset={[0, -12]}>
                  <strong>{l.companyName}</strong><br />
                  {l.name}{l.city ? ` · ${l.city}` : ''}
                  <br />
                  {selected?.latitude != null && selected.longitude != null
                    ? `${distanceKm(selected.latitude, selected.longitude, l.latitude, l.longitude).toFixed(1)} km from ${selected.shopName}`
                    : 'Pick a shop to see the distance'}
                </Tooltip>
              </Marker>
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
            {showSuppliers && (
              <span className="inline-flex items-center gap-2 text-[11px] font-bold text-gray-600"><i className="h-2.5 w-2.5 rounded-[3px] bg-[#25449A]" /> Supplier</span>
            )}
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
              {/* The shop's QR sticker, to view, print or save */}
              {selected.shopNumber && (
                <ShopSticker shopNumber={selected.shopNumber} shopName={selected.shopName} verified={selected.isVerified} />
              )}
              {/* How far each supplier's nearest place is, and which can deliver here */}
              <div className="pt-3 border-t border-gray-100">
                <p className="text-xs font-bold uppercase tracking-wider text-gray-400 mb-2 flex items-center gap-1.5">
                  <Warehouse size={13} /> Nearest suppliers
                </p>
                {selected.latitude == null || selected.longitude == null ? (
                  <p className="text-xs text-gray-400">This shop has no map location, so distances can't be worked out.</p>
                ) : nearby.length === 0 ? (
                  <p className="text-xs text-gray-400">No suppliers are on the map yet.</p>
                ) : (
                  <ul className="space-y-1.5">
                    {nearby.slice(0, 8).map((n) => (
                      <li key={n.location.supplierId} className="flex items-center justify-between gap-2 text-sm">
                        <div className="min-w-0">
                          <p className="font-semibold text-gray-800 truncate">{n.location.companyName}</p>
                          <p className="text-[11px] text-gray-400 truncate">{n.location.name}{n.location.city ? ` · ${n.location.city}` : ''}</p>
                        </div>
                        <div className="text-right flex-shrink-0">
                          <p className="font-black tabular-nums text-gray-900">{n.km.toFixed(1)} km</p>
                          <p className={clsx('text-[10px] font-bold', n.inReach ? 'text-emerald-700' : 'text-gray-400')}>
                            {n.inReach ? 'Delivers here' : 'Out of reach'}
                          </p>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
                <p className="text-[10px] text-gray-400 mt-2">Straight-line distance. Suppliers deliver up to {reachKm} km within the same province.</p>
              </div>
            </div>
          ) : (
            <>
              <div className="border-b border-gray-100 bg-gradient-to-r from-primary-50/70 to-white p-3">
                <div className="relative">
                  <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                  <input
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Search shop number, name, owner or city"
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
                        <p className="font-bold text-sm text-gray-900 truncate">
                          {s.shopName}
                          {s.shopNumber && <span className="ml-1.5 font-mono text-[11px] font-bold text-brand-blue">{s.shopNumber}</span>}
                        </p>
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
