import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import toast from 'react-hot-toast';
import { lastMonths, monthKey, monthLabel, isoDate, today } from './logic';
import { RecordModal, type FieldDef } from './ui';
import type { WorkspaceApi } from './useWorkspace';
import {
  DATA_SOURCES, EVAL_STATUSES, EVAL_TYPES, ITEM_AREAS, ITEM_STATUSES, LESSON_SOURCES, LESSON_STATUSES, MILESTONE_STATUSES,
  OPP_STAGES, OPP_TYPES, PRIORITIES, RISK_STATUSES, TASK_STATUSES, type Kind, type KindMap, type Saved, type Workspace,
} from './types';

export type EditableKind = 'project' | 'task' | 'milestone' | 'risk' | 'opportunity' | 'initiative' | 'person' | 'meeting' | 'indicator' | 'evaluation' | 'lesson';

const LEVELS = [{ value: '1', label: 'Low' }, { value: '2', label: 'Medium' }, { value: '3', label: 'High' }];
const projectOptions = (ws: Workspace) => ws.project.map((p) => ({ value: p.id, label: p.short || p.name }));

function fieldsFor(kind: EditableKind, ws: Workspace): FieldDef[] {
  switch (kind) {
    case 'project': return [
      { key: 'name', label: 'Project name', required: true, full: true }, { key: 'short', label: 'Short name', required: true },
      { key: 'programme', label: 'Programme' }, { key: 'lead', label: 'Lead', type: 'person', required: true },
      { key: 'endDate', label: 'End / event date', type: 'date' }, { key: 'funder', label: 'Funder', full: true },
      { key: 'budget', label: 'Budget (R)', type: 'number' }, { key: 'spent', label: 'Spent to date (R)', type: 'number' },
      { key: 'purpose', label: 'Purpose', type: 'textarea' },
    ];
    case 'task': return [
      { key: 'title', label: 'Task / deliverable', required: true, full: true },
      { key: 'projectId', label: 'Project', type: 'select', options: projectOptions(ws), required: true },
      { key: 'owner', label: 'Owner', type: 'person', required: true },
      { key: 'priority', label: 'Priority', type: 'select', options: PRIORITIES }, { key: 'due', label: 'Due date', type: 'date', required: true },
      { key: 'status', label: 'Status', type: 'select', options: TASK_STATUSES, required: true }, { key: 'pct', label: '% complete', type: 'number' },
      { key: 'blocker', label: 'Blocker / issue', full: true }, { key: 'next', label: 'Next action', type: 'textarea' },
      { key: 'commitment', label: 'This week’s commitment', full: true },
    ];
    case 'milestone': return [
      { key: 'title', label: 'Milestone', required: true, full: true }, { key: 'projectId', label: 'Project', type: 'select', options: projectOptions(ws), required: true },
      { key: 'due', label: 'Due date', type: 'date', required: true }, { key: 'status', label: 'Status', type: 'select', options: MILESTONE_STATUSES, required: true },
      { key: 'evidence', label: 'Evidence of completion', full: true },
    ];
    case 'risk': return [
      { key: 'title', label: 'Risk', required: true, full: true }, { key: 'projectId', label: 'Project', type: 'select', options: projectOptions(ws), required: true },
      { key: 'owner', label: 'Owner', type: 'person', required: true },
      { key: 'likelihood', label: 'Likelihood', type: 'select', options: LEVELS, required: true }, { key: 'impact', label: 'Impact', type: 'select', options: LEVELS, required: true },
      { key: 'mitigation', label: 'Mitigation', type: 'textarea' }, { key: 'status', label: 'Status', type: 'select', options: RISK_STATUSES, required: true },
    ];
    case 'opportunity': return [
      { key: 'title', label: 'Opportunity', required: true, full: true }, { key: 'type', label: 'Type', type: 'select', options: OPP_TYPES },
      { key: 'stage', label: 'Stage', type: 'select', options: OPP_STAGES, required: true }, { key: 'value', label: 'Potential value (R)', type: 'number' },
      { key: 'owner', label: 'Owner', type: 'person', required: true }, { key: 'contact', label: 'Contact / organisation', full: true },
      { key: 'contactEmail', label: 'Partner email', type: 'email' }, { key: 'contactPhone', label: 'Partner phone' },
      { key: 'due', label: 'Target date', type: 'date', required: true }, { key: 'next', label: 'Decision / next step', type: 'textarea' }, { key: 'notes', label: 'Notes', type: 'textarea' },
    ];
    case 'initiative': return [
      { key: 'title', label: 'Item / initiative', required: true, full: true }, { key: 'area', label: 'Area', type: 'select', options: ITEM_AREAS, required: true },
      { key: 'customer', label: 'Target customer' }, { key: 'priority', label: 'Priority', type: 'select', options: PRIORITIES },
      { key: 'status', label: 'Status', type: 'select', options: ITEM_STATUSES, required: true }, { key: 'owner', label: 'Owner', type: 'person', required: true },
      { key: 'due', label: 'Target date', type: 'date', required: true }, { key: 'metric', label: 'Success metric', full: true },
      { key: 'next', label: 'Next action', type: 'textarea' }, { key: 'notes', label: 'Notes', type: 'textarea' },
    ];
    case 'person': return [
      { key: 'name', label: 'Name', required: true, full: true }, { key: 'role', label: 'Role', full: true },
      { key: 'email', label: 'Email (for reminders)', type: 'email', full: true }, { key: 'phone', label: 'Phone / WhatsApp', full: true },
    ];
    case 'meeting': return [
      { key: 'time', label: 'Start time (SAST)', type: 'time', required: true }, { key: 'duration', label: 'Duration (minutes)', type: 'number', required: true },
      { key: 'link', label: 'Google Meet link', type: 'url', required: true, full: true, hint: 'One permanent link for the recurring meeting.' },
      { key: 'chair', label: 'Chair', type: 'person', required: true }, { key: 'attendees', label: 'Attendees', type: 'people', required: true },
    ];
    case 'indicator': return [
      { key: 'resultMonth', label: 'Result for month', type: 'select', options: lastMonths(6).reverse().map((m) => ({ value: m, label: monthLabel(m) })) },
      { key: 'resultValue', label: 'Result', type: 'number', hint: 'Leave empty to keep the month blank.' },
      { key: 'name', label: 'Indicator', required: true, full: true }, { key: 'code', label: 'Code', required: true },
      { key: 'level', label: 'Level', type: 'select', options: ['Impact', 'Outcome', 'Output'], required: true },
      { key: 'link', label: 'Measures (framework ID)', hint: 'e.g. O1 or OP3', required: true },
      { key: 'measure', label: 'Type', type: 'select', options: [{ value: 'count', label: 'Running count' }, { value: 'rate', label: 'Rate or monthly level' }], required: true },
      { key: 'unit', label: 'Unit', hint: 'R, %, shops, orders…' }, { key: 'baseline', label: 'Baseline', type: 'number' },
      { key: 'target', label: 'Target', type: 'number', required: true }, { key: 'source', label: 'Data source', type: 'select', options: DATA_SOURCES, required: true },
      { key: 'frequency', label: 'Frequency' }, { key: 'owner', label: 'Owner', type: 'person', required: true },
      { key: 'disaggregation', label: 'Break down by', full: true },
    ];
    case 'evaluation': return [
      { key: 'name', label: 'Evaluation', required: true, full: true }, { key: 'type', label: 'Type', type: 'select', options: EVAL_TYPES },
      { key: 'status', label: 'Status', type: 'select', options: EVAL_STATUSES, required: true }, { key: 'timing', label: 'When', full: true },
      { key: 'questions', label: 'Key questions', type: 'textarea' }, { key: 'method', label: 'Method', full: true }, { key: 'sample', label: 'Sample', full: true },
      { key: 'owner', label: 'Owner', type: 'person', required: true }, { key: 'link', label: 'Tests (framework ID)' },
    ];
    case 'lesson': return [
      { key: 'title', label: 'What we learned', required: true, full: true }, { key: 'evidence', label: 'Evidence', type: 'textarea' },
      { key: 'action', label: 'What we’re changing', type: 'textarea' }, { key: 'source', label: 'Source', type: 'select', options: LESSON_SOURCES },
      { key: 'status', label: 'Status', type: 'select', options: LESSON_STATUSES, required: true }, { key: 'owner', label: 'Owner', type: 'person', required: true },
      { key: 'projectId', label: 'Project', type: 'select', options: projectOptions(ws) }, { key: 'date', label: 'Date', type: 'date' },
    ];
  }
}

