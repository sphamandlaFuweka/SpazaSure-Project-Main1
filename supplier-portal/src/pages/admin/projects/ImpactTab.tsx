import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { fmtDate, fmtIndicator, indicatorStatus, lastMonths, monthLabel, projectHealth } from './logic';
import { PersonBadge, Pill, Segmented, TONE_HEX, toneOf } from './ui';
import { useEditor } from './editors';
import type { Indicator, Saved, Workspace } from './types';

type Section = 'framework' | 'indicators' | 'evaluation' | 'learning';

function Trend({ k, state }: { k: Indicator; state: string }) {
  const months = lastMonths(3); const vals = months.map((m) => k.actuals?.[m]);
  const max = Math.max(k.target, ...vals.filter((v): v is number => v != null), 1);
  const h = 26; const colour = TONE_HEX[toneOf(state)];
  const ty = k.measure === 'rate' ? h - (k.target / max) * h : null;
  return (
    <svg width="76" height={h + 12} viewBox={`0 0 76 ${h + 12}`} role="img" aria-label={`${k.name}: ${months.map((m, i) => `${monthLabel(m)} ${fmtIndicator(k, vals[i])}`).join(', ')}`}>
      {vals.map((v, i) => { const bh = v == null ? 0 : Math.max(2, (v / max) * h); return <rect key={i} x={i * 25 + 2} y={h - bh} width={17} height={bh} rx={3} fill={v == null ? '#E8ECF8' : colour} opacity={i === 2 ? 1 : 0.55} />; })}
      {ty != null && <line x1={0} x2={76} y1={ty} y2={ty} stroke="#3A3A5E" strokeDasharray="3 3" strokeWidth={1} />}
      {months.map((m, i) => <text key={m} x={i * 25 + 10.5} y={h + 10} textAnchor="middle" fontSize={8.5} fontWeight={700} fill="#8888AA">{monthLabel(m).split(' ')[0]}</text>)}
    </svg>
  );
}

