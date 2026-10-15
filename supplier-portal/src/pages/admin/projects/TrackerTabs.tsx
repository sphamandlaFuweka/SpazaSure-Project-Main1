import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Mail, MessageCircle, Plus, Search, Star } from 'lucide-react';
import clsx from 'clsx';
import { daysTo, fmtDate, isOverdue, itemOpen, mailtoLink, personName, rand, whatsappLink } from './logic';
import { Bar, PersonBadge, Pill, Prio, Segmented } from './ui';
import { useEditor } from './editors';
import type { WorkspaceApi } from './useWorkspace';
import { ITEM_AREAS, OPP_STAGES, PRIORITIES, TASK_STATUSES, type Saved, type Task, type TaskStatus } from './types';

// ── Tasks (reviewed Monday) ─────────────────────────────────────────────────────
export function TasksTab({ api }: { api: WorkspaceApi }) {
  const { ws } = api; const edit = useEditor();
  const [project, setProject] = useState('');
  const [q, setQ] = useState(''); const [owner, setOwner] = useState(''); const [status, setStatus] = useState(''); const [prio, setPrio] = useState('');
  const [mode, setMode] = useState<'table' | 'board'>('table');

  const rows = useMemo(() => ws.task.filter((t) =>
    (!project || t.projectId === project) && (!owner || t.owner === owner) && (!status || t.status === status) &&
    (!prio || (prio === 'none' ? !t.priority : t.priority === prio)) &&
    (!q || `${t.title} ${t.next} ${t.blocker}`.toLowerCase().includes(q.toLowerCase()))), [ws.task, project, owner, status, prio, q]);

  const setTaskStatus = (t: Saved<Task>, s: TaskStatus) =>
    api.update('task', t, s === 'Completed' ? { status: s, pct: 100, doneAt: t.doneAt || new Date().toISOString().slice(0, 10) } : { status: s });
  const toggleTop = (t: Saved<Task>) => {
    if (!t.top && ws.task.filter((x) => x.top).length >= 3) { toast.error('Pick at most 3 top priorities for the week'); return; }
    api.update('task', t, { top: !t.top });
  };
  const reminder = (t: Saved<Task>) => {
    const p = ws.person.find((x) => x.id === t.owner); const first = p?.name.split(' ')[0] ?? '';
    const subject = `Reminder: ${t.title}${t.due ? ` (due ${fmtDate(t.due)})` : ''}`;
    const body = `Hi ${first},\n\nA quick reminder about "${t.title}".\n\nStatus: ${t.status} (${t.pct}%)\n${t.due ? `Due: ${fmtDate(t.due)}` : 'Due date: not set yet, please agree one'}\n${t.blocker ? `Blocker: ${t.blocker}\n` : ''}Next action: ${t.next || 'please add one'}\n\nPlease update it in the admin portal before Monday's 09:00 Progress & Accountability meeting.\n\nSpazaSure`;
    return { email: p?.email ? mailtoLink(p.email, subject, body) : '', wa: p?.phone ? whatsappLink(p.phone, `Hi ${first}, reminder about "${t.title}"${t.due ? ` (due ${fmtDate(t.due)})` : ''}. Next action: ${t.next || 'please update'}.`) : '' };
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm font-semibold text-ink-3 max-w-2xl">Reviewed in Monday’s 09:00 Progress & Accountability meeting. Every action needs an owner and a deadline. Use the envelope to email the owner a reminder, or the chat icon to open WhatsApp with a message ready.</p>
        <button className="btn-primary" onClick={() => edit('task', null, project ? { projectId: project } : undefined)}><Plus size={16} /> New task</button>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <Segmented label="Project" value={project} onChange={setProject} options={[{ value: '', label: 'All projects' }, ...ws.project.map((p) => ({ value: p.id, label: p.short || p.name }))]} />
        <div className="relative flex-1 min-w-[180px] max-w-xs"><Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-ink-3" /><input aria-label="Search tasks" className="input pl-9 rounded-full" placeholder="Search tasks, next actions, blockers" value={q} onChange={(e) => setQ(e.target.value)} /></div>
        <select aria-label="Owner" className="select rounded-full w-auto" value={owner} onChange={(e) => setOwner(e.target.value)}><option value="">All owners</option>{ws.person.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select>
        <select aria-label="Status" className="select rounded-full w-auto" value={status} onChange={(e) => setStatus(e.target.value)}><option value="">All statuses</option>{TASK_STATUSES.map((s) => <option key={s}>{s}</option>)}</select>
        <select aria-label="Priority" className="select rounded-full w-auto" value={prio} onChange={(e) => setPrio(e.target.value)}><option value="">All priorities</option>{PRIORITIES.map((s) => <option key={s}>{s}</option>)}<option value="none">Not set</option></select>
        <span className="flex-1" />
        <Segmented label="View" value={mode} onChange={setMode} options={[{ value: 'table', label: 'Table' }, { value: 'board', label: 'Board' }]} />
      </div>

      {mode === 'table' ? (
        <div className="card overflow-hidden"><div className="overflow-x-auto"><table className="w-full">
          <thead><tr>
            <th className="table-header w-10" title="This week's top priorities"><Star size={13} /></th><th className="table-header">Task / deliverable</th><th className="table-header">Owner</th>
            <th className="table-header">Priority</th><th className="table-header">Due</th><th className="table-header">Status</th><th className="table-header">Progress</th><th className="table-header">Next action</th><th className="table-header">Remind</th>
          </tr></thead>
          <tbody>
            {rows.map((t) => { const r = reminder(t); return (
              <tr key={t.id} className="table-row cursor-pointer" onClick={() => edit('task', t)}>
                <td className="table-cell" onClick={(e) => e.stopPropagation()}><button aria-pressed={t.top} aria-label="Top priority this week" onClick={() => toggleTop(t)} className={t.top ? 'text-brand-yellow' : 'text-gray-300 hover:text-brand-yellow'}><Star size={17} fill={t.top ? 'currentColor' : 'none'} /></button></td>
                <td className="table-cell min-w-[220px]"><p className="font-bold text-gray-900">{t.title}</p>{!project && <p className="text-xs font-semibold text-ink-3">{ws.project.find((p) => p.id === t.projectId)?.short}</p>}{t.blocker && <p className="text-xs font-bold text-brand-red">Blocker: {t.blocker}</p>}</td>
                <td className="table-cell whitespace-nowrap"><PersonBadge people={ws.person} id={t.owner} showName size="sm" /></td>
                <td className="table-cell"><Prio p={t.priority} /></td>
                <td className={clsx('table-cell whitespace-nowrap font-bold tabular-nums', isOverdue(t) ? 'text-brand-red' : !t.due && 'text-accent-700')}>{t.due ? fmtDate(t.due) : 'Not set'}</td>
                <td className="table-cell" onClick={(e) => e.stopPropagation()}><select aria-label="Status" className="bg-transparent text-sm font-bold rounded-full px-2 py-1 border border-transparent hover:border-brand-blue-light" value={t.status} onChange={(e) => setTaskStatus(t, e.target.value as TaskStatus)}>{TASK_STATUSES.map((s) => <option key={s}>{s}</option>)}</select></td>
                <td className="table-cell"><div className="flex items-center gap-2"><div className="w-16"><Bar pct={t.pct} /></div><span className="text-xs font-bold tabular-nums">{t.pct}%</span></div></td>
                <td className={clsx('table-cell min-w-[170px]', !t.next && 'text-ink-3')}>{t.next || '—'}</td>
                <td className="table-cell" onClick={(e) => e.stopPropagation()}><div className="flex gap-1">
                  <a className={clsx('btn-icon border border-brand-blue-light', !r.email && 'opacity-30 pointer-events-none')} href={r.email || undefined} aria-label="Email reminder" title={r.email ? 'Email reminder' : 'No email on file'}><Mail size={15} /></a>
                  <a className={clsx('btn-icon border border-brand-blue-light text-brand-green', !r.wa && 'opacity-30 pointer-events-none')} href={r.wa || undefined} target="_blank" rel="noreferrer" aria-label="WhatsApp owner" title={r.wa ? 'WhatsApp owner' : 'No phone on file'}><MessageCircle size={15} /></a>
                </div></td>
              </tr>); })}
            {!rows.length && <tr><td colSpan={9} className="table-cell text-center text-ink-3 py-10">No tasks match these filters.</td></tr>}
          </tbody>
        </table></div></div>
      ) : (
        <div className="grid grid-flow-col auto-cols-[minmax(230px,1fr)] gap-3 overflow-x-auto pb-2">
          {TASK_STATUSES.map((s) => { const col = rows.filter((t) => t.status === s); return (
            <div key={s} className="bg-brand-blue-light/50 rounded-3xl p-3 flex flex-col gap-2 min-h-[140px]">
              <div className="flex items-center justify-between px-1"><Pill status={s} /><span className="text-xs font-extrabold text-ink-3">{col.length}</span></div>
              {col.map((t) => (
                <button key={t.id} onClick={() => edit('task', t)} className="bg-white rounded-2xl border border-brand-blue-light p-3 text-left hover:border-brand-blue/40 flex flex-col gap-2">
                  <span className="text-sm font-bold text-gray-900">{t.title}</span>
                  <span className="flex items-center justify-between gap-2 text-xs font-semibold text-ink-3"><span className="flex items-center gap-2"><PersonBadge people={ws.person} id={t.owner} size="sm" /><Prio p={t.priority} /></span>{fmtDate(t.due)}</span>
                </button>
              ))}
              {!col.length && <p className="text-xs font-semibold text-ink-3 px-1">None</p>}
            </div>); })}
        </div>
      )}
      <p className="text-xs font-semibold text-ink-3">{rows.length} of {ws.task.length} tasks. The star marks this week’s top priorities (3 at most).</p>
    </div>
  );
}

// ── Opportunities (reviewed Wednesday) ───────────────────────────────────────
export function OpportunitiesTab({ api }: { api: WorkspaceApi }) {
  const { ws } = api; const edit = useEditor();
  const openOpps = ws.opportunity.filter((o) => !['Won', 'Lost'].includes(o.stage));
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm font-semibold text-ink-3 max-w-2xl">Reviewed in Wednesday’s 10:00 Strategy & Opportunities meeting. Partner contacts are kept here, but partners are only ever emailed by a person, never by an automatic reminder.</p>
        <button className="btn-primary" onClick={() => edit('opportunity')}><Plus size={16} /> Add opportunity</button>
      </div>
      <div className="grid grid-cols-3 gap-3">
        <div className="card p-4"><p className="kpi-value text-brand-blue">{openOpps.length}</p><p className="text-xs font-bold text-ink-3">Open</p></div>
        <div className="card p-4"><p className="kpi-value text-brand-blue">{rand(ws.opportunity.filter((o) => o.stage !== 'Lost').reduce((a, o) => a + (Number(o.value) || 0), 0))}</p><p className="text-xs font-bold text-ink-3">Potential value (excl. lost)</p></div>
        <div className="card p-4"><p className="kpi-value text-brand-blue">{openOpps.filter((o) => o.due && daysTo(o.due) >= 0 && daysTo(o.due) <= 7).length}</p><p className="text-xs font-bold text-ink-3">Target date within 7 days</p></div>
      </div>
      <div className="grid grid-flow-col auto-cols-[minmax(220px,1fr)] gap-3 overflow-x-auto pb-2">
        {OPP_STAGES.map((s) => { const col = ws.opportunity.filter((o) => o.stage === s); return (
          <div key={s} className="bg-brand-blue-light/50 rounded-3xl p-3 flex flex-col gap-2 min-h-[140px]">
            <div className="flex items-center justify-between px-1"><Pill status={s} /><span className="text-xs font-extrabold text-ink-3">{col.length}</span></div>
            {col.map((o) => (
              <button key={o.id} onClick={() => edit('opportunity', o)} className="bg-white rounded-2xl border border-brand-blue-light p-3 text-left hover:border-brand-blue/40 flex flex-col gap-1.5">
                <span className="text-sm font-bold text-gray-900">{o.title}</span>
                <span className="text-xs font-semibold text-ink-3">{o.type} · {o.contact}</span>
                {o.next && <span className="text-xs font-semibold text-ink">Next: {o.next}</span>}
                <span className="flex items-center justify-between gap-2 text-xs font-bold text-ink-3"><span className="flex items-center gap-2"><PersonBadge people={ws.person} id={o.owner} size="sm" />{o.value ? rand(o.value) : ''}</span>{fmtDate(o.due)}</span>
              </button>
            ))}
            {!col.length && <p className="text-xs font-semibold text-ink-3 px-1">—</p>}
          </div>); })}
      </div>
    </div>
  );
}

