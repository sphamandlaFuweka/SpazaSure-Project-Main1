import { useEffect, useRef, useState } from 'react';
import { MapPin, CheckCircle2, Loader2 } from 'lucide-react';
import { searchAddresses, type AddressSuggestion } from '../../services/address';

interface Props {
  value: string;
  confirmed: boolean;
  onTextChange: (text: string) => void;
  onSelect: (suggestion: AddressSuggestion) => void;
  placeholder?: string;
  error?: string;
}

/** Text input that suggests real South African addresses. Only a picked suggestion counts as confirmed. */
export default function AddressAutocomplete({ value, confirmed, onTextChange, onSelect, placeholder, error }: Props) {
  const [suggestions, setSuggestions] = useState<AddressSuggestion[]>([]);
  const [loading, setLoading] = useState(false);
  const [searched, setSearched] = useState(false);
  const [failed, setFailed] = useState(false);
  const [open, setOpen] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout>>();
  const abort = useRef<AbortController>();
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const close = (e: MouseEvent) => { if (!box.current?.contains(e.target as Node)) setOpen(false); };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  useEffect(() => () => { clearTimeout(timer.current); abort.current?.abort(); }, []);

  const handleChange = (text: string) => {
    onTextChange(text);
    clearTimeout(timer.current);
    abort.current?.abort();
    if (text.trim().length < 3) { setSuggestions([]); setSearched(false); return; }
    timer.current = setTimeout(async () => {
      const controller = new AbortController();
      abort.current = controller;
      setLoading(true);
      setFailed(false);
      try {
        setSuggestions(await searchAddresses(text, controller.signal));
        setSearched(true);
        setOpen(true);
      } catch (e) {
        if ((e as Error).name !== 'AbortError') setFailed(true);
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }, 600);
  };

  const hint = failed
    ? 'Could not search addresses. Check your connection.'
    : confirmed
      ? 'Address confirmed on the map'
      : searched && suggestions.length === 0
        ? 'No matches. Try adding the suburb or city.'
        : 'Choose a suggestion to confirm the address';

  return (
    <div ref={box} className="relative">
      <div className="relative">
        <MapPin size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
        <input
          value={value}
          onChange={(e) => handleChange(e.target.value)}
          onFocus={() => suggestions.length > 0 && setOpen(true)}
          placeholder={placeholder ?? 'Start typing the street address'}
          autoComplete="off"
          className={`input pl-9 pr-9 ${error ? 'input-error' : ''}`}
        />
        <span className="absolute right-3 top-1/2 -translate-y-1/2">
          {loading ? <Loader2 size={16} className="animate-spin text-gray-400" /> : confirmed ? <CheckCircle2 size={16} className="text-emerald-600" /> : null}
        </span>
      </div>
      {open && suggestions.length > 0 && (
        <ul className="absolute z-30 mt-1 w-full max-h-64 overflow-y-auto rounded-xl border border-gray-200 bg-white shadow-xl">
          {suggestions.map((s, i) => (
            <li key={`${s.label}-${i}`}>
              <button
                type="button"
                onClick={() => { onSelect(s); setOpen(false); setSuggestions([]); }}
                className="w-full text-left px-3 py-2.5 text-sm hover:bg-primary-50 flex items-start gap-2"
              >
                <MapPin size={14} className="mt-0.5 text-gray-400 flex-shrink-0" />
                <span>{s.label}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <p className={`text-xs mt-1.5 font-medium ${error || failed ? 'text-red-500' : confirmed ? 'text-emerald-600' : 'text-gray-400'}`}>
        {error ?? hint}
      </p>
    </div>
  );
}