export default function ImpactTab({ ws }: { ws: Workspace }) {
  const edit = useEditor();
  const [section, setSection] = useState<Section>('framework');
  const fw = ws.framework[0];
  if (!fw) return <p className="text-sm font-semibold text-ink-3">No results framework yet.</p>;
  const status = (k: Saved<Indicator>) => indicatorStatus(k, fw.yearStart, fw.targetDate);
  const all = ws.indicator.map((k) => status(k).state);
  const count = (s: string) => all.filter((x) => x === s).length;
  const newKind = { framework: null, indicators: 'indicator', evaluation: 'evaluation', learning: 'lesson' }[section] as 'indicator' | 'evaluation' | 'lesson' | null;

  const dots = (link: string) => ws.indicator.filter((k) => k.link === link).map((k) => { const s = status(k); return (
    <button key={k.id} onClick={() => edit('indicator', k)} className="w-full flex items-center gap-2 text-left text-xs font-bold rounded-xl px-2.5 py-1.5 bg-brand-blue-light/60 hover:bg-brand-blue-light text-ink">
      <span className="w-2 h-2 rounded-full flex-shrink-0" style={{ background: TONE_HEX[toneOf(s.state)] }} />
      <span className="flex-1 min-w-0"><span className="text-brand-blue font-black">{k.code}</span> {k.name}</span>
      <span className="font-black text-gray-900 whitespace-nowrap">{fmtIndicator(k, s.latest)}</span>
    </button>); });

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
        {[['On track', 'text-brand-green'], ['At risk', 'text-accent-600'], ['Off track', 'text-brand-red'], ['No data', 'text-ink-3']].map(([s, c]) => (
          <div key={s} className="card p-4"><p className={`kpi-value ${c}`}>{count(s)}</p><p className="text-xs font-bold text-ink-3">{s === 'No data' ? 'Waiting for data' : `Indicators ${s.toLowerCase()}`}</p></div>
        ))}
        <div className="card p-4"><p className="kpi-value text-brand-blue">{ws.lesson.filter((l) => l.status === 'Adopted').length}/{ws.lesson.length}</p><p className="text-xs font-bold text-ink-3">Lessons acted on</p></div>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <Segmented label="Impact section" value={section} onChange={setSection} options={[
          { value: 'framework', label: 'Results framework' }, { value: 'indicators', label: 'Indicators' },
          { value: 'evaluation', label: 'Evaluation plan' }, { value: 'learning', label: 'Learning log' }]} />
        {newKind && <button className="btn-primary" onClick={() => edit(newKind)}><Plus size={16} /> {newKind === 'indicator' ? 'New indicator' : newKind === 'evaluation' ? 'New evaluation' : 'Log a lesson'}</button>}
      </div>

      {section === 'framework' && (
        <div className="space-y-4">
          {[
            { label: 'Impact', sub: 'Long-term change', body: (
              <div className="card-gradient p-5 space-y-2"><p className="text-[11px] font-black text-brand-yellow">{fw.impact.id}</p><p className="text-lg font-black text-white">{fw.impact.name}</p>
                <div className="space-y-1.5 [&_button]:bg-white/15 [&_button]:text-white [&_button:hover]:bg-white/25 [&_.text-brand-blue]:text-brand-yellow [&_.text-gray-900]:text-white">{dots(fw.impact.id)}</div></div>) },
            { label: 'Outcomes', sub: 'Changes for shops, consumers, suppliers', body: (
              <div className="grid sm:grid-cols-2 xl:grid-cols-4 gap-3">{fw.outcomes.map((o) => (
                <div key={o.id} className="card p-4 space-y-2"><p className="text-[11px] font-black text-brand-blue">{o.id}</p><p className="font-extrabold text-gray-900">{o.name}</p>{dots(o.id).length ? dots(o.id) : <p className="text-xs font-semibold text-ink-3">No outcome indicator yet</p>}</div>))}</div>) },
            { label: 'Outputs', sub: 'What we deliver', body: (
              <div className="grid sm:grid-cols-2 xl:grid-cols-3 gap-3">{fw.outputs.map((o) => (
                <div key={o.id} className="rounded-3xl bg-surface border border-brand-blue-light p-4 space-y-2"><p className="text-[11px] font-black text-brand-blue">{o.id} → {o.outcome}</p><p className="font-extrabold text-gray-900">{o.name}</p>{dots(o.id)}</div>))}</div>) },
            { label: 'Activities', sub: 'Projects in the portfolio', body: (
              <div className="grid sm:grid-cols-2 xl:grid-cols-3 gap-3">{ws.project.map((p) => { const h = projectHealth(p, ws); return (
                <Link key={p.id} to="/admin/projects/portfolio" className="card-hover p-4 space-y-2 block"><p className="text-[11px] font-black text-brand-blue">{p.outcomes.map((o) => `→ ${o}`).join(' ')}</p><p className="font-extrabold text-gray-900">{p.name}</p><div className="flex items-center gap-2"><Pill status={h.rag} /><span className="text-xs font-bold text-ink-3">{h.pct}% complete</span></div></Link>); })}</div>) },
          ].map((tier) => (
            <div key={tier.label} className="grid lg:grid-cols-[160px_1fr] gap-3 items-start">
              <div className="lg:pt-4"><p className="font-black text-gray-900">{tier.label}</p><p className="text-xs font-semibold text-ink-3">{tier.sub}</p></div>
              {tier.body}
            </div>
          ))}
          <p className="text-xs font-semibold text-ink-3">This framework is a draft built from SpazaSure’s positioning and the success metrics in the Product & Marketing tracker. Agree it with the team and funders before locking targets ({fmtDate(fw.targetDate)}).</p>
        </div>
      )}

      {section === 'indicators' && (
        <>
          <div className="card overflow-hidden"><div className="overflow-x-auto"><table className="w-full">
            <thead><tr>{['Indicator', 'Level', 'Baseline', 'Latest', 'Target', 'Trend', 'Status', 'Source · frequency', 'Owner'].map((h) => <th key={h} className="table-header">{h}</th>)}</tr></thead>
            <tbody>{ws.indicator.map((k) => { const s = status(k); return (
              <tr key={k.id} className="table-row cursor-pointer" onClick={() => edit('indicator', k)}>
                <td className="table-cell min-w-[230px]"><span className="text-[11px] font-black text-brand-blue">{k.code}</span> <span className="font-bold text-gray-900">{k.name}</span></td>
                <td className="table-cell">{k.level}</td>
                <td className="table-cell font-bold whitespace-nowrap">{k.baseline == null ? <span className="text-accent-700">Needed</span> : fmtIndicator(k, k.baseline)}</td>
                <td className="table-cell font-bold whitespace-nowrap">{fmtIndicator(k, s.latest)}{s.month && <span className="block text-[11px] font-semibold text-ink-3">{monthLabel(s.month)}</span>}</td>
                <td className="table-cell font-bold whitespace-nowrap">{fmtIndicator(k, k.target)}</td>
                <td className="table-cell"><Trend k={k} state={s.state} /></td>
                <td className="table-cell"><Pill status={s.state} /></td>
                <td className="table-cell text-xs font-semibold text-ink-3 min-w-[150px]">{k.source}<br />{k.frequency}</td>
                <td className="table-cell"><PersonBadge people={ws.person} id={k.owner} /></td>
              </tr>); })}</tbody>
          </table></div></div>
          <p className="text-xs font-semibold text-ink-3">Open an indicator to record a month’s result. Running counts are judged against a straight line to the target; rates against the target itself. Each indicator is broken down by area, owner gender and youth ownership. Platform indicators will fill themselves once connected to order and onboarding data.</p>
        </>
      )}

      {section === 'evaluation' && (
        <>
          <div className="grid md:grid-cols-2 gap-4">{ws.evaluation.map((e) => (
            <button key={e.id} onClick={() => edit('evaluation', e)} className="card-hover p-5 text-left space-y-3">
              <div className="flex items-start justify-between gap-3"><div><p className="text-[11px] font-extrabold uppercase tracking-wider text-ink-3">{e.type} · tests {e.link}</p><h3 className="text-lg font-black text-gray-900">{e.name}</h3></div><Pill status={e.status} /></div>
              <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                <div><dt className="text-[11px] font-extrabold uppercase text-ink-3">When</dt><dd className="font-bold text-gray-900">{e.timing}</dd></div>
                <div><dt className="text-[11px] font-extrabold uppercase text-ink-3">Owner</dt><dd><PersonBadge people={ws.person} id={e.owner} showName size="sm" /></dd></div>
                <div className="col-span-2"><dt className="text-[11px] font-extrabold uppercase text-ink-3">Key questions</dt><dd className="font-semibold text-ink">{e.questions}</dd></div>
                <div><dt className="text-[11px] font-extrabold uppercase text-ink-3">Method</dt><dd className="font-semibold text-ink">{e.method}</dd></div>
                <div><dt className="text-[11px] font-extrabold uppercase text-ink-3">Sample</dt><dd className="font-semibold text-ink">{e.sample}</dd></div>
              </dl>
            </button>))}</div>
          <p className="text-xs font-semibold text-ink-3">Baseline surveys must happen before the pilot outreach and the scanner launch, or the endline evaluation has nothing to compare against.</p>
        </>
      )}

      {section === 'learning' && (
        <ul className="space-y-2">
          {[...ws.lesson].sort((a, b) => b.date.localeCompare(a.date)).map((l) => (
            <li key={l.id}><button onClick={() => edit('lesson', l)} className="w-full card-hover p-4 text-left flex items-start gap-3">
              <PersonBadge people={ws.person} id={l.owner} />
              <div className="flex-1 min-w-0 space-y-0.5">
                <p className="font-extrabold text-gray-900">{l.title}</p>
                {l.evidence && <p className="text-xs font-semibold text-ink-3">Evidence: {l.evidence}</p>}
                <p className="text-xs font-semibold text-ink-3">What we’re changing: <b className="text-gray-900">{l.action || 'Not decided'}</b></p>
                <p className="text-xs font-semibold text-ink-3">{fmtDate(l.date)} · {l.source} · {ws.project.find((p) => p.id === l.projectId)?.short ?? 'All projects'}</p>
              </div>
              <Pill status={l.status} /></button></li>
          ))}
          {!ws.lesson.length && <p className="text-sm font-semibold text-ink-3">No lessons yet. Log one here or during any meeting.</p>}
        </ul>
      )}
    </div>
  );
}
