import { useCallback, useEffect, useState } from 'react';
import { AlertTriangle, Star } from 'lucide-react';
import { format } from 'date-fns';
import toast from 'react-hot-toast';
import { supplierFeedbackApi } from '../../services/api';
import type { DeliveryDispute, SupplierRating } from '../../types';
import PageLoader from '../../components/ui/PageLoader';

type FeedbackTab = 'ratings' | 'disputes';

export default function FeedbackPage() {
  const [tab, setTab] = useState<FeedbackTab>('ratings');
  const [ratings, setRatings] = useState<SupplierRating[]>([]);
  const [disputes, setDisputes] = useState<DeliveryDispute[]>([]);
  const [averageDelivery, setAverageDelivery] = useState(0);
  const [averageSupplier, setAverageSupplier] = useState(0);
  const [openDisputes, setOpenDisputes] = useState(0);
  const [loading, setLoading] = useState(true);

  const loadFeedback = useCallback(async () => {
    setLoading(true);
    const [ratingResult, disputeResult] = await Promise.allSettled([
      supplierFeedbackApi.ratings(),
      supplierFeedbackApi.disputes(),
    ]);
    if (ratingResult.status === 'fulfilled') {
      setRatings(ratingResult.value.ratings);
      setAverageDelivery(ratingResult.value.averageDeliveryRating);
      setAverageSupplier(ratingResult.value.averageSupplierRating);
    } else {
      toast.error('Could not load your ratings.');
    }
    if (disputeResult.status === 'fulfilled') {
      setDisputes(disputeResult.value.disputes);
      setOpenDisputes(disputeResult.value.openCount);
    } else {
      toast.error('Could not load delivery disputes.');
    }
    setLoading(false);
  }, []);

  useEffect(() => { void loadFeedback(); }, [loadFeedback]);

  if (loading) return <PageLoader variant="table" />;

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div>
        <h1 className="page-title">Ratings & disputes</h1>
        <p className="page-subtitle">Shop feedback and delivery issues for your orders.</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="card p-4">
          <p className="text-xs font-bold uppercase tracking-wide text-gray-500">Delivery rating</p>
          <p className="mt-1 text-2xl font-black text-gray-900">{averageDelivery.toFixed(1)} / 5</p>
        </div>
        <div className="card p-4">
          <p className="text-xs font-bold uppercase tracking-wide text-gray-500">Supplier rating</p>
          <p className="mt-1 text-2xl font-black text-gray-900">{averageSupplier.toFixed(1)} / 5</p>
        </div>
      </div>

      <div className="flex gap-2 border-b border-gray-200">
        <button
          type="button"
          onClick={() => setTab('ratings')}
          className={`px-4 py-3 text-sm font-bold border-b-2 ${tab === 'ratings' ? 'border-brand-blue text-brand-blue' : 'border-transparent text-gray-500'}`}
        >
          Ratings ({ratings.length})
        </button>
        <button
          type="button"
          onClick={() => setTab('disputes')}
          className={`px-4 py-3 text-sm font-bold border-b-2 ${tab === 'disputes' ? 'border-brand-blue text-brand-blue' : 'border-transparent text-gray-500'}`}
        >
          Disputes ({openDisputes} open)
        </button>
      </div>

      {tab === 'ratings' ? (
        ratings.length === 0 ? (
          <div className="card p-10 text-center text-sm text-gray-500">No ratings yet.</div>
        ) : (
          <div className="space-y-3">
            {ratings.map((rating) => (
              <article key={rating.id} className="card p-4 space-y-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <p className="font-bold text-gray-900">{rating.shopName}</p>
                    <p className="text-xs text-gray-500">Order {rating.orderNumber}</p>
                  </div>
                  <time className="text-xs text-gray-500">{format(new Date(rating.createdAt), 'dd MMM yyyy')}</time>
                </div>
                <div className="flex flex-wrap gap-x-5 gap-y-2 text-sm">
                  <Rating label="Delivery" value={rating.deliveryRating} />
                  <Rating label="Supplier" value={rating.supplierRating} />
                </div>
                {rating.comment && <p className="text-sm text-gray-700 whitespace-pre-wrap">{rating.comment}</p>}
              </article>
            ))}
          </div>
        )
      ) : disputes.length === 0 ? (
        <div className="card p-10 text-center text-sm text-gray-500">No delivery disputes.</div>
      ) : (
        <div className="space-y-3">
          {disputes.map((dispute) => (
            <article key={dispute.id} className="card p-4 space-y-2">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="font-bold text-gray-900">{dispute.issueType}</p>
                  <p className="text-xs text-gray-500">Order {dispute.orderNumber} · {dispute.shopName}</p>
                </div>
                <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${dispute.status === 'open' ? 'bg-brand-red-light text-brand-red' : 'bg-gray-100 text-gray-700'}`}>
                  {dispute.status.replace(/_/g, ' ')}
                </span>
              </div>
              <p className="text-sm text-gray-700 whitespace-pre-wrap">{dispute.description}</p>
              <p className="text-xs text-gray-500">Reported {format(new Date(dispute.createdAt), 'dd MMM yyyy, HH:mm')}</p>
              {dispute.resolutionNote && (
                <p className="rounded-xl bg-gray-50 p-3 text-sm text-gray-600">
                  SpazaSure update: {dispute.resolutionNote}
                </p>
              )}
              {dispute.refundStatus && (
                <p className="text-xs text-gray-500">Shop refund: {dispute.refundStatus.replace(/_/g, ' ')}</p>
              )}
              {dispute.status === 'open' && (
                <p className="flex items-center gap-2 text-xs font-semibold text-amber-700">
                  <AlertTriangle size={14} /> SpazaSure is reviewing this delivery issue.
                </p>
              )}
            </article>
          ))}
        </div>
      )}
    </div>
  );
}

function Rating({ label, value }: { label: string; value: number }) {
  return (
    <span className="inline-flex items-center gap-1 text-sm font-semibold text-gray-700">
      <Star size={14} className="fill-amber-400 text-amber-400" />
      {label}: {value}/5
    </span>
  );
}
