// Starter data imported once into an empty workspace. It comes from the two weekly-management workbooks
// (version 1: NDZ event tasks; version 2: platform tasks, opportunities, product & marketing, contacts) plus
// a draft results framework. Indicator results are left empty: only real figures should be entered.
import type {
  Evaluation, Framework, Indicator, Initiative, Kind, Lesson, Meeting, Milestone, Opportunity, Person, Project,
  Risk, Settings, Task,
} from './types';

type PersonKey = 'sf' | 'bh' | 'ops' | 'fc' | 'mkt' | 'tech';
type ProjectKey = 'p1' | 'p2' | 'p3';

const PEOPLE: Record<PersonKey, Person> = {
  sf: { name: 'Sphamandla Fuweka', role: 'Founder · meeting chair', email: 'sphamandla@spazasure.co.za', phone: '+27 82 123 4567' },
  bh: { name: 'Bheka', role: 'Events & logistics', email: '', phone: '' },
  ops: { name: 'Operations Lead', role: 'Operations', email: 'ops@spazasure.co.za', phone: '+27 83 987 6543', note: 'Workbook lists two numbers: +27 83 987 6543 and +27 84 333 4444.' },
  fc: { name: 'Field Coordinator', role: 'Field operations', email: 'field@spazasure.co.za', phone: '+27 84 555 1212' },
  mkt: { name: 'Marketing Lead', role: 'Marketing', email: 'marketing@spazasure.co.za', phone: '+27 83 111 2222' },
  tech: { name: 'Tech Team', role: 'Platform development', email: 'dev@spazasure.co.za', phone: '+27 82 000 0000' },
};

const PROJECTS: Record<ProjectKey, Omit<Project, 'lead'> & { lead: PersonKey }> = {
  p1: { name: 'NDZ Spazashop Awareness & Consultation', short: 'NDZ event', programme: 'Awareness & engagement', lead: 'sf', endDate: '2026-10-31', funder: '', budget: null, spent: null, purpose: 'Awareness and consultation event for spaza shop owners, with FMCG exhibitors, a panel and a live platform demo.', outcomes: ['O3', 'O4'] },
  p2: { name: 'Platform & operations', short: 'Platform & ops', programme: 'Platform', lead: 'sf', endDate: '', funder: 'SAB Foundation (milestone report due)', budget: null, spent: null, purpose: 'Platform build, data infrastructure and operational readiness.', outcomes: ['O1', 'O2', 'O3'] },
  p3: { name: 'Umlazi & KwaMashu pilot', short: 'Township pilot', programme: 'Market pilots', lead: 'fc', endDate: '', funder: '', budget: null, spent: null, purpose: 'Field outreach to spaza shops in Umlazi and KwaMashu with the local spaza association.', outcomes: ['O1', 'O3'] },
};

const FRAMEWORK: Framework = {
  impact: { id: 'I1', name: 'Township spaza shops are more profitable, competitive and trusted by their communities' },
  outcomes: [
    { id: 'O1', name: 'Shops buy stock at lower cost through bulk buying' },
    { id: 'O2', name: 'Consumers trust the products spaza shops sell' },
    { id: 'O3', name: 'Shop owners run their business with digital tools' },
    { id: 'O4', name: 'A stronger network of verified suppliers serves townships' },
  ],
  outputs: [
    { id: 'OP1', name: 'Shops onboarded to the platform', outcome: 'O3' },
    { id: 'OP2', name: 'Suppliers verified', outcome: 'O4' },
    { id: 'OP3', name: 'Bulk-buy orders placed', outcome: 'O1' },
    { id: 'OP4', name: 'Awareness events held', outcome: 'O3' },
    { id: 'OP5', name: 'Product verification scans', outcome: 'O2' },
  ],
  yearStart: '2026-08-01',
  targetDate: '2027-03-31',
};

const AGENDAS = {
  mon: ['What was completed last week?', 'What is overdue or incomplete?', 'What is blocked and why?', 'Review key activity/results.', 'Agree on the top 3 priorities for this week.', 'Assign owners and deadlines.', 'Confirm support required.'],
  wed: ['Review strategic priorities.', 'New customer opportunities.', 'Supplier opportunities.', 'Partnerships, municipality and corporate opportunities.', 'Funding, competitions and support programmes.', 'Market/competitor developments.', 'Select priority opportunities and next actions.'],
  fri: ['Customer feedback and pain points.', 'Product/service improvements.', 'Platform/MVP development priorities.', 'Product offering and pricing review.', 'Marketing campaigns and content.', 'Leads, engagement, conversions and orders.', 'Set next week’s product and marketing priorities.'],
};

