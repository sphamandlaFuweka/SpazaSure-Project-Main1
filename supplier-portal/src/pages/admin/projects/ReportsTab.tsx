import { useRef, useState, type ReactNode } from 'react';
import toast from 'react-hot-toast';
import { Copy } from 'lucide-react';
import clsx from 'clsx';
import {
  AUTOMATIONS, daysTo, fmtDate, fmtIndicator, indicatorStatus, isOpen, isOverdue, itemOpen, parseDate, personName, projectHealth,
  rand, riskScore, ruleOn, today,
} from './logic';
import type { WorkspaceApi } from './useWorkspace';
import type { Indicator, Saved, Workspace } from './types';

type ReportType = 'weekly' | 'project' | 'impact' | 'event';
const TYPES: Record<ReportType, { name: string; audience: string; schedule: string }> = {
  weekly: { name: 'Weekly management report', audience: 'Team', schedule: 'Mondays 07:00, emailed to the team' },
  project: { name: 'Monthly project report', audience: 'Chair & funders', schedule: '1st working day, to the chair for review' },
  impact: { name: 'Quarterly impact report', audience: 'Board & funders', schedule: 'First week after quarter end' },
  event: { name: 'Event report', audience: 'Team & partners', schedule: 'Readiness before an event; results within 7 days after' },
};

const H = ({ children }: { children: ReactNode }) => <h3 className="text-base font-black text-brand-blue mt-6 mb-2">{children}</h3>;
const List = ({ items }: { items: ReactNode[] }) => items.length
  ? <ul className="list-disc pl-5 space-y-1">{items.map((x, i) => <li key={i}>{x}</li>)}</ul>
  : <p className="text-ink-3">None.</p>;

function IndicatorTable({ ws, rows }: { ws: Workspace; rows: Saved<Indicator>[] }) {
  const fw = ws.framework[0];
  if (!fw || !rows.length) return <p className="text-ink-3">No indicators linked.</p>;
  return (
    <table className="w-full text-sm border border-brand-blue-light rounded-xl overflow-hidden">
      <thead><tr>{['Indicator', 'Latest', 'Target', 'Status'].map((h) => <th key={h} className="table-header py-2">{h}</th>)}</tr></thead>
      <tbody>{rows.map((k) => { const s = indicatorStatus(k, fw.yearStart, fw.targetDate); return (
        <tr key={k.id} className="border-t border-brand-blue-light"><td className="px-3 py-1.5">{k.code} {k.name}</td><td className="px-3 py-1.5 font-bold">{fmtIndicator(k, s.latest)}</td><td className="px-3 py-1.5">{fmtIndicator(k, k.target)}</td><td className="px-3 py-1.5">{s.state}</td></tr>); })}</tbody>
    </table>
  );
}

