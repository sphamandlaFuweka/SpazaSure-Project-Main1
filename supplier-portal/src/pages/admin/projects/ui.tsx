import { useState, type ReactNode } from 'react';
import clsx from 'clsx';
import { Modal } from '../../../components/ui';
import { initials, personColour, personName } from './logic';
import type { Person, Saved } from './types';

// ── Status pills (same tint style as the portal's badges) ───────────────────────
const TONES = {
  ok: 'bg-brand-green-light text-brand-green',
  warn: 'bg-brand-yellow-light text-accent-700',
  bad: 'bg-brand-red-light text-brand-red',
  info: 'bg-brand-blue-light text-brand-blue',
  idle: 'bg-gray-100 text-ink-2',
  defer: 'bg-violet-50 text-violet-700',
} as const;
type Tone = keyof typeof TONES;
const STATUS_TONE: Record<string, Tone> = {
  'Not Started': 'idle', 'In Progress': 'info', Blocked: 'bad', Completed: 'ok', Deferred: 'defer',
  New: 'idle', Researching: 'idle', Contacted: 'info', Meeting: 'info', Proposal: 'warn', Negotiation: 'warn', Won: 'ok', Lost: 'bad', 'On Hold': 'defer',
  Planned: 'idle', Testing: 'warn', Live: 'ok', Paused: 'defer',
  'On track': 'ok', 'At risk': 'warn', 'Off track': 'bad', 'No data': 'idle', Missed: 'bad',
  Open: 'warn', Mitigating: 'info', Closed: 'ok', 'Testing a change': 'info', Adopted: 'ok', Rejected: 'idle',
  Designing: 'info', 'In field': 'warn', Analysing: 'info', Complete: 'ok', Overdue: 'bad',
};
export const toneOf = (s: string): Tone => STATUS_TONE[s] ?? 'idle';
export const TONE_HEX: Record<Tone, string> = { ok: '#078C50', warn: '#D99508', bad: '#E02526', info: '#25449A', idle: '#C3C9DD', defer: '#7A6AA8' };

export function Pill({ status, className }: { status: string; className?: string }) {
  return (
    <span className={clsx('inline-flex items-center gap-1.5 text-xs font-bold px-2.5 py-1 rounded-full whitespace-nowrap', TONES[toneOf(status)], className)}>
      <span className="w-1.5 h-1.5 rounded-full bg-current" />{status}
    </span>
  );
}

const PRIO: Record<string, string> = { Critical: 'text-brand-red', High: 'text-accent-600', Medium: 'text-brand-blue', Low: 'text-ink-3' };
export function Prio({ p }: { p?: string }) {
  return <span className={clsx('text-[11px] font-black uppercase tracking-wider whitespace-nowrap', PRIO[p ?? ''] ?? 'text-ink-3')}>{p || 'Not set'}</span>;
}

export function PersonBadge({ people, id, showName = false, size = 'md' }: { people: Saved<Person>[]; id?: string; showName?: boolean; size?: 'sm' | 'md' }) {
  const name = personName(people, id);
  return (
    <span className="inline-flex items-center gap-2 min-w-0" title={name}>
      <span
        className={clsx('flex-shrink-0 inline-flex items-center justify-center rounded-lg text-white font-extrabold', size === 'sm' ? 'w-6 h-6 text-[10px]' : 'w-8 h-8 text-[11px]')}
        style={{ background: personColour(people, id) }}
      >{id ? initials(name) : '?'}</span>
      {showName && <span className="truncate text-sm font-semibold text-ink">{name}</span>}
    </span>
  );
}

export function Segmented<T extends string>({ value, options, onChange, label }: { value: T; options: { value: T; label: string }[]; onChange: (v: T) => void; label: string }) {
  return (
    <div role="group" aria-label={label} className="inline-flex flex-wrap gap-1 p-1 bg-brand-blue-light/60 rounded-full">
      {options.map((o) => (
        <button
          key={o.value} type="button" aria-pressed={value === o.value} onClick={() => onChange(o.value)}
          className={clsx('px-3.5 py-1.5 rounded-full text-sm font-bold transition-colors', value === o.value ? 'bg-brand-blue text-white' : 'text-brand-blue hover:bg-white/70')}
        >{o.label}</button>
      ))}
    </div>
  );
}

export function Bar({ pct, tone = 'ok' }: { pct: number; tone?: Tone }) {
  return (
    <div className="h-2 bg-brand-blue-light/70 rounded-full overflow-hidden min-w-[60px]">
      <div className="h-full rounded-full" style={{ width: `${Math.max(1, Math.min(100, pct))}%`, background: TONE_HEX[tone] }} />
    </div>
  );
}

export function Card({ title, action, children, className }: { title?: ReactNode; action?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={clsx('card p-5', className)}>
      {(title || action) && <header className="flex items-baseline justify-between gap-3 mb-3"><h3 className="section-title">{title}</h3>{action}</header>}
      {children}
    </section>
  );
}

