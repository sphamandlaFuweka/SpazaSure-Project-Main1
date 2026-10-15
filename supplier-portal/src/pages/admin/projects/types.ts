// Admin Projects & Impact workspace: project management, monitoring, evaluation & learning (MEL), reporting.
// Every record is stored by the backend in pm_records as { id, kind, data }; these types describe `data`.

export type Kind =
  | 'project' | 'task' | 'milestone' | 'risk' | 'opportunity' | 'initiative' | 'person'
  | 'meeting' | 'meetingRecord' | 'indicator' | 'evaluation' | 'lesson' | 'framework' | 'settings';

export interface RawRecord { id: string; kind: Kind; data: Record<string, unknown>; createdAt: string; updatedAt?: string | null }
export type Saved<T> = T & { id: string; createdAt: string; updatedAt?: string | null };

export const PRIORITIES = ['Critical', 'High', 'Medium', 'Low'] as const;
export type Priority = typeof PRIORITIES[number];
export const TASK_STATUSES = ['Not Started', 'In Progress', 'Blocked', 'Completed', 'Deferred'] as const;
export type TaskStatus = typeof TASK_STATUSES[number];
export const OPP_TYPES = ['Customer', 'Supplier', 'Partnership', 'Funding', 'Municipality', 'Corporate', 'Competition', 'Investor', 'Other'] as const;
export const OPP_STAGES = ['New', 'Researching', 'Contacted', 'Meeting', 'Proposal', 'Negotiation', 'Won', 'Lost', 'On Hold'] as const;
export const ITEM_AREAS = ['Product', 'Marketing'] as const;
export const ITEM_STATUSES = ['Planned', 'In Progress', 'Testing', 'Live', 'Completed', 'Paused'] as const;
export const MILESTONE_STATUSES = ['Not Started', 'In Progress', 'Completed', 'Missed'] as const;
export const RISK_STATUSES = ['Open', 'Mitigating', 'Closed'] as const;
export const EVAL_STATUSES = ['Planned', 'Designing', 'In field', 'Analysing', 'Complete'] as const;
export const EVAL_TYPES = ['Baseline', 'Event survey', 'Midline', 'Endline', 'Case study'] as const;
export const LESSON_SOURCES = ['Meeting', 'Evaluation', 'Field visit', 'Platform data', 'Customer feedback'] as const;
export const LESSON_STATUSES = ['Open', 'Testing a change', 'Adopted', 'Rejected'] as const;
export const DATA_SOURCES = ['Platform data (automatic)', 'Verification register', 'Event register', 'Survey', 'Manual entry'] as const;

export interface Person { name: string; role: string; email: string; phone: string; note?: string }

export interface Project {
  name: string; short: string; programme: string; lead: string; endDate: string; funder: string;
  budget: number | null; spent: number | null; purpose: string; outcomes: string[];
}

export interface Task {
  projectId: string; title: string; owner: string; priority: Priority | ''; due: string; status: TaskStatus;
  pct: number; blocker: string; next: string; commitment: string; top: boolean;
  source?: { meetingKey: MeetingKey; date: string } | null; doneAt?: string | null;
}

export interface Milestone { projectId: string; title: string; due: string; status: typeof MILESTONE_STATUSES[number]; evidence: string }

export interface Risk {
  projectId: string; title: string; likelihood: 1 | 2 | 3; impact: 1 | 2 | 3; owner: string; mitigation: string;
  status: typeof RISK_STATUSES[number];
}

export interface Opportunity {
  title: string; type: typeof OPP_TYPES[number]; value: number | null; contact: string; contactEmail: string; contactPhone: string;
  stage: typeof OPP_STAGES[number]; next: string; owner: string; due: string; notes: string;
}

export interface Initiative {
  area: typeof ITEM_AREAS[number]; title: string; customer: string; priority: Priority; status: typeof ITEM_STATUSES[number];
  owner: string; due: string; metric: string; next: string; notes: string;
}

export type MeetingKey = 'mon' | 'wed' | 'fri';
export interface Meeting {
  key: MeetingKey; name: string; day: string; dow: number; time: string; duration: number; link: string;
  attendees: string[]; chair: string; focus: string; output: string; agenda: string[];
}
export interface MeetingRecord { meetingKey: MeetingKey; date: string; checked: number; decisions: string[]; actions: string[]; lessons: string[] }

export interface Indicator {
  code: string; level: 'Impact' | 'Outcome' | 'Output'; link: string; name: string; unit: string; measure: 'count' | 'rate';
  baseline: number | null; target: number; actuals: Record<string, number>; source: typeof DATA_SOURCES[number];
  frequency: string; owner: string; disaggregation: string;
}

export interface Evaluation {
  name: string; type: typeof EVAL_TYPES[number]; timing: string; questions: string; method: string; sample: string;
  owner: string; status: typeof EVAL_STATUSES[number]; link: string;
}

export interface Lesson {
  date: string; title: string; evidence: string; source: typeof LESSON_SOURCES[number]; action: string;
  status: typeof LESSON_STATUSES[number]; owner: string; projectId: string;
}

export interface Framework {
  impact: { id: string; name: string };
  outcomes: { id: string; name: string }[];
  outputs: { id: string; name: string; outcome: string }[];
  yearStart: string; targetDate: string;
}

export interface Settings { automations: Record<string, boolean> }

export interface KindMap {
  project: Project; task: Task; milestone: Milestone; risk: Risk; opportunity: Opportunity; initiative: Initiative;
  person: Person; meeting: Meeting; meetingRecord: MeetingRecord; indicator: Indicator; evaluation: Evaluation;
  lesson: Lesson; framework: Framework; settings: Settings;
}

export type Workspace = { [K in Kind]: Saved<KindMap[K]>[] };

export const emptyWorkspace = (): Workspace => ({
  project: [], task: [], milestone: [], risk: [], opportunity: [], initiative: [], person: [], meeting: [],
  meetingRecord: [], indicator: [], evaluation: [], lesson: [], framework: [], settings: [],
});
