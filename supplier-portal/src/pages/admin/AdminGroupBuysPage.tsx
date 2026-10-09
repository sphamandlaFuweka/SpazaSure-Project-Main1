import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { CheckCircle, XCircle, Ban, Plus, ChevronDown, ChevronUp, Users, Loader2 } from 'lucide-react';
import { adminGroupBuyApi } from '../../services/api';
import type { GroupBuy, GroupBuyFilter } from '../../types';
import GroupBuyCreateModal from '../group-buy/GroupBuyCreateModal';

const filters: { value: GroupBuyFilter; label: string }[] = [
  { value: 'pending', label: 'Awaiting approval' },
  { value: 'active', label: 'Active' },
  { value: 'completed', label: 'Completed' },
  { value: 'rejected', label: 'Rejected' },
  { value: 'cancelled', label: 'Cancelled' },
  { value: 'all', label: 'All' },
];

const statusStyle = (status: string) => {
  if (status === 'active' || status === 'completed') return 'bg-emerald-50 text-emerald-700 border-emerald-200';
  if (status === 'pending_approval') return 'bg-accent-50 text-accent-700 border-accent-400';
  return 'bg-red-50 text-red-700 border-red-200';
};

const label = (status: string) => (status === 'pending_approval' ? 'Awaiting approval' : status);

export default function AdminGroupBuysPage() {
  const [filter, setFilter] = useState<GroupBuyFilter>('pending');
  const [groups, setGroups] = useState<GroupBuy[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
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

  return (
    <div className="p-6 compact:p-4 space-y-5 max-w-7xl mx-auto">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="page-title">Group Buys</h1>
          <p className="page-subtitle">Approve supplier proposals, create deals, and monitor how many shops have joined.</p>
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
        <div className="space-y-4">
          {groups.map((g) => {
            const isPending = g.status === 'pending_approval';
            const open = expanded === g.id;
            const busy = busyId === g.id;
            return (
              <article key={g.id} className="card overflow-hidden">
                <div className="p-5 flex flex-col lg:flex-row lg:items-start justify-between gap-4">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <h2 className="text-lg font-black text-gray-900">{g.title}</h2>
                      <span className={`text-[11px] font-bold px-2.5 py-1 rounded-full border capitalize ${statusStyle(g.status)}`}>{label(g.status)}</span>
                      <span className="text-[11px] font-semibold text-gray-500 bg-gray-100 px-2 py-1 rounded-full">
                        {g.createdByRole === 'admin' ? 'Created by admin' : g.createdByRole === 'shop' ? 'Shop-created (legacy)' : 'Supplier proposal'}
                      </span>
                    </div>
                    <p className="text-sm text-gray-500 mt-1">
                      {g.supplierName} &middot; {g.products.length} product{g.products.length === 1 ? '' : 's'} &middot; {g.participantCount} shop{g.participantCount === 1 ? '' : 's'} joined
                    </p>
                    {g.description && <p className="text-sm text-gray-500 mt-1">{g.description}</p>}
                    {g.rejectionNote && <p className="text-sm text-red-700 mt-2">Note: {g.rejectionNote}</p>}
                  </div>
                  <div className="flex flex-wrap gap-2 flex-shrink-0">
                    {isPending && (
                      <>
                        <button disabled={busy} className="btn-primary flex items-center gap-1.5 text-sm" onClick={() => act(g.id, () => adminGroupBuyApi.approve(g.id), 'Group buy approved and live')}>
                          <CheckCircle size={15} /> Approve
                        </button>
                        <button disabled={busy} className="btn-secondary flex items-center gap-1.5 text-sm text-red-600" onClick={() => reject(g)}>
                          <XCircle size={15} /> Reject
                        </button>
                      </>
                    )}
                    {(isPending || g.status === 'active') && (
                      <button disabled={busy} className="btn-secondary flex items-center gap-1.5 text-sm" onClick={() => cancel(g)}>
                        <Ban size={15} /> Cancel
                      </button>
                    )}
                  </div>
                </div>

                <div className="px-5 pb-4 space-y-3">
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
                  <div className="flex items-center justify-between pt-1">
                    <p className="text-xs text-gray-400">Ends {new Date(g.expiresAt).toLocaleDateString('en-ZA')}</p>
                    <button className="text-sm font-semibold text-primary flex items-center gap-1" onClick={() => setExpanded(open ? null : g.id)}>
                      <Users size={14} /> Participants {open ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                    </button>
                  </div>
                  {open && (
                    <div className="rounded-xl bg-gray-50 p-3 text-sm">
                      {g.participants.filter((p) => p.status !== 'cancelled').length === 0 ? (
                        <p className="text-gray-400">No shops have joined yet.</p>
                      ) : (
                        <ul className="space-y-1">
                          {g.participants.filter((p) => p.status !== 'cancelled').map((p) => (
                            <li key={p.id} className="flex justify-between">
                              <span className="font-semibold text-gray-800">{p.shopName}</span>
                              <span className="text-gray-500">{p.items.filter((i) => i.status !== 'cancelled').map((i) => `${i.productName} x${i.quantity}`).join(', ')}</span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  )}
                </div>
              </article>
            );
          })}
        </div>
      )}

      {showCreate && <GroupBuyCreateModal mode="admin" onClose={() => setShowCreate(false)} onCreated={() => { setFilter('active'); load(); }} />}
    </div>
  );
}
