import { useState } from 'react';
import { Plus } from 'lucide-react';
import { AUTOMATIONS, isOpen, itemOpen, ruleOn, upcomingSends } from './logic';
import ScheduleList from './ScheduleList';
import { Card, PersonBadge, Segmented, Warn } from './ui';
import { useEditor } from './editors';
import type { WorkspaceApi } from './useWorkspace';

// ── Reminders: rules and the schedule they produce ───────────────────────────
export function RemindersTab({ api }: { api: WorkspaceApi }) {
  const { ws } = api;
  const [horizon, setHorizon] = useState<'7' | '14'>('14');
  const settings = ws.settings[0];
  const sends = upcomingSends(ws, Number(horizon));
  const missing = ws.person.filter((p) => !p.email);
  const toggle = (key: string, on: boolean) => { if (settings) api.update('settings', settings, { automations: { ...settings.automations, [key]: on } }); };

  return (
    <div className="space-y-5">
      <p className="text-sm font-semibold text-ink-3 max-w-3xl">
        Rules decide who is told what, and when. Messages only go out 07:00–18:00 on weekdays, and reminders to the same person at the same time are combined into one.
        Sending starts once the email service is connected (stage 2); until then this page shows exactly what would be sent.
      </p>
      {missing.length > 0 && <div className="card p-4"><Warn>{missing.map((p) => p.name).join(', ')} {missing.length > 1 ? 'have' : 'has'} no email address, so they would miss every reminder. Add it on the People tab.</Warn></div>}
      <div className="grid lg:grid-cols-2 gap-5 items-start">
        <Card title={<>Rules <span className="text-ink-3 text-sm">{AUTOMATIONS.filter((a) => ruleOn(ws, a.key)).length} of {AUTOMATIONS.length} on</span></>}>
          <ul className="divide-y divide-brand-blue-light">
            {AUTOMATIONS.map((a) => (
              <li key={a.key} className="py-3">
                <label className="flex items-start gap-3 cursor-pointer">
                  <input type="checkbox" className="mt-1 w-4 h-4 accent-[#25449A]" checked={ruleOn(ws, a.key)} onChange={(e) => toggle(a.key, e.target.checked)} />
                  <span className="flex-1 min-w-0">
                    <span className="block text-sm font-extrabold text-gray-900">{a.name}</span>
                    <span className="block text-xs font-semibold text-ink-3">{a.when} → {a.to}</span>
                    <span className="block text-xs font-semibold text-ink-3">{a.description}</span>
                  </span>
                  <span className="flex flex-wrap gap-1 justify-end">{a.channels.map((c) => <span key={c} className="text-[11px] font-extrabold px-2 py-0.5 rounded-full bg-brand-blue-light text-brand-blue whitespace-nowrap">{c}</span>)}</span>
                </label>
              </li>
            ))}
          </ul>
        </Card>
        <Card title="Scheduled" action={<Segmented label="Window" value={horizon} onChange={setHorizon} options={[{ value: '7', label: '7 days' }, { value: '14', label: '14 days' }]} />}>
          <p className="text-xs font-semibold text-ink-3 mb-3">{sends.length} sends worked out from the live trackers. Change a due date or switch a rule off and this list updates.</p>
          <ScheduleList sends={sends} people={ws.person} />
        </Card>
      </div>
    </div>
  );
}

// ── People: one contact directory for owners, attendees and reminders ────────
export function PeopleTab({ api }: { api: WorkspaceApi }) {
  const { ws } = api; const edit = useEditor();
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm font-semibold text-ink-3 max-w-2xl">Contact details live here once. Tasks, meetings and reminders all look people up from this list, so changing a number updates it everywhere.</p>
        <button className="btn-primary" onClick={() => edit('person')}><Plus size={16} /> Add person</button>
      </div>
      <div className="card overflow-hidden"><div className="overflow-x-auto"><table className="w-full">
        <thead><tr>{['Name', 'Role', 'Email', 'Phone / WhatsApp', 'Open items', 'Reminders'].map((h) => <th key={h} className="table-header">{h}</th>)}</tr></thead>
        <tbody>
          {ws.person.map((p) => {
            const n = ws.task.filter((t) => t.owner === p.id && isOpen(t)).length + ws.initiative.filter((i) => i.owner === p.id && itemOpen(i)).length;
            return (
              <tr key={p.id} className="table-row cursor-pointer" onClick={() => edit('person', p)}>
                <td className="table-cell"><PersonBadge people={ws.person} id={p.id} showName />{p.note && <p className="text-xs font-semibold text-ink-3 mt-1">{p.note}</p>}</td>
                <td className="table-cell text-sm font-semibold text-ink-2">{p.role}</td>
                <td className="table-cell text-sm font-semibold">{p.email || <span className="text-brand-red">Missing</span>}</td>
                <td className="table-cell text-sm font-semibold tabular-nums">{p.phone || <span className="text-accent-700">Missing</span>}</td>
                <td className="table-cell text-sm font-extrabold tabular-nums">{n}</td>
                <td className="table-cell"><span className={p.email ? 'text-xs font-bold text-brand-green' : 'text-xs font-bold text-brand-red'}>{p.email ? 'Can receive' : 'No email'}</span></td>
              </tr>
            );
          })}
        </tbody>
      </table></div></div>
    </div>
  );
}
