import { useState } from 'react';
import toast from 'react-hot-toast';
import { CalendarPlus, Settings2, Video } from 'lucide-react';
import clsx from 'clsx';
import {
  fmtDate, fmtDay, googleCalendarUrl, isOpen, isOverdue, isPlaceholderLink, isoDate, itemOpen, daysTo, nextOccurrence, personName, today,
} from './logic';
import { Card, PersonBadge, Warn } from './ui';
import { useEditor } from './editors';
import type { WorkspaceApi } from './useWorkspace';
import { TASK_STATUSES, type Meeting, type MeetingKey, type Saved, type Task, type TaskStatus } from './types';

const RULES = [
  'Keep each meeting focused on its stated purpose.',
  'Every action must have an owner and a deadline.',
  'Record decisions immediately in the relevant tracker.',
  'Carry unfinished actions forward until completed or formally deferred.',
  'End every meeting with clear next actions.',
];

interface Live { key: MeetingKey; date: string; checked: boolean[]; decisions: string[]; actions: string[]; lessons: string[] }

export default function MeetingsTab({ api, live, setLive }: { api: WorkspaceApi; live: Live | null; setLive: (l: Live | null) => void }) {
  const { ws } = api; const edit = useEditor();
  const meetings = [...ws.meeting].sort((a, b) => a.dow - b.dow);

  if (live) {
    const m = ws.meeting.find((x) => x.key === live.key);
    if (m) return <Runner api={api} m={m} live={live} setLive={setLive} />;
  }

  return (
    <div className="space-y-5">
      <p className="text-sm font-semibold text-ink-3 max-w-3xl">Three focused meetings a week. Each one is a recurring Google Calendar event with one permanent Meet link, runs from a fixed agenda and ends with actions that have an owner and a deadline.</p>
      <div className="grid md:grid-cols-3 gap-4">
        {meetings.map((m) => (
          <section key={m.id} className="card p-5 flex flex-col gap-3">
            <p className="text-xs font-extrabold uppercase tracking-wider text-brand-blue">{m.day} {m.time} · next {fmtDay(nextOccurrence(m))}</p>
            <h3 className="text-lg font-black text-gray-900 -mt-1">{m.name}</h3>
            <p className="text-sm font-semibold text-ink-3">{m.focus}</p>
            <p className="text-sm"><b className="text-gray-900">Output:</b> <span className="text-ink-2">{m.output}</span></p>
            <div className="flex items-center gap-2">
              <span className="flex -space-x-1.5">{m.attendees.slice(0, 6).map((id) => <PersonBadge key={id} people={ws.person} id={id} size="sm" />)}</span>
              <span className="text-xs font-semibold text-ink-3">{m.attendees.length} attendees · chair {personName(ws.person, m.chair)}</span>
            </div>
            {isPlaceholderLink(m) && <Warn>Placeholder Meet link. Replace it in Settings.</Warn>}
            <div className="flex flex-wrap gap-2 mt-auto pt-1">
              <a className="btn-accent" href={m.link} target="_blank" rel="noreferrer"><Video size={15} /> Join Meet</a>
              <a className="btn-secondary" href={googleCalendarUrl(m, ws.person)} target="_blank" rel="noreferrer"><CalendarPlus size={15} /> Add to calendar</a>
            </div>
            <div className="flex flex-wrap gap-2">
              <button className="btn-primary" onClick={() => setLive({ key: m.key, date: isoDate(today()), checked: m.agenda.map(() => false), decisions: [], actions: [], lessons: [] })}>Run meeting</button>
              <button className="btn-ghost" onClick={() => edit('meeting', m)}><Settings2 size={15} /> Settings</button>
            </div>
          </section>
        ))}
      </div>
      <div className="grid lg:grid-cols-[1.4fr_1fr] gap-5">
        <Card title="Past meetings">
          {ws.meetingRecord.length ? (
            <ul className="space-y-2">
              {[...ws.meetingRecord].sort((a, b) => b.date.localeCompare(a.date)).map((r) => (
                <li key={r.id} className="p-3 rounded-2xl bg-surface">
                  <p className="text-sm font-extrabold text-gray-900">{fmtDate(r.date)} · {ws.meeting.find((m) => m.key === r.meetingKey)?.name}</p>
                  <p className="text-xs font-semibold text-ink-3">{r.checked} agenda items covered · {r.decisions.length} decisions · {r.actions.length} actions · {r.lessons.length} lessons</p>
                  {r.decisions.map((d, i) => <p key={i} className="text-xs font-semibold text-ink-2">• {d}</p>)}
                </li>
              ))}
            </ul>
          ) : <p className="text-sm font-semibold text-ink-3">Meetings you close here are saved with their decisions, actions and lessons.</p>}
        </Card>
        <Card title="Meeting rules"><ol className="list-decimal pl-5 space-y-1 text-sm font-semibold text-ink-2">{RULES.map((r) => <li key={r}>{r}</li>)}</ol></Card>
      </div>
    </div>
  );
}

