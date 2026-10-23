import { useCallback, useEffect, useState } from 'react';
import { Search, UserRound, CheckCircle, Ban, Eye, X, ScanLine, Flag, Gift, MapPin, Phone, Mail, Loader2 } from 'lucide-react';
import { format, formatDistanceToNow } from 'date-fns';
import clsx from 'clsx';
import toast from 'react-hot-toast';
import { adminCustomersApi, apiErrorMessage, resolveUploadUrl, type AdminCustomer } from '../../services/api';
import { Spinner } from '../../components/ui';

type Filter = 'all' | 'active' | 'suspended';

/** Customers of the SpazaSure customer app, listed like shops and suppliers. */
export default function CustomersPanel() {
  const [customers, setCustomers] = useState<AdminCustomer[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const [selected, setSelected] = useState<AdminCustomer | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await adminCustomersApi.list({ search: search.trim() || undefined, status: filter === 'all' ? undefined : filter, pageSize: 200 });
      setCustomers(data.items);
      setTotal(data.total);
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Failed to load customers'));
    } finally {
      setLoading(false);
    }
  }, [search, filter]);

  // Search as the admin types, without a request per keystroke
  useEffect(() => {
    const timer = window.setTimeout(load, 300);
    return () => window.clearTimeout(timer);
  }, [load]);

  const setSuspended = async (c: AdminCustomer, suspend: boolean) => {
    if (suspend && !window.confirm(`Suspend ${c.fullName || c.phone}? They are signed out and can't sign in until you reinstate them.`)) return;
    setBusy(true);
    try {
      if (suspend) await adminCustomersApi.suspend(c.id);
      else await adminCustomersApi.reinstate(c.id);
      toast.success(suspend ? 'Customer suspended' : 'Customer reinstated');
      setSelected(null);
      await load();
    } catch (err) {
      toast.error(apiErrorMessage(err, suspend ? 'Could not suspend the customer' : 'Could not reinstate the customer'));
    } finally {
      setBusy(false);
    }
  };

  const suspendedCount = customers.filter((c) => c.isSuspended).length;
  const plusCount = customers.filter((c) => c.plan !== 'free').length;
  const location = (c: AdminCustomer) => [c.suburb, c.city, c.province].filter(Boolean).join(', ');

  return (
    <>
      {/* Stats Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 sm:gap-4">
        <Stat icon={<UserRound size={18} className="text-sky-600" />} tint="from-sky-50" bg="bg-sky-50" value={total} label="Total Customers" />
        <Stat icon={<Gift size={18} className="text-emerald-600" />} tint="from-emerald-50" bg="bg-emerald-50" value={plusCount} label="On a paid plan" />
        <Stat icon={<Ban size={18} className="text-red-500" />} tint="from-red-50" bg="bg-red-50" value={suspendedCount} label="Suspended" />
      </div>

      {/* Search & Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[220px] max-w-sm">
          <Search size={15} className="absolute left-4 top-3.5 text-gray-300" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full border border-gray-200 rounded-2xl pl-11 pr-4 py-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-100 focus:border-blue-300 transition-all placeholder:text-gray-300 font-medium"
            placeholder="Search by name, phone, email or city..."
          />
        </div>
        <div className="flex items-center gap-1.5">
          {(['all', 'active', 'suspended'] as Filter[]).map((f) => (
            <button key={f} onClick={() => setFilter(f)}
              className={clsx('px-4 py-2.5 rounded-xl text-xs font-semibold capitalize transition-all border',
                filter === f ? 'bg-gray-900 text-white border-gray-900 shadow-sm' : 'bg-white text-gray-500 border-gray-200 hover:border-gray-300')}>
              {f}
            </button>
          ))}
        </div>
      </div>

      {/* Table */}
      <div className="rounded-2xl bg-white border border-gray-100 shadow-sm overflow-x-auto">
        {loading && customers.length === 0 ? (
          <div className="p-16 flex justify-center"><Spinner /></div>
        ) : customers.length === 0 ? (
          <div className="p-16 text-center">
            <div className="w-14 h-14 bg-gray-50 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <UserRound size={24} className="text-gray-300" />
            </div>
            <p className="font-bold text-gray-600">No customers found</p>
            <p className="text-sm text-gray-400 mt-1">Try adjusting your search or filter</p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/40">
                {['Customer', 'Phone', 'Location', 'Plan', 'Scans', 'Reports', 'Points', 'Status', 'Joined', 'Actions'].map((h, i) => (
                  <th key={h} className={clsx('px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider',
                    i === 0 ? 'text-left px-5' : ['Scans', 'Reports', 'Points'].includes(h) ? 'text-right' : ['Plan', 'Status', 'Actions'].includes(h) ? 'text-center' : 'text-left')}>
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {customers.map((c) => (
                <tr key={c.id} className="group border-b border-gray-50 last:border-0 hover:bg-gray-50/60 transition-colors">
                  <td className="px-5 py-3.5">
                    <div className="flex items-center gap-3">
                      <Avatar c={c} />
                      <div>
                        <p className="font-semibold text-gray-900">{c.fullName || 'No name yet'}</p>
                        <p className="text-xs text-gray-400">{c.email || 'No email'}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3.5 text-gray-700 tabular-nums">{c.phone}</td>
                  <td className="px-4 py-3.5 text-xs text-gray-500">{location(c) || '—'}</td>
                  <td className="px-4 py-3.5 text-center">
                    <span className={clsx('text-[11px] font-bold rounded-full px-2 py-0.5 capitalize', c.plan === 'free' ? 'bg-gray-100 text-gray-500' : 'bg-emerald-50 text-emerald-700')}>{c.plan}</span>
                  </td>
                  <td className="px-4 py-3.5 text-right font-bold tabular-nums text-gray-700">{c.scanCount}</td>
                  <td className="px-4 py-3.5 text-right font-bold tabular-nums text-gray-700">{c.reportCount}</td>
                  <td className="px-4 py-3.5 text-right font-bold tabular-nums text-gray-700">{c.rewardPoints}</td>
                  <td className="px-4 py-3.5 text-center"><StatusBadge c={c} /></td>
                  <td className="px-4 py-3.5 text-xs text-gray-500">{format(new Date(c.joinedAt), 'dd MMM yyyy')}</td>
                  <td className="px-4 py-3.5 text-center">
                    <button onClick={() => setSelected(c)} className="p-2 rounded-lg text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all" title="View">
                      <Eye size={15} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Customer detail */}
      {selected && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4" onClick={() => setSelected(null)}>
          <div className="modal-slide-up w-full max-w-lg rounded-3xl bg-white shadow-2xl overflow-hidden" onClick={(e) => e.stopPropagation()}>
            <div className="bg-gradient-to-br from-sky-600 to-indigo-700 p-6 text-white flex items-start justify-between">
              <div className="flex items-center gap-3">
                <Avatar c={selected} large />
                <div>
                  <p className="font-bold text-lg">{selected.fullName || 'No name yet'}</p>
                  <p className="text-sm text-sky-100">{selected.preferredName ? `Goes by ${selected.preferredName} · ` : ''}{selected.preferredLanguage}</p>
                </div>
              </div>
              <button onClick={() => setSelected(null)} className="p-1.5 rounded-lg hover:bg-white/15" aria-label="Close"><X size={18} /></button>
            </div>
            <div className="p-6 space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
                <Info icon={<Phone size={14} />} label="Phone" value={selected.phone} />
                <Info icon={<Mail size={14} />} label="Email" value={selected.email || '—'} />
                <Info icon={<MapPin size={14} />} label="Area" value={location(selected) || '—'} />
                <Info icon={<UserRound size={14} />} label="Age" value={selected.age ? String(selected.age) : '—'} />
              </div>
              <div className="grid grid-cols-3 gap-3">
                <Count icon={<ScanLine size={15} />} value={selected.scanCount} label="Scans" />
                <Count icon={<Flag size={15} />} value={selected.reportCount} label="Reports" />
                <Count icon={<Gift size={15} />} value={selected.rewardPoints} label="Points" />
              </div>
              <div className="text-sm text-gray-600 space-y-1">
                <p><b>Plan:</b> <span className="capitalize">{selected.plan}</span></p>
                <p><b>Allergies:</b> {selected.allergies.length ? selected.allergies.join(', ') : 'None declared'}</p>
                <p><b>Marketing messages:</b> {selected.marketingOptIn ? 'Opted in' : 'Not opted in'}</p>
                <p><b>Joined:</b> {format(new Date(selected.joinedAt), 'dd MMM yyyy')}
                  {selected.lastLoginAt && <> · last signed in {formatDistanceToNow(new Date(selected.lastLoginAt), { addSuffix: true })}</>}</p>
              </div>
              {selected.isSuspended ? (
                <button disabled={busy} onClick={() => setSuspended(selected, false)}
                  className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-emerald-600 px-4 py-3 text-sm font-bold text-white hover:bg-emerald-700 disabled:opacity-60">
                  {busy ? <Loader2 size={15} className="animate-spin" /> : <CheckCircle size={15} />} Reinstate customer
                </button>
              ) : (
                <button disabled={busy} onClick={() => setSuspended(selected, true)}
                  className="w-full inline-flex items-center justify-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-bold text-red-600 hover:bg-red-100 disabled:opacity-60">
                  {busy ? <Loader2 size={15} className="animate-spin" /> : <Ban size={15} />} Suspend customer
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  );
}

function Avatar({ c, large }: { c: AdminCustomer; large?: boolean }) {
  const size = large ? 'w-12 h-12 text-base' : 'w-9 h-9 text-sm';
  const photo = resolveUploadUrl(c.profilePhotoUrl);
  return photo ? (
    <img src={photo} alt="" className={clsx(size, 'rounded-2xl object-cover flex-shrink-0')} />
  ) : (
    <div className={clsx(size, 'rounded-2xl bg-gradient-to-br from-sky-50 to-sky-100/50 ring-1 ring-sky-200/60 flex items-center justify-center font-bold text-sky-600 flex-shrink-0')}>
      {(c.fullName || c.phone).charAt(0).toUpperCase()}
    </div>
  );
}

function StatusBadge({ c }: { c: AdminCustomer }) {
  return c.isSuspended ? (
    <span className="inline-flex items-center gap-1 text-[11px] font-bold rounded-full px-2 py-0.5 bg-red-50 text-red-600"><Ban size={11} /> Suspended</span>
  ) : (
    <span className="inline-flex items-center gap-1 text-[11px] font-bold rounded-full px-2 py-0.5 bg-emerald-50 text-emerald-700"><CheckCircle size={11} /> Active</span>
  );
}

function Stat({ icon, tint, bg, value, label }: { icon: React.ReactNode; tint: string; bg: string; value: number; label: string }) {
  return (
    <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow">
      <div className={clsx('absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl to-transparent rounded-bl-[40px]', tint)} />
      <div className="relative flex items-center gap-3">
        <div className={clsx('w-10 h-10 rounded-xl flex items-center justify-center', bg)}>{icon}</div>
        <div>
          <p className="text-2xl font-black text-gray-900 tabular-nums">{value}</p>
          <p className="text-xs text-gray-500 font-semibold">{label}</p>
        </div>
      </div>
    </div>
  );
}

function Info({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="rounded-xl bg-gray-50 p-3">
      <p className="flex items-center gap-1.5 text-[11px] font-bold text-gray-400 uppercase tracking-wide">{icon} {label}</p>
      <p className="text-sm font-medium text-gray-900 mt-0.5 break-all">{value}</p>
    </div>
  );
}

function Count({ icon, value, label }: { icon: React.ReactNode; value: number; label: string }) {
  return (
    <div className="rounded-2xl border border-gray-100 p-3 text-center">
      <p className="flex items-center justify-center gap-1 text-gray-400">{icon}</p>
      <p className="text-xl font-black text-gray-900 tabular-nums">{value}</p>
      <p className="text-[11px] text-gray-500 font-semibold">{label}</p>
    </div>
  );
}
