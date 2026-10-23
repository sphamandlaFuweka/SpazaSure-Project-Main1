import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Loader2, Trash2, CheckCircle2 } from 'lucide-react';
import { env } from '../../config/env';

type Role = 'spaza_owner' | 'customer' | 'supplier';

const roles: { id: Role; label: string; login: string; privacy: string }[] = [
  { id: 'spaza_owner', label: 'Spaza shop owner', login: '/shop/auth/login', privacy: '/shop/profile/privacy' },
  { id: 'customer', label: 'Customer', login: '/customer/auth/login', privacy: '/customer/profile/privacy' },
  { id: 'supplier', label: 'Supplier', login: '/supplier/auth/login', privacy: '/supplier/profile/privacy' },
];

/**
 * Public page for deleting a SpazaSure account without the app, as Google Play requires.
 * The person signs in to prove the account is theirs; the request then joins the same queue as
 * "Delete my account" in the apps, and an admin completes it within 30 days.
 * Plain fetch, not the portal's api client: this must not touch a signed-in supplier's session.
 */
export default function DeleteAccountPage() {
  const [role, setRole] = useState<Role>('spaza_owner');
  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);

  const chosen = roles.find((r) => r.id === role)!;
  const usesEmail = role === 'supplier' && login.includes('@');

  const call = async (path: string, body: unknown, token?: string) => {
    const res = await fetch(`${env.apiUrl}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: JSON.stringify(body),
    });
    const json = await res.json().catch(() => ({}));
    return { ok: res.ok, status: res.status, json };
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!login.trim() || !password) { setError('Enter your phone number (or supplier email) and password.'); return; }
    setBusy(true);
    try {
      const signIn = await call(chosen.login, usesEmail
        ? { email: login.trim(), password }
        : { phone: login.trim(), password });
      const token = signIn.json?.data?.accessToken as string | undefined;
      if (!signIn.ok || !token) {
        setError(signIn.json?.message ?? 'Those details are not right. Check the account type, phone number and password.');
        return;
      }
      const request = await call(`${chosen.privacy}/deletion-request`, { reason: reason.trim() || null }, token);
      if (request.ok) {
        setDone(request.json?.message ?? 'We received your request. Your account will be deleted within 30 days.');
      } else if (request.status === 409) {
        setDone(request.json?.message ?? 'You already asked for your account to be deleted. We will let you know when it is done.');
      } else {
        setError(request.json?.message ?? 'We could not send your request. Please try again.');
      }
    } catch {
      setError('We could not reach SpazaSure. Check your connection and try again.');
    } finally {
      setPassword('');
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50 px-4 py-10">
      <article className="mx-auto max-w-xl rounded-3xl bg-white p-6 sm:p-10 shadow-sm border border-gray-100">
        <div className="flex items-center gap-3 mb-2">
          <Trash2 className="text-red-500" size={26} />
          <h1 className="text-2xl font-black text-gray-900">Delete your SpazaSure account</h1>
        </div>
        <p className="text-sm text-gray-600">
          You can also do this in the SpazaSure app under <b>Profile → Delete my account</b>.
        </p>

        <section className="mt-6 rounded-2xl bg-gray-50 p-4 text-sm text-gray-700 space-y-2">
          <p className="font-semibold text-gray-900">What happens</p>
          <ul className="list-disc pl-5 space-y-1">
            <li>We delete your account within 30 days and let you know when it is done.</li>
            <li>We erase your name, contact details, ID number, addresses, documents and photos.</li>
            <li>Order, payment and wallet records are kept for 5 years <b>without your personal details</b>, because tax law requires it.</li>
            <li>Unpaid orders or money left in your wallet must be settled first.</li>
          </ul>
          <p>
            Read the <Link to="/privacy" className="font-semibold text-primary hover:underline">privacy notice</Link> for details.
          </p>
        </section>

        {done ? (
          <div className="mt-6 flex items-start gap-3 rounded-2xl bg-emerald-50 border border-emerald-200 p-4 text-sm text-emerald-800">
            <CheckCircle2 className="shrink-0 mt-0.5" size={18} />
            <p>{done}</p>
          </div>
        ) : (
          <form onSubmit={submit} className="mt-6 space-y-4">
            <p className="text-sm text-gray-600">Sign in to confirm the account is yours.</p>
            <div>
              <label className="label">Account type</label>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                {roles.map((r) => (
                  <button type="button" key={r.id} onClick={() => setRole(r.id)}
                    className={`rounded-xl border px-3 py-2 text-sm font-semibold ${role === r.id ? 'border-primary bg-primary text-white' : 'border-gray-200 text-gray-700 hover:border-primary'}`}>
                    {r.label}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <label className="label" htmlFor="login">{role === 'supplier' ? 'Phone number or email' : 'Phone number'}</label>
              <input id="login" className="input" value={login} onChange={(e) => setLogin(e.target.value)}
                autoComplete="username" placeholder={role === 'supplier' ? '082 123 4567 or you@company.co.za' : '082 123 4567'} />
            </div>
            <div>
              <label className="label" htmlFor="password">Password</label>
              <input id="password" type="password" className="input" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
            </div>
            <div>
              <label className="label" htmlFor="reason">Why are you leaving? (optional)</label>
              <textarea id="reason" className="input" rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
            </div>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button type="submit" disabled={busy} className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-red-600 px-4 py-3 text-sm font-bold text-white hover:bg-red-700 disabled:opacity-60">
              {busy && <Loader2 size={16} className="animate-spin" />} Request account deletion
            </button>
            <p className="text-xs text-gray-500">
              Forgot your password? Reset it in the app first, or contact SpazaSure support and we will help you.
            </p>
          </form>
        )}

        <Link to="/login" className="mt-8 inline-block text-sm font-semibold text-primary hover:underline">Back to SpazaSure</Link>
      </article>
    </div>
  );
}
