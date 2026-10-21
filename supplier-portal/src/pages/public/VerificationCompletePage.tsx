import { CheckCircle } from 'lucide-react';

/**
 * Where Didit sends a shop owner after the identity check. The SpazaSure app catches this address
 * and closes Didit itself; this page is only seen if the check was finished in a normal browser.
 */
export default function VerificationCompletePage() {
  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
      <div className="max-w-sm w-full rounded-3xl bg-white p-8 text-center shadow-sm border border-gray-100">
        <CheckCircle className="mx-auto text-emerald-500" size={48} />
        <h1 className="mt-4 text-xl font-black text-gray-900">Verification finished</h1>
        <p className="mt-2 text-sm text-gray-600">
          Thank you. You can close this page and go back to the SpazaSure app. Your result appears on the
          Compliance screen shortly.
        </p>
      </div>
    </div>
  );
}
