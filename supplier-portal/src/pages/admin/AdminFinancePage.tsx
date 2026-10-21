import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowDownLeft, ArrowUpRight, Banknote, Landmark, Wallet, RefreshCw, AlertTriangle,
  ChevronLeft, ChevronRight, Receipt, Clock, Undo2, BookOpen,
} from 'lucide-react';
import clsx from 'clsx';
import { format } from 'date-fns';
import {
  adminFinanceApi,
  type FinanceCategory, type FinanceEntry, type FinanceFilters, type FinanceStatus, type FinanceSummary,
} from '../../services/api';
import { StatCard, EmptyState } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import { formatCurrency } from '../../utils';

const PAGE_SIZE = 25;

const CATEGORY_LABEL: Record<FinanceCategory, string> = {
  wallet_top_up: 'Wallet top-ups',
  order_paystack: 'Orders paid by Paystack',
  order_cash_on_delivery: 'Cash on delivery',
  onboarding_fee: 'Onboarding fees',
  supplier_subscription: 'Supplier subscriptions',
  supplier_payout: 'Supplier payouts',
  shop_withdrawal: 'Shop withdrawals',
};

const STATUS_TAG: Record<FinanceStatus, { label: string; className: string }> = {
  confirmed: { label: 'Confirmed', className: 'tag-green' },
  pending: { label: 'Pending', className: 'tag-yellow' },
  rejected: { label: 'Rejected', className: 'tag-red' },
  cancelled: { label: 'Cancelled', className: 'tag bg-gray-100 text-ink-2' },
  failed: { label: 'Failed', className: 'tag-red' },
  refund_due: { label: 'Refund due', className: 'tag-red' },
};

const toDateInput = (d: Date) => format(d, 'yyyy-MM-dd');

