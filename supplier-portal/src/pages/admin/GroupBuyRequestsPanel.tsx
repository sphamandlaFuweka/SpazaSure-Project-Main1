import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { CheckCircle, Clock, Loader2, XCircle } from 'lucide-react';
import { adminGroupBuyApi, type ShopGroupBuyRequest } from '../../services/api';

const statusLabel: Record<ShopGroupBuyRequest['status'], string> = {
  new: 'New',
  planned: 'Being arranged',
  created: 'Deal arranged',
  declined: 'Declined',
};

const statusStyle: Record<ShopGroupBuyRequest['status'], string> = {
  new: 'bg-accent-50 text-accent-700 border-accent-400',
  planned: 'bg-accent-50 text-accent-700 border-accent-400',
  created: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  declined: 'bg-red-50 text-red-700 border-red-200',
};

const date = (value: string | null) => (value ? new Date(value).toLocaleDateString() : '-');

/** What shops asked SpazaSure to arrange. Each answer notifies the shop; the deal itself is made with New offer. */
export default function GroupBuyRequestsPanel() {
  const [requests, setRequests] = useState<ShopGroupBuyRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setRequests(await adminGroupBuyApi.requests());
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const update = async (r: ShopGroupBuyRequest, status: 'planned' | 'created' | 'declined', note?: string) => {
    setBusyId(r.id);
    try {
      await adminGroupBuyApi.updateRequest(r.id, status, note);
      toast.success('Request updated. The shop has been told.');
      await load();
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setBusyId(null);
    }
  };

  const decline = (r: ShopGroupBuyRequest) => {
    const note = window.prompt(`Why can't "${r.productName}" be arranged? The shop will see this. (optional)`);
    if (note !== null) update(r, 'declined', note.trim() || undefined);
  };

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-primary" /></div>;
  if (requests.length === 0) return <div className="card py-16 text-center text-gray-400">No shop has requested a group buy yet.</div>;

  return (
    <div className="card overflow-x-auto">
      <table className="w-full text-sm min-w-[820px]">
        <thead>
          <tr className="text-left text-xs text-gray-500 border-b border-gray-100">
            <th className="p-3">Product</th>
            <th className="p-3">Shop</th>
            <th className="p-3 text-right">Per shop</th>
            <th className="p-3 text-right">Shops</th>
            <th className="p-3">Runs</th>
            <th className="p-3">Requested</th>
            <th className="p-3">Status</th>
            <th className="p-3 text-right">Answer</th>
          </tr>
        </thead>
        <tbody>
          {requests.map((r) => {
            const busy = busyId === r.id;
            return (
              <tr key={r.id} className="border-b border-gray-50 align-top">
                <td className="p-3">
                  {r.items.length > 0 ? (
                    r.items.map((item) => (
                      <p key={item.productName} className="font-bold text-gray-900">{item.quantity} x {item.productName}</p>
                    ))
                  ) : (
                    <p className="font-bold text-gray-900">{r.productName}</p>
                  )}
                  {r.supplierName && <p className="text-xs text-gray-500 mt-0.5">{r.supplierName}</p>}
                  {r.targetDiscountPct != null && <p className="text-xs text-gray-500 mt-0.5">Hopes for {r.targetDiscountPct}% off</p>}
                  {r.note && <p className="text-xs text-gray-500 mt-0.5 max-w-[280px]">{r.note}</p>}
                  {r.adminNote && <p className="text-xs text-primary mt-0.5 max-w-[280px]">You said: {r.adminNote}</p>}
                </td>
                <td className="p-3">
                  <p className="font-semibold text-gray-800">{r.shopName}</p>
                  <p className="text-xs text-gray-500">{r.area ?? [r.city, r.province].filter(Boolean).join(', ')}</p>
                </td>
                <td className="p-3 text-right tabular-nums">{r.quantity ?? '-'}</td>
                <td className="p-3 text-right tabular-nums">{r.shopsWanted ?? '-'}</td>
                <td className="p-3 whitespace-nowrap">{date(r.opensOn)} to {date(r.closesOn)}</td>
                <td className="p-3 whitespace-nowrap">{new Date(r.createdAt).toLocaleDateString()}</td>
                <td className="p-3">
                  <span className={`text-xs font-semibold px-2 py-0.5 rounded-full border whitespace-nowrap ${statusStyle[r.status]}`}>
                    {statusLabel[r.status]}
                  </span>
                </td>
                <td className="p-3">
                  <div className="flex items-center justify-end gap-1.5">
                    {r.status === 'new' && (
                      <button disabled={busy} className="btn-secondary flex items-center gap-1.5 text-xs whitespace-nowrap" onClick={() => update(r, 'planned')}>
                        <Clock size={14} /> Working on it
                      </button>
                    )}
                    {r.status !== 'created' && (
                      <button disabled={busy} className="btn-secondary flex items-center gap-1.5 text-xs whitespace-nowrap" onClick={() => update(r, 'created')}>
                        <CheckCircle size={14} /> Deal arranged
                      </button>
                    )}
                    {r.status !== 'declined' && r.status !== 'created' && (
                      <button disabled={busy} className="btn-secondary flex items-center gap-1.5 text-xs text-red-600 whitespace-nowrap" onClick={() => decline(r)}>
                        <XCircle size={14} /> Decline
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
