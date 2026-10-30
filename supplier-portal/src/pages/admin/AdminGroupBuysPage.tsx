import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { CheckCircle, XCircle, Ban, Plus, Users, Loader2, MapPin, Eye } from 'lucide-react';
import { Modal } from '../../components/ui';
import { adminGroupBuyApi, resolveUploadUrl } from '../../services/api';
import type { GroupBuy, GroupBuyFilter } from '../../types';
import GroupBuyCreateModal from '../group-buy/GroupBuyCreateModal';

const filters: { value: GroupBuyFilter; label: string }[] = [
  { value: 'active', label: 'Active' },
  { value: 'completed', label: 'Completed' },
  { value: 'cancelled', label: 'Cancelled' },
  { value: 'all', label: 'All' },
  // Suppliers can no longer propose campaigns; these tabs only clear out older proposals.
  { value: 'pending', label: 'Older supplier proposals' },
  { value: 'rejected', label: 'Rejected' },
];

/** Where the supplier is with the request: waiting to accept, dispatching, or done. */
const supplierProgress = (g: GroupBuy) => {
  const orders = g.orders ?? [];
  if (orders.length === 0) {
    return g.products.some((p) => p.status === 'qualified') ? 'Target reached, waiting for supplier to accept' : null;
  }
  const waiting = orders.filter((o) => ['pending', 'confirmed', 'processing'].includes(o.status)).length;
  return waiting > 0
    ? `Supplier accepted, ${waiting} of ${orders.length} order${orders.length === 1 ? '' : 's'} still to dispatch`
    : `All ${orders.length} order${orders.length === 1 ? '' : 's'} dispatched`;
};

const statusStyle = (status: string) => {
  if (status === 'active' || status === 'completed') return 'bg-emerald-50 text-emerald-700 border-emerald-200';
  if (status === 'pending_approval') return 'bg-accent-50 text-accent-700 border-accent-400';
  return 'bg-red-50 text-red-700 border-red-200';
};

const label = (status: string) => (status === 'pending_approval' ? 'Awaiting approval' : status);

/** Units committed across all products, as a share of the combined targets. */
const overallProgress = (g: GroupBuy) => {
  const target = g.products.reduce((total, p) => total + p.targetQty, 0);
  const current = g.products.reduce((total, p) => total + p.currentQty, 0);
  return target > 0 ? Math.min(100, Math.round(current / target * 100)) : 0;
};

