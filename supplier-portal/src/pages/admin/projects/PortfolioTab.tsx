import { useState } from 'react';
import { Plus } from 'lucide-react';
import clsx from 'clsx';
import { fmtDate, milestoneLate, personName, projectHealth, rand, riskScore } from './logic';
import { Bar, Card, PersonBadge, Pill } from './ui';
import { useEditor } from './editors';
import type { Workspace } from './types';

type Section = 'projects' | 'milestones' | 'risks';

export default function PortfolioTab({ ws }: { ws: Workspace }) {
  const edit = useEditor();
  const [section, setSection] = useState<Section>('projects');
  const fw = ws.framework[0];
  const newKind = section === 'projects' ? 'project' : section === 'milestones' ? 'milestone' : 'risk';

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div role="group" aria-label="Portfolio section" className="inline-flex flex-wrap gap-1 p-1 bg-brand-blue-light/60 rounded-full">
          {(['projects', 'milestones', 'risks'] as Section[]).map((s) => (
            <button key={s} aria-pressed={section === s} onClick={() => setSection(s)}
              className={clsx('px-3.5 py-1.5 rounded-full text-sm font-bold', section === s ? 'bg-brand-blue text-white' : 'text-brand-blue hover:bg-white/70')}>
              {s === 'projects' ? 'Projects' : s === 'milestones' ? 'Milestones' : 'Risk register'}
            </button>
          ))}
        </div>
        <button className="btn-primary" onClick={() => edit(newKind)}><Plus size={16} /> New {newKind}</button>
      </div>

      {section === 'projects' && (
        <div className="grid md:grid-cols-2 xl:grid-cols-3 gap-4">
          {ws.project.map((p) => {
            const h = projectHealth(p, ws); const ms = ws.milestone.filter((m) => m.projectId === p.id);
            const nextMs = ms.filter((m) => m.status !== 'Completed').sort((a, b) => (a.due || '9').localeCompare(b.due || '9'))[0];
            return (
              <button key={p.id} onClick={() => edit('project', p)} className="card-hover p-5 text-left flex flex-col gap-3">
                <div className="flex items-start justify-between gap-3"><div className="min-w-0"><p className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">{p.programme || 'Project'}</p><h3 className="text-lg font-black text-gray-900 leading-tight">{p.name}</h3></div><Pill status={h.rag} /></div>
                {p.purpose && <p className="text-sm text-ink-2">{p.purpose}</p>}
                <div className="flex items-center gap-3"><div className="flex-1"><Bar pct={h.pct} /></div><span className="text-sm font-black text-gray-900">{h.pct}%</span></div>
                <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                  <div><dt className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">Lead</dt><dd><PersonBadge people={ws.person} id={p.lead} showName size="sm" /></dd></div>
                  <div><dt className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">Tasks</dt><dd className="font-bold text-gray-900">{h.open} open / {h.total}</dd></div>
                  <div><dt className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">Milestones</dt><dd className="font-bold text-gray-900">{ms.filter((m) => m.status === 'Completed').length}/{ms.length} done</dd></div>
                  <div><dt className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">End date</dt><dd className={clsx('font-bold', p.endDate ? 'text-gray-900' : 'text-accent-700')}>{p.endDate ? fmtDate(p.endDate) : 'Not set'}</dd></div>
                  <div><dt className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">Funder</dt><dd className="font-bold text-gray-900">{p.funder || '—'}</dd></div>
                  <div><dt className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">Budget</dt><dd className={clsx('font-bold', p.budget ? 'text-gray-900' : 'text-accent-700')}>{p.budget ? `${rand(p.budget)}${p.spent ? ` · ${rand(p.spent)} spent` : ''}` : 'Not set'}</dd></div>
                </dl>
                <ul className="bg-surface rounded-2xl px-3 py-2 text-xs font-semibold text-ink-2 space-y-0.5">{h.why.map((w) => <li key={w}>• {w}</li>)}</ul>
                {nextMs && <p className="text-xs font-semibold text-ink-3">Next milestone: <b className="text-gray-900">{nextMs.title}</b> · {nextMs.due ? fmtDate(nextMs.due) : 'no date'}</p>}
                <div className="flex flex-wrap gap-1.5">{p.outcomes.map((o) => <span key={o} className="tag-blue" title={fw?.outcomes.find((x) => x.id === o)?.name}>Serves {o}</span>)}</div>
              </button>
            );
          })}
        </div>
      )}

      {section === 'milestones' && ws.project.map((p) => {
        const ms = ws.milestone.filter((m) => m.projectId === p.id);
        return (
          <section key={p.id} className="card overflow-hidden">
            <h3 className="section-title px-5 pt-4 pb-2">{p.name}</h3>
            <div className="overflow-x-auto"><table className="w-full">
              <thead><tr><th className="table-header">Milestone</th><th className="table-header">Due</th><th className="table-header">Status</th><th className="table-header">Evidence of completion</th></tr></thead>
              <tbody>{ms.length ? ms.map((m) => { const late = milestoneLate(m); return (
                <tr key={m.id} className="table-row cursor-pointer" onClick={() => edit('milestone', m)}>
                  <td className="table-cell font-bold text-gray-900">{m.title}</td>
                  <td className={clsx('table-cell whitespace-nowrap font-bold', late ? 'text-brand-red' : m.due ? '' : 'text-accent-700')}>{m.due ? fmtDate(m.due) : 'Not set'}</td>
                  <td className="table-cell"><Pill status={late && m.status !== 'Missed' ? 'Overdue' : m.status} /></td>
                  <td className="table-cell text-ink-3">{m.evidence || '—'}</td>
                </tr>); }) : <tr><td className="table-cell text-ink-3" colSpan={4}>No milestones yet.</td></tr>}</tbody>
            </table></div>
          </section>
        );
      })}

      {section === 'risks' && (() => {
        const open = ws.risk.filter((r) => r.status !== 'Closed');
        const tone = (s: number) => (s >= 6 ? 'bg-brand-red-light text-brand-red' : s >= 3 ? 'bg-brand-yellow-light text-accent-700' : 'bg-brand-green-light text-brand-green');
        return (
          <div className="grid lg:grid-cols-[1.4fr_1fr] gap-5">
            <Card title="Risk register" action={<span className="text-xs font-bold text-ink-3">{open.length} open</span>}>
              <ul className="space-y-2">
                {[...ws.risk].sort((a, b) => riskScore(b) - riskScore(a)).map((r) => (
                  <li key={r.id}><button className="w-full text-left flex items-center gap-3 p-3 rounded-2xl bg-surface hover:bg-brand-blue-light/50" onClick={() => edit('risk', r)}>
                    <span className={clsx('w-9 h-9 rounded-xl grid place-items-center font-black flex-shrink-0', tone(riskScore(r)))}>{riskScore(r)}</span>
                    <div className="flex-1 min-w-0"><p className="text-sm font-extrabold text-gray-900">{r.title}</p><p className="text-xs font-semibold text-ink-3">{ws.project.find((p) => p.id === r.projectId)?.short ?? '—'} · Owner {personName(ws.person, r.owner)} · Mitigation: {r.mitigation || 'none recorded'}</p></div>
                    <Pill status={r.status} /></button></li>
                ))}
              </ul>
            </Card>
            <Card title="Heat map" action={<span className="text-xs font-bold text-ink-3">Likelihood × impact</span>}>
              <div className="grid grid-cols-[auto_1fr] gap-2 items-center">
                <span className="[writing-mode:vertical-rl] rotate-180 text-[11px] font-extrabold uppercase tracking-wider text-ink-3 text-center">Likelihood</span>
                <div className="grid grid-cols-[44px_repeat(3,1fr)] gap-1.5">
                  {[3, 2, 1].map((l) => [
                    <span key={`l${l}`} className="text-[11px] font-extrabold text-ink-3 text-center self-center">{['', 'Low', 'Med', 'High'][l]}</span>,
                    ...[1, 2, 3].map((i) => { const n = open.filter((r) => r.likelihood === l && r.impact === i).length; return <div key={`${l}${i}`} className={clsx('aspect-[1.5] rounded-xl grid place-items-center text-xl font-black', tone(l * i))}>{n || ''}</div>; }),
                  ])}
                  <span />{['Low', 'Med', 'High'].map((x) => <span key={x} className="text-[11px] font-extrabold text-ink-3 text-center">{x}</span>)}
                </div>
                <span /><span className="text-center text-[11px] font-extrabold uppercase tracking-wider text-ink-3">Impact</span>
              </div>
              <p className="text-xs font-semibold text-ink-3 mt-3">A risk scoring 9 turns its project red on the Projects view.</p>
            </Card>
          </div>
        );
      })()}
    </div>
  );
}
