import { useCallback, useEffect, useState } from 'react';
import { MessageSquareHeart, Star, Image as ImageIcon } from 'lucide-react';
import toast from 'react-hot-toast';
import { format } from 'date-fns';
import clsx from 'clsx';
import {
  adminFeedbackApi, apiErrorMessage, resolveUploadUrl,
  type AdminFeedbackItem, type AdminFeedbackPage, type FeedbackStatus,
} from '../../services/api';
import { Spinner } from '../../components/ui';

const typeLabels: Record<string, string> = { bug: 'Bug', idea: 'Idea', question: 'Question', praise: 'Praise' };
const typeStyles: Record<string, string> = {
  bug: 'bg-red-50 text-red-700 border-red-200',
  idea: 'bg-indigo-50 text-indigo-700 border-indigo-200',
  question: 'bg-amber-50 text-amber-700 border-amber-200',
  praise: 'bg-emerald-50 text-emerald-700 border-emerald-200',
};
const roleLabels: Record<string, string> = { spaza_owner: 'Shop owner', supplier: 'Supplier', customer: 'Customer' };
const statusLabels: Record<FeedbackStatus, string> = {
  new: 'New', planned: 'Planned', in_progress: 'In progress', done: 'Done', wont_fix: "Won't fix",
};
const statuses = Object.keys(statusLabels) as FeedbackStatus[];

/** What testers say about SpazaSure, sent from the app and the supplier portal. */
export default function AdminFeedbackPage() {
  const [data, setData] = useState<AdminFeedbackPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState<string>('new');
  const [type, setType] = useState('');
  const [role, setRole] = useState('');
  const [page, setPage] = useState(1);

  const load = useCallback(() => {
    setLoading(true);
    adminFeedbackApi.list({ status: status || undefined, type: type || undefined, role: role || undefined, page })
      .then(setData)
      .catch((err) => toast.error(apiErrorMessage(err, 'Failed to load feedback')))
      .finally(() => setLoading(false));
  }, [status, type, role, page]);

  useEffect(() => { load(); }, [load]);

  const count = (s: FeedbackStatus) => data?.counts.find((c) => c.status === s)?.count ?? 0;
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 bg-gradient-to-br from-indigo-500 to-violet-600 rounded-xl flex items-center justify-center shadow-lg shadow-indigo-200">
          <MessageSquareHeart size={18} className="text-white" />
        </div>
        <div>
          <h1 className="text-xl font-black text-gray-900 tracking-tight">Tester Feedback</h1>
          <p className="text-sm text-gray-400 font-medium">
            Bugs, ideas, questions and praise sent from the app and the supplier portal
          </p>
        </div>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        {statuses.map((s) => (
          <button
            key={s}
            onClick={() => { setStatus(status === s ? '' : s); setPage(1); }}
            className={clsx('card p-3 text-left transition', status === s && 'ring-2 ring-indigo-400')}
          >
            <p className="text-xs text-gray-500 font-semibold">{statusLabels[s]}</p>
            <p className="text-xl font-black text-gray-900">{count(s)}</p>
          </button>
        ))}
        <div className="card p-3">
          <p className="text-xs text-gray-500 font-semibold">Average rating</p>
          <p className="text-xl font-black text-gray-900 flex items-center gap-1">
            {data?.averageRating ?? '–'}
            <Star size={16} className="text-amber-400 fill-amber-400" />
          </p>
          <p className="text-[11px] text-gray-400">{data?.ratingCount ?? 0} ratings</p>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        <select className="select w-auto" value={type} onChange={(e) => { setType(e.target.value); setPage(1); }}>
          <option value="">All types</option>
          {Object.entries(typeLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        <select className="select w-auto" value={role} onChange={(e) => { setRole(e.target.value); setPage(1); }}>
          <option value="">Everyone</option>
          {Object.entries(roleLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        {status && (
          <button className="text-sm font-semibold text-indigo-600" onClick={() => setStatus('')}>
            Show all statuses
          </button>
        )}
      </div>

      {loading && !data ? (
        <div className="flex justify-center py-16"><Spinner /></div>
      ) : !data || data.items.length === 0 ? (
        <div className="card p-10 text-center text-sm text-gray-500">No feedback here yet.</div>
      ) : (
        <div className="space-y-3">
          {data.items.map((item) => <FeedbackCard key={item.id} item={item} onSaved={load} />)}
          {pages > 1 && (
            <div className="flex items-center justify-center gap-3 pt-2">
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

function FeedbackCard({ item, onSaved }: { item: AdminFeedbackItem; onSaved: () => void }) {
  const [note, setNote] = useState(item.adminNote ?? '');
  const [saving, setSaving] = useState(false);

  const save = async (body: { status?: FeedbackStatus; adminNote?: string }) => {
    setSaving(true);
    try {
      await adminFeedbackApi.update(item.id, body);
      toast.success('Saved');
      onSaved();
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not save'));
    } finally {
      setSaving(false);
    }
  };

  const who = item.senderName || item.senderEmail || item.senderPhone || 'Unknown';
  const shot = item.screenshotUrl ? resolveUploadUrl(item.screenshotUrl) : undefined;

  return (
    <div className="card p-4 sm:p-5">
      <div className="flex flex-col lg:flex-row gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className={clsx('text-[11px] font-bold border px-2 py-0.5 rounded-lg', typeStyles[item.type])}>
              {typeLabels[item.type] ?? item.type}
            </span>
            <p className="font-bold text-gray-900">{who}</p>
            <span className="text-[11px] font-bold text-gray-500 bg-gray-100 px-2 py-0.5 rounded-lg">
              {roleLabels[item.role] ?? item.role}
            </span>
            {item.rating && (
              <span className="text-xs font-bold text-amber-600 flex items-center gap-0.5">
                {item.rating}<Star size={12} className="fill-amber-400 text-amber-400" />
              </span>
            )}
          </div>
          <p className="text-xs text-gray-500 mt-1">
            {format(new Date(item.createdAt), 'd MMM yyyy, HH:mm')}
            {item.screen && ` · ${item.screen}`}
            {` · ${item.source === 'supplier_portal' ? 'Supplier portal' : 'App'}`}
            {item.appVersion && ` ${item.appVersion}`}
            {item.platform && ` (${item.platform})`}
          </p>
          <p className="text-sm text-gray-800 mt-3 whitespace-pre-wrap">{item.message}</p>
          {shot && (
            <a href={shot} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1.5 mt-3 text-xs font-semibold text-indigo-600">
              <ImageIcon size={14} /> View screenshot (link works for 30 minutes)
            </a>
          )}
        </div>
        <div className="lg:w-72 space-y-2">
          <select
            className="select"
            value={item.status}
            disabled={saving}
            onChange={(e) => save({ status: e.target.value as FeedbackStatus })}
          >
            {statuses.map((s) => <option key={s} value={s}>{statusLabels[s]}</option>)}
          </select>
          <textarea
            className="input min-h-[72px]"
            placeholder="Team note (only the team sees this)"
            value={note}
            maxLength={1000}
            onChange={(e) => setNote(e.target.value)}
          />
          {note !== (item.adminNote ?? '') && (
            <button className="btn-primary w-full" disabled={saving} onClick={() => save({ adminNote: note })}>
              Save note
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