const E = '2026-10-31';
type T = Omit<Task, 'projectId' | 'owner'> & { projectId: ProjectKey; owner: PersonKey };
const t = (projectId: ProjectKey, title: string, owner: PersonKey, priority: Task['priority'], due: string, extra: Partial<T> = {}): T =>
  ({ projectId, title, owner, priority, due, status: 'Not Started', pct: 0, blocker: '', next: '', commitment: '', top: false, ...extra });

const TASKS: T[] = [
  t('p2', 'Finalize SpazaShop onboarding workflow UI', 'sf', 'High', '', { status: 'In Progress', pct: 60, blocker: 'Awaiting design feedback', next: 'Review Figma components', top: true }),
  t('p2', 'Integrate PostgreSQL database migrations on Coolify', 'sf', 'High', '', { status: 'In Progress', pct: 40, next: 'Run staging tests', top: true }),
  t('p2', 'Prepare SAB Foundation progress milestone report', 'sf', 'Medium', '', { next: 'Gather grant financials' }),
  t('p2', 'Draft bulk-buying flyer pricing from Tradeport catalog', 'ops', 'High', '', { status: 'In Progress', pct: 75, blocker: 'Supplier price validation', next: 'Confirm sugar & oil prices', top: true }),
  t('p3', 'Schedule pilot outreach in Umlazi & KwaMashu', 'fc', 'Medium', '', { blocker: 'Transport logistics', next: 'Contact spaza association' }),
  t('p1', 'Venue/Hall', 'bh', 'High', E),
  t('p1', 'Hall setup', 'bh', 'High', E),
  t('p1', 'Tables & chairs', 'bh', '', E),
  t('p1', 'Tables: 10 (6 for suppliers, 2 chairs per table)', 'bh', '', E, { next: 'Find out if they have 2, can we have 8 more, do we hire them?' }),
  t('p1', 'Panel couches/seating: 4 seats', 'bh', 'High', E),
  t('p1', 'Sound system (2 speakers, 5 microphones)', 'sf', 'High', E),
  t('p1', 'Projector/screen', 'sf', 'Medium', E),
  t('p1', 'Electricity/plug points', 'sf', 'High', E),
  t('p1', 'Extension cables', 'sf', '', E),
  t('p1', 'Wi-Fi/internet', 'bh', '', E, { next: 'Coverage is there; have data as back-up' }),
  t('p1', 'Security', 'bh', '', E),
  t('p1', 'Catering/refreshments (water and light lunch, if necessary)', 'sf', '', E),
  t('p1', 'Registration tables', 'sf', '', E),
  t('p1', 'Registration equipment, business cards, pamphlets (10 About/profile printouts)', 'sf', 'High', E),
  t('p1', 'Printer (if it can be provided; not compulsory)', 'sf', '', E),
  t('p1', 'Staff', 'sf', '', E),
  t('p1', 'SpazaSure platform demonstration', 'sf', 'Critical', E, { next: 'Testing with minimum users', commitment: 'Platform 40% ready' }),
  t('p1', 'Awareness presentation', 'sf', 'High', E),
  t('p1', 'Panel facilitation', 'sf', 'High', E),
  t('p1', 'Attendee registration', 'sf', 'Medium', E),
  t('p1', 'Site visit', 'bh', 'High', E),
  t('p1', 'Event programme (20 printouts + digital)', 'bh', 'High', E),
  t('p1', 'Attendee invitations', 'sf', 'Critical', E),
  t('p1', 'Exhibitors (FMCG): 6', 'bh', 'High', E),
  t('p1', 'IDs/documents', 'bh', 'Medium', E),
  t('p1', 'Photography/documentation', 'sf', 'High', E),
  t('p1', 'Branding/backdrop', 'sf', '', E, { status: 'In Progress', pct: 5 }),
  t('p1', 'Publications/radio', 'bh', 'Medium', E),
  t('p1', 'Accommodation', 'bh', 'High', E),
  t('p1', 'Travel/transport', 'bh', 'Critical', E),
];

