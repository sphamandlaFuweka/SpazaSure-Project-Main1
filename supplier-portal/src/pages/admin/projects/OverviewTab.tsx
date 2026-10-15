import { Link } from 'react-router-dom';
import { AlertTriangle, CalendarClock, CheckCircle2, Flame, Wallet } from 'lucide-react';
import { StatCard } from '../../../components/ui';
import {
  daysTo, fmtDate, fmtDay, fmtIndicator, googleCalendarUrl, indicatorStatus, isOpen, isOverdue, isPlaceholderLink, itemOpen,
  nextOccurrence, projectHealth, rand, today, upcomingSends,
} from './logic';
import ScheduleList from './ScheduleList';
import { Bar, Card, PersonBadge, Pill, Prio, TONE_HEX, Warn, toneOf } from './ui';
import { useEditor } from './editors';
import type { Workspace } from './types';

export default function OverviewTab({ ws, onRunMeeting }: { ws: Workspace; onRunMeeting: (key: string) => void }) {
  const edit = useEditor();
  const fw = ws.framework[0];
  const open = ws.task.filter(isOpen);
  const critical = open.filter((t) => t.priority === 'Critical');
  const blockers = open.filter((t) => t.blocker || t.status === 'Blocked');
  const noDue = open.filter((t) => !t.due);
  const pipeline = ws.opportunity.filter((o) => !['Won', 'Lost'].includes(o.stage)).reduce((a, o) => a + (Number(o.value) || 0), 0);
  const next = ws.meeting.map((m) => ({ m, at: nextOccurrence(m) })).sort((a, b) => a.at.getTime() - b.at.getTime())[0];
  const event = ws.project.filter((p) => p.endDate && daysTo(p.endDate) >= 0).sort((a, b) => a.endDate.localeCompare(b.endDate))[0];
  const lateItems = ws.initiative.filter((i) => itemOpen(i) && i.due && daysTo(i.due) < 0);
  const attention = [
    ...lateItems.map((i) => ({ key: i.id, title: i.title, why: `Overdue since ${fmtDate(i.due)}`, owner: i.owner, prio: i.priority, status: i.status, open: () => edit('initiative', i) })),
    ...open.filter(isOverdue).map((t) => ({ key: t.id, title: t.title, why: `Overdue since ${fmtDate(t.due)}`, owner: t.owner, prio: t.priority, status: t.status, open: () => edit('task', t) })),
    ...blockers.map((t) => ({ key: t.id, title: t.title, why: `Blocker: ${t.blocker || 'marked blocked'}`, owner: t.owner, prio: t.priority, status: t.status, open: () => edit('task', t) })),
    ...critical.map((t) => ({ key: t.id, title: t.title, why: t.next || 'No next action recorded', owner: t.owner, prio: t.priority, status: t.status, open: () => edit('task', t) })),
  ].filter((x, i, a) => a.findIndex((y) => y.key === x.key) === i).slice(0, 7);
  const statuses = fw ? ws.indicator.map((k) => ({ k, s: indicatorStatus(k, fw.yearStart, fw.targetDate) })) : [];
  const count = (s: string) => statuses.filter((x) => x.s.state === s).length;
  const soon = upcomingSends(ws, 1).filter((s) => s.at < new Date(today().getTime() + 2 * 864e5));

  return (
    <div className="space-y-5">
      {next && (
        <section className="hero-banner p-6 flex flex-wrap items-center justify-between gap-4">
          <div className="min-w-0">
            <span className="hero-pill">Next meeting</span>
            <h2 className="text-2xl font-black text-white mt-2">{next.m.name}</h2>
            <p className="text-sm font-semibold text-white/80">{fmtDay(next.at)} at {next.m.time} SAST · {next.m.attendees.length} attendees</p>
            {isPlaceholderLink(next.m) && <p className="text-xs font-bold text-brand-yellow mt-1">The Meet link is still the placeholder from the workbook. Set the real one in Meetings.</p>}
          </div>
          <div className="flex flex-wrap gap-2">
            <a className="btn-accent" href={next.m.link} target="_blank" rel="noreferrer">Join Meet</a>
            <a className="inline-flex items-center gap-2 px-4 py-2.5 rounded-full font-bold text-sm text-white border border-white/50 hover:border-white" href={googleCalendarUrl(next.m, ws.person)} target="_blank" rel="noreferrer">Add to Google Calendar</a>
            <button className="inline-flex items-center gap-2 px-4 py-2.5 rounded-full font-bold text-sm text-white border border-white/50 hover:border-white" onClick={() => onRunMeeting(next.m.key)}>Run meeting</button>
          </div>
        </section>
      )}

      {event && (() => {
        const h = projectHealth(event, ws);
        return (
          <section className="card p-5 flex flex-wrap items-center gap-6">
            <div className="min-w-0 flex-1"><p className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">Next deadline</p><h3 className="text-lg font-black text-gray-900">{event.name}</h3><p className="text-sm font-semibold text-ink-3">{fmtDate(event.endDate)} · {h.total} deliverables · <Pill status={h.rag} /></p></div>
            <p className="text-5xl font-black text-brand-blue tabular-nums">{daysTo(event.endDate)}<span className="text-sm font-bold text-ink-3 ml-2">days to go</span></p>
            <div className="flex-1 min-w-[180px]"><div className="flex justify-between text-xs font-bold text-ink-3 mb-1.5"><span>Average completion</span><span className="text-gray-900">{h.pct}%</span></div><Bar pct={h.pct} /></div>
          </section>
        );
      })()}

      <div className="grid grid-cols-2 lg:grid-cols-5 gap-3">
        <StatCard title="Tasks completed" value={`${ws.task.length - open.length}/${ws.task.length}`} icon={<CheckCircle2 />} variant="green" description="All projects" />
        <StatCard title="Critical open" value={critical.length} icon={<Flame />} variant={critical.length ? 'red' : 'default'} description="Priority: critical" />
        <StatCard title="Open blockers" value={blockers.length} icon={<AlertTriangle />} variant={blockers.length ? 'amber' : 'default'} description="Need support" />
        <StatCard title="No due date" value={noDue.length} icon={<CalendarClock />} description="Breaks the deadline rule" />
        <StatCard title="Open pipeline" value={rand(pipeline)} icon={<Wallet />} variant="blue" description={`${ws.opportunity.length} opportunities`} />
      </div>

      <div className="grid lg:grid-cols-2 gap-5">
        <Card title="Portfolio health" action={<Link to="/admin/projects/portfolio" className="btn-ghost">Portfolio →</Link>}>
          <ul className="space-y-2">
            {ws.project.map((p) => { const h = projectHealth(p, ws); return (
              <li key={p.id}><button className="w-full text-left flex items-center gap-3 p-3 rounded-2xl bg-surface hover:bg-brand-blue-light/50" onClick={() => edit('project', p)}>
                <div className="flex-1 min-w-0"><p className="text-sm font-extrabold text-gray-900">{p.name}</p><p className="text-xs font-semibold text-ink-3">{h.why[0]}</p></div>
                <span className="text-sm font-black text-gray-900">{h.pct}%</span><Pill status={h.rag} /></button></li>
            ); })}
          </ul>
        </Card>
        <Card title="Impact snapshot" action={<Link to="/admin/projects/impact" className="btn-ghost">Impact →</Link>}>
          <p className="text-xs font-semibold text-ink-3">{statuses.length} indicators · {count('On track')} on track, {count('At risk')} at risk, {count('Off track')} off track, {count('No data')} waiting for data</p>
          <div className="flex h-3.5 rounded-full overflow-hidden bg-brand-blue-light/70 my-3">
            {(['On track', 'At risk', 'Off track', 'No data'] as const).map((s) => count(s) ? <i key={s} title={`${s}: ${count(s)}`} style={{ width: `${(count(s) / statuses.length) * 100}%`, background: TONE_HEX[toneOf(s)] }} /> : null)}
          </div>
          <ul className="space-y-2">
            {statuses.filter((x) => x.s.state === 'Off track' || x.s.state === 'At risk').slice(0, 3).map(({ k, s }) => (
              <li key={k.id}><button className="w-full text-left flex items-center gap-3 p-3 rounded-2xl bg-surface hover:bg-brand-blue-light/50" onClick={() => edit('indicator', k)}>
                <div className="flex-1 min-w-0"><p className="text-sm font-extrabold text-gray-900">{k.name}</p><p className="text-xs font-semibold text-ink-3">{fmtIndicator(k, s.latest)} vs target {fmtIndicator(k, k.target)}</p></div><Pill status={s.state} /></button></li>
            ))}
            {count('No data') === statuses.length && statuses.length > 0 && <Warn>No results recorded yet. Open an indicator on the Impact tab to enter this month’s figure.</Warn>}
          </ul>
        </Card>
      </div>

      <div className="grid lg:grid-cols-[1.4fr_1fr] gap-5">
        <Card title="Needs attention" action={<Link to="/admin/projects/tasks" className="btn-ghost">All tasks →</Link>}>
          {attention.length ? (
            <ul className="space-y-2">
              {attention.map((a) => (
                <li key={a.key}><button className="w-full text-left flex items-center gap-3 p-3 rounded-2xl bg-surface hover:bg-brand-blue-light/50" onClick={a.open}>
                  <PersonBadge people={ws.person} id={a.owner} />
                  <div className="flex-1 min-w-0"><p className="text-sm font-extrabold text-gray-900">{a.title}</p><p className="text-xs font-semibold text-ink-3 truncate">{a.why}</p></div>
                  <Prio p={a.prio} /><Pill status={a.status} /></button></li>
              ))}
            </ul>
          ) : <p className="text-sm font-semibold text-ink-3">Nothing urgent.</p>}
        </Card>
        <Card title="Reminders going out" action={<Link to="/admin/projects/reminders" className="btn-ghost">All →</Link>}>
          <p className="text-xs font-semibold text-ink-3 mb-3">Today and tomorrow</p>
          <ScheduleList sends={soon} people={ws.person} />
        </Card>
      </div>
    </div>
  );
}
