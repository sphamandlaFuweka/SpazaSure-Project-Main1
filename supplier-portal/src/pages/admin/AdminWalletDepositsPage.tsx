import { useEffect, useState } from 'react';
import { Landmark, Clock, CheckCircle, XCircle, RefreshCw, AlertTriangle, Loader2, Copy, Settings } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import clsx from 'clsx';
import { format } from 'date-fns';
import { walletTopUpsApi, type WalletTopUp, type WalletTopUpStatus } from '../../services/api';
import { StatCard, Modal, EmptyState } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import { formatCurrency } from '../../utils';
import { useAdminBadgeStore } from '../../store/adminBadgeStore';

const TABS: { key: WalletTopUpStatus | ''; label: string }[] = [
  { key: 'pending', label: 'Awaiting confirmation' },
  { key: 'approved', label: 'Confirmed' },
  { key: 'rejected', label: 'Rejected' },
  { key: '', label: 'All' },
];

const STATUS_TAG: Record<string, { label: string; className: string }> = {
  pending: { label: 'Awaiting deposit', className: 'tag-yellow' },
  approved: { label: 'Confirmed', className: 'tag-green' },
  rejected: { label: 'Rejected', className: 'tag-red' },
  cancelled: { label: 'Cancelled by shop', className: 'tag bg-gray-100 text-ink-2' },
  failed: { label: 'Failed', className: 'tag-red' },
};

const METHOD_LABEL: Record<string, string> = {
  eft: 'Direct deposit',
  paystack: 'Paystack',
  cash: 'Cash / voucher',
  kazang: 'Kazang',
  shop2shop: 'Shop2Shop',
};

