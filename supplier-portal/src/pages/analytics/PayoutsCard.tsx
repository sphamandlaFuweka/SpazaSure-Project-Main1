import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Wallet, AlertTriangle, ArrowRight } from 'lucide-react';
import { format } from 'date-fns';
import clsx from 'clsx';
import { payoutsApi, type SupplierPayoutSummary, type PayoutStage } from '../../services/api';
import { formatCurrency, shortOrderNumber } from '../../utils';

const STAGE_LABEL: Record<PayoutStage, { label: string; className: string }> = {
  awaiting_delivery: { label: 'Awaiting delivery', className: 'bg-gray-100 text-ink-2' },
  pending:           { label: 'Pending',           className: 'bg-brand-yellow-light text-accent-700' },
  available:         { label: 'Ready to pay out',  className: 'bg-brand-green-light text-brand-green' },
  processing:        { label: 'Being paid',        className: 'bg-sky-50 text-sky-700' },
  failed:            { label: 'Payout failed',     className: 'bg-brand-red-light text-brand-red' },
  paid_out:          { label: 'Paid out',          className: 'bg-brand-blue-light text-brand-blue' },
};

/** The supplier's money held by SpazaSure for orders paid through Paystack. */
export default function PayoutsCard() {
  const navigate = useNavigate();
  const [data, setData] = useState<SupplierPayoutSummary | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    payoutsApi.supplierSummary().then(setData).catch(() => setFailed(true));
  }, []);

  if (failed) return null; // payouts are supplementary; the rest of Analytics still works
  if (!data) return <div className="card p-5 h-40 skeleton" aria-hidden="true" />;

  const tiles = [
    { label: 'Awaiting delivery', value: data.awaitingDelivery, hint: 'Paid, not delivered by SpazaSure yet' },
    {
      label: 'Pending', value: data.pending,
      hint: data.nextAvailableAt ? `Next release ${format(new Date(data.nextAvailableAt), 'd MMM')}` : `${data.payoutDelayDays}-day wait after delivery`,
    },
    { label: 'Ready to pay out', value: data.available + data.processing, hint: 'Paid to your bank automatically', strong: true },
    { label: 'Paid out', value: data.paidOut, hint: 'Already sent to you' },
  ];

  return (
    <div className="card p-5">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-2xl bg-brand-green text-white flex items-center justify-center"><Wallet size={18} /></div>
          <div>
            <h3 className="section-title">Your payouts</h3>
            <p className="text-xs text-ink-3">
              SpazaSure collects each shop's payment, delivers the order, and pays you for the goods {data.payoutDelayDays} day{data.payoutDelayDays === 1 ? '' : 's'} after delivery.
            </p>
          </div>
        </div>
      </div>

      {data.failed > 0 && (
        <button
          onClick={() => navigate('/profile')}
          className="w-full mb-4 flex items-center gap-3 rounded-2xl border border-red-200 bg-brand-red-light p-3 text-left text-sm text-red-900 hover:bg-red-100 transition-colors"
        >
          <AlertTriangle size={16} className="text-brand-red flex-shrink-0" />
          <span className="flex-1">
            A payout of {formatCurrency(data.failed)} couldn't be sent
            {data.recent.find((r) => r.failureReason)?.failureReason ? `: ${data.recent.find((r) => r.failureReason)!.failureReason}` : ''}.
            Check your banking details; we'll try again once they're updated.
          </span>
          <ArrowRight size={14} />
        </button>
      )}

      {!data.hasBankDetails && (
        <button
          onClick={() => navigate('/profile')}
          className="w-full mb-4 flex items-center gap-3 rounded-2xl border border-amber-200 bg-brand-yellow-light p-3 text-left text-sm text-amber-900 hover:bg-amber-100 transition-colors"
        >
          <AlertTriangle size={16} className="text-amber-600 flex-shrink-0" />
          <span className="flex-1">Add your bank details so SpazaSure can pay you.</span>
          <ArrowRight size={14} />
        </button>
      )}

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {tiles.map((t) => (
          <div key={t.label} className={clsx('rounded-2xl p-4 border', t.strong ? 'bg-brand-green-light border-emerald-100' : 'bg-surface border-brand-blue-light')}>
            <p className={clsx('text-[11px] font-extrabold uppercase tracking-wider', t.strong ? 'text-brand-green' : 'text-ink-3')}>{t.label}</p>
            <p className="text-xl font-black text-gray-900 mt-1 tabular-nums">{formatCurrency(t.value)}</p>
            <p className="text-[11px] text-ink-3 mt-1">{t.hint}</p>
          </div>
        ))}
      </div>

      {data.recent.length > 0 && (
        <div className="mt-4 overflow-x-auto">
          <table className="w-full text-sm min-w-[520px]">
            <thead>
              <tr>
                <th className="table-header">Order</th>
                <th className="table-header text-right">Amount</th>
                <th className="table-header">Status</th>
                <th className="table-header">When</th>
              </tr>
            </thead>
            <tbody>
              {data.recent.slice(0, 6).map((r) => {
                const stage = STAGE_LABEL[r.stage];
                const when = r.stage === 'paid_out' && r.paidOutAt
                  ? `Paid ${format(new Date(r.paidOutAt), 'd MMM')}${r.payoutReference ? ` · ${r.payoutReference}` : ''}`
                  : r.stage === 'pending' && r.availableAt
                    ? `Available ${format(new Date(r.availableAt), 'd MMM')}`
                    : r.stage === 'available' ? 'Waiting for payout'
                    : r.stage === 'processing' ? 'On its way to your bank'
                    : r.stage === 'failed' ? (r.failureReason ?? 'Payout failed') : '—';
                return (
                  <tr key={r.id} className="table-row cursor-pointer" onClick={() => navigate(`/orders?order=${r.id}`)}>
                    <td className="table-cell font-bold text-brand-blue whitespace-nowrap" title={r.orderNumber}>{shortOrderNumber(r.orderNumber)}</td>
                    <td className="table-cell text-right font-bold">{formatCurrency(r.amount)}</td>
                    <td className="table-cell"><span className={clsx('badge', stage.className)}>{stage.label}</span></td>
                    <td className="table-cell text-xs text-ink-3">{when}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
