import { useEffect, useState } from 'react';
import { X, Loader2, Sparkles, Search } from 'lucide-react';
import toast from 'react-hot-toast';
import clsx from 'clsx';
import { adminProductsApi, type EnrichmentProgress, type EnrichmentStatus } from '../../services/api';

const statusInfo: Record<EnrichmentStatus, { label: string; style: string }> = {
  filled:        { label: 'Filled in',          style: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  up_to_date:    { label: 'Already complete',   style: 'bg-blue-50 text-blue-700 border-blue-200' },
  mismatch:      { label: 'Barcode mismatch',   style: 'bg-red-50 text-red-700 border-red-200' },
  not_found:     { label: 'Not in databases',   style: 'bg-gray-100 text-gray-600 border-gray-200' },
  no_barcode:    { label: 'No pack barcode',    style: 'bg-gray-100 text-gray-500 border-gray-200' },
  lookup_failed: { label: 'Lookup failed',      style: 'bg-amber-50 text-amber-700 border-amber-200' },
  invalid:       { label: 'Could not save',     style: 'bg-amber-50 text-amber-700 border-amber-200' },
};
const order: EnrichmentStatus[] = ['filled', 'mismatch', 'up_to_date', 'not_found', 'no_barcode', 'lookup_failed', 'invalid'];

/**
 * Admin tool: looks every product up by barcode in the open product databases and fills its empty
 * details & specifications, ingredients & nutrition and "spot a fake" checks.
 */
export default function ProductEnrichmentModal({ onClose, onFinished }: { onClose: () => void; onFinished: () => void }) {
  const [progress, setProgress] = useState<EnrichmentProgress | null>(null);
  const [starting, setStarting] = useState(false);
  const [filter, setFilter] = useState<EnrichmentStatus | 'all'>('all');

  useEffect(() => { adminProductsApi.enrichmentStatus().then(setProgress).catch(() => {}); }, []);

  // Poll while a run is going
  const running = progress?.running ?? false;
  useEffect(() => {
    if (!running) return;
    const timer = setInterval(async () => {
      try {
        const next = await adminProductsApi.enrichmentStatus();
        setProgress(next);
        if (!next.running) {
          toast.success(next.dryRun ? 'Check finished. Nothing was saved.' : 'Product information filled in.');
          if (!next.dryRun) onFinished();
        }
      } catch { /* keep polling */ }
    }, 2000);
    return () => clearInterval(timer);
  }, [running, onFinished]);

  const start = async (dryRun: boolean) => {
    if (!dryRun && !confirm('Fill in empty product information for every product? Supplier-entered information is never overwritten.')) return;
    setStarting(true);
    try {
      setProgress(await adminProductsApi.startEnrichment(dryRun));
      setFilter('all');
    } catch (e: any) {
      toast.error(e?.response?.data?.message ?? 'Could not start');
    } finally {
      setStarting(false);
    }
  };

  const results = (progress?.results ?? []).filter((r) => filter === 'all' || r.status === filter);
  const pct = progress && progress.total > 0 ? Math.round((progress.processed / progress.total) * 100) : 0;
  const hasRun = !!progress?.startedAt;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-3xl max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
        <div className="flex items-start justify-between gap-4 p-5 border-b border-gray-100">
          <div>
            <h2 className="font-bold text-gray-900 flex items-center gap-2"><Sparkles size={18} className="text-primary" /> Fill product information</h2>
            <p className="text-sm text-gray-500 mt-1">
              Looks each product up by its pack barcode in the open product databases (Open Food Facts, Open Beauty Facts and others)
              and fills in empty <strong>details &amp; specifications</strong>, <strong>ingredients &amp; nutrition</strong> and
              <strong> spot a fake</strong> checks. Anything a supplier entered is never overwritten, and a barcode that belongs to a
              different product is flagged instead of used.
            </p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close"><X size={18} /></button>
        </div>

        <div className="p-5 space-y-4 overflow-y-auto">
          <div className="flex flex-wrap gap-2">
            <button onClick={() => start(true)} disabled={running || starting} className="btn-secondary flex items-center gap-2 disabled:opacity-50">
              <Search size={14} /> Check first (saves nothing)
            </button>
            <button onClick={() => start(false)} disabled={running || starting} className="btn-primary flex items-center gap-2 disabled:opacity-50">
              {starting || running ? <Loader2 size={14} className="animate-spin" /> : <Sparkles size={14} />} Fill in now
            </button>
          </div>

          {hasRun && progress && (
            <>
              <div>
                <div className="flex justify-between text-xs text-gray-500 mb-1">
                  <span>
                    {progress.dryRun ? 'Check' : 'Fill'} {progress.running ? 'in progress' : 'finished'}: {progress.processed} of {progress.total} products
                  </span>
                  <span>{pct}%</span>
                </div>
                <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                  <div className="h-full bg-primary transition-all" style={{ width: `${pct}%` }} />
                </div>
                {progress.running && <p className="text-xs text-gray-400 mt-1">About 2 seconds per product, so the databases aren't overloaded. You can close this and come back.</p>}
              </div>

              <div className="flex flex-wrap gap-1.5">
                <button onClick={() => setFilter('all')} className={clsx('text-xs px-2.5 py-1 rounded-full border', filter === 'all' ? 'bg-primary text-white border-primary' : 'border-gray-200 text-gray-600')}>
                  All ({progress.results.length})
                </button>
                {order.filter((s) => progress.counts[s]).map((s) => (
                  <button key={s} onClick={() => setFilter(s)} className={clsx('text-xs px-2.5 py-1 rounded-full border', filter === s ? 'ring-2 ring-primary/40' : '', statusInfo[s].style)}>
                    {statusInfo[s].label} ({progress.counts[s]})
                  </button>
                ))}
              </div>

              <div className="border border-gray-100 rounded-xl divide-y divide-gray-100">
                {results.length === 0 && <p className="p-4 text-sm text-gray-400">No products here yet.</p>}
                {results.map((r) => (
                  <div key={r.productId} className="p-3 text-sm flex items-start gap-3">
                    <span className={clsx('text-[11px] font-semibold px-2 py-0.5 rounded-full border whitespace-nowrap', statusInfo[r.status].style)}>
                      {statusInfo[r.status].label}
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="font-semibold text-gray-900 truncate">{r.name}</p>
                      <p className="text-xs text-gray-400">{r.supplier ?? 'Supplier'}{r.code ? ` · ${r.code}` : ''}{r.source ? ` · ${r.source}` : ''}</p>
                      {r.filled.length > 0 && (
                        <p className="text-xs text-emerald-700 mt-0.5">{progress.dryRun ? 'Would fill' : 'Filled'}: {r.filled.join(', ')}</p>
                      )}
                      {r.note && r.status !== 'filled' && <p className="text-xs text-gray-500 mt-0.5">{r.note}</p>}
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