function blank(kind: EditableKind, ws: Workspace): Record<string, unknown> {
  const fw = ws.framework[0];
  switch (kind) {
    case 'project': return { name: '', short: '', programme: '', lead: '', endDate: '', funder: '', budget: null, spent: null, purpose: '', outcomes: [] };
    case 'task': return { projectId: ws.project[0]?.id ?? '', title: '', owner: '', priority: 'Medium', due: '', status: 'Not Started', pct: 0, blocker: '', next: '', commitment: '', top: false, source: null, doneAt: null };
    case 'milestone': return { projectId: ws.project[0]?.id ?? '', title: '', due: '', status: 'Not Started', evidence: '' };
    case 'risk': return { projectId: ws.project[0]?.id ?? '', title: '', likelihood: 2, impact: 2, owner: '', mitigation: '', status: 'Open' };
    case 'opportunity': return { title: '', type: 'Customer', value: null, contact: '', contactEmail: '', contactPhone: '', stage: 'New', next: '', owner: '', due: '', notes: '' };
    case 'initiative': return { area: 'Product', title: '', customer: '', priority: 'Medium', status: 'Planned', owner: '', due: '', metric: '', next: '', notes: '' };
    case 'person': return { name: '', role: '', email: '', phone: '' };
    case 'meeting': return {};
    case 'indicator': return { code: '', level: 'Output', link: fw?.outputs[0]?.id ?? '', name: '', unit: '', measure: 'count', baseline: null, target: null, actuals: {}, source: 'Manual entry', frequency: 'Monthly', owner: '', disaggregation: 'Area · Owner gender · Youth-owned (18–35)', resultMonth: monthKey(), resultValue: null };
    case 'evaluation': return { name: '', type: 'Baseline', timing: '', questions: '', method: '', sample: '', owner: '', status: 'Planned', link: fw?.impact.id ?? '' };
    case 'lesson': return { date: isoDate(today()), title: '', evidence: '', source: 'Meeting', action: '', status: 'Open', owner: '', projectId: '' };
  }
}

