import { useEffect, useState } from 'react';
import { Wallet, Clock, CheckCircle, RefreshCw, AlertTriangle, Landmark, Loader2, Settings } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { payoutsApi, type AdminPayoutSupplier } from '../../services/api';
import { StatCard, Modal, EmptyState } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import { formatCurrency } from '../../utils';

export default function AdminPayoutsPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [data, setData] = useState<{ payoutDelayDays: number; totalAvailable: number; totalPending: number; suppliers: AdminPayoutSupplier[] } | null>(null);
  const [paying, setPaying] = useState<AdminPayoutSupplier | null>(null);
  const [reference, setReference] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const load = async () => {
    setLoading(true);
    setLoadError(false);
    try {
      setData(await payoutsApi.adminList());
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const markPaid = async () => {
    if (!paying || reference.trim().length < 3) return;
    setSubmitting(true);
    try {
      const result = await payoutsApi.markPaid(paying.supplierId, reference.trim());
      toast.success(`Recorded ${formatCurrency(result.total)} paid to ${paying.companyName}`);
      setPaying(null);
      setReference('');
      await load();
    } catch {
      // error toast handled by api interceptor
    } finally {
      setSubmitting(false);
    }
  };

  if (loading && !data) return <PageLoader variant="table" />;

  const suppliers = data?.suppliers ?? [];

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Supplier Payouts</h1>
          <p className="page-subtitle">
            Suppliers are owed products + delivery for orders paid through Paystack,
            {data ? ` ${data.payoutDelayDays} day${data.payoutDelayDays === 1 ? '' : 's'}` : ''} after delivery.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={() => navigate('/admin/settings')} className="btn-secondary"><Settings size={14} /> Payout delay</button>
          <button onClick={load} className="btn-secondary" aria-label="Refresh payouts"><RefreshCw size={14} /></button>
        </div>
      </div>

      {loadError && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
          <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-red-800">We couldn't load payouts. Check your connection and try again.</p>
          <button onClick={load} className="btn-secondary text-sm">Try again</button>
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <StatCard title="Ready to pay" value={formatCurrency(data?.totalAvailable ?? 0)} icon={<Wallet />} variant="green" description="Past the payout delay" />
        <StatCard title="Pending" value={formatCurrency(data?.totalPending ?? 0)} icon={<Clock />} variant="amber" description="Delivered, still in the delay window" />
        <StatCard title="Suppliers owed" value={suppliers.filter((s) => s.availableOrders > 0).length} icon={<Landmark />} variant="blue" description="With money ready to pay" />
      </div>

      <div className="card overflow-hidden">
        {suppliers.length === 0 ? (
          <EmptyState
            title="Nothing to pay out"
            description="When shops pay through Paystack and orders are delivered, the amounts owed to suppliers appear here."
            icon={<CheckCircle />}
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[820px]">
              <thead>
                <tr>
                  <th className="table-header">Supplier</th>
                  <th className="table-header">Bank details</th>
                  <th className="table-header text-right">Ready to pay</th>
                  <th className="table-header text-right">Pending</th>
                  <th className="table-header text-center">Action</th>
                </tr>
              </thead>
              <tbody>
                {suppliers.map((s) => (
                  <tr key={s.supplierId} className="table-row">
                    <td className="table-cell">
                      <p className="font-bold text-gray-900">{s.companyName}</p>
                      <p className="text-xs text-ink-3">{s.email}</p>
                    </td>
                    <td className="table-cell">
                      {s.bankAccount ? (
                        <div className="text-xs">
                          <p className="font-semibold text-gray-900">{s.bankName || 'Bank'}</p>
                          <p className="font-mono text-ink-2">{s.bankAccount}{s.bankBranchCode ? ` · ${s.bankBranchCode}` : ''}</p>
                        </div>
                      ) : (
                        <span className="tag-red">No bank details</span>
                      )}
                    </td>
                    <td className="table-cell text-right">
                      <p className="font-black text-gray-900">{formatCurrency(s.availableAmount)}</p>
                      <p className="text-xs text-ink-3">{s.availableOrders} order{s.availableOrders === 1 ? '' : 's'}</p>
                    </td>
                    <td className="table-cell text-right">
                      <p className="font-semibold text-ink-2">{formatCurrency(s.pendingAmount)}</p>
                      <p className="text-xs text-ink-3">{s.pendingOrders} order{s.pendingOrders === 1 ? '' : 's'}</p>
                    </td>
                    <td className="table-cell text-center">
                      <button
                        onClick={() => { setReference(''); setPaying(s); }}
                        disabled={s.availableOrders === 0}
                        className="btn-primary text-xs px-4 py-2"
                        title={s.availableOrders === 0 ? 'Nothing is ready to pay yet' : undefined}
                      >
                        Mark as paid
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {paying && (
        <Modal title={`Record payout to ${paying.companyName}`} onClose={() => setPaying(null)} size="sm">
          <form className="p-6 space-y-4" onSubmit={(e) => { e.preventDefault(); markPaid(); }}>
            <div className="rounded-2xl bg-brand-green-light p-4">
              <p className="text-xs font-bold uppercase tracking-wider text-brand-green">Amount to pay</p>
              <p className="text-2xl font-black text-gray-900 mt-1">{formatCurrency(paying.availableAmount)}</p>
              <p className="text-xs text-ink-2 mt-1">
                {paying.availableOrders} delivered order{paying.availableOrders === 1 ? '' : 's'}
                {paying.bankAccount ? ` → ${paying.bankName || 'Bank'} ${paying.bankAccount}` : ''}
              </p>
            </div>
            <p className="text-sm text-ink-2">
              Make the EFT first, then record its reference here. The supplier is notified and these orders are marked as paid.
            </p>
            <div>
              <label htmlFor="payout-ref" className="label">Payment reference</label>
              <input id="payout-ref" className="input font-mono" value={reference} autoFocus
                onChange={(e) => setReference(e.target.value)} placeholder="e.g. EFT-2026-10-001" maxLength={100} />
            </div>
            <div className="flex justify-end gap-3">
              <button type="button" onClick={() => setPaying(null)} className="btn-secondary">Cancel</button>
              <button type="submit" disabled={submitting || reference.trim().length < 3} className="btn-primary">
                {submitting ? <Loader2 size={14} className="animate-spin" /> : <CheckCircle size={14} />} Record payout
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