/** Everything money-related in one place: what came in, what went out, and what SpazaSure holds or owes. */
export default function AdminFinancePage() {
  const [from, setFrom] = useState(() => toDateInput(new Date(Date.now() - 30 * 86_400_000)));
  const [to, setTo] = useState(() => toDateInput(new Date()));
  const [direction, setDirection] = useState<FinanceFilters['direction']>('');
  const [category, setCategory] = useState<FinanceFilters['category']>('');
  const [status, setStatus] = useState<FinanceFilters['status']>('');
  const [page, setPage] = useState(1);

  const [summary, setSummary] = useState<FinanceSummary | null>(null);
  const [entries, setEntries] = useState<FinanceEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(false);
    try {
      const [s, t] = await Promise.all([
        adminFinanceApi.summary({ from, to }),
        adminFinanceApi.transactions({ from, to, direction, category, status }, page, PAGE_SIZE),
      ]);
      setSummary(s);
      setEntries(t.items);
      setTotal(t.total);
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [from, to, direction, category, status, page]);

  useEffect(() => { load(); }, [load]);

  const changeFilter = <T,>(setter: (v: T) => void) => (v: T) => { setter(v); setPage(1); };
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  if (loading && !summary) return <PageLoader variant="table" />;

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Finance</h1>
          <p className="page-subtitle">
            All money coming in and going out: wallet top-ups, Paystack payments, cash collected on delivery, fees, subscriptions, supplier payouts and shop withdrawals.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Link to="/admin/wallet-ledger" className="btn-secondary"><BookOpen size={14} /> Wallet ledger</Link>
          <button onClick={load} className="btn-secondary" aria-label="Refresh finance"><RefreshCw size={14} /></button>
        </div>
      </div>

      {loadError && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
          <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-red-800">We couldn't load the finance data. Check your connection and try again.</p>
          <button onClick={load} className="btn-secondary text-sm">Try again</button>
        </div>
      )}

      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label htmlFor="fin-from" className="block text-xs font-bold text-ink-3 mb-1">From</label>
          <input id="fin-from" type="date" className="input" value={from} max={to}
            onChange={(e) => e.target.value && changeFilter(setFrom)(e.target.value)} />
        </div>
        <div>
          <label htmlFor="fin-to" className="block text-xs font-bold text-ink-3 mb-1">To</label>
          <input id="fin-to" type="date" className="input" value={to} min={from}
            onChange={(e) => e.target.value && changeFilter(setTo)(e.target.value)} />
        </div>
      </div>

      {summary && (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
            <StatCard title="Money in" value={formatCurrency(summary.moneyIn)} icon={<ArrowDownLeft />} variant="green" description="Confirmed in this period" />
            <StatCard title="Money out" value={formatCurrency(summary.moneyOut)} icon={<ArrowUpRight />} variant="red"             description="Supplier payouts and shop withdrawals paid" />
            <StatCard title="Net" value={formatCurrency(summary.net)} icon={<Banknote />} variant="blue" description="Money in minus money out" />
            <StatCard title="SpazaSure earnings" value={formatCurrency(summary.platformEarnings)} icon={<Receipt />} variant="default" description="Commission on delivered orders, onboarding fees and subscriptions" />
          </div>

                      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-5 gap-4">
                        <StatCard title="Held in shop wallets" value={formatCurrency(summary.walletBalancesHeld)} icon={<Wallet />} variant="default" description="Total of the wallet ledger right now" />
            <StatCard title="Owed to suppliers" value={formatCurrency(summary.owedToSuppliers)} icon={<Landmark />} variant="amber" description="Delivered and paid, not yet paid out (all time)" />
            <StatCard title="Cash still to collect" value={formatCurrency(summary.codOutstanding.amount)} icon={<Clock />} variant="amber" description={`${summary.codOutstanding.count} cash on delivery order${summary.codOutstanding.count === 1 ? '' : 's'} not yet delivered`} />
            <StatCard title="Deposits awaiting you" value={formatCurrency(summary.pendingDeposits.amount)} icon={<Clock />} variant="amber" description={`${summary.pendingDeposits.count} bank deposit${summary.pendingDeposits.count === 1 ? '' : 's'} to confirm`} />
            <StatCard title="Withdrawals to pay" value={formatCurrency(summary.pendingWithdrawals.amount)} icon={<Clock />} variant="amber" description={`${summary.pendingWithdrawals.count} shop withdrawal${summary.pendingWithdrawals.count === 1 ? '' : 's'} to pay`} />
          </div>

          {summary.refundsDue.count > 0 && (
            <div role="status" className="flex items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
              <Undo2 size={18} className="text-red-500 flex-shrink-0" />
              <p className="text-sm font-medium text-red-800">
                {summary.refundsDue.count} Paystack order{summary.refundsDue.count === 1 ? ' was' : 's were'} cancelled after payment in this period ({formatCurrency(summary.refundsDue.amount)}). Refund through Paystack and arrange recovery of the supplier share separately; completion isn't tracked here.
              </p>
            </div>
          )}

          <div className="card p-4">
            <h2 className="font-bold text-gray-900 mb-3">By source</h2>
            {summary.byCategory.length === 0 ? (
              <p className="text-sm text-ink-3">No confirmed money movements in this period.</p>
            ) : (
              <ul className="divide-y divide-gray-100">
                {summary.byCategory.map((c) => (
                  <li key={c.category} className="flex items-center justify-between py-2 text-sm">
                    <span className="flex items-center gap-2 text-gray-700">
                      {c.direction === 'in'
                        ? <ArrowDownLeft size={14} className="text-brand-green" />
                        : <ArrowUpRight size={14} className="text-brand-red" />}
                      {CATEGORY_LABEL[c.category] ?? c.category}
                      <span className="text-xs text-ink-3">({c.count})</span>
                    </span>
                    <span className={clsx('font-black tabular-nums', c.direction === 'in' ? 'text-gray-900' : 'text-brand-red')}>
                      {c.direction === 'out' ? '-' : ''}{formatCurrency(c.amount)}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}

      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label htmlFor="fin-direction" className="block text-xs font-bold text-ink-3 mb-1">Type</label>
          <select id="fin-direction" className="select" value={direction}
            onChange={(e) => changeFilter(setDirection)(e.target.value as FinanceFilters['direction'])}>
            <option value="">Money in and out</option>
            <option value="in">Money in</option>
            <option value="out">Money out</option>
          </select>
        </div>
        <div>
          <label htmlFor="fin-category" className="block text-xs font-bold text-ink-3 mb-1">Source</label>
          <select id="fin-category" className="select" value={category}
            onChange={(e) => changeFilter(setCategory)(e.target.value as FinanceFilters['category'])}>
            <option value="">All sources</option>
            {(Object.keys(CATEGORY_LABEL) as FinanceCategory[]).map((k) => (
              <option key={k} value={k}>{CATEGORY_LABEL[k]}</option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="fin-status" className="block text-xs font-bold text-ink-3 mb-1">Status</label>
          <select id="fin-status" className="select" value={status}
            onChange={(e) => changeFilter(setStatus)(e.target.value as FinanceFilters['status'])}>
            <option value="">All statuses</option>
            {(Object.keys(STATUS_TAG) as FinanceStatus[]).map((k) => (
              <option key={k} value={k}>{STATUS_TAG[k].label}</option>
            ))}
          </select>
        </div>
      </div>

      <div className="card overflow-hidden">
        {entries.length === 0 ? (
          <EmptyState title="No transactions" description="Nothing matches these filters for the selected dates." icon={<Banknote />} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[860px]">
              <thead>
                <tr>
                  <th className="table-header">Date</th>
                  <th className="table-header">Details</th>
                  <th className="table-header">Shop / supplier</th>
                  <th className="table-header">Reference</th>
                  <th className="table-header text-right">Amount</th>
                  <th className="table-header text-center">Status</th>
                </tr>
              </thead>
              <tbody>
                {entries.map((e) => {
                  const tag = STATUS_TAG[e.status] ?? { label: e.status, className: 'tag bg-gray-100 text-ink-2' };
                  return (
                    <tr key={e.id} className="table-row">
                      <td className="table-cell text-xs text-ink-2 whitespace-nowrap">{format(new Date(e.date), 'd MMM yyyy, HH:mm')}</td>
                      <td className="table-cell">
                        <p className="font-semibold text-gray-900">{e.description}</p>
                        <p className="text-xs text-ink-3">{CATEGORY_LABEL[e.category] ?? e.category}</p>
                      </td>
                      <td className="table-cell text-gray-700">{e.party}</td>
                      <td className="table-cell font-mono text-xs text-ink-2">{e.reference ?? '—'}</td>
                      <td className={clsx('table-cell text-right font-black tabular-nums', e.direction === 'out' ? 'text-brand-red' : 'text-gray-900')}>
                        {e.direction === 'out' ? '-' : ''}{formatCurrency(e.amount)}
                      </td>
                      <td className="table-cell text-center"><span className={tag.className}>{tag.label}</span></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
        {total > PAGE_SIZE && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100 text-sm">
            <span className="text-ink-3">{total} transactions</span>
            <div className="flex items-center gap-2">
              <button className="btn-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} aria-label="Previous page"><ChevronLeft size={14} /></button>
              <span className="font-semibold">Page {page} of {pages}</span>
              <button className="btn-secondary" disabled={page >= pages} onClick={() => setPage((p) => p + 1)} aria-label="Next page"><ChevronRight size={14} /></button>
            </div>
          </div>
        )}
      </div>

      <p className="text-xs text-ink-3">
        Customer subscriptions aren't listed because no payment record is kept for them. Paystack order dates are when the order was placed.
      </p>
    </div>
  );
}