/** Shops top up their wallet by depositing into SpazaSure's bank account; admins confirm each deposit here. */
export default function AdminWalletDepositsPage() {
  const navigate = useNavigate();
  const [tab, setTab] = useState<WalletTopUpStatus | ''>('pending');
  const [items, setItems] = useState<WalletTopUp[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [approving, setApproving] = useState<WalletTopUp | null>(null);
  const [rejecting, setRejecting] = useState<WalletTopUp | null>(null);
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const load = async (status = tab) => {
    setLoading(true);
    setLoadError(false);
    try {
      // Paystack top-ups confirm themselves; only show the ones an admin has to act on
      const list = await walletTopUpsApi.list(status);
      setItems(list.filter((t) => t.method !== 'paystack'));
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(tab); }, [tab]); // eslint-disable-line react-hooks/exhaustive-deps

  const refreshBadges = () => {
    useAdminBadgeStore.setState({ lastFetched: 0 });
    useAdminBadgeStore.getState().fetchBadges();
  };

  const approve = async () => {
    if (!approving) return;
    setSubmitting(true);
    try {
      await walletTopUpsApi.approve(approving.id);
      toast.success(`${formatCurrency(approving.amount)} added to ${approving.shopName}'s wallet`);
      setApproving(null);
      refreshBadges();
      await load();
    } catch {
      // error toast handled by api interceptor
    } finally {
      setSubmitting(false);
    }
  };

  const reject = async () => {
    if (!rejecting || reason.trim().length < 3) return;
    setSubmitting(true);
    try {
      await walletTopUpsApi.reject(rejecting.id, reason.trim());
      toast.success(`Deposit from ${rejecting.shopName} rejected`);
      setRejecting(null);
      setReason('');
      refreshBadges();
      await load();
    } catch {
      // error toast handled by api interceptor
    } finally {
      setSubmitting(false);
    }
  };

  const copy = (text: string) => {
    navigator.clipboard?.writeText(text).then(() => toast.success('Reference copied'));
  };

  if (loading && !items) return <PageLoader variant="table" />;

  const list = items ?? [];
  const total = list.reduce((sum, t) => sum + t.amount, 0);

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Wallet Deposits</h1>
          <p className="page-subtitle">
            Shops deposit into SpazaSure's bank account using their reference. Confirm each deposit once it reflects in the bank, and the shop's wallet is credited.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={() => navigate('/admin/settings')} className="btn-secondary"><Settings size={14} /> Bank details</button>
          <button onClick={() => load()} className="btn-secondary" aria-label="Refresh deposits"><RefreshCw size={14} /></button>
        </div>
      </div>

      {loadError && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
          <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-red-800">We couldn't load deposits. Check your connection and try again.</p>
          <button onClick={() => load()} className="btn-secondary text-sm">Try again</button>
        </div>
      )}

      {tab === 'pending' && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <StatCard title="Awaiting confirmation" value={list.length} icon={<Clock />} variant="amber" description="Deposits shops say they will make" />
          <StatCard title="Total expected" value={formatCurrency(total)} icon={<Landmark />} variant="blue" description="Check these against the bank statement" />
        </div>
      )}

      <div className="flex flex-wrap gap-2" role="tablist">
        {TABS.map((t) => (
          <button key={t.label} role="tab" aria-selected={tab === t.key} onClick={() => setTab(t.key)}
            className={clsx('px-4 py-2 rounded-full text-sm font-bold transition-colors',
              tab === t.key ? 'bg-brand-blue text-white' : 'bg-white border border-brand-blue-light text-ink-2 hover:bg-surface')}>
            {t.label}
          </button>
        ))}
      </div>

      <div className="card overflow-hidden">
        {list.length === 0 ? (
          <EmptyState
            title={tab === 'pending' ? 'No deposits to confirm' : 'Nothing here yet'}
            description={tab === 'pending'
              ? 'When a shop chooses direct deposit in the app, it appears here with its reference.'
              : 'Deposits will appear here once shops start topping up.'}
            icon={<CheckCircle />}
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[820px]">
              <thead>
                <tr>
                  <th className="table-header">Shop</th>
                  <th className="table-header">Reference</th>
                  <th className="table-header text-right">Amount</th>
                  <th className="table-header">Requested</th>
                  <th className="table-header text-center">{tab === 'pending' ? 'Action' : 'Status'}</th>
                </tr>
              </thead>
              <tbody>
                {list.map((t) => {
                  const tag = STATUS_TAG[t.status] ?? { label: t.status, className: 'tag bg-gray-100 text-ink-2' };
                  return (
                    <tr key={t.id} className="table-row">
                      <td className="table-cell">
                        <p className="font-bold text-gray-900">{t.shopName}</p>
                        <p className="text-xs text-ink-3">{[t.ownerName, t.shopPhone].filter(Boolean).join(' · ')}</p>
                      </td>
                      <td className="table-cell">
                        {t.reference ? (
                          <button onClick={() => copy(t.reference!)} title="Copy reference"
                            className="inline-flex items-center gap-1.5 font-mono font-bold text-brand-blue hover:underline">
                            {t.reference} <Copy size={12} />
                          </button>
                        ) : <span className="text-ink-3">—</span>}
                        <p className="text-xs text-ink-3">{METHOD_LABEL[t.method] ?? t.method}</p>
                      </td>
                      <td className="table-cell text-right font-black text-gray-900 tabular-nums">{formatCurrency(t.amount)}</td>
                      <td className="table-cell text-xs text-ink-2">
                        {format(new Date(t.createdAt), 'd MMM yyyy, HH:mm')}
                        {t.status === 'rejected' && t.notes && <p className="text-brand-red mt-0.5">{t.notes}</p>}
                      </td>
                      <td className="table-cell text-center">
                        {t.status === 'pending' ? (
                          <div className="flex items-center justify-center gap-2">
                            <button onClick={() => setApproving(t)} className="btn-primary text-xs px-4 py-2">
                              <CheckCircle size={13} /> Confirm
                            </button>
                            <button onClick={() => { setReason(''); setRejecting(t); }} className="btn-secondary text-xs px-3 py-2" aria-label={`Reject deposit from ${t.shopName}`}>
                              <XCircle size={13} /> Reject
                            </button>
                          </div>
                        ) : (
                          <span className={tag.className}>{tag.label}</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {approving && (
        <Modal title="Confirm deposit" onClose={() => setApproving(null)} size="sm">
          <div className="p-6 space-y-4">
            <div className="rounded-2xl bg-brand-green-light p-4">
              <p className="text-xs font-bold uppercase tracking-wider text-brand-green">Credit wallet</p>
              <p className="text-2xl font-black text-gray-900 mt-1">{formatCurrency(approving.amount)}</p>
              <p className="text-xs text-ink-2 mt-1">{approving.shopName}{approving.reference ? ` · reference ${approving.reference}` : ''}</p>
            </div>
            <p className="text-sm text-ink-2">
              Only confirm once this amount, with this reference, has reflected in SpazaSure's bank account.
              The shop can spend it immediately and is notified.
            </p>
            <div className="flex justify-end gap-3">
              <button type="button" onClick={() => setApproving(null)} className="btn-secondary">Cancel</button>
              <button onClick={approve} disabled={submitting} className="btn-primary">
                {submitting ? <Loader2 size={14} className="animate-spin" /> : <CheckCircle size={14} />} Money received
              </button>
            </div>
          </div>
        </Modal>
      )}

      {rejecting && (
        <Modal title={`Reject deposit from ${rejecting.shopName}`} onClose={() => setRejecting(null)} size="sm">
          <form className="p-6 space-y-4" onSubmit={(e) => { e.preventDefault(); reject(); }}>
            <p className="text-sm text-ink-2">
              {formatCurrency(rejecting.amount)}{rejecting.reference ? ` · ${rejecting.reference}` : ''}. The shop is notified with your reason.
            </p>
            <div>
              <label htmlFor="reject-reason" className="label">Reason</label>
              <input id="reject-reason" className="input" value={reason} autoFocus maxLength={200}
                onChange={(e) => setReason(e.target.value)} placeholder="e.g. No deposit received after 5 days" />
            </div>
            <div className="flex justify-end gap-3">
              <button type="button" onClick={() => setRejecting(null)} className="btn-secondary">Cancel</button>
              <button type="submit" disabled={submitting || reason.trim().length < 3} className="btn-danger">
                {submitting ? <Loader2 size={14} className="animate-spin" /> : <XCircle size={14} />} Reject deposit
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