const m = (projectId: ProjectKey, title: string, due = '', status: Milestone['status'] = 'Not Started', evidence = '') => ({ projectId, title, due, status, evidence });
const MILESTONES = [
  m('p1', 'Site visit and venue confirmed'), m('p1', 'Invitations sent and exhibitors confirmed'),
  m('p1', 'Event day', '2026-10-31', 'Not Started', 'Attendance register, photos'), m('p1', 'Post-event report and feedback survey', '', 'Not Started', 'Survey results'),
  m('p2', 'Retailer onboarding flow live', '2026-10-15', 'In Progress', 'Onboarding completion rate'), m('p2', 'Supplier catalogue & margin calculator', '2026-10-22'),
  m('p2', 'Verification scanner refined', '2026-10-30', 'Not Started', 'Scans per month'), m('p2', 'SAB Foundation milestone report submitted', '', 'Not Started', 'Submitted report'),
  m('p3', 'Pilot outreach scheduled with spaza association'),
];

const r = (projectId: ProjectKey, title: string, likelihood: 1 | 2 | 3, impact: 1 | 2 | 3, owner: PersonKey, mitigation: string, status: Risk['status'] = 'Open') => ({ projectId, title, likelihood, impact, owner, mitigation, status });
const RISKS = [
  r('p1', 'Platform not ready for the live demo (40% ready)', 3, 3, 'sf', 'Testing with minimum users'),
  r('p1', 'Not enough tables and seating at the venue', 2, 2, 'bh', 'Find out if venue has 2; hire 8 more if not'),
  r('p1', 'Venue internet drops during the demo', 1, 2, 'bh', 'Mobile data as back-up', 'Mitigating'),
  r('p2', 'Supplier price validation delays bulk-buy flyer', 2, 2, 'ops', 'Confirm sugar & oil prices with Tradeport'),
  r('p3', 'Transport logistics block pilot outreach', 2, 3, 'fc', 'Contact spaza association'),
  r('p1', 'Bheka can’t receive reminders (no contact details)', 3, 1, 'sf', 'Add Bheka’s email and phone in People'),
];

const OPPS: (Omit<Opportunity, 'owner'> & { owner: PersonKey })[] = [
  { title: 'UJ TTO Incubation Grant Support', type: 'Funding', value: 250000, contact: 'University of Johannesburg TTO', contactPhone: '+27 11 559 4555', contactEmail: 'tto@uj.ac.za', stage: 'Meeting', next: 'Submit revised budget breakdown', owner: 'sf', due: '2026-10-25', notes: 'Workbook stage: In Discussion' },
  { title: 'FMCG Supplier Bulk Supply Agreement', type: 'Supplier', value: 500000, contact: 'Tradeport Wholesalers', contactPhone: '+27 31 300 1122', contactEmail: 'sales@tradeport.co.za', stage: 'Proposal', next: 'Finalize tier discount terms', owner: 'sf', due: '2026-10-18', notes: 'Workbook stage: Proposal Sent' },
  { title: 'eThekwini Township Retail Partnership', type: 'Municipality', value: 150000, contact: 'eThekwini Business Support', contactPhone: '+27 31 311 1111', contactEmail: 'business@durban.gov.za', stage: 'Contacted', next: 'Present SpazaSure platform pitch', owner: 'ops', due: '2026-11-05', notes: 'Workbook stage: Initial Contact' },
];

const ITEMS: (Omit<Initiative, 'owner'> & { owner: PersonKey })[] = [
  { area: 'Product', title: 'Improve retailer onboarding flow', customer: 'Spaza shops', priority: 'High', status: 'In Progress', owner: 'sf', due: '2026-10-15', metric: 'Higher onboarding completion', next: 'Deploy mobile step-by-step UI', notes: '' },
  { area: 'Product', title: 'Improve supplier catalogue/pricing experience', customer: 'Spaza shops & suppliers', priority: 'High', status: 'Planned', owner: 'sf', due: '2026-10-22', metric: 'More orders / repeat use', next: 'Add automated 5% margin calculator', notes: '' },
  { area: 'Product', title: 'Improve product verification experience', customer: 'Consumers & retailers', priority: 'High', status: 'Planned', owner: 'tech', due: '2026-10-30', metric: 'Scans/reports completed', next: 'Refine QR/authentication scanner', notes: '' },
  { area: 'Marketing', title: 'Weekly bulk-buy campaign', customer: 'Spaza shops', priority: 'High', status: 'In Progress', owner: 'mkt', due: '2026-10-12', metric: 'Leads/orders generated', next: 'Distribute printed & WhatsApp flyers', notes: '' },
  { area: 'Marketing', title: 'Supplier acquisition campaign', customer: 'FMCG suppliers', priority: 'Medium', status: 'Planned', owner: 'ops', due: '2026-10-28', metric: 'Supplier meetings/onboarding', next: 'Send B2B pitch deck', notes: '' },
  { area: 'Marketing', title: 'Customer education and trust content', customer: 'Consumers', priority: 'Medium', status: 'Planned', owner: 'mkt', due: '2026-11-02', metric: 'Reach/engagement/reports', next: 'Publish short explainer video', notes: '' },
];

