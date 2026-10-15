import { fmtDay, hm, personName, type ScheduledSend } from './logic';
import { PersonBadge, Warn } from './ui';
import type { Person, Saved } from './types';

/** Reminders the scheduler will send, grouped by day. */
export default function ScheduleList({ sends, people }: { sends: ScheduledSend[]; people: Saved<Person>[] }) {
  if (!sends.length) return <p className="text-sm font-semibold text-ink-3">Nothing scheduled in this window.</p>;
  const days: { label: string; items: ScheduledSend[] }[] = [];
  sends.forEach((s) => {
    const label = fmtDay(s.at);
    if (days[days.length - 1]?.label !== label) days.push({ label, items: [] });
    days[days.length - 1].items.push(s);
  });
  return (
    <div className="space-y-4">
      {days.map((d) => (
        <div key={d.label}>
          <p className="text-[11px] font-extrabold uppercase tracking-wider text-brand-blue mb-1">{d.label}</p>
          <ul className="divide-y divide-brand-blue-light/70">
            {d.items.map((s, i) => {
              const missing = s.to.filter((id) => !people.find((p) => p.id === id)?.email);
              return (
                <li key={i} className="grid grid-cols-[48px_1fr_auto] gap-3 py-2 items-start">
                  <span className="text-sm font-extrabold text-gray-900 tabular-nums pt-0.5">{hm(s.at)}</span>
                  <div className="min-w-0">
                    <p className="text-sm font-bold text-gray-900 break-words">{s.subject}</p>
                    <p className="text-xs font-semibold text-ink-3">{s.rule} → {s.to.length > 2 ? `${s.to.length} people` : s.to.map((id) => personName(people, id)).join(', ')}</p>
                    {missing.length > 0 && <Warn>No email for {missing.map((id) => personName(people, id)).join(', ')}</Warn>}
                  </div>
                  <span className="flex -space-x-1.5">{s.to.slice(0, 4).map((id) => <PersonBadge key={id} people={people} id={id} size="sm" />)}</span>
                </li>
              );
            })}
          </ul>
        </div>
      ))}
    </div>
  );
}
