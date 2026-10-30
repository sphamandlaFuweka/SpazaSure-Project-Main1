import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertTriangle,
  CheckCircle,
  Clock,
  Eye,
  Inbox,
  MapPin,
  Package,
  Share2,
  Truck,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { Modal } from '../../components/ui';
import { groupBuyApi, resolveUploadUrl } from '../../services/api';
import type { GroupBuy, GroupBuyOrder, GroupBuyProduct, SupplierGroupBuyFilter } from '../../types';
import { groupBuyPlaces } from '../../types';
import { shortOrderNumber } from '../../utils';

const filters: { value: SupplierGroupBuyFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'requests', label: 'Requests to accept' },
  { value: 'active', label: 'Collecting' },
  { value: 'to_dispatch', label: 'To dispatch' },
  { value: 'completed', label: 'Completed' },
  { value: 'cancelled', label: 'Cancelled' },
];

const emptyText: Record<SupplierGroupBuyFilter, string> = {
  all: 'SpazaSure has not run a group buy for your products yet.',
  requests: 'No product has reached its target yet. Requests show here as soon as one does.',
  active: 'No group buys are collecting shop commitments right now.',
  to_dispatch: 'Nothing is waiting to be dispatched.',
  completed: 'No completed group buys yet.',
  cancelled: 'No cancelled group buys.',
};

/** Orders in these states have not left the warehouse yet. */
const DISPATCHABLE = ['pending', 'confirmed', 'processing'];

const money = (value: number) =>
  new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' }).format(Number(value) || 0);

const statusClass = (status: string) => {
  const normalized = status.toLowerCase();
  if (['qualified', 'completed', 'approved', 'confirmed', 'delivered'].includes(normalized)) return 'bg-emerald-100 text-emerald-700';
  if (['dispatched'].includes(normalized)) return 'bg-blue-100 text-blue-700';
  if (['expired', 'cancelled', 'rejected'].includes(normalized)) return 'bg-red-100 text-red-700';
  return 'bg-amber-100 text-amber-700';
};

const qualifiedProducts = (group: GroupBuy) =>
  group.products.filter((product) => product.status.toLowerCase() === 'qualified');

const isLive = (group: GroupBuy) =>
  group.status.toLowerCase() === 'active' && new Date(group.expiresAt).getTime() > Date.now();

const canAccept = (group: GroupBuy) => isLive(group) && qualifiedProducts(group).length > 0;

const dispatchableOrders = (group: GroupBuy) =>
  (group.orders ?? []).filter((order) => DISPATCHABLE.includes(order.status.toLowerCase()));

/** Revenue at the advertised group price for every product that has reached its target. */
const acceptValue = (group: GroupBuy) =>
  qualifiedProducts(group).reduce((total, product) => total + product.discountPrice * product.currentQty, 0);

/** Units committed across all products, as a share of the combined targets. */
const overallProgress = (group: GroupBuy) => {
  const target = group.products.reduce((total, p) => total + p.targetQty, 0);
  const current = group.products.reduce((total, p) => total + p.currentQty, 0);
  return target > 0 ? Math.min(100, Math.round(current / target * 100)) : 0;
};

const displayStatusOf = (group: GroupBuy) =>
  group.status.toLowerCase() === 'active' && !isLive(group) ? 'expired' : group.status;