/** Turns form values back into the stored shape (numbers, results by month, completion). */
function normalise(kind: EditableKind, values: Record<string, unknown>, existing?: Record<string, unknown>) {
  const out: Record<string, unknown> = { ...values };
  if (kind === 'risk') { out.likelihood = Number(out.likelihood); out.impact = Number(out.impact); }
  if (kind === 'task') {
    out.pct = Math.max(0, Math.min(100, Number(out.pct) || 0));
    if (out.status === 'Completed') { out.pct = 100; out.doneAt = (existing?.doneAt as string) || isoDate(today()); }
  }
  if (kind === 'indicator') {
    const actuals = { ...((existing?.actuals as Record<string, number>) ?? (out.actuals as Record<string, number>) ?? {}) };
    const m = out.resultMonth as string; const v = out.resultValue as number | null;
    if (m) { if (v == null || Number.isNaN(v)) delete actuals[m]; else actuals[m] = v; }
    out.actuals = actuals; delete out.resultMonth; delete out.resultValue;
  }
  return out;
}

const LABEL: Record<EditableKind, string> = {
  project: 'project', task: 'task', milestone: 'milestone', risk: 'risk', opportunity: 'opportunity', initiative: 'item',
  person: 'person', meeting: 'meeting settings', indicator: 'indicator', evaluation: 'evaluation', lesson: 'lesson',
};

type Open = (kind: EditableKind, record?: Saved<object> | null, defaults?: Record<string, unknown>) => void;
const EditorContext = createContext<Open>(() => {});
export const useEditor = () => useContext(EditorContext);

export function EditorProvider({ api, children }: { api: WorkspaceApi; children: ReactNode }) {
  const [state, setState] = useState<{ kind: EditableKind; record: Saved<object> | null; defaults?: Record<string, unknown> } | null>(null);
  const open = useCallback<Open>((kind, record = null, defaults) => setState({ kind, record, defaults }), []);
  const { ws } = api;

  let modal: ReactNode = null;
  if (state) {
    const { kind, record } = state;
    const rec = record as (Saved<object> & Record<string, unknown>) | null;
    const initial: Record<string, unknown> = rec ? { ...rec } : { ...blank(kind, ws), ...state.defaults };
    if (kind === 'risk') { initial.likelihood = String(initial.likelihood); initial.impact = String(initial.impact); }
    if (kind === 'indicator' && rec) { const m = monthKey(); initial.resultMonth = m; initial.resultValue = (rec.actuals as Record<string, number>)?.[m] ?? null; }
    const title = kind === 'meeting' && rec ? `${String(rec.name)} settings` : `${rec ? 'Edit' : 'New'} ${LABEL[kind]}`;

    modal = (
      <RecordModal
        key={`${kind}-${rec?.id ?? 'new'}`}
        title={title} fields={fieldsFor(kind, ws)} initial={initial} people={ws.person} onClose={() => setState(null)}
        footerNote={kind === 'meeting' ? 'Once calendar sync is switched on, saving here also updates the recurring Google Calendar event.' : undefined}
        onSave={async (values) => {
          const { id: _i, createdAt: _c, updatedAt: _u, ...data } = normalise(kind, values, rec ?? undefined);
          if (kind === 'meeting' && !/^https:\/\/meet\.google\.com\/[a-z]{3}-[a-z]{4}-[a-z]{3}/.test(String(data.link))) {
            toast.error('Use a Google Meet link like https://meet.google.com/xxx-xxxx-xxx');
            return false;
          }
          if (rec) return api.update(kind as Kind, rec as unknown as Saved<KindMap[Kind]>, data as unknown as Partial<KindMap[Kind]>);
          return !!(await api.create(kind as Kind, data as unknown as KindMap[Kind]));
        }}
        onDelete={rec && kind !== 'meeting' ? () => api.remove(kind, rec.id) : undefined}
      />
    );
  }

  return <EditorContext.Provider value={open}>{children}{modal}</EditorContext.Provider>;
}
