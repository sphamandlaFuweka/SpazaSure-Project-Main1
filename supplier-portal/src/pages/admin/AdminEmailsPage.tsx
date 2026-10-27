import { useEffect, useState } from 'react';
import { MailCheck, Send, AlertTriangle, CheckCircle2, PauseCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { format } from 'date-fns';
import clsx from 'clsx';
import { adminEmailApi, apiErrorMessage, type EmailSequenceSummary } from '../../services/api';
import { Spinner } from '../../components/ui';

const steps: { key: string; label: string; when: string }[] = [
  { key: 'welcome', label: 'Welcome', when: 'Within 3 days of joining' },
  { key: 'education_1', label: 'Feature tip 1', when: 'Day 2' },
  { key: 'education_2', label: 'Feature tip 2', when: 'Day 5' },
  { key: 'education_3', label: 'Feature tip 3', when: 'Day 9' },
  { key: 'inactive_7', label: 'We miss you', when: 'After 7 days without using the app' },
  { key: 'inactive_21', label: 'Feedback request', when: 'After 21 days without using the app' },
];
const roles: { key: string; label: string }[] = [
  { key: 'customer', label: 'Shopper' },
  { key: 'spaza_owner', label: 'Spaza shop' },
  { key: 'supplier', label: 'Supplier' },
];

/**
 * The automated emails: whether they are switched on, what went out, and a preview of each one.
 * Switching them on is a server setting (PROD_EMAIL_SEQUENCES_ENABLED), not a button here.
 */
export default function AdminEmailsPage() {
  const [summary, setSummary] = useState<EmailSequenceSummary | null>(null);
  const [step, setStep] = useState('welcome');
  const [role, setRole] = useState('customer');
  const [preview, setPreview] = useState<{ subject: string; html: string } | null>(null);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    adminEmailApi.summary()
      .then(setSummary)
      .catch((err) => toast.error(apiErrorMessage(err, 'Failed to load email status')));
  }, []);

  useEffect(() => {
    setPreview(null);
    adminEmailApi.preview(step, role)
      .then(setPreview)
      .catch((err) => toast.error(apiErrorMessage(err, 'Could not load the preview')));
  }, [step, role]);

  const sendTest = async () => {
    setSending(true);
    try {
      const res = await adminEmailApi.sendTest(step, role);
      toast.success(res.message ?? 'Test email sent');
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not send the test email'));
    } finally {
      setSending(false);
    }
  };

  const stat = (key: string) => summary?.byEmail.find((b) => b.step === key);

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 bg-gradient-to-br from-sky-500 to-blue-600 rounded-xl flex items-center justify-center shadow-lg shadow-sky-200">
          <MailCheck size={18} className="text-white" />
        </div>
        <div>
          <h1 className="text-xl font-black text-gray-900 tracking-tight">Automated Emails</h1>
          <p className="text-sm text-gray-400 font-medium">Welcome, feature tips and reminders for testers</p>
        </div>
      </div>

      {!summary ? (
        <div className="flex justify-center py-10"><Spinner /></div>
      ) : (
        <>
          <StatusBanner s={summary} />

          <div className="card overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-gray-500 border-b border-gray-100">
                  <th className="p-3">Email</th>
                  <th className="p-3">When</th>
                  <th className="p-3 text-right">Sent (30 days)</th>
                  <th className="p-3 text-right">Failed</th>
                </tr>
              </thead>
              <tbody>
                {steps.map((st) => (
                  <tr key={st.key} className="border-b border-gray-50 last:border-0">
                    <td className="p-3 font-semibold text-gray-900">{st.label}</td>
                    <td className="p-3 text-gray-500">{st.when}</td>
                    <td className="p-3 text-right font-bold">{stat(st.key)?.sent ?? 0}</td>
                    <td className={clsx('p-3 text-right font-bold', (stat(st.key)?.failed ?? 0) > 0 ? 'text-red-600' : 'text-gray-400')}>
                      {stat(st.key)?.failed ?? 0}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p className="px-3 py-2 text-xs text-gray-400 border-t border-gray-50">
              {summary.unsubscribed} people have unsubscribed. Waitlist confirmations are sent separately and are always on.
            </p>
          </div>
        </>
      )}

      <div className="card p-4 space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="font-bold text-gray-900 mr-auto">Preview</h2>
          <select className="select w-auto" value={step} onChange={(e) => setStep(e.target.value)}>
            {steps.map((st) => <option key={st.key} value={st.key}>{st.label}</option>)}
          </select>
          <select className="select w-auto" value={role} onChange={(e) => setRole(e.target.value)}>
            {roles.map((r) => <option key={r.key} value={r.key}>{r.label}</option>)}
          </select>
          <button className="btn-primary flex items-center gap-2" disabled={sending || !summary?.emailConfigured} onClick={sendTest}
            title={summary?.emailConfigured ? 'Sends this email to your own address' : 'Email is not set up on the server yet'}>
            <Send size={14} /> Send a test to me
          </button>
        </div>
        {preview ? (
          <>
            <p className="text-sm"><span className="text-gray-500">Subject:</span> <span className="font-semibold">{preview.subject}</span></p>
            {/* Sandboxed: the email's links are shown, not followed, from inside the console */}
            <iframe title="Email preview" srcDoc={preview.html} sandbox="" className="w-full h-[640px] rounded-xl border border-gray-100 bg-gray-50" />
          </>
        ) : (
          <div className="flex justify-center py-10"><Spinner /></div>
        )}
      </div>

      {summary && summary.latest.length > 0 && (
        <div className="card overflow-x-auto">
          <h2 className="font-bold text-gray-900 p-3 pb-0">Latest emails</h2>
          <table className="w-full text-sm">
            <tbody>
              {summary.latest.map((l, i) => (
                <tr key={i} className="border-b border-gray-50 last:border-0">
                  <td className="p-3 text-gray-500 whitespace-nowrap">{format(new Date(l.sentAt), 'd MMM, HH:mm')}</td>
                  <td className="p-3 break-all">{l.email}</td>
                  <td className="p-3">{l.subject}</td>
                  <td className="p-3 whitespace-nowrap">
                    <span className={clsx('text-xs font-bold', l.status === 'sent' ? 'text-emerald-600' : 'text-red-600')} title={l.error ?? undefined}>
                      {l.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function StatusBanner({ s }: { s: EmailSequenceSummary }) {
  if (!s.emailConfigured)
    return (
      <Banner tone="red" icon={<AlertTriangle size={18} />} title="Email isn't set up on the server">
        Add the SMTP settings (PROD_SMTP_HOST, PROD_SMTP_USERNAME, PROD_SMTP_PASSWORD and PROD_EMAIL_FROM) as GitHub secrets
        and redeploy. For Brevo: host smtp-relay.brevo.com, port 587, and an SMTP key as the password.
      </Banner>
    );
  if (!s.enabled)
    return (
      <Banner tone="amber" icon={<PauseCircle size={18} />} title="Sequences are switched off">
        Email works (waitlist confirmations go out), but welcome, tips and reminders don't. Set the
        PROD_EMAIL_SEQUENCES_ENABLED secret to true and redeploy. Tip: set PROD_EMAIL_SEQUENCES_ONLY_TO to your
        testers' addresses first.
      </Banner>
    );
  return (
    <Banner tone="green" icon={<CheckCircle2 size={18} />} title={s.dryRun ? 'On, in dry-run mode' : 'Sending'}>
      {s.dryRun && 'Nothing is sent: the server only logs who would get which email. '}
      {s.onlyTo ? `Only to: ${s.onlyTo}. ` : 'To everyone with an email address. '}
      {s.sentToday} of {s.dailyLimit} sent today · sent between {s.sendingHours} (South African time).
    </Banner>
  );
}

function Banner({ tone, icon, title, children }: { tone: 'red' | 'amber' | 'green'; icon: React.ReactNode; title: string; children: React.ReactNode }) {
  const styles = {
    red: 'border-red-200 bg-red-50 text-red-800',
    amber: 'border-amber-200 bg-amber-50 text-amber-800',
    green: 'border-emerald-200 bg-emerald-50 text-emerald-800',
  }[tone];
  return (
    <div className={clsx('rounded-2xl border p-4 flex gap-3', styles)}>
      <div className="mt-0.5">{icon}</div>
      <div>
        <p className="font-bold text-sm">{title}</p>
        <p className="text-xs mt-1 leading-relaxed">{children}</p>
      </div>
    </div>
  );
}
