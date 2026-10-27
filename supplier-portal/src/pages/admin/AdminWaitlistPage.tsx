import { useCallback, useEffect, useState } from 'react';
import { ListPlus, Download, Trash2, CheckCircle2, Clock } from 'lucide-react';
import toast from 'react-hot-toast';
import { format } from 'date-fns';
import { adminWaitlistApi, apiErrorMessage, type WaitlistPage, type WaitlistSignup } from '../../services/api';
import { Spinner } from '../../components/ui';

const interestLabels: Record<string, string> = { customer: 'Shopper', spaza_owner: 'Spaza shop', supplier: 'Supplier' };

/**
 * People who joined the waitlist on the SpazaSure website. Each gets a confirmation email; only
 * confirmed addresses should be invited or emailed again.
 */
export default function AdminWaitlistPage() {
  const [data, setData] = useState<WaitlistPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [interest, setInterest] = useState('');
  const [confirmed, setConfirmed] = useState<'' | 'yes' | 'no'>('');
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);

  const filters = {
    interest: interest || undefined,
    confirmed: confirmed === '' ? undefined : confirmed === 'yes',
  };

  const load = useCallback(() => {
    setLoading(true);
    adminWaitlistApi.list({ ...filters, page })
      .then(setData)
      .catch((err) => toast.error(apiErrorMessage(err, 'Failed to load the waitlist')))
      .finally(() => setLoading(false));
  }, [interest, confirmed, page]);

  useEffect(() => { load(); }, [load]);

  const exportCsv = async () => {
    setExporting(true);
    try {
      await adminWaitlistApi.exportCsv(filters);
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not export the waitlist'));
    } finally {
      setExporting(false);
    }
  };

  const remove = async (w: WaitlistSignup) => {
    if (!window.confirm(`Remove ${w.email} from the waitlist? Their details are deleted.`)) return;
    try {
      await adminWaitlistApi.remove(w.id);
      toast.success('Removed');
      load();
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not remove'));
    }
  };

  const s = data?.summary;
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const confirmRate = s && s.total > 0 ? Math.round((s.confirmed / s.total) * 100) : 0;

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex flex-wrap items-center gap-3">
        <div className="w-10 h-10 bg-gradient-to-br from-emerald-500 to-teal-600 rounded-xl flex items-center justify-center shadow-lg shadow-emerald-200">
          <ListPlus size={18} className="text-white" />
        </div>
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-black text-gray-900 tracking-tight">Waitlist</h1>
          <p className="text-sm text-gray-400 font-medium">People who signed up on the website to get access</p>
        </div>
        <button className="btn-secondary flex items-center gap-2" disabled={exporting} onClick={exportCsv}>
          <Download size={16} /> Export CSV
        </button>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <Stat label="Signed up" value={s?.total ?? 0} />
        <Stat label="Confirmed email" value={s?.confirmed ?? 0} hint={`${confirmRate}% of sign-ups`} />
        <Stat label="Last 7 days" value={s?.lastSevenDays ?? 0} />
        <div className="card p-3">
          <p className="text-xs text-gray-500 font-semibold mb-1">Who they are</p>
          {(s?.byInterest ?? []).map((b) => (
            <p key={b.interest} className="text-sm text-gray-700">
              {interestLabels[b.interest] ?? b.interest}: <span className="font-bold">{b.count}</span>
            </p>
          ))}
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        <select className="select w-auto" value={interest} onChange={(e) => { setInterest(e.target.value); setPage(1); }}>
          <option value="">Everyone</option>
          {Object.entries(interestLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        <select className="select w-auto" value={confirmed} onChange={(e) => { setConfirmed(e.target.value as '' | 'yes' | 'no'); setPage(1); }}>
          <option value="">Confirmed and unconfirmed</option>
          <option value="yes">Confirmed only</option>
          <option value="no">Not confirmed yet</option>
        </select>
      </div>

      {loading && !data ? (
        <div className="flex justify-center py-16"><Spinner /></div>
      ) : !data || data.items.length === 0 ? (
        <div className="card p-10 text-center text-sm text-gray-500">
          Nobody here yet. Sign-ups from the website's waitlist form appear here.
        </div>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-gray-500 border-b border-gray-100">
                <th className="p-3">Email</th>
                <th className="p-3">Name & phone</th>
                <th className="p-3">As a</th>
                <th className="p-3">Area</th>
                <th className="p-3">Joined</th>
                <th className="p-3">Status</th>
                <th className="p-3" />
              </tr>
            </thead>
            <tbody>
              {data.items.map((w) => (
                <tr key={w.id} className="border-b border-gray-50 last:border-0">
                  <td className="p-3 font-semibold text-gray-900 break-all">{w.email}</td>
                  <td className="p-3 text-gray-600">{[w.name, w.phone].filter(Boolean).join(' · ') || '–'}</td>
                  <td className="p-3 text-gray-600">{interestLabels[w.interest] ?? w.interest}</td>
                  <td className="p-3 text-gray-600">{w.area || '–'}</td>
                  <td className="p-3 text-gray-600 whitespace-nowrap">
                    {format(new Date(w.createdAt), 'd MMM yyyy')}
                    {w.source && <span className="block text-[11px] text-gray-400">via {w.source}</span>}
                  </td>
                  <td className="p-3 whitespace-nowrap">
                    {w.unsubscribedAt ? (
                      <span className="text-xs font-semibold text-gray-400">Left the list</span>
                    ) : w.confirmedAt ? (
                      <span className="inline-flex items-center gap-1 text-xs font-semibold text-emerald-600">
                        <CheckCircle2 size={14} /> Confirmed
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-xs font-semibold text-amber-600">
                        <Clock size={14} /> Waiting for confirmation
                      </span>
                    )}
                    {w.marketingConsent && <span className="block text-[11px] text-gray-400">Agreed to updates</span>}
                  </td>
                  <td className="p-3 text-right">
                    <button className="btn-icon" title="Remove from the waitlist" onClick={() => remove(w)}>
                      <Trash2 size={16} className="text-gray-400" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {pages > 1 && (
            <div className="flex items-center justify-center gap-3 p-3">
              <button className="btn-secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button>
              <span className="text-sm text-gray-500">Page {page} of {pages}</span>
              <button className="btn-secondary" disabled={page >= pages} onClick={() => setPage(page + 1)}>Next</button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: number; hint?: string }) {
  return (
    <div className="card p-3">
      <p className="text-xs text-gray-500 font-semibold">{label}</p>
      <p className="text-xl font-black text-gray-900">{value}</p>
      {hint && <p className="text-[11px] text-gray-400">{hint}</p>}
    </div>
  );
}
