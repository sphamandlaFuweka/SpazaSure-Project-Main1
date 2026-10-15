import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { BadgeCheck, ShieldAlert, Search, Loader2, MapPin, CalendarCheck, Store } from 'lucide-react';
import { format } from 'date-fns';
import clsx from 'clsx';
import { publicShopsApi, type ShopVerificationResult } from '../../services/api';

/**
 * Public page anyone can open by scanning a shop's SpazaSure QR sticker or typing its shop
 * number. Verified shops get the green "SpazaSure Verified" sticker.
 */
export default function VerifyShopPage() {
  const { code } = useParams<{ code?: string }>();
  const navigate = useNavigate();
  const [input, setInput] = useState(code ?? '');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ShopVerificationResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!code) { setResult(null); setError(null); return; }
    setInput(code);
    let cancelled = false;
    setLoading(true);
    setError(null);
    publicShopsApi.verify(code)
      .then((r) => { if (!cancelled) setResult(r); })
      .catch((e) => {
        if (cancelled) return;
        setResult(null);
        setError(e?.response?.data?.message
          ?? (e?.response ? 'We could not check this shop right now. Please try again.' : 'Could not reach SpazaSure. Check your internet connection.'));
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [code]);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const value = input.trim();
    if (value) navigate(`/verify/${encodeURIComponent(value.toUpperCase())}`);
  };

  return (
    <div className="min-h-[100dvh] bg-[#F5F7FF] flex flex-col items-center px-4 py-8 sm:py-14">
      <a href="https://spazasure.co.za" className="flex items-center gap-3 mb-8">
        <img src="/spazasure_logo.png" alt="" className="w-11 h-11 object-contain" />
        <span>
          <span className="block text-lg font-black text-gray-900 leading-tight">SpazaSure</span>
          <span className="block text-[11px] font-bold uppercase tracking-[0.18em] text-brand-blue">Shop verification</span>
        </span>
      </a>

      <div className="w-full max-w-md space-y-5">
        {loading && (
          <div className="card p-10 flex flex-col items-center gap-3 text-ink-2">
            <Loader2 className="animate-spin text-brand-blue" size={28} />
            <p className="text-sm font-semibold">Checking shop {code}...</p>
          </div>
        )}

        {!loading && result && (
          <div className={clsx('card overflow-hidden', result.verified ? 'border-2 border-brand-green' : 'border-2 border-gray-200')}>
            <div className={clsx('px-6 pt-8 pb-6 flex flex-col items-center text-center',
              result.verified ? 'bg-brand-green-light' : 'bg-gray-50')}>
              {result.verified ? (
                // The green sticker
                <div className="w-36 h-36 rounded-full bg-brand-green text-white flex flex-col items-center justify-center shadow-xl shadow-brand-green/30 ring-8 ring-white">
                  <BadgeCheck size={46} strokeWidth={2.2} />
                  <span className="mt-1 text-[11px] font-black uppercase tracking-[0.2em]">SpazaSure</span>
                  <span className="text-lg font-black leading-none">VERIFIED</span>
                </div>
              ) : (
                <div className="w-28 h-28 rounded-full bg-white text-gray-400 flex items-center justify-center ring-8 ring-gray-100">
                  <ShieldAlert size={44} />
                </div>
              )}
              <h1 className="mt-5 text-2xl font-black text-gray-900">{result.shopName}</h1>
              <p className={clsx('mt-1 text-sm font-bold', result.verified ? 'text-brand-green' : 'text-ink-2')}>
                {result.verified ? 'SpazaSure Verified shop' : 'Not verified'}
              </p>
            </div>
            <div className="px-6 py-5 space-y-3 text-sm">
              <p className="text-ink-2">{result.message}</p>
              <div className="flex items-center gap-2.5 text-gray-900"><Store size={16} className="text-ink-3" /> Shop number <span className="font-mono font-bold">{result.shopNumber}</span></div>
              {result.area && <div className="flex items-center gap-2.5 text-gray-900"><MapPin size={16} className="text-ink-3" /> {result.area}</div>}
              {result.verified && result.validUntil && (
                <div className="flex items-center gap-2.5 text-gray-900"><CalendarCheck size={16} className="text-ink-3" /> Documents valid until {format(new Date(result.validUntil), 'd MMMM yyyy')}</div>
              )}
              <p className="text-xs text-ink-3 pt-1">Checked {format(new Date(result.checkedAt), 'd MMM yyyy, HH:mm')}</p>
            </div>
          </div>
        )}

        {!loading && error && (
          <div role="alert" className="card p-6 flex items-start gap-3 border-2 border-red-200">
            <ShieldAlert className="text-brand-red flex-shrink-0" size={22} />
            <div>
              <p className="font-bold text-gray-900">Shop not found</p>
              <p className="text-sm text-ink-2 mt-0.5">{error}</p>
            </div>
          </div>
        )}

        <form onSubmit={submit} className="card p-5 space-y-3">
          <label htmlFor="shop-number" className="label">{code ? 'Check another shop' : 'Enter the shop number on the sticker'}</label>
          <div className="flex gap-2">
            <input id="shop-number" className="input font-mono uppercase placeholder:normal-case flex-1" placeholder="e.g. SS482913"
              value={input} onChange={(e) => setInput(e.target.value)} maxLength={20} autoComplete="off" />
            <button type="submit" disabled={!input.trim()} className="btn-primary px-4" aria-label="Check shop">
              <Search size={16} />
            </button>
          </div>
        </form>

        <p className="text-xs text-ink-3 text-center leading-relaxed px-2">
          Only trust results on <strong>portal.spazasure.co.za</strong>. Make sure the shop name above matches the shop you are in.
          A verified shop has had its business permit, health certificate and owner ID checked by SpazaSure.
        </p>
      </div>
    </div>
  );
}