const DIS = 'Area · Owner gender · Youth-owned (18–35)';
const i = (code: string, level: Indicator['level'], link: string, name: string, unit: string, measure: Indicator['measure'], baseline: number | null, target: number, source: Indicator['source'], frequency: string, owner: PersonKey) =>
  ({ code, level, link, name, unit, measure, baseline, target, actuals: {} as Record<string, number>, source, frequency, owner, disaggregation: DIS });
const INDICATORS = [
  i('IMP.1', 'Impact', 'I1', 'Shop owners reporting higher monthly profit', '%', 'rate', null, 60, 'Survey', 'Baseline, midline, endline', 'fc'),
  i('OC1.1', 'Outcome', 'O1', 'Average monthly saving per active shop', 'R', 'rate', null, 400, 'Platform data (automatic)', 'Monthly', 'ops'),
  i('OC2.1', 'Outcome', 'O2', 'Consumers who say they trust products at SpazaSure shops', '%', 'rate', null, 75, 'Survey', 'Baseline, endline', 'fc'),
  i('OC3.1', 'Outcome', 'O3', 'Onboarded shops active in the month', '%', 'rate', null, 60, 'Platform data (automatic)', 'Monthly', 'tech'),
  i('OC3.2', 'Outcome', 'O3', 'Shops that reorder within 30 days', '%', 'rate', null, 50, 'Platform data (automatic)', 'Monthly', 'tech'),
  i('OP1.1', 'Output', 'OP1', 'Shops onboarded (cumulative)', 'shops', 'count', 0, 500, 'Platform data (automatic)', 'Monthly', 'fc'),
  i('OP1.2', 'Output', 'OP1', 'Onboarding completion rate', '%', 'rate', null, 70, 'Platform data (automatic)', 'Monthly', 'tech'),
  i('OP2.1', 'Output', 'OP2', 'Suppliers verified (cumulative)', 'suppliers', 'count', 0, 30, 'Verification register', 'Monthly', 'ops'),
  i('OP3.1', 'Output', 'OP3', 'Bulk-buy orders placed (cumulative)', 'orders', 'count', 0, 2000, 'Platform data (automatic)', 'Monthly', 'tech'),
  { ...i('OP4.1', 'Output', 'OP4', 'Awareness events held (cumulative)', 'events', 'count', 0, 6, 'Event register', 'After each event', 'sf'), actuals: { '2026-10': 0 } },
  i('OP5.1', 'Output', 'OP5', 'Product verification scans per month', 'scans', 'rate', null, 1000, 'Platform data (automatic)', 'Monthly', 'tech'),
];

const EVALS: (Omit<Evaluation, 'owner'> & { owner: PersonKey })[] = [
  { name: 'NDZ event feedback survey', type: 'Event survey', timing: '31 Oct 2026, on the day', questions: 'Was the event useful? Will you register on SpazaSure? Which suppliers interest you?', method: 'Short paper or phone form at registration and exit', sample: 'All attendees', owner: 'sf', status: 'Planned', link: 'OP4' },
  { name: 'Shop baseline survey', type: 'Baseline', timing: 'Before pilot outreach starts', questions: 'Monthly stock spend, suppliers used, prices paid, profit, phone and app use', method: 'In-person survey by field coordinator', sample: 'To set (pilot shops plus comparison shops)', owner: 'fc', status: 'Designing', link: 'I1' },
  { name: 'Consumer trust baseline', type: 'Baseline', timing: 'Before verification scanner launch', questions: 'Do you trust products at your local spaza? Have you bought expired or fake goods?', method: 'Intercept survey outside pilot shops', sample: 'To set', owner: 'fc', status: 'Planned', link: 'O2' },
  { name: 'Pilot midline review', type: 'Midline', timing: '3 months into the pilot', questions: 'What is working, what isn’t, and what should change?', method: 'Platform data review plus 10–15 owner interviews', sample: 'Pilot shops', owner: 'sf', status: 'Planned', link: 'O1' },
  { name: 'Year 1 impact evaluation', type: 'Endline', timing: 'Mar 2027', questions: 'Did savings, profit and trust improve compared with baseline?', method: 'Repeat baseline surveys; compare with platform data', sample: 'Same as baseline', owner: 'sf', status: 'Planned', link: 'I1' },
];

