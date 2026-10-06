import { useState, useEffect, useCallback } from 'react';
import { Search, Eye, Flag, Loader2, Send, Mail, CheckCircle, XCircle, PlayCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { format } from 'date-fns';
import clsx from 'clsx';
import { adminReportsApi, resolveUploadUrl } from '../../services/api';
import { useAdminBadgeStore } from '../../store/adminBadgeStore';
import { Modal } from '../../components/ui';
import type { AdminReport, ReportStatus } from '../../types/adminReport';

const statusTabs: { value: string; label: string }[] = [
  { value: 'all',          label: 'All' },
  { value: 'submitted',    label: 'New' },
  { value: 'under_review', label: 'Under review' },
  { value: 'escalated',    label: 'Escalated' },
  { value: 'resolved',     label: 'Resolved' },
  { value: 'dismissed',    label: 'Dismissed' },
];

const statusStyle: Record<ReportStatus, string> = {
  submitted:    'bg-blue-50 text-blue-700',
  under_review: 'bg-amber-50 text-amber-700',
  escalated:    'bg-violet-50 text-violet-700',
  resolved:     'bg-emerald-50 text-emerald-700',
  dismissed:    'bg-gray-100 text-gray-600',
};

const statusLabel = (s: string) => statusTabs.find((t) => t.value === s)?.label ?? s;

const AUTHORITIES = ['eThekwini Environmental Health', 'SABS', 'Brand owner', 'Other'];

function StatusPill({ status }: { status: ReportStatus }) {
  return <span className={clsx('text-xs font-bold px-2.5 py-1 rounded-full', statusStyle[status] ?? statusStyle.dismissed)}>{statusLabel(status)}</span>;
}

function Field({ label, value }: { label: string; value?: string | number | null }) {
  if (value === null || value === undefined || value === '') return null;
  return (
    <div>
      <p className="text-[11px] font-bold uppercase tracking-wide text-gray-400">{label}</p>
      <p className="text-sm font-semibold text-gray-800 break-words">{value}</p>
    </div>
  );
}

// Reporter identity is left out: the recipient is an outside authority.
function buildEvidenceEmail(report: AdminReport, authority: string, note: string) {
  const lines = [
    `Reference: SpazaSure report ${report.id}`,
    `Submitted: ${format(new Date(report.createdAt), 'dd MMM yyyy HH:mm')}`,
    `Type: ${report.reportType ?? 'Not specified'}`,
    `Product: ${report.productName ?? 'Not matched to a registered product'}`,
    `Barcode: ${report.barcode ?? 'Not provided'}`,
    `Batch number: ${report.batchNumber ?? 'Not provided'}`,
    `Expiry date: ${report.expiryDate ?? 'Not provided'}`,
    `Purchase location: ${report.purchaseLocation ?? 'Not provided'}`,
    `Shop: ${report.shopName ?? 'Not provided'}`,
    `Supplier: ${report.supplierName ?? 'Not provided'}`,
    `Other reports for this product: ${report.relatedReports}`,
    `Photo evidence: ${resolveUploadUrl(report.photoUrl) ?? 'None attached'}`,
    '',
    'Reporter description:',
    report.description.length > 600 ? `${report.description.slice(0, 600)}...` : report.description,
  ];
  if (note.trim()) lines.push('', 'SpazaSure note:', note.trim());
  return {
    subject: `SpazaSure case referral: ${report.reportType ?? 'Product report'} (${report.id.slice(0, 8)})`,
    body: `Dear ${authority},\n\nSpazaSure is referring the following consumer report for your investigation.\n\n${lines.join('\n')}\n\nKind regards,\nSpazaSure Platform Admin`,
  };
}

export default function AdminReportsPage() {
  const [reports, setReports] = useState<AdminReport[]>([]);
  const [counts, setCounts] = useState<Record<string, number>>({});
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState('all');
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<AdminReport | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params: { page: number; pageSize: number; status?: string; search?: string } = { page: 1, pageSize: 100 };
      if (statusFilter !== 'all') params.status = statusFilter;
      if (search.trim()) params.search = search.trim();
      const res = await adminReportsApi.list(params);
      setReports(res.items);
      setCounts(Object.fromEntries(res.counts.map((c) => [c.status, c.count])));
    } catch {
      toast.error('Failed to load reports');
    } finally {
      setLoading(false);
    }
  }, [statusFilter, search]);

  useEffect(() => {
    const t = setTimeout(load, 250);
    return () => clearTimeout(t);
  }, [load]);

  const totalCount = Object.values(counts).reduce((a, b) => a + b, 0);

  const onUpdated = (updated: Partial<AdminReport> & { id: string }) => {
    setReports((prev) => prev.map((r) => (r.id === updated.id ? { ...r, ...updated } : r)));
    setSelected((prev) => (prev && prev.id === updated.id ? { ...prev, ...updated } : prev));
    useAdminBadgeStore.setState({ lastFetched: 0 });
    useAdminBadgeStore.getState().fetchBadges();
    load();
  };

  return (
    <div className="p-6 space-y-5 animate-in">
      <div>
        <h1 className="page-title">Reports</h1>
        <p className="page-subtitle">Review reported products and shops, respond to reporters, and escalate to the authorities</p>
      </div>

      <div className="flex items-center gap-0.5 border-b border-gray-200 overflow-x-auto">
        {statusTabs.map(({ value, label }) => {
          const count = value === 'all' ? totalCount : counts[value] ?? 0;
          return (
            <button key={value} onClick={() => setStatusFilter(value)}
              className={clsx('flex items-center gap-1.5 px-4 py-3 text-sm font-semibold border-b-2 transition-all whitespace-nowrap',
                statusFilter === value ? 'border-slate-800 text-slate-900' : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300')}>
              {label}
              {count > 0 && (
                <span className={clsx('text-[10px] font-black px-1.5 py-0.5 rounded-full min-w-[18px] text-center',
                  statusFilter === value ? 'bg-slate-800 text-white' : 'bg-gray-100 text-gray-600')}>{count}</span>
              )}
            </button>
          );
        })}
      </div>

      <div className="relative max-w-sm">
        <Search size={14} className="absolute left-3.5 top-3 text-gray-400" />
        <input value={search} onChange={(e) => setSearch(e.target.value)} className="input pl-10" placeholder="Search product, barcode, shop or description..." />
      </div>

      <div className="card overflow-hidden">
        {loading ? (
          <div className="p-16 text-center">
            <Loader2 size={36} className="text-gray-300 mx-auto mb-3 animate-spin" />
            <p className="font-bold text-gray-500">Loading reports...</p>
          </div>
        ) : reports.length === 0 ? (
          <div className="p-16 text-center">
            <Flag size={36} className="text-gray-200 mx-auto mb-3" />
            <p className="font-bold text-gray-600">No reports found</p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/60">
                <th className="table-header">Report</th>
                <th className="table-header">Product / Shop</th>
                <th className="table-header">Reporter</th>
                <th className="table-header text-center">Related</th>
                <th className="table-header text-center">Status</th>
                <th className="table-header">Date</th>
                <th className="table-header text-center">Actions</th>
              </tr>
            </thead>
            <tbody>
              {reports.map((r) => (
                <tr key={r.id} className="table-row">
                  <td className="table-cell">
                    <button onClick={() => setSelected(r)} className="font-bold text-blue-600 hover:underline text-left">{r.reportType ?? 'Report'}</button>
                    <p className="text-xs text-gray-400 max-w-xs truncate">{r.description}</p>
                  </td>
                  <td className="table-cell">
                    <p className="font-semibold text-gray-900">{r.productName ?? r.barcode ?? '—'}</p>
                    {r.shopName && <p className="text-xs text-gray-400">{r.shopName}</p>}
                  </td>
                  <td className="table-cell text-xs text-gray-500 font-semibold">{r.isAnonymous ? 'Anonymous' : r.reporterName ?? '—'}</td>
                  <td className="table-cell text-center">
                    {r.relatedReports > 0
                      ? <span className="text-xs font-black px-2 py-0.5 rounded-full bg-red-50 text-red-600">{r.relatedReports}</span>
                      : <span className="text-gray-300">—</span>}
                  </td>
                  <td className="table-cell text-center"><StatusPill status={r.status} /></td>
                  <td className="table-cell text-xs text-gray-400">{format(new Date(r.createdAt), 'dd MMM yyyy')}</td>
                  <td className="table-cell">
                    <div className="flex items-center justify-center">
                      <button onClick={() => setSelected(r)} className="p-2 rounded-lg text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all" title="Open">
                        <Eye size={14} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selected && <ReportDetailModal report={selected} onClose={() => setSelected(null)} onUpdated={onUpdated} />}
    </div>
  );
}

function ReportDetailModal({ report, onClose, onUpdated }: {
  report: AdminReport;
  onClose: () => void;
  onUpdated: (updated: Partial<AdminReport> & { id: string }) => void;
}) {
  const [message, setMessage] = useState(report.resolutionNote ?? '');
  const [authority, setAuthority] = useState(AUTHORITIES[0]);
  const [recipient, setRecipient] = useState('');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const photo = resolveUploadUrl(report.photoUrl);
  const closed = report.status === 'resolved' || report.status === 'dismissed';

  const respond = async (status: 'under_review' | 'resolved' | 'dismissed') => {
    if (status !== 'under_review' && !message.trim()) {
      toast.error('Write a response for the reporter first');
      return;
    }
    setBusy(true);
    try {
      await adminReportsApi.respond(report.id, { status, message: message.trim() || undefined });
      toast.success(status === 'under_review' ? 'Marked as under review' : 'Response sent to the reporter');
      onUpdated({ id: report.id, status, resolutionNote: message.trim() || report.resolutionNote });
    } catch {
      /* the API client already shows the error toast */
    } finally {
      setBusy(false);
    }
  };

  const escalate = async () => {
    setBusy(true);
    try {
      await adminReportsApi.escalate(report.id, { escalatedTo: authority, resolutionNote: note.trim() || undefined });
      toast.success(`Escalated to ${authority}`);
      onUpdated({ id: report.id, status: 'escalated', escalatedTo: authority, escalatedAt: new Date().toISOString() });
      if (recipient.trim()) {
        const { subject, body } = buildEvidenceEmail(report, authority, note);
        window.location.href = `mailto:${encodeURIComponent(recipient.trim())}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
      }
    } catch {
      /* the API client already shows the error toast */
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={report.reportType ?? 'Report'} onClose={onClose} size="lg">
      <div className="p-6 space-y-6">
        <div className="flex items-center justify-between">
          <StatusPill status={report.status} />
          <p className="text-xs text-gray-400">Submitted {format(new Date(report.createdAt), 'dd MMM yyyy HH:mm')}</p>
        </div>

        {report.relatedReports > 0 && (
          <div className="rounded-xl bg-red-50 border border-red-100 px-4 py-3 text-sm font-semibold text-red-700">
            {report.relatedReports} other report{report.relatedReports > 1 ? 's' : ''} exist for this product. Consider escalating.
          </div>
        )}

        <div className="grid grid-cols-2 gap-4">
          <Field label="Product" value={report.productName} />
          <Field label="Barcode" value={report.barcode} />
          <Field label="Batch number" value={report.batchNumber} />
          <Field label="Expiry date" value={report.expiryDate} />
          <Field label="Shop" value={report.shopName} />
          <Field label="Purchase location" value={report.purchaseLocation} />
          <Field label="Supplier" value={report.supplierName} />
          <Field label="Reporter" value={report.isAnonymous ? 'Anonymous' : report.reporterName} />
        </div>

        <div>
          <p className="text-[11px] font-bold uppercase tracking-wide text-gray-400 mb-1">Description</p>
          <p className="text-sm text-gray-800 whitespace-pre-wrap">{report.description}</p>
        </div>

        {photo && (
          <a href={photo} target="_blank" rel="noreferrer">
            <img src={photo} alt="Evidence" className="max-h-64 rounded-xl border border-gray-100 object-contain" />
          </a>
        )}

        {report.escalatedTo && (
          <div className="rounded-xl bg-violet-50 border border-violet-100 px-4 py-3 text-sm text-violet-800">
            Escalated to <b>{report.escalatedTo}</b>
            {report.escalatedAt && <> on {format(new Date(report.escalatedAt), 'dd MMM yyyy')}</>}
          </div>
        )}

        <div className="border-t border-gray-100 pt-5 space-y-3">
          <p className="text-sm font-bold text-gray-900">Respond to reporter</p>
          <textarea value={message} onChange={(e) => setMessage(e.target.value)} rows={3} className="input"
            placeholder="Tell the reporter what happened. They will see this in the app." />
          <div className="flex flex-wrap gap-2">
            {report.status === 'submitted' && (
              <button disabled={busy} onClick={() => respond('under_review')} className="btn-secondary flex items-center gap-1.5">
                <PlayCircle size={15} /> Start review
              </button>
            )}
            <button disabled={busy || closed} onClick={() => respond('resolved')} className="btn-primary flex items-center gap-1.5">
              <CheckCircle size={15} /> Resolve
            </button>
            <button disabled={busy || closed} onClick={() => respond('dismissed')} className="btn-secondary flex items-center gap-1.5">
              <XCircle size={15} /> Dismiss
            </button>
          </div>
        </div>

        <div className="border-t border-gray-100 pt-5 space-y-3">
          <p className="text-sm font-bold text-gray-900">Escalate to an authority</p>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Authority</label>
              <select value={authority} onChange={(e) => setAuthority(e.target.value)} className="select">
                {AUTHORITIES.map((a) => <option key={a}>{a}</option>)}
              </select>
            </div>
            <div>
              <label className="label">Recipient email (optional)</label>
              <input value={recipient} onChange={(e) => setRecipient(e.target.value)} className="input" placeholder="name@authority.gov.za" type="email" />
            </div>
          </div>
          <textarea value={note} onChange={(e) => setNote(e.target.value)} rows={2} className="input"
            placeholder="Internal note added to the referral (optional)" />
          <p className="text-xs text-gray-400">
            With a recipient email, your mail app opens with an evidence package to review and send. The reporter's identity is not included.
          </p>
          <button disabled={busy} onClick={escalate} className="btn-primary flex items-center gap-1.5">
            {busy ? <Loader2 size={15} className="animate-spin" /> : recipient.trim() ? <Mail size={15} /> : <Send size={15} />}
            {recipient.trim() ? 'Escalate and draft email' : 'Escalate'}
          </button>
        </div>
      </div>
    </Modal>
  );
}
