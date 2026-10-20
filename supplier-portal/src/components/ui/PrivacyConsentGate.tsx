import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldCheck } from 'lucide-react';
import { privacyApi } from '../../services/api';
import { useAuthStore } from '../../store/authStore';

/**
 * Asks suppliers who haven't accepted the current privacy notice (they registered before it
 * existed, or it has changed) to accept it or sign out. The server records the consent.
 */
export default function PrivacyConsentGate() {
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const { logout } = useAuthStore();
  const navigate = useNavigate();

  useEffect(() => {
    // If the check fails (offline), don't block the portal; it asks again next time.
    privacyApi.consent().then((c) => setOpen(!c.accepted)).catch(() => setOpen(false));
  }, []);

  if (!open) return null;

  const agree = async () => {
    setSaving(true);
    try {
      await privacyApi.acceptConsent();
    } finally {
      setSaving(false);
      setOpen(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/50 p-4" role="dialog" aria-modal="true">
      <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
        <div className="flex items-center gap-2 mb-2">
          <ShieldCheck className="text-primary" size={22} />
          <h2 className="font-bold text-gray-900">Our privacy notice</h2>
        </div>
        <p className="text-sm text-gray-600">
          SpazaSure has a privacy notice explaining how we handle your and your company's information, including business
          checks by our partner Didit, which take place outside South Africa. Please read it and agree to keep using the portal.
        </p>
        <a href="/privacy" target="_blank" rel="noreferrer" className="inline-block mt-2 text-sm font-semibold text-primary underline">
          Read the privacy notice
        </a>
        <div className="flex justify-end gap-2 mt-5">
          <button type="button" onClick={() => { logout(); navigate('/login'); }} className="text-sm font-semibold px-4 py-2 rounded-xl border border-gray-200">
            Sign out
          </button>
          <button type="button" onClick={agree} disabled={saving} className="btn-primary text-sm">I agree</button>
        </div>
      </div>
    </div>
  );
}