// ── Product & marketing (reviewed Friday) ─────────────────────────────────────
export function ProductTab({ api }: { api: WorkspaceApi }) {
  const { ws } = api; const edit = useEditor();
  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm font-semibold text-ink-3 max-w-2xl">Reviewed in Friday’s 14:00 Product & Marketing meeting. Each item has a target customer and a success metric.</p>
        <button className="btn-primary" onClick={() => edit('initiative')}><Plus size={16} /> Add item</button>
      </div>
      {ITEM_AREAS.map((area) => { const rows = ws.initiative.filter((i) => i.area === area); return (
        <section key={area} className="card overflow-hidden">
          <h3 className="section-title px-5 pt-4 pb-2">{area} <span className="text-ink-3 font-bold">{rows.length}</span></h3>
          <div className="overflow-x-auto"><table className="w-full">
            <thead><tr>{['Item / initiative', 'Target customer', 'Priority', 'Status', 'Owner', 'Target', 'Success metric'].map((h) => <th key={h} className="table-header">{h}</th>)}</tr></thead>
            <tbody>{rows.map((i) => { const late = itemOpen(i) && i.due && daysTo(i.due) < 0; return (
              <tr key={i.id} className="table-row cursor-pointer" onClick={() => edit('initiative', i)}>
                <td className="table-cell min-w-[220px]"><p className="font-bold text-gray-900">{i.title}</p>{i.next && <p className="text-xs font-semibold text-ink-3">Next: {i.next}</p>}</td>
                <td className="table-cell">{i.customer}</td><td className="table-cell"><Prio p={i.priority} /></td><td className="table-cell"><Pill status={i.status} /></td>
                <td className="table-cell whitespace-nowrap">{i.owner ? <PersonBadge people={ws.person} id={i.owner} showName size="sm" /> : <span className="text-xs font-bold text-accent-700">No owner</span>}</td>
                <td className={clsx('table-cell whitespace-nowrap font-bold', late ? 'text-brand-red' : !i.due && 'text-accent-700')}>{i.due ? fmtDate(i.due) : 'No date'}{late && <span className="block text-[11px]">overdue</span>}</td>
                <td className="table-cell min-w-[160px]">{i.metric}</td>
              </tr>); })}
              {!rows.length && <tr><td colSpan={7} className="table-cell text-ink-3">No {area.toLowerCase()} items yet.</td></tr>}</tbody>
          </table></div>
        </section>); })}
      <p className="text-xs font-semibold text-ink-3">Owners: {[...new Set(ws.initiative.map((i) => personName(ws.person, i.owner)))].join(', ')}</p>
    </div>
  );
}