export default function GroupBuyPage() {
  const [groups, setGroups] = useState<GroupBuy[]>([]);
  const [filter, setFilter] = useState<SupplierGroupBuyFilter>('all');
  const [viewId, setViewId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);

  const loadGroups = useCallback(async () => {
    setLoading(true);
    try {
      setGroups(await groupBuyApi.list(filter));
    } catch {
      // The shared API interceptor displays the backend message.
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => { loadGroups(); }, [loadGroups]);

  const accept = async (group: GroupBuy) => {
    const products = qualifiedProducts(group);
    if (products.length === 0) return;
    const confirmed = window.confirm(
      `Accept "${group.title}"?\n\n` +
      `${products.map((p) => `${p.productName}: ${p.currentQty} units at ${money(p.discountPrice)}`).join('\n')}\n\n` +
      'Each participating shop gets one order at the group price advertised to shops. Delivery is priced from your dispatch location. Stock is reserved now.'
    );
    if (!confirmed) return;

    setBusyId(group.id);
    try {
      const result = await groupBuyApi.accept(group.id);
      toast.success(result?.message ?? 'Request accepted. The orders are ready to dispatch.');
      await loadGroups();
    } catch {
      // The shared API interceptor displays the backend message.
    } finally {
      setBusyId(null);
    }
  };

  const dispatch = async (group: GroupBuy) => {
    const orders = dispatchableOrders(group);
    if (orders.length === 0) return;
    const confirmed = window.confirm(
      `Dispatch ${orders.length} order${orders.length === 1 ? '' : 's'} for "${group.title}"? Each shop is told its order is on the way.`
    );
    if (!confirmed) return;

    setBusyId(group.id);
    try {
      const result = await groupBuyApi.dispatch(group.id);
      toast.success(result?.message ?? 'Orders dispatched');
      await loadGroups();
    } catch {
      // The shared API interceptor displays the backend message.
    } finally {
      setBusyId(null);
    }
  };

  const stats = useMemo(() => ({
    requests: groups.filter(canAccept).length,
    collecting: groups.filter(isLive).length,
    toDispatch: groups.reduce((total, group) => total + dispatchableOrders(group).length, 0),
    shops: groups.reduce((total, group) => total + group.participantCount, 0),
  }), [groups]);

  // Looked up by id so the details refresh after accepting or dispatching
  const viewing = groups.find((group) => group.id === viewId);

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto animate-in">
      <div>
        <h1 className="page-title">Group Buy Requests</h1>
        <p className="page-subtitle">
          SpazaSure runs group buys for your products. When shops reach a product target, accept the request and dispatch the orders.
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-3">
        <Stat icon={<Inbox size={19} />} label="Requests to accept" value={stats.requests} tone="green" />
        <Stat icon={<Clock size={19} />} label="Collecting commitments" value={stats.collecting} tone="amber" />
        <Stat icon={<Truck size={19} />} label="Orders to dispatch" value={stats.toDispatch} tone="blue" />
        <Stat icon={<Users size={19} />} label="Participating shops" value={stats.shops} tone="violet" />
      </div>

      <div className="flex items-center gap-1 border-b border-gray-200 overflow-x-auto">
        {filters.map(({ value, label }) => (
          <button
            key={value}
            onClick={() => setFilter(value)}
            className={`px-4 py-3 text-sm font-semibold border-b-2 whitespace-nowrap transition-colors ${
              filter === value ? 'border-primary text-primary' : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="flex justify-center py-16">
          <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin" />
        </div>
      ) : groups.length === 0 ? (
        <div className="card py-16 text-center">
          <Users size={44} className="mx-auto text-gray-300 mb-3" />
          <p className="font-semibold text-gray-700">Nothing here</p>
          <p className="text-sm text-gray-400 mt-1">{emptyText[filter]}</p>
        </div>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-sm min-w-[760px]">
            <thead>
              <tr className="text-left text-xs text-gray-500 border-b border-gray-100">
                <th className="p-3">Group buy</th>
                <th className="p-3">Status</th>
                <th className="p-3 text-right">Products</th>
                <th className="p-3 text-right">Shops</th>
                <th className="p-3 w-40">Progress</th>
                <th className="p-3">Ends</th>
                <th className="p-3" />
              </tr>
            </thead>
            <tbody>
              {groups.map((group) => {
                const status = displayStatusOf(group);
                const toDispatch = dispatchableOrders(group).length;
                const progress = overallProgress(group);
                return (
                  <tr key={group.id} className="border-b border-gray-50 last:border-0 hover:bg-gray-50/60">
                    <td className="p-3">
                      <div className="flex items-center gap-3 min-w-0">
                        {group.bannerImageUrl
                          ? <img src={resolveUploadUrl(group.bannerImageUrl)} alt="" className="w-16 h-7 rounded-md object-cover flex-shrink-0" />
                          : <div className="w-16 h-7 rounded-md bg-primary/10 flex-shrink-0" />}
                        <div className="min-w-0">
                          <p className="font-bold text-gray-900 truncate max-w-[260px]">{group.title}</p>
                          {canAccept(group) && <p className="text-[11px] font-bold text-emerald-700">Waiting for you to accept</p>}
                          {toDispatch > 0 && <p className="text-[11px] font-bold text-blue-700">{toDispatch} to dispatch</p>}
                        </div>
                      </div>
                    </td>
                    <td className="p-3">
                      <span className={`text-[11px] font-bold px-2.5 py-1 rounded-full capitalize whitespace-nowrap ${statusClass(status)}`}>
                        {status.replace('_', ' ')}
                      </span>
                    </td>
                    <td className="p-3 text-right font-semibold tabular-nums">{group.products.length}</td>
                    <td className="p-3 text-right font-semibold tabular-nums">{group.participantCount}</td>
                    <td className="p-3">
                      <div className="flex items-center gap-2">
                        <div className="h-2 flex-1 bg-gray-200 rounded-full overflow-hidden">
                          <div className="h-full bg-primary rounded-full" style={{ width: `${progress}%` }} />
                        </div>
                        <span className="text-xs font-bold text-gray-600 tabular-nums w-9 text-right">{progress}%</span>
                      </div>
                    </td>
                    <td className="p-3 text-gray-500 whitespace-nowrap">{new Date(group.expiresAt).toLocaleDateString('en-ZA')}</td>
                    <td className="p-3 text-right">
                      <button onClick={() => setViewId(group.id)} className="btn-secondary inline-flex items-center gap-1.5 text-sm">
                        <Eye size={14} /> View
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {viewing && (
        <Modal title={viewing.title} size="xl" onClose={() => setViewId(null)}>
          <GroupDetails
            group={viewing}
            busy={busyId === viewing.id}
            onAccept={() => accept(viewing)}
            onDispatch={() => dispatch(viewing)}
          />
        </Modal>
      )}
    </div>
  );
}

function Stat({ icon, label, value, tone }: { icon: ReactNode; label: string; value: number; tone: string }) {
  const tones: Record<string, string> = {
    blue: 'bg-blue-50 text-blue-600',
    amber: 'bg-amber-50 text-amber-600',
    green: 'bg-emerald-50 text-emerald-600',
    violet: 'bg-violet-50 text-violet-600',
  };
  return (
    <div className="card p-4 flex items-center gap-3">
      <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${tones[tone]}`}>{icon}</div>
      <div><p className="text-xl font-black text-gray-900">{value}</p><p className="text-xs text-gray-500">{label}</p></div>
    </div>
  );
}

/** Everything about one group buy, shown in the View pop-up. */
function GroupDetails({
  group,
  busy,
  onAccept,
  onDispatch,
}: {
  group: GroupBuy;
  busy: boolean;
  onAccept: () => void;
  onDispatch: () => void;
}) {
  const acceptable = canAccept(group);
  const toDispatch = dispatchableOrders(group);
  const orders = group.orders ?? [];
  const daysLeft = Math.max(0, Math.ceil((new Date(group.expiresAt).getTime() - Date.now()) / 86_400_000));
  const live = isLive(group);
  const displayStatus = displayStatusOf(group);

  return (
    <div>
      {group.bannerImageUrl && <img src={resolveUploadUrl(group.bannerImageUrl)} alt="" className="w-full aspect-[16/7] object-cover" />}
      <div className="p-5 border-b border-gray-100 bg-gradient-to-r from-gray-50 to-white">
        <div className="flex flex-col lg:flex-row lg:items-start justify-between gap-4">
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className={`text-[11px] font-bold px-2.5 py-1 rounded-full capitalize ${statusClass(displayStatus)}`}>
                {displayStatus.replace('_', ' ')}
              </span>
              {acceptable && (
                <span className="text-[11px] font-bold px-2.5 py-1 rounded-full bg-emerald-100 text-emerald-700">
                  Waiting for you to accept
                </span>
              )}
              {toDispatch.length > 0 && (
                <span className="text-[11px] font-bold px-2.5 py-1 rounded-full bg-blue-100 text-blue-700">
                  {toDispatch.length} to dispatch
                </span>
              )}
            </div>
            {group.description && <p className="text-sm text-gray-500 mt-1 max-w-3xl">{group.description}</p>}
            <div className="flex flex-wrap gap-x-5 gap-y-1 mt-3 text-xs text-gray-500">
              <span>Run by <strong className="text-gray-700">SpazaSure</strong></span>
              <span>{group.participantCount} shop{group.participantCount === 1 ? '' : 's'}</span>
              <span>{group.products.length} product{group.products.length === 1 ? '' : 's'}</span>
              <span>{live ? `${daysLeft} day${daysLeft === 1 ? '' : 's'} remaining` : 'Closed to new shops'}</span>
              {group.area && (
                <span className="inline-flex items-center gap-1">
                  <MapPin size={12} /> From <strong className="text-gray-700">{groupBuyPlaces(group.area)}</strong> to {group.area.description} (each shop from its nearest)
                </span>
              )}
            </div>
          </div>
          {acceptable && (
            <div className="text-left lg:text-right flex-shrink-0">
              <p className="text-xs text-gray-400">Value if you accept now</p>
              <p className="text-2xl font-black text-emerald-700">{money(acceptValue(group))}</p>
              <p className="text-[11px] text-gray-400">At the group price advertised to shops</p>
            </div>
          )}
        </div>
      </div>

      <div className="p-5 space-y-3">
        {group.products.map((product) => <ProductRow key={product.id} product={product} />)}

        {group.status.toLowerCase() === 'cancelled' && group.rejectionNote && (
          <div className="flex items-start gap-3 bg-red-50 border border-red-100 rounded-xl p-4">
            <AlertTriangle size={17} className="text-red-600 mt-0.5 flex-shrink-0" />
            <p className="text-sm text-red-800"><strong>Cancelled by SpazaSure:</strong> {group.rejectionNote}</p>
          </div>
        )}

        {live && !acceptable && orders.length === 0 && (
          <div className="flex items-start gap-3 bg-blue-50 border border-blue-100 rounded-xl p-4">
            <Share2 size={17} className="text-blue-600 mt-0.5 flex-shrink-0" />
            <div>
              <p className="text-sm font-bold text-blue-900">Shops are still joining</p>
              <p className="text-xs text-blue-700 mt-0.5">You can accept as soon as any product reaches its target. Nothing is needed from you until then.</p>
            </div>
          </div>
        )}

        {orders.length > 0 && <OrderList orders={orders} />}

        <div className="flex flex-col sm:flex-row gap-3 sm:items-center justify-end pt-2">
          <div className="flex flex-col sm:flex-row gap-2">
            {acceptable && (
              <button
                onClick={onAccept}
                disabled={busy}
                className="btn-primary flex items-center justify-center gap-2 disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <CheckCircle size={15} /> {busy ? 'Working...' : 'Accept request'}
              </button>
            )}
            {toDispatch.length > 0 && (
              <button
                onClick={onDispatch}
                disabled={busy}
                className="btn-primary flex items-center justify-center gap-2 disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <Truck size={15} /> {busy ? 'Working...' : `Dispatch ${toDispatch.length} order${toDispatch.length === 1 ? '' : 's'}`}
              </button>
            )}
          </div>
        </div>
      </div>

      <Participants group={group} />
    </div>
  );
}

function ProductRow({ product }: { product: GroupBuyProduct }) {
  const progress = Math.max(0, Math.min(100, Number(product.progress) || (product.targetQty > 0 ? product.currentQty / product.targetQty * 100 : 0)));
  const qualified = product.status.toLowerCase() === 'qualified';

  return (
    <div className={`rounded-xl border p-4 ${qualified ? 'border-emerald-200 bg-emerald-50/40' : 'border-gray-100 bg-gray-50/60'}`}>
      <div className="flex flex-col md:flex-row md:items-center gap-4">
        <div className="w-10 h-10 rounded-xl bg-white border border-gray-100 flex items-center justify-center flex-shrink-0">
          <Package size={18} className={qualified ? 'text-emerald-600' : 'text-primary'} />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <p className="font-bold text-gray-900">{product.productName}</p>
            <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full capitalize ${statusClass(product.status)}`}>
              {qualified ? 'target reached' : product.status}
            </span>
          </div>
          <div className="mt-2">
            <div className="flex justify-between text-xs mb-1.5">
              <span className="text-gray-600"><strong>{product.currentQty}</strong> / {product.targetQty} units</span>
              <span className="font-bold text-gray-700">{progress.toFixed(0)}%</span>
            </div>
            <div className="h-2.5 bg-gray-200 rounded-full overflow-hidden">
              <div className={`h-full rounded-full ${qualified ? 'bg-emerald-500' : 'bg-primary'}`} style={{ width: `${progress}%` }} />
            </div>
          </div>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 md:min-w-[420px]">
          <Metric label="Your price" value={money(product.originalPrice)} />
          <Metric label="Group discount" value={`${product.discountPct}%`} />
          <Metric label="Group price" value={money(product.discountPrice)} />
          <Metric label="Shops" value={`${product.participantCount}`} />
        </div>
      </div>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-[10px] uppercase tracking-wide text-gray-400 font-bold">{label}</p>
      <p className="text-sm font-black text-gray-900 mt-0.5">{value}</p>
    </div>
  );
}

function OrderList({ orders }: { orders: GroupBuyOrder[] }) {
  return (
    <div className="rounded-xl border border-gray-100">
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
        <p className="text-sm font-bold text-gray-800 flex items-center gap-2"><Truck size={15} /> Shop orders</p>
        <Link to="/orders" className="text-xs font-semibold text-primary hover:underline">Open in Orders</Link>
      </div>
      <div className="divide-y divide-gray-100">
        {orders.map((order) => (
          <div key={order.id} className="flex items-center justify-between gap-3 px-4 py-2.5 text-sm">
            <div className="min-w-0">
              <p className="font-semibold text-gray-900 truncate">{order.shopName || 'Shop'}</p>
              <p className="text-[11px] text-gray-400 truncate" title={order.orderNumber}>{shortOrderNumber(order.orderNumber)}</p>
            </div>
            <div className="flex items-center gap-3 flex-shrink-0">
              <span className="font-bold text-gray-700">{money(order.subtotal)}</span>
              <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full capitalize ${statusClass(order.status)}`}>{order.status}</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function Participants({ group }: { group: GroupBuy }) {
  return (
    <div className="border-t border-gray-100 bg-gray-50/70 p-5">
      <div className="flex items-center gap-2 mb-3">
        <Users size={16} className="text-gray-500" />
        <h3 className="text-sm font-bold text-gray-800">Participating shops</h3>
      </div>
      {group.participants.length === 0 ? (
        <p className="text-sm text-gray-400 py-3">No shops have joined yet.</p>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
          {group.participants.map((participant) => (
            <div key={participant.id} className="bg-white border border-gray-100 rounded-xl p-4">
              <div className="flex items-center justify-between gap-3 mb-3">
                <div className="flex items-center gap-2 min-w-0">
                  <div className="w-8 h-8 rounded-full bg-primary/10 text-primary flex items-center justify-center text-xs font-black flex-shrink-0">
                    {participant.shopName?.charAt(0) || '?'}
                  </div>
                  <p className="text-sm font-bold text-gray-900 truncate">{participant.shopName}</p>
                </div>
                <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full capitalize ${statusClass(participant.status)}`}>
                  {participant.status}
                </span>
              </div>
              <div className="space-y-2">
                {participant.items.length === 0 ? (
                  <p className="text-xs text-gray-400">No product quantities recorded.</p>
                ) : participant.items.map((item) => (
                  <div key={`${participant.id}-${item.groupBuyProductId}`} className="flex items-center justify-between gap-3 text-xs bg-gray-50 rounded-lg px-3 py-2">
                    <div className="min-w-0">
                      <p className="font-semibold text-gray-700 truncate">{item.productName}</p>
                      <p className="text-[10px] text-gray-400 capitalize">{item.status}</p>
                    </div>
                    <span className="font-black text-gray-900 whitespace-nowrap">{item.quantity} units</span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
      <div className="flex items-start gap-2 mt-4 text-xs text-gray-500">
        <MapPin size={14} className="mt-0.5 flex-shrink-0" />
        <span>When you accept, each shop receives one order with all of its qualified products. Delivery is priced by distance from your dispatch location.</span>
      </div>
    </div>
  );
}
