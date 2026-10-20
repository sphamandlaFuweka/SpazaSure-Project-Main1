import { useEffect, useState } from 'react';
import { UserX, CheckCircle, XCircle, Clock, AlertTriangle, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { format } from 'date-fns';
import clsx from 'clsx';
import { adminDeletionApi, apiErrorMessage, type AdminDeletionRequest } from '../../services/api';
import { Spinner } from '../../components/ui';

const roleLabels: Record<string, string> = { spaza_owner: 'Shop owner', supplier: 'Supplier', customer: 'Customer' };

const statusStyles: Record<string, string> = {
  pending:   'bg-amber-50 text-amber-700 border-amber-200',
  approved:  'bg-emerald-50 text-emerald-700 border-emerald-200',
  rejected:  'bg-red-50 text-red-700 border-red-200',
  cancelled: 'bg-gray-100 text-gray-500 border-gray-200',
};

/**
 * Users' requests to delete their account (POPIA s24). Approving erases their personal details and
 * documents for good; order and payment records stay, without those details. Master admins only.
 */
export default function AdminDeletionRequestsPage() {
  const [requests, setRequests] = useState<AdminDeletionRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState<AdminDeletionRequest | null>(null);
  const [note, setNote] = useState('');

  const load = () =>
    adminDeletionApi.list()
      .then(setRequests)
      .catch((err) => toast.error(apiErrorMessage(err, 'Failed to load deletion requests')))
      .finally(() => setLoading(false));

  useEffect(() => { load(); }, []);

  const approve = async (r: AdminDeletionRequest) => {
    const who = r.name || r.email || r.phone || 'this account';
    if (!window.confirm(`Delete ${who} permanently? Their personal details and documents will be erased. This can't be undone.`)) return;
    setBusyId(r.id);
    try {
      await adminDeletionApi.approve(r.id);
      toast.success('Account deleted and personal information erased');
      await load();
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not delete the account'));
    } finally {
      setBusyId(null);
    }
  };

  const reject = async () => {
    if (!rejecting || !note.trim()) return;
    setBusyId(rejecting.id);
    try {
      await adminDeletionApi.reject(rejecting.id, note.trim());
      toast.success('Request rejected; the user was told why');
      setRejecting(null);
      setNote('');
      await load();
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not reject the request'));
    } finally {
      setBusyId(null);
    }
  };

  const pending = requests.filter((r) => r.status === 'pending').length;

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 bg-gradient-to-br from-rose-500 to-red-600 rounded-xl flex items-center justify-center shadow-lg shadow-rose-200">
          <UserX size={18} className="text-white" />
        </div>
        <div>
          <h1 className="text-xl font-black text-gray-900 tracking-tight">Deletion Requests</h1>
          <p className="text-sm text-gray-400 font-medium">
            {pending} waiting · Users asking for their account and personal information to be deleted (POPIA)
          </p>
        </div>
      </div>

      <div className="rounded-2xl border border-sky-100 bg-sky-50 p-4 text-xs text-sky-800">
        Respond within 30 days. Approving erases the person's name, ID number, contact details, address and documents
        for good. Orders, payments and wallet history stay for 5 years without those details, as tax law requires.
        A request can't be approved while money, orders or a renewing subscription are still open; the reason is shown on it.
      </div>

      {loading ? (
        <div className="flex justify-center py-16"><Spinner /></div>
      ) : requests.length === 0 ? (
        <div className="card p-10 text-center text-sm text-gray-500">No deletion requests yet.</div>
      ) : (
        <div className="space-y-3">
          {requests.map((r) => (
            <div key={r.id} className="card p-4 sm:p-5">
              <div className="flex flex-col sm:flex-row sm:items-start gap-3">
                <div className="flex-1 min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-bold text-gray-900">{r.name || 'Unknown'}</p>
                    <span className="text-[11px] font-bold text-gray-500 bg-gray-100 px-2 py-0.5 rounded-lg">{roleLabels[r.role] ?? r.role}</span>
                    <span className={clsx('text-[11px] font-bold border px-2 py-0.5 rounded-lg capitalize', statusStyles[r.status])}>{r.status}</span>
                  </div>
                  <p className="text-xs text-gray-500 mt-1">
                    {[r.email, r.phone].filter(Boolean).join(' · ')} · requested {format(new Date(r.createdAt), 'd MMM yyyy')}
                    {r.decidedAt && ` · decided ${format(new Date(r.decidedAt), 'd MMM yyyy')}`}
                  </p>
                  {r.reason && <p className="text-sm text-gray-700 mt-2">“{r.reason}”</p>}
                  {r.decisionNote && <p className="text-xs text-red-600 mt-2">Rejected: {r.decisionNote}</p>}
                  {r.blocker && (
                    <p className="text-xs font-semibold text-amber-700 mt-2 flex items-start gap-1.5">
                      <AlertTriangle size={13} className="mt-0.5 flex-shrink-0" /> {r.blocker}
                    </p>
                  )}
                </div>
                {r.status === 'pending' && (
                  <div className="flex gap-2 flex-shrink-0">
                    <button
                      onClick={() => approve(r)}
                      disabled={busyId === r.id || !!r.blocker}
                      title={r.blocker ?? 'Delete the account and erase the personal information'}
                      className="flex items-center gap-1.5 text-sm font-bold bg-red-600 text-white px-4 py-2 rounded-xl hover:bg-red-700 disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                      {busyId === r.id ? <Clock size={14} className="animate-spin" /> : <CheckCircle size={14} />} Delete account
                    </button>
                    <button
                      onClick={() => { setRejecting(r); setNote(''); }}
                      disabled={busyId === r.id}
                      className="flex items-center gap-1.5 text-sm font-semibold border border-gray-200 text-gray-700 px-4 py-2 rounded-xl hover:bg-gray-50"
                    >
                      <XCircle size={14} /> Reject
                    </button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {rejecting && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-white p-5 shadow-xl">
            <div className="flex items-center justify-between mb-3">
              <h2 className="font-bold text-gray-900">Reject deletion request</h2>
              <button onClick={() => setRejecting(null)} className="text-gray-400 hover:text-gray-600"><X size={18} /></button>
            </div>
            <p className="text-xs text-gray-500 mb-2">Tell {rejecting.name || 'the user'} why their account can't be deleted yet. They'll see this.</p>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              rows={3}
              className="input w-full"
              placeholder="e.g. Your order SS-1042 is still being delivered. Ask again once it has arrived."
            />
            <div className="flex justify-end gap-2 mt-4">
              <button onClick={() => setRejecting(null)} className="text-sm font-semibold px-4 py-2 rounded-xl border border-gray-200">Cancel</button>
              <button onClick={reject} disabled={!note.trim() || busyId === rejecting.id} className="btn-primary text-sm disabled:opacity-50">Reject request</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
