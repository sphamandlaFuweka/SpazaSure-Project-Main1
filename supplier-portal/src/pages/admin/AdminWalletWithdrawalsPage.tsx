import { useEffect, useState } from 'react';
import { Banknote, Clock, CheckCircle, XCircle, RefreshCw, AlertTriangle, Loader2, Copy } from 'lucide-react';
import toast from 'react-hot-toast';
import clsx from 'clsx';
import { format } from 'date-fns';
import { walletWithdrawalsApi, type WalletWithdrawal, type WalletWithdrawalStatus } from '../../services/api';
import { StatCard, Modal, EmptyState } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import { formatCurrency } from '../../utils';
import { useAdminBadgeStore } from '../../store/adminBadgeStore';

const TABS: { key: WalletWithdrawalStatus | ''; label: string }[] = [
  { key: 'pending', label: 'To pay out' },
  { key: 'paid', label: 'Paid' },
  { key: 'rejected', label: 'Declined' },
  { key: '', label: 'All' },
];

const STATUS_TAG: Record<string, { label: string; className: string }> = {
  pending: { label: 'To pay out', className: 'tag-yellow' },
  paid: { label: 'Paid', className: 'tag-green' },
  rejected: { label: 'Declined', className: 'tag-red' },
  cancelled: { label: 'Cancelled by shop', className: 'tag bg-gray-100 text-ink-2' },
};