export function Warn({ children }: { children: ReactNode }) {
  return <p className="flex items-start gap-1.5 text-xs font-bold text-accent-700"><span aria-hidden className="mt-px w-4 h-4 flex-shrink-0 rounded-full bg-brand-yellow-light grid place-items-center text-[10px] font-black">!</span>{children}</p>;
}

// ── Generic record form ───────────────────────────────────────────────────────
export type Option = string | { value: string; label: string };
export interface FieldDef {
  key: string; label: string; type?: 'text' | 'textarea' | 'number' | 'date' | 'email' | 'time' | 'url' | 'select' | 'person' | 'people';
  options?: readonly Option[]; required?: boolean; full?: boolean; hint?: string;
}
type Values = Record<string, unknown>;

export function RecordModal({ title, fields, initial, people, onSave, onDelete, onClose, footerNote }: {
  title: string; fields: FieldDef[]; initial: Values; people: Saved<Person>[];
  onSave: (values: Values) => Promise<boolean>; onDelete?: () => Promise<boolean>; onClose: () => void; footerNote?: ReactNode;
}) {
  const [values, setValues] = useState<Values>(initial);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const set = (k: string, v: unknown) => setValues((x) => ({ ...x, [k]: v }));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    const missing = fields.filter((f) => f.required && (values[f.key] === '' || values[f.key] == null || (Array.isArray(values[f.key]) && !(values[f.key] as unknown[]).length)));
    if (missing.length) { setError(`Add ${missing.map((f) => f.label.toLowerCase()).join(', ')} before saving.`); return; }
    setBusy(true);
    const ok = await onSave(values);
    setBusy(false);
    if (ok) onClose();
  };
  const del = async () => {
    if (!confirmDelete) { setConfirmDelete(true); return; }
    setBusy(true); const ok = await onDelete!(); setBusy(false); if (ok) onClose();
  };

  return (
    <Modal title={title} onClose={onClose} size="lg">
      <form onSubmit={submit} className="p-6 grid grid-cols-1 sm:grid-cols-2 gap-4">
        {fields.map((f) => {
          const id = `pm-${f.key}`; const v = values[f.key];
          const opts = (f.type === 'person' || f.type === 'people') ? people.map((p) => ({ value: p.id, label: p.name })) : (f.options ?? []).map((o) => (typeof o === 'string' ? { value: o, label: o } : o));
          return (
            <div key={f.key} className={clsx((f.full || f.type === 'textarea' || f.type === 'people') && 'sm:col-span-2')}>
              <label htmlFor={id} className="label">{f.label}{f.required && <span className="text-brand-red"> *</span>}</label>
              {f.type === 'textarea' ? (
                <textarea id={id} rows={3} className="input" value={String(v ?? '')} onChange={(e) => set(f.key, e.target.value)} />
              ) : f.type === 'select' || f.type === 'person' ? (
                <select id={id} className="select" value={String(v ?? '')} onChange={(e) => set(f.key, e.target.value)}>
                  <option value="">—</option>
                  {opts.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              ) : f.type === 'people' ? (
                <div id={id} className="flex flex-wrap gap-x-4 gap-y-2 pt-1">
                  {opts.map((o) => {
                    const list = (v as string[] | undefined) ?? [];
                    return (
                      <label key={o.value} className="inline-flex items-center gap-2 text-sm font-semibold text-ink">
                        <input type="checkbox" className="accent-[#25449A] w-4 h-4" checked={list.includes(o.value)}
                          onChange={(e) => set(f.key, e.target.checked ? [...list, o.value] : list.filter((x) => x !== o.value))} />
                        {o.label}
                      </label>
                    );
                  })}
                </div>
              ) : (
                <input id={id} className="input" type={f.type ?? 'text'} value={v == null ? '' : String(v)}
                  onChange={(e) => set(f.key, f.type === 'number' ? (e.target.value === '' ? null : Number(e.target.value)) : e.target.value)} />
              )}
              {f.hint && <p className="text-xs text-ink-3 mt-1 font-semibold">{f.hint}</p>}
            </div>
          );
        })}
        {footerNote && <div className="sm:col-span-2 text-xs font-semibold text-ink-3">{footerNote}</div>}
        {error && <p role="alert" className="sm:col-span-2 text-sm font-bold text-brand-red">{error}</p>}
        <div className="sm:col-span-2 flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-brand-blue-light">
          {onDelete ? <button type="button" className="btn-ghost text-brand-red" onClick={del} disabled={busy}>{confirmDelete ? 'Click again to delete' : 'Delete'}</button> : <span />}
          <div className="flex gap-2">
            <button type="button" className="btn-secondary" onClick={onClose}>Cancel</button>
            <button type="submit" className="btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
          </div>
        </div>
      </form>
    </Modal>
  );
}
