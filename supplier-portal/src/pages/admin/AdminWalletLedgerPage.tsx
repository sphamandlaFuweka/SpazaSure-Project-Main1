import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, ChevronLeft, ChevronRight, RefreshCw, AlertTriangle, BookOpen, Wallet } from 'lucide-react';
import { format } from 'date-fns';
import { adminFinanceApi, type WalletLedgerEntry } from '../../services/api';
import { StatCard, EmptyState } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import { formatCurrency } from '../../utils';

const PAGE_SIZE = 25;

const TYPE_LABEL: Record<string, string> = {
  top_up: 'Top-up',
  top_up_reversal: 'Top-up reversed',
  order_payment: 'Order payment',
  order_refund: 'Order refund',
  withdrawal_debit: 'Withdrawal',
  withdrawal_reversal: 'Withdrawal returned',
};

/** Every signed movement on shop wallets. The balance of a wallet is the sum of its entries. */
export default function AdminWalletLedgerPage() {
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<{ total: number; balance: number; items: WalletLedgerEntry[] } | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(false);
    try {
      setData(await adminFinanceApi.walletLedger({ search: query || undefined }, page, PAGE_SIZE));
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [query, page]);

  useEffect(() => { load(); }, [load]);

  if (loading && !data) return <PageLoader variant="table" />;

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link to="/admin/finance" className="inline-flex items-center gap-1 text-xs font-bold text-brand-blue hover:underline mb-1">
            <ArrowLeft size={12} /> Finance
          </Link>
          <h1 className="page-title">Wallet Ledger</h1>
          <p className="page-subtitle">Every credit and debit on shop wallets: top-ups, order payments, refunds and withdrawals.</p>
        </div>
        <button onClick={load} className="btn-secondary" aria-label="Refresh ledger"><RefreshCw size={14} /></button>
      </div>

      {loadError && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
          <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-red-800">We couldn't load the ledger. Check your connection and try again.</p>
          <button onClick={load} className="btn-secondary text-sm">Try again</button>
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <StatCard title={query ? 'Balance of matching shops' : 'Held in shop wallets'} value={formatCurrency(data?.balance ?? 0)}
          icon={<Wallet />} variant="blue" description="Sum of all ledger entries" />
        <StatCard title="Entries" value={total} icon={<BookOpen />} variant="amber" description={query ? `Matching "${query}"` : 'All time'} />
      </div>

      <form className="flex gap-2 max-w-md" onSubmit={(e) => { e.preventDefault(); setPage(1); setQuery(search.trim()); }}>
        <label htmlFor="ledger-search" className="sr-only">Search by shop name</label>
        <input id="ledger-search" className="input" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search by shop name" />
        <button type="submit" className="btn-primary">Search</button>
      </form>

      <div className="card overflow-hidden">
        {items.length === 0 ? (
          <EmptyState title="No ledger entries" description="Wallet movements appear here as shops top up, pay and withdraw." icon={<BookOpen />} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[760px]">
              <thead>
                <tr>
                  <th className="table-header">Date</th>
                  <th className="table-header">Shop</th>
                  <th className="table-header">Type</th>
                  <th className="table-header">Description</th>
                  <th className="table-header text-right">Amount</th>
                </tr>
              </thead>
              <tbody>
                {items.map((e) => (
                  <tr key={e.id} className="table-row">
                    <td className="table-cell text-xs text-ink-2 whitespace-nowrap">{format(new Date(e.date), 'd MMM yyyy, HH:mm')}</td>
                    <td className="table-cell font-bold text-gray-900">{e.shopName}</td>
                    <td className="table-cell">{TYPE_LABEL[e.type] ?? e.type}</td>
                    <td className="table-cell text-ink-2">
                      {e.description}
                      {e.reference && <p className="text-xs font-mono text-ink-3">{e.reference}</p>}
                    </td>
                    <td className={`table-cell text-right font-black tabular-nums ${e.amount < 0 ? 'text-brand-red' : 'text-brand-green'}`}>
                      {e.amount < 0 ? '−' : '+'}{formatCurrency(Math.abs(e.amount))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {pages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-ink-3">Page {page} of {pages}</p>
          <div className="flex gap-2">
            <button className="btn-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} aria-label="Previous page"><ChevronLeft size={14} /></button>
            <button className="btn-secondary" disabled={page >= pages} onClick={() => setPage((p) => p + 1)} aria-label="Next page"><ChevronRight size={14} /></button>
          </div>
        </div>
      )}
    </div>
  );
}