function Runner({ api, m, live, setLive }: { api: WorkspaceApi; m: Saved<Meeting>; live: Live; setLive: (l: Live | null) => void }) {
  const { ws } = api; const edit = useEditor();
  const [decision, setDecision] = useState(''); const [lesson, setLesson] = useState('');
  const [title, setTitle] = useState(''); const [owner, setOwner] = useState(''); const [due, setDue] = useState(''); const [err, setErr] = useState('');
  const [closing, setClosing] = useState(false);
  const carried = ws.task.filter((t) => t.source && isOpen(t) && !live.actions.includes(t.id));

  const setTaskStatus = (t: Saved<Task>, s: TaskStatus) =>
    api.update('task', t, s === 'Completed' ? { status: s, pct: 100, doneAt: t.doneAt || isoDate(today()) } : { status: s });

  const context = (i: number) => {
    const link = (t: Saved<Task>, text: string) => <button key={t.id} className="block text-left font-bold text-brand-blue underline underline-offset-2 decoration-brand-blue-light" onClick={() => edit('task', t)}>{text}</button>;
    if (m.key === 'mon') {
      const weekAgo = new Date(today().getTime() - 7 * 864e5);
      if (i === 0) { const c = ws.task.filter((t) => t.status === 'Completed' && t.doneAt && new Date(`${t.doneAt}T00:00:00`) >= weekAgo); return c.length ? c.map((t) => link(t, `✓ ${t.title}`)) : 'Nothing marked completed in the last 7 days.'; }
      if (i === 1) { const c = ws.task.filter(isOverdue); const n = ws.task.filter((t) => isOpen(t) && !t.due).length; return <>{c.length ? c.map((t) => link(t, `${t.title} · due ${fmtDate(t.due)}`)) : 'No overdue tasks.'}{n ? ` ${n} open task${n > 1 ? 's have' : ' has'} no due date.` : ''}</>; }
      if (i === 2) { const c = ws.task.filter((t) => isOpen(t) && (t.blocker || t.status === 'Blocked')); return c.length ? c.map((t) => link(t, `${t.title}: ${t.blocker || 'blocked'}`)) : 'No blocked tasks recorded.'; }
      if (i === 4) { const c = ws.task.filter((t) => t.top); return <>{c.length ? c.map((t) => link(t, `★ ${t.title}`)) : 'No top priorities picked yet.'} <span>Pick up to 3 with the star on the Tasks tab ({c.length}/3).</span></>; }
    }
    if (m.key === 'wed' && i === 0) return `${ws.opportunity.length} opportunities in the pipeline, ${ws.opportunity.filter((o) => o.due && daysTo(o.due) >= 0 && daysTo(o.due) <= 7).length} with a target date this week.`;
    if (m.key === 'fri' && i === 0) { const o = ws.lesson.filter((l) => l.status === 'Open'); return `${o.length} open lesson${o.length === 1 ? '' : 's'} waiting for a decision.`; }
    if (m.key === 'fri' && (i === 1 || i === 4)) { const area = i === 1 ? 'Product' : 'Marketing'; const c = ws.initiative.filter((x) => x.area === area); const late = c.filter((x) => itemOpen(x) && x.due && daysTo(x.due) < 0).length; return `${c.length} ${area.toLowerCase()} items${late ? `, ${late} overdue` : ''}.`; }
    return null;
  };

  const addAction = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !owner || !due) { setErr('Every action needs a description, an owner and a deadline.'); return; }
    const saved = await api.create('task', {
      projectId: ws.project[0]?.id ?? '', title: title.trim(), owner, priority: 'High', due, status: 'Not Started', pct: 0,
      blocker: '', next: '', commitment: '', top: false, source: { meetingKey: m.key, date: live.date }, doneAt: null,
    });
    if (!saved) return;
    setLive({ ...live, actions: [...live.actions, saved.id] }); setTitle(''); setOwner(''); setDue(''); setErr('');
    toast.success(`Action added for ${personName(ws.person, owner)}`);
  };
  const addLesson = async (e: React.FormEvent) => {
    e.preventDefault(); if (!lesson.trim()) return;
    const saved = await api.create('lesson', { date: live.date, title: lesson.trim(), evidence: `Raised in ${m.name}`, source: 'Meeting', action: '', status: 'Open', owner: m.chair, projectId: '' });
    if (saved) { setLive({ ...live, lessons: [...live.lessons, lesson.trim()] }); setLesson(''); toast.success('Saved to the learning log'); }
  };
  const close = async () => {
    setClosing(true);
    const ok = await api.create('meetingRecord', { meetingKey: m.key, date: live.date, checked: live.checked.filter(Boolean).length, decisions: live.decisions, actions: live.actions, lessons: live.lessons });
    setClosing(false);
    if (ok) { setLive(null); toast.success('Meeting saved'); }
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs font-extrabold uppercase tracking-wider text-brand-red">Live · {new Date(`${live.date}T00:00:00`).toLocaleDateString('en-ZA', { weekday: 'long', day: 'numeric', month: 'long' })}</p>
          <h2 className="text-2xl font-black text-gray-900">{m.name}</h2>
          <p className="text-sm font-semibold text-ink-3">{m.focus}. Expected output: {m.output.toLowerCase()}.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <a className="btn-secondary" href={m.link} target="_blank" rel="noreferrer"><Video size={15} /> Open Meet</a>
          <button className="btn-ghost" onClick={() => setLive(null)}>Discard</button>
          <button className="btn-primary" disabled={closing} onClick={close}>{closing ? 'Saving…' : 'Close meeting'}</button>
        </div>
      </div>
      <div className="grid lg:grid-cols-2 gap-5 items-start">
        <Card title={<>Agenda <span className="text-ink-3 text-sm">{live.checked.filter(Boolean).length}/{m.agenda.length}</span></>}>
          <ol className="divide-y divide-brand-blue-light">
            {m.agenda.map((a, i) => { const ctx = context(i); return (
              <li key={i} className="py-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
                <input id={`ag-${i}`} type="checkbox" className="mt-1 w-4 h-4 accent-[#078C50]" checked={live.checked[i] ?? false}
                  onChange={(e) => { const c = [...live.checked]; c[i] = e.target.checked; setLive({ ...live, checked: c }); }} />
                <label htmlFor={`ag-${i}`} className={clsx('text-sm font-bold cursor-pointer', live.checked[i] ? 'text-ink-3 line-through' : 'text-gray-900')}>{a}</label>
                {ctx && <div className="col-start-2 text-xs font-semibold text-ink-3 space-y-0.5">{ctx}</div>}
              </li>); })}
          </ol>
        </Card>
        <div className="space-y-5">
          <Card title="Decisions">
            <form className="flex gap-2" onSubmit={(e) => { e.preventDefault(); if (decision.trim()) { setLive({ ...live, decisions: [...live.decisions, decision.trim()] }); setDecision(''); } }}>
              <input aria-label="Decision" className="input" placeholder="e.g. Accept Tradeport's tier-2 discount terms" value={decision} onChange={(e) => setDecision(e.target.value)} />
              <button className="btn-secondary">Record</button>
            </form>
            <ul className="mt-3 space-y-1.5">{live.decisions.map((d, i) => <li key={i} className="flex justify-between gap-2 p-2.5 rounded-xl bg-surface text-sm font-bold text-gray-900">{d}<button className="text-xs text-ink-3 hover:text-brand-red" onClick={() => setLive({ ...live, decisions: live.decisions.filter((_, j) => j !== i) })}>Remove</button></li>)}</ul>
          </Card>
          <Card title="Actions">
            <form className="space-y-2" onSubmit={addAction}>
              <input aria-label="Action" className="input" placeholder="What needs to happen?" value={title} onChange={(e) => setTitle(e.target.value)} />
              <div className="grid sm:grid-cols-2 gap-2">
                <select aria-label="Owner" className="select" value={owner} onChange={(e) => setOwner(e.target.value)}><option value="">Owner…</option>{ws.person.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select>
                <input aria-label="Deadline" type="date" className="input" value={due} onChange={(e) => setDue(e.target.value)} />
              </div>
              <div className="flex items-center justify-between gap-2"><span role="alert" className="text-xs font-bold text-brand-red">{err}</span><button className="btn-primary">Add action</button></div>
            </form>
            <ul className="mt-3 space-y-1.5">{live.actions.map((id) => { const t = ws.task.find((x) => x.id === id); return t ? <li key={id} className="flex items-center justify-between gap-2 p-2.5 rounded-xl bg-surface text-sm font-bold text-gray-900">{t.title}<span className="flex items-center gap-2 text-xs text-ink-3"><PersonBadge people={ws.person} id={t.owner} size="sm" />{fmtDate(t.due)}</span></li> : null; })}</ul>
          </Card>
          <Card title="Lessons">
            <form className="flex gap-2" onSubmit={addLesson}>
              <input aria-label="Lesson" className="input" placeholder="What did we learn this week?" value={lesson} onChange={(e) => setLesson(e.target.value)} />
              <button className="btn-secondary">Log</button>
            </form>
            <ul className="mt-3 space-y-1.5">{live.lessons.map((l, i) => <li key={i} className="p-2.5 rounded-xl bg-surface text-sm font-bold text-gray-900">{l}</li>)}</ul>
          </Card>
          <Card title={<>Carried forward <span className="text-ink-3 text-sm">{carried.length}</span></>}>
            {carried.length ? (
              <ul className="space-y-2">{carried.map((t) => (
                <li key={t.id} className="flex items-center gap-3 p-2.5 rounded-xl bg-surface">
                  <PersonBadge people={ws.person} id={t.owner} size="sm" />
                  <div className="flex-1 min-w-0"><p className="text-sm font-bold text-gray-900">{t.title}</p><p className="text-xs font-semibold text-ink-3">From {ws.meeting.find((x) => x.key === t.source!.meetingKey)?.day} {fmtDate(t.source!.date)} · due {fmtDate(t.due)}</p></div>
                  <select aria-label="Status" className="select w-auto py-1 text-xs" value={t.status} onChange={(e) => setTaskStatus(t, e.target.value as TaskStatus)}>{TASK_STATUSES.map((s) => <option key={s}>{s}</option>)}</select>
                </li>))}</ul>
            ) : <p className="text-sm font-semibold text-ink-3">Actions from earlier meetings stay here until they are completed or formally deferred.</p>}
          </Card>
        </div>
      </div>
    </div>
  );
}

export type { Live as LiveMeeting };