function ReportBody({ ws, type, projectId }: { ws: Workspace; type: ReportType; projectId: string }) {
  const now = today(); const weekAgo = new Date(now.getTime() - 7 * 864e5); const fw = ws.framework[0];
  const who = (id: string) => personName(ws.person, id);

  if (type === 'weekly') {
    const done = ws.task.filter((t) => t.status === 'Completed' && t.doneAt && parseDate(t.doneAt)! >= weekAgo);
    const od = ws.task.filter(isOverdue); const lateItems = ws.initiative.filter((i) => itemOpen(i) && i.due && daysTo(i.due) < 0);
    const bl = ws.task.filter((t) => isOpen(t) && (t.blocker || t.status === 'Blocked'));
    const decisions = ws.meetingRecord.filter((r) => parseDate(r.date)! >= weekAgo).flatMap((r) => r.decisions.map((d) => `${d} (${ws.meeting.find((m) => m.key === r.meetingKey)?.day ?? ''})`));
    const nextEvent = ws.project.filter((p) => p.endDate && daysTo(p.endDate) >= 0).sort((a, b) => a.endDate.localeCompare(b.endDate))[0];
    return (<>
      <h2 className="text-2xl font-black text-gray-900">Weekly management report</h2>
      <p className="text-ink-3 font-semibold">Week of {now.toLocaleDateString('en-ZA', { day: 'numeric', month: 'long', year: 'numeric' })} · prepared from the portal</p>
      <H>Summary</H>
      <List items={[
        `${done.length} tasks completed in the last 7 days; ${ws.task.filter(isOpen).length} open across ${ws.project.length} projects.`,
        `${od.length + lateItems.length} overdue items and ${bl.length} open blockers.`,
        ...(nextEvent ? [`${nextEvent.name} in ${daysTo(nextEvent.endDate)} days; ${projectHealth(nextEvent, ws).pct}% average completion.`] : []),
        `Open pipeline: ${rand(ws.opportunity.filter((o) => !['Won', 'Lost'].includes(o.stage)).reduce((a, o) => a + (Number(o.value) || 0), 0))} across ${ws.opportunity.length} opportunities.`,
      ]} />
      <H>This week’s top priorities</H><List items={ws.task.filter((t) => t.top).map((t) => `${t.title} (${who(t.owner)}, ${t.pct}%)`)} />
      <H>Blockers needing support</H><List items={bl.map((t) => `${t.title}: ${t.blocker || 'blocked'} (${who(t.owner)})`)} />
      <H>Overdue</H><List items={[...od, ...lateItems].map((t) => `${t.title}, due ${fmtDate(t.due)} (${who(t.owner)})`)} />
      <H>Project health</H><List items={ws.project.map((p) => { const h = projectHealth(p, ws); return <><b>{p.short || p.name}</b>: {h.rag}. {h.why.join('; ')}</>; })} />
      <H>Decisions this week</H><List items={decisions} />
    </>);
  }

  if (type === 'project') {
    const p = ws.project.find((x) => x.id === projectId) ?? ws.project[0];
    if (!p) return <p>No projects yet.</p>;
    const h = projectHealth(p, ws); const ms = ws.milestone.filter((m) => m.projectId === p.id);
    const outputsServing = fw?.outputs.filter((o) => p.outcomes.includes(o.outcome)).map((o) => o.id) ?? [];
    const inds = ws.indicator.filter((k) => p.outcomes.includes(k.link) || outputsServing.includes(k.link));
    return (<>
      <h2 className="text-2xl font-black text-gray-900">{p.name}: monthly report</h2>
      <p className="text-ink-3 font-semibold">{now.toLocaleDateString('en-ZA', { month: 'long', year: 'numeric' })} · {p.programme}{p.funder ? ` · ${p.funder}` : ''}</p>
      <H>Overall status: {h.rag}</H><List items={h.why} /><p className="mt-1">{h.pct}% average task completion · {h.open} of {h.total} tasks open.</p>
      <H>Milestones</H>
      <table className="w-full text-sm border border-brand-blue-light"><thead><tr>{['Milestone', 'Due', 'Status'].map((x) => <th key={x} className="table-header py-2">{x}</th>)}</tr></thead>
        <tbody>{ms.map((m) => <tr key={m.id} className="border-t border-brand-blue-light"><td className="px-3 py-1.5">{m.title}</td><td className="px-3 py-1.5">{m.due ? fmtDate(m.due) : 'Not set'}</td><td className="px-3 py-1.5">{m.status}</td></tr>)}</tbody></table>
      <H>Results this month</H><IndicatorTable ws={ws} rows={inds} />
      <H>Top risks</H><List items={ws.risk.filter((r) => r.projectId === p.id && r.status !== 'Closed').sort((a, b) => riskScore(b) - riskScore(a)).slice(0, 4).map((r) => `${r.title} (score ${riskScore(r)}). Mitigation: ${r.mitigation || 'none recorded'}`)} />
      <H>What we learned</H><List items={ws.lesson.filter((l) => l.projectId === p.id).map((l) => `${l.title}. Change: ${l.action || 'not decided'}`)} />
      <H>Budget</H><p>{p.budget ? `${rand(p.budget)} budget, ${rand(p.spent ?? 0)} spent.` : 'Budget not set for this project yet.'}</p>
      <H>Next month</H><List items={ms.filter((m) => m.status !== 'Completed').map((m) => m.title)} />
    </>);
  }

  if (type === 'impact') {
    if (!fw) return <p>No results framework yet.</p>;
    const q = Math.floor(now.getMonth() / 3) + 1;
    return (<>
      <h2 className="text-2xl font-black text-gray-900">Quarterly impact report</h2>
      <p className="text-ink-3 font-semibold">Q{q} {now.getFullYear()} to date · results framework</p>
      <H>Impact goal</H><p>{fw.impact.name}.</p>
      {fw.outcomes.map((o) => { const outs = fw.outputs.filter((x) => x.outcome === o.id).map((x) => x.id); return (
        <div key={o.id}><H>{o.id}: {o.name}</H><IndicatorTable ws={ws} rows={ws.indicator.filter((k) => k.link === o.id || outs.includes(k.link))} /></div>); })}
      <H>Evaluation progress</H><List items={ws.evaluation.map((e) => `${e.name}: ${e.status} (${e.timing})`)} />
      <H>Learning and adaptation</H><List items={ws.lesson.map((l) => `${l.title}. ${l.status}: ${l.action || 'not decided'}`)} />
      <H>Data quality notes</H><List items={[
        `${ws.indicator.filter((k) => k.baseline == null).length} indicators still need a baseline.`,
        `${ws.indicator.filter((k) => !Object.keys(k.actuals ?? {}).length).length} indicators have no results recorded yet.`,
        'Breakdowns by area, gender and youth ownership are planned but not yet collected.',
      ]} />
    </>);
  }

  const p = ws.project.find((x) => x.endDate) ?? ws.project[0];
  if (!p) return <p>No projects yet.</p>;
  const ts = ws.task.filter((t) => t.projectId === p.id);
  return (<>
    <h2 className="text-2xl font-black text-gray-900">{p.short || p.name}: event report</h2>
    <p className="text-ink-3 font-semibold">{p.name} · {fmtDate(p.endDate)} · {p.endDate && daysTo(p.endDate) >= 0 ? `readiness, ${daysTo(p.endDate)} days out` : 'results'}</p>
    <H>Readiness</H><p>{ts.filter((t) => !isOpen(t)).length} of {ts.length} deliverables complete.</p>
    <List items={ws.person.filter((x) => ts.some((t) => t.owner === x.id)).map((x) => `${x.name}: ${ts.filter((t) => t.owner === x.id && !isOpen(t)).length}/${ts.filter((t) => t.owner === x.id).length} done`)} />
    <H>Critical items</H><List items={ts.filter((t) => t.priority === 'Critical').map((t) => `${t.title}: ${t.status}${t.next ? `. Next: ${t.next}` : ''}`)} />
    <H>Risks</H><List items={ws.risk.filter((r) => r.projectId === p.id && r.status !== 'Closed').map((r) => `${r.title} (score ${riskScore(r)})`)} />
    <H>Results to capture on the day</H><List items={[
      'Attendance by type (shop owners, suppliers, officials), from the registration sheet',
      'Shops registered on SpazaSure at the event (feeds the shops onboarded indicator)',
      'Feedback survey responses (see the Evaluation plan)',
      'Supplier leads from the FMCG exhibitors (add them to Opportunities)',
    ]} />
  </>);
}