const LESSONS: (Omit<Lesson, 'owner' | 'projectId'> & { owner: PersonKey; projectId: ProjectKey })[] = [
  { date: '2026-10-14', title: 'Venue Wi-Fi exists but can’t be relied on for a live demo', evidence: 'NDZ venue check: coverage is there; mobile data needed as back-up', source: 'Field visit', action: 'Always bring mobile data for demos', status: 'Adopted', owner: 'bh', projectId: 'p1' },
  { date: '2026-10-14', title: 'Unconfirmed supplier prices hold up marketing material', evidence: 'Bulk-buy flyer is 75% done but blocked on sugar and oil prices', source: 'Meeting', action: 'To decide in Friday’s Product & Marketing meeting', status: 'Open', owner: 'ops', projectId: 'p2' },
];

const meeting = (key: Meeting['key'], name: string, day: string, dow: number, time: string, focus: string, output: string): Omit<Meeting, 'attendees' | 'chair'> =>
  ({ key, name, day, dow, time, duration: 60, link: 'https://meet.google.com/abc-defg-hij', focus, output, agenda: AGENDAS[key] });
const MEETINGS = [
  meeting('mon', 'Progress & Accountability', 'Monday', 1, '09:00', 'Execution, tasks, blockers & commitments', 'Updated task board & clear weekly commitments'),
  meeting('wed', 'Strategy & Opportunities', 'Wednesday', 3, '10:00', 'Growth, key accounts, funding & partnerships', 'Priority partner list, decisions & next actions'),
  meeting('fri', 'Product & Marketing', 'Friday', 5, '14:00', 'App features, supplier onboarding & campaigns', 'Sprint priorities, marketing push & metrics'),
];

export type Batch = { kind: Kind; data: object }[];

/** Phase 1: records that others refer to. */
export function starterPhase1(): { batch: Batch; personKeys: PersonKey[]; projectKeys: ProjectKey[] } {
  const personKeys = Object.keys(PEOPLE) as PersonKey[];
  const projectKeys = Object.keys(PROJECTS) as ProjectKey[];
  const settings: Settings = { automations: {} };
  return {
    personKeys, projectKeys,
    batch: [
      ...personKeys.map((k) => ({ kind: 'person' as const, data: PEOPLE[k] })),
      ...projectKeys.map((k) => ({ kind: 'project' as const, data: { ...PROJECTS[k], lead: '' } })),
      { kind: 'framework', data: FRAMEWORK },
      { kind: 'settings', data: settings },
    ],
  };
}

/** Phase 2: everything else, with person and project keys swapped for the ids the server gave them. */
export function starterPhase2(person: Record<PersonKey, string>, project: Record<ProjectKey, string>): { batch: Batch; projectLeads: Record<string, string> } {
  const P = (k: PersonKey) => person[k];
  const J = (k: ProjectKey) => project[k];
  return {
    projectLeads: Object.fromEntries((Object.keys(PROJECTS) as ProjectKey[]).map((k) => [J(k), P(PROJECTS[k].lead)])),
    batch: [
      ...MEETINGS.map((mt) => ({ kind: 'meeting' as const, data: { ...mt, attendees: Object.values(person), chair: P('sf') } })),
      ...TASKS.map((x) => ({ kind: 'task' as const, data: { ...x, projectId: J(x.projectId), owner: P(x.owner), source: null, doneAt: null } })),
      ...MILESTONES.map((x) => ({ kind: 'milestone' as const, data: { ...x, projectId: J(x.projectId) } })),
      ...RISKS.map((x) => ({ kind: 'risk' as const, data: { ...x, projectId: J(x.projectId), owner: P(x.owner) } })),
      ...OPPS.map((x) => ({ kind: 'opportunity' as const, data: { ...x, owner: P(x.owner) } })),
      ...ITEMS.map((x) => ({ kind: 'initiative' as const, data: { ...x, owner: P(x.owner) } })),
      ...INDICATORS.map((x) => ({ kind: 'indicator' as const, data: { ...x, owner: P(x.owner) } })),
      ...EVALS.map((x) => ({ kind: 'evaluation' as const, data: { ...x, owner: P(x.owner) } })),
      ...LESSONS.map((x) => ({ kind: 'lesson' as const, data: { ...x, owner: P(x.owner), projectId: J(x.projectId) } })),
    ],
  };
}
