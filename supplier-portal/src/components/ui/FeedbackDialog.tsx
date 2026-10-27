import { useState } from 'react';
import { Star, Send } from 'lucide-react';
import toast from 'react-hot-toast';
import clsx from 'clsx';
import { Modal } from './index';
import { apiErrorMessage, feedbackApi, type FeedbackType } from '../../services/api';

const types: { key: FeedbackType; label: string }[] = [
  { key: 'bug', label: "Something's not working" },
  { key: 'idea', label: 'An idea' },
  { key: 'question', label: 'A question' },
  { key: 'praise', label: 'Something I like' },
];

/** "Send feedback" from the supplier portal while SpazaSure is being tested. */
export default function FeedbackDialog({ onClose }: { onClose: () => void }) {
  const [type, setType] = useState<FeedbackType>('bug');
  const [message, setMessage] = useState('');
  const [rating, setRating] = useState<number | null>(null);
  const [screenshot, setScreenshot] = useState<File | null>(null);
  const [sending, setSending] = useState(false);

  const send = async () => {
    if (message.trim().length < 3) {
      toast.error('Please tell us a little more.');
      return;
    }
    setSending(true);
    try {
      await feedbackApi.send({
        type,
        message: message.trim(),
        rating: rating ?? undefined,
        // The page they were on, without any query string
        screen: window.location.pathname,
        screenshot,
      });
      toast.success('Thank you! Your feedback helps us make SpazaSure better.');
      onClose();
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not send your feedback'));
    } finally {
      setSending(false);
    }
  };

  return (
    <Modal title="Send feedback" onClose={onClose}>
      <div className="p-6 space-y-4">
        <p className="text-sm text-gray-500">
          SpazaSure is still being tested. Tell us what works, what doesn't and what you'd like to see. We read every message.
        </p>

        <div className="flex flex-wrap gap-2">
          {types.map((t) => (
            <button
              key={t.key}
              type="button"
              onClick={() => setType(t.key)}
              className={clsx(
                'px-3 py-1.5 rounded-full text-sm font-semibold border transition',
                type === t.key ? 'bg-brand-blue text-white border-brand-blue' : 'bg-white text-gray-600 border-gray-200 hover:border-brand-blue'
              )}
            >
              {t.label}
            </button>
          ))}
        </div>

        <textarea
          className="input min-h-[120px]"
          placeholder={type === 'bug' ? 'What happened, and what did you expect to happen?' : 'Tell us more...'}
          maxLength={2000}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
        />

        <div>
          <p className="text-sm font-semibold text-gray-700 mb-1">How do you rate SpazaSure so far? (optional)</p>
          <div className="flex gap-1">
            {[1, 2, 3, 4, 5].map((n) => (
              <button key={n} type="button" aria-label={`${n} of 5`} onClick={() => setRating(rating === n ? null : n)}>
                <Star size={26} className={clsx(rating !== null && n <= rating ? 'fill-amber-400 text-amber-400' : 'text-gray-300')} />
              </button>
            ))}
          </div>
        </div>

        <label className="block">
          <span className="text-sm font-semibold text-gray-700">Screenshot (optional)</span>
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="block mt-1 text-sm text-gray-600"
            onChange={(e) => setScreenshot(e.target.files?.[0] ?? null)}
          />
        </label>

        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="btn-secondary" onClick={onClose} disabled={sending}>Cancel</button>
          <button type="button" className="btn-primary flex items-center gap-2" onClick={send} disabled={sending}>
            <Send size={15} /> {sending ? 'Sending...' : 'Send feedback'}
          </button>
        </div>
      </div>
    </Modal>
  );
}