export default function ReportsTab({ api }: { api: WorkspaceApi }) {
  const { ws } = api;
  const [type, setType] = useState<ReportType>('weekly');
  const [projectId, setProjectId] = useState(ws.project[0]?.id ?? '');
  const docRef = useRef<HTMLDivElement>(null);
  const settings = ws.settings[0];

  const copy = async () => {
    const text = docRef.current?.innerText ?? '';
    try { await navigator.clipboard.writeText(text); toast.success('Report copied'); }
    catch { toast.error('Copy was blocked by the browser. Select the text and copy it instead.'); }
  };
  const toggle = (key: string, on: boolean) => { if (settings) api.update('settings', settings, { automations: { ...settings.automations, [key]: on } }); };

  return (
    <div className="grid lg:grid-cols-[320px_1fr] gap-5 items-start">
      <div className="space-y-5">
        <section className="card p-5 space-y-2">
          <h3 className="section-title mb-1">Report type</h3>
          {(Object.keys(TYPES) as ReportType[]).map((k) => (
            <button key={k} aria-pressed={type === k} onClick={() => setType(k)}
              className={clsx('w-full text-left rounded-2xl border-2 px-3.5 py-2.5', type === k ? 'border-brand-blue bg-brand-blue-light/60' : 'border-brand-blue-light hover:border-brand-blue/40')}>
              <span className="block text-sm font-extrabold text-gray-900">{TYPES[k].name}</span>
              <span className="block text-xs font-semibold text-ink-3">{TYPES[k].audience} · {TYPES[k].schedule}</span>
            </button>
          ))}
          {type === 'project' && (
            <div className="pt-2"><label htmlFor="pm-report-project" className="label">Project</label>
              <select id="pm-report-project" className="select" value={projectId} onChange={(e) => setProjectId(e.target.value)}>{ws.project.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select></div>
          )}
        </section>
        <section className="card p-5 space-y-3">
          <h3 className="section-title">Schedule</h3>
          {AUTOMATIONS.filter((a) => ['reportweekly', 'reportmonthly', 'inddata'].includes(a.key)).map((a) => (
            <label key={a.key} className="flex items-start gap-3 cursor-pointer">
              <input type="checkbox" className="mt-1 w-4 h-4 accent-[#25449A]" checked={ruleOn(ws, a.key)} onChange={(e) => toggle(a.key, e.target.checked)} />
              <span><span className="block text-sm font-extrabold text-gray-900">{a.name}</span><span className="block text-xs font-semibold text-ink-3">{a.when} → {a.to}</span></span>
            </label>
          ))}
          <p className="text-xs font-semibold text-ink-3">Scheduled emails start once the notification service is connected (stage 2). Until then, copy the report from here.</p>
        </section>
      </div>
      <section className="card p-6">
        <div className="flex items-center justify-between gap-3 mb-2"><h3 className="section-title">Preview</h3><button className="btn-secondary" onClick={copy}><Copy size={15} /> Copy text</button></div>
        <div ref={docRef} className="text-sm text-ink leading-relaxed max-w-3xl"><ReportBody ws={ws} type={type} projectId={projectId} /></div>
      </section>
    </div>
  );
}