/** Shops withdraw wallet money to their bank account; admins pay it out by EFT and record the bank reference here. */
export default function AdminWalletWithdrawalsPage() {
  const [tab, setTab] = useState<WalletWithdrawalStatus | ''>('pending');
  const [items, setItems] = useState<WalletWithdrawal[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [paying, setPaying] = useState<WalletWithdrawal | null>(null);
  const [paymentRef, setPaymentRef] = useState('');
  const [rejecting, setRejecting] = useState<WalletWithdrawal | null>(null);
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const load = async (status = tab) => {
    setLoading(true);
    setLoadError(false);
    try {
      setItems(await walletWithdrawalsApi.list(status));
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

  const markPaid = async () => {
    if (!paying || paymentRef.trim().length < 3) return;
    setSubmitting(true);
    try {
      await walletWithdrawalsApi.markPaid(paying.id, paymentRef.trim());
      toast.success(`${formatCurrency(paying.amount)} marked as paid to ${paying.shopName}`);
      setPaying(null);
      setPaymentRef('');
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
      await walletWithdrawalsApi.reject(rejecting.id, reason.trim());
      toast.success(`Withdrawal declined. ${formatCurrency(rejecting.amount)} returned to ${rejecting.shopName}'s wallet`);
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

  const copy = (text: string, what: string) => {
    navigator.clipboard?.writeText(text).then(() => toast.success(`${what} copied`));
  };

  if (loading && !items) return <PageLoader variant="table" />;

  const list = items ?? [];
  const total = list.reduce((sum, t) => sum + t.amount, 0);

  const CopyValue = ({ value, what }: { value?: string | null; what: string }) =>
    value ? (
      <button onClick={() => copy(value, what)} title={`Copy ${what.toLowerCase()}`}
        className="inline-flex items-center gap-1.5 font-mono font-bold text-brand-blue hover:underline">
        {value} <Copy size={12} />
      </button>
    ) : <span className="text-ink-3">—</span>;

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Wallet Withdrawals</h1>
          <p className="page-subtitle">
            Shops ask to move wallet money to their bank account. The amount is already held from their wallet. Pay it by EFT, then record the bank reference. Declining returns the money.
          </p>
        </div>
        <button onClick={() => load()} className="btn-secondary" aria-label="Refresh withdrawals"><RefreshCw size={14} /></button>
      </div>

      {loadError && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
          <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-red-800">We couldn't load withdrawals. Check your connection and try again.</p>
          <button onClick={() => load()} className="btn-secondary text-sm">Try again</button>
        </div>
      )}

      {tab === 'pending' && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <StatCard title="Waiting to be paid" value={list.length} icon={<Clock />} variant="amber" description="Withdrawal requests from shops" />
          <StatCard title="Total to pay out" value={formatCurrency(total)} icon={<Banknote />} variant="blue" description="Already held from shop wallets" />
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
            title={tab === 'pending' ? 'No withdrawals to pay' : 'Nothing here yet'}
            description={tab === 'pending'
              ? 'When a shop withdraws from its wallet in the app, the request appears here with bank details.'
              : 'Withdrawals will appear here once shops start requesting them.'}
            icon={<CheckCircle />}
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[980px]">
              <thead>
                <tr>
                  <th className="table-header">Shop</th>
                  <th className="table-header">Bank account</th>
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
                        <p className="font-semibold text-gray-900">{t.accountHolder ?? '—'}</p>
                        <p className="text-xs text-ink-2">{t.bankName ?? '—'}{t.branchCode ? ` · branch ${t.branchCode}` : ''}</p>
                        <CopyValue value={t.accountNumber} what="Account number" />
                      </td>
                      <td className="table-cell">
                        <CopyValue value={t.reference} what="Reference" />
                      </td>
                      <td className="table-cell text-right font-black text-gray-900 tabular-nums">{formatCurrency(t.amount)}</td>
                      <td className="table-cell text-xs text-ink-2">
                        {format(new Date(t.createdAt), 'd MMM yyyy, HH:mm')}
                        {t.status === 'paid' && t.notes && <p className="text-ink-3 mt-0.5">Bank ref: {t.notes}</p>}
                        {t.status === 'rejected' && t.notes && <p className="text-brand-red mt-0.5">{t.notes}</p>}
                      </td>
                      <td className="table-cell text-center">
                        {t.status === 'pending' ? (
                          <div className="flex items-center justify-center gap-2">
                            <button onClick={() => { setPaymentRef(''); setPaying(t); }} className="btn-primary text-xs px-4 py-2">
                              <CheckCircle size={13} /> Mark paid
                            </button>
                            <button onClick={() => { setReason(''); setRejecting(t); }} className="btn-secondary text-xs px-3 py-2" aria-label={`Decline withdrawal from ${t.shopName}`}>
                              <XCircle size={13} /> Decline
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

      {paying && (
        <Modal title="Mark withdrawal as paid" onClose={() => setPaying(null)} size="sm">
          <form className="p-6 space-y-4" onSubmit={(e) => { e.preventDefault(); markPaid(); }}>
            <div className="rounded-2xl bg-brand-green-light p-4">
              <p className="text-xs font-bold uppercase tracking-wider text-brand-green">Pay out</p>
              <p className="text-2xl font-black text-gray-900 mt-1">{formatCurrency(paying.amount)}</p>
              <p className="text-xs text-ink-2 mt-1">
                {paying.shopName} · {paying.accountHolder} · {paying.bankName} {paying.accountNumber}
              </p>
            </div>
            <div>
              <label htmlFor="payment-ref" className="label">Bank payment reference</label>
              <input id="payment-ref" className="input" value={paymentRef} autoFocus maxLength={100}
                onChange={(e) => setPaymentRef(e.target.value)} placeholder="Reference from your bank's EFT confirmation" />
              <p className="text-xs text-ink-3 mt-1">Only record this after the EFT has been sent. The shop is notified.</p>
            </div>
            <div className="flex justify-end gap-3">
              <button type="button" onClick={() => setPaying(null)} className="btn-secondary">Cancel</button>
              <button type="submit" disabled={submitting || paymentRef.trim().length < 3} className="btn-primary">
                {submitting ? <Loader2 size={14} className="animate-spin" /> : <CheckCircle size={14} />} Money sent
              </button>
            </div>
          </form>
        </Modal>
      )}

      {rejecting && (
        <Modal title={`Decline withdrawal from ${rejecting.shopName}`} onClose={() => setRejecting(null)} size="sm">
          <form className="p-6 space-y-4" onSubmit={(e) => { e.preventDefault(); reject(); }}>
            <p className="text-sm text-ink-2">
              {formatCurrency(rejecting.amount)} goes back to the shop's wallet and the shop is notified with your reason.
            </p>
            <div>
              <label htmlFor="decline-reason" className="label">Reason</label>
              <input id="decline-reason" className="input" value={reason} autoFocus maxLength={200}
                onChange={(e) => setReason(e.target.value)} placeholder="e.g. Bank details could not be verified" />
            </div>
            <div className="flex justify-end gap-3">
              <button type="button" onClick={() => setRejecting(null)} className="btn-secondary">Cancel</button>
              <button type="submit" disabled={submitting || reason.trim().length < 3} className="btn-danger">
                {submitting ? <Loader2 size={14} className="animate-spin" /> : <XCircle size={14} />} Decline withdrawal
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