export default function AdminGroupBuysPage() {
  const [filter, setFilter] = useState<GroupBuyFilter>('active');
  const [groups, setGroups] = useState<GroupBuy[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [viewId, setViewId] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setGroups(await adminGroupBuyApi.list(filter));
    } catch {
      setGroups([]);
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => { load(); }, [load]);

  const act = async (id: string, fn: () => Promise<unknown>, done: string) => {
    setBusyId(id);
    try {
      await fn();
      toast.success(done);
      await load();
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setBusyId(null);
    }
  };

  const reject = (g: GroupBuy) => {
    const note = window.prompt(`Why is "${g.title}" being rejected? The supplier will see this.`);
    if (note && note.trim()) act(g.id, () => adminGroupBuyApi.reject(g.id, note.trim()), 'Group buy rejected');
  };

  const cancel = (g: GroupBuy) => {
    if (!window.confirm(`Cancel "${g.title}"? Every shop's commitment is released.`)) return;
    const note = window.prompt('Reason (optional)') ?? undefined;
    act(g.id, () => adminGroupBuyApi.cancel(g.id, note), 'Group buy cancelled');
  };

  // Looked up by id so the details refresh after an action (and close if it left this tab)
  const viewing = groups.find((g) => g.id === viewId);

  return (
    <div className="p-6 compact:p-4 space-y-5 max-w-7xl mx-auto">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="page-title">Group Buys</h1>
          <p className="page-subtitle">Create deals for suppliers' products and follow them until the supplier accepts and dispatches.</p>
        </div>
        <button className="btn-primary flex items-center gap-2" onClick={() => setShowCreate(true)}>
          <Plus size={16} /> Create group buy
        </button>
      </div>

      <div className="flex items-center gap-1 border-b border-gray-200 overflow-x-auto">
        {filters.map((f) => (
          <button
            key={f.value}
            onClick={() => setFilter(f.value)}
            className={`px-4 py-3 text-sm font-semibold border-b-2 whitespace-nowrap transition-colors ${
              filter === f.value ? 'border-primary text-primary' : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {f.label}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>
      ) : groups.length === 0 ? (
        <div className="card py-16 text-center text-gray-400">No group buys here.</div>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-sm min-w-[820px]">
            <thead>
              <tr className="text-left text-xs text-gray-500 border-b border-gray-100">
                <th className="p-3">Group buy</th>
                <th className="p-3">Supplier</th>
                <th className="p-3">Status</th>
                <th className="p-3 text-right">Shops</th>
                <th className="p-3 w-40">Progress</th>
                <th className="p-3">Ends</th>
                <th className="p-3" />
              </tr>
            </thead>
            <tbody>
              {groups.map((g) => {
                const progress = overallProgress(g);
                return (
                  <tr key={g.id} className="border-b border-gray-50 last:border-0 hover:bg-gray-50/60">
                    <td className="p-3">
                      <div className="flex items-center gap-3 min-w-0">
                        {g.bannerImageUrl
                          ? <img src={resolveUploadUrl(g.bannerImageUrl)} alt="" className="w-16 h-7 rounded-md object-cover flex-shrink-0" />
                          : <div className="w-16 h-7 rounded-md bg-primary/10 flex-shrink-0" />}
                        <div className="min-w-0">
                          <p className="font-bold text-gray-900 truncate max-w-[240px]">{g.title}</p>
                          {supplierProgress(g) && <p className="text-[11px] font-semibold text-brand-blue truncate max-w-[240px]">{supplierProgress(g)}</p>}
                        </div>
                      </div>
                    </td>
                    <td className="p-3 text-gray-700 truncate max-w-[180px]">{g.supplierName}</td>
                    <td className="p-3">
                      <span className={`text-[11px] font-bold px-2.5 py-1 rounded-full border capitalize whitespace-nowrap ${statusStyle(g.status)}`}>{label(g.status)}</span>
                    </td>
                    <td className="p-3 text-right font-semibold tabular-nums">{g.participantCount}</td>
                    <td className="p-3">
                      <div className="flex items-center gap-2">
                        <div className="h-2 flex-1 bg-primary-100 rounded-full overflow-hidden">
                          <div className="h-full bg-accent rounded-full" style={{ width: `${progress}%` }} />
                        </div>
                        <span className="text-xs font-bold text-gray-600 tabular-nums w-9 text-right">{progress}%</span>
                      </div>
                    </td>
                    <td className="p-3 text-gray-500 whitespace-nowrap">{new Date(g.expiresAt).toLocaleDateString('en-ZA')}</td>
                    <td className="p-3 text-right">
                      <button onClick={() => setViewId(g.id)} className="btn-secondary inline-flex items-center gap-1.5 text-sm">
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
            g={viewing}
            busy={busyId === viewing.id}
            onApprove={() => act(viewing.id, () => adminGroupBuyApi.approve(viewing.id), 'Group buy approved and live')}
            onReject={() => reject(viewing)}
            onCancel={() => cancel(viewing)}
          />
        </Modal>
      )}

      {showCreate && <GroupBuyCreateModal onClose={() => setShowCreate(false)} onCreated={() => { setFilter('active'); load(); }} />}
    </div>
  );
}

/** Everything about one group buy, shown in the View pop-up. */
function GroupDetails({ g, busy, onApprove, onReject, onCancel }: {
  g: GroupBuy;
  busy: boolean;
  onApprove: () => void;
  onReject: () => void;
  onCancel: () => void;
}) {
  const isPending = g.status === 'pending_approval';
  const participants = g.participants.filter((p) => p.status !== 'cancelled');

  return (
    <div>
      {g.bannerImageUrl && <img src={resolveUploadUrl(g.bannerImageUrl)} alt="" className="w-full aspect-[16/7] object-cover" />}
      <div className="p-5 space-y-5">
        <div className="space-y-1.5">
          <div className="flex items-center gap-2 flex-wrap">
            <span className={`text-[11px] font-bold px-2.5 py-1 rounded-full border capitalize ${statusStyle(g.status)}`}>{label(g.status)}</span>
            <span className="text-[11px] font-semibold text-gray-500 bg-gray-100 px-2 py-1 rounded-full">
              {g.createdByRole === 'admin' ? 'Created by admin' : g.createdByRole === 'shop' ? 'Shop-created (legacy)' : 'Supplier proposal'}
            </span>
          </div>
          <p className="text-sm text-gray-500">
            {g.supplierName} &middot; {g.products.length} product{g.products.length === 1 ? '' : 's'} &middot; {g.participantCount} shop{g.participantCount === 1 ? '' : 's'} joined &middot; Ends {new Date(g.expiresAt).toLocaleDateString('en-ZA')}
          </p>
          <p className="text-xs text-gray-500 flex items-center gap-1">
            <MapPin size={12} />
            {g.area ? <>From {g.area.branchName} &middot; open to {g.area.description}</> : 'Open to shops near the supplier (no area set)'}
          </p>
          {g.description && <p className="text-sm text-gray-600">{g.description}</p>}
          {g.rejectionNote && <p className="text-sm text-red-700">Note: {g.rejectionNote}</p>}
          {supplierProgress(g) && <p className="text-sm font-semibold text-brand-blue">{supplierProgress(g)}</p>}
        </div>

        <div className="space-y-3">
          {g.products.map((p) => (
            <div key={p.id}>
              <div className="flex items-center justify-between text-sm">
                <span className="font-semibold text-gray-800">{p.productName} <span className="text-xs text-accent-700 font-bold">Save {p.discountPct}%</span></span>
                <span className="text-gray-500 tabular-nums">{p.currentQty} / {p.targetQty} &middot; {p.participantCount} shops</span>
              </div>
              <div className="h-2 bg-primary-100 rounded-full overflow-hidden mt-1">
                <div className="h-full bg-accent rounded-full" style={{ width: `${Math.min(p.progress, 100)}%` }} />
              </div>
            </div>
          ))}
        </div>

        <div>
          <p className="text-sm font-bold text-gray-800 flex items-center gap-2 mb-2"><Users size={15} /> Participating shops</p>
          <div className="rounded-xl bg-gray-50 p-3 text-sm">
            {participants.length === 0 ? (
              <p className="text-gray-400">No shops have joined yet.</p>
            ) : (
              <ul className="space-y-1">
                {participants.map((p) => (
                  <li key={p.id} className="flex justify-between gap-3">
                    <span className="font-semibold text-gray-800">{p.shopName}</span>
                    <span className="text-gray-500 text-right">{p.items.filter((i) => i.status !== 'cancelled').map((i) => `${i.productName} x${i.quantity}`).join(', ')}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {(isPending || g.status === 'active') && (
          <div className="flex flex-wrap justify-end gap-2 pt-1">
            {isPending && (
              <>
                <button disabled={busy} className="btn-primary flex items-center gap-1.5 text-sm" onClick={onApprove}>
                  <CheckCircle size={15} /> Approve
                </button>
                <button disabled={busy} className="btn-secondary flex items-center gap-1.5 text-sm text-red-600" onClick={onReject}>
                  <XCircle size={15} /> Reject
                </button>
              </>
            )}
            <button disabled={busy} className="btn-secondary flex items-center gap-1.5 text-sm" onClick={onCancel}>
              <Ban size={15} /> Cancel group buy
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
