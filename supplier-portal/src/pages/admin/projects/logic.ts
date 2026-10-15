import type {
  Indicator, Initiative, Meeting, Milestone, Person, Project, Risk, Saved, Task, Workspace,
} from './types';

// ── Dates (all local, ISO yyyy-mm-dd) ───────────────────────────────────────────
export const today = () => { const d = new Date(); d.setHours(0, 0, 0, 0); return d; };
export const parseDate = (s?: string | null) => (s ? new Date(`${s}T00:00:00`) : null);
export const isoDate = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
export const fmtDate = (s?: string | null) =>
  s ? parseDate(s)!.toLocaleDateString('en-ZA', { day: 'numeric', month: 'short' }) : '—';
export const fmtDay = (d: Date) => d.toLocaleDateString('en-ZA', { weekday: 'short', day: 'numeric', month: 'short' });
export const hm = (d: Date) => `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
export const daysTo = (s: string) => Math.round((parseDate(s)!.getTime() - today().getTime()) / 864e5);
export const monthKey = (d = new Date()) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
export const monthLabel = (k: string) => new Date(`${k}-01T00:00:00`).toLocaleDateString('en-ZA', { month: 'short', year: 'numeric' });
export const lastMonths = (n: number) => {
  const out: string[] = []; const d = new Date(); d.setDate(1);
  for (let i = n - 1; i >= 0; i--) { const x = new Date(d); x.setMonth(d.getMonth() - i); out.push(monthKey(x)); }
  return out;
};
export const rand = (n: number) => `R ${Math.round(n).toLocaleString('en-ZA')}`;

// ── People ─────────────────────────────────────────────────────────────────────
export const personName = (people: Saved<Person>[], id?: string) => people.find((p) => p.id === id)?.name ?? 'Unassigned';
export const initials = (name: string) => name.split(/\s+/).map((w) => w[0]).join('').slice(0, 2).toUpperCase();
const AVATAR_COLOURS = ['#25449A', '#078C50', '#E02526', '#D99508', '#5B6BBF', '#0F7C8C'];
export const personColour = (people: Saved<Person>[], id?: string) => {
  const i = people.findIndex((p) => p.id === id);
  return i < 0 ? '#8888AA' : AVATAR_COLOURS[i % AVATAR_COLOURS.length];
};
export const whatsappLink = (phone: string, text: string) => {
  const n = phone.replace(/\D/g, '').replace(/^0/, '27');
  return n ? `https://wa.me/${n}?text=${encodeURIComponent(text)}` : '';
};
export const mailtoLink = (email: string, subject: string, body: string) =>
  `mailto:${email}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;

// ── Tasks & initiatives ─────────────────────────────────────────────────────────
export const isOpen = (t: Task) => t.status !== 'Completed' && t.status !== 'Deferred';
export const isOverdue = (t: Task) => isOpen(t) && !!t.due && daysTo(t.due) < 0;
export const itemOpen = (i: Initiative) => !['Completed', 'Live', 'Paused'].includes(i.status);
export const riskScore = (r: Risk) => r.likelihood * r.impact;

// ── Meetings ───────────────────────────────────────────────────────────────────
export function nextOccurrence(m: Pick<Meeting, 'time' | 'dow'>, after = new Date()) {
  const [h, mi] = m.time.split(':').map(Number);
  const d = new Date(after); d.setHours(h, mi, 0, 0);
  for (let i = 0; i < 8; i++) { if (d.getDay() === m.dow && d > after) return d; d.setDate(d.getDate() + 1); }
  return d;
}
export const isPlaceholderLink = (m: Meeting) => !m.link || m.link.includes('abc-defg-hij');
const BYDAY = ['SU', 'MO', 'TU', 'WE', 'TH', 'FR', 'SA'];
export function googleCalendarUrl(m: Meeting, people: Saved<Person>[]) {
  const s = nextOccurrence(m); const e = new Date(s.getTime() + m.duration * 6e4);
  const g = (d: Date) => `${isoDate(d).replace(/-/g, '')}T${hm(d).replace(':', '')}00`;
  const emails = m.attendees.map((id) => people.find((p) => p.id === id)?.email).filter(Boolean).join(',');
  const q = new URLSearchParams({
    action: 'TEMPLATE', text: `SpazaSure: ${m.name}`, dates: `${g(s)}/${g(e)}`, ctz: 'Africa/Johannesburg',
    recur: `RRULE:FREQ=WEEKLY;BYDAY=${BYDAY[m.dow]}`, location: m.link,
    details: `Join: ${m.link}\n\nAgenda\n${m.agenda.map((a, i) => `${i + 1}. ${a}`).join('\n')}`, add: emails,
  });
  return `https://calendar.google.com/calendar/render?${q.toString()}`;
}

// ── Project health ─────────────────────────────────────────────────────────────
export type Rag = 'On track' | 'At risk' | 'Off track';
export function projectHealth(p: Saved<Project>, ws: Workspace) {
  const all = ws.task.filter((t) => t.projectId === p.id);
  const open = all.filter(isOpen);
  const ms = ws.milestone.filter((m) => m.projectId === p.id);
  const why: string[] = []; let lvl = 0;
  const lateMs = ms.filter((m) => m.status !== 'Completed' && m.due && daysTo(m.due) < 0);
  if (lateMs.length) { why.push(`${lateMs.length} milestone${lateMs.length > 1 ? 's' : ''} overdue`); lvl = 2; }
  const critical = ws.risk.filter((r) => r.projectId === p.id && r.status !== 'Closed' && riskScore(r) >= 9);
  if (critical.length) { why.push(`Critical risk: ${critical[0].title}`); lvl = 2; }
  const od = open.filter(isOverdue);
  if (od.length) { why.push(`${od.length} overdue task${od.length > 1 ? 's' : ''}`); lvl = 2; }
  const bl = open.filter((t) => t.blocker || t.status === 'Blocked');
  if (bl.length) { why.push(`${bl.length} blocker${bl.length > 1 ? 's' : ''}`); lvl = Math.max(lvl, 1); }
  const undated = open.filter((t) => !t.due).length + ms.filter((m) => !m.due && m.status !== 'Completed').length;
  if (undated) { why.push(`${undated} item${undated > 1 ? 's' : ''} without a date`); lvl = Math.max(lvl, 1); }
  const pct = all.length ? Math.round(all.reduce((a, t) => a + (t.status === 'Completed' ? 100 : Number(t.pct) || 0), 0) / all.length) : 0;
  const rag: Rag = (['On track', 'At risk', 'Off track'] as const)[lvl];
  return { rag, why: why.length ? why : ['No issues found'], pct, open: open.length, total: all.length };
}

// ── Indicators ─────────────────────────────────────────────────────────────────
export type IndicatorState = 'On track' | 'At risk' | 'Off track' | 'No data';
export function indicatorStatus(k: Indicator, yearStart: string, targetDate: string) {
  const months = Object.keys(k.actuals ?? {}).filter((m) => k.actuals[m] != null).sort();
  if (!months.length) return { state: 'No data' as IndicatorState, latest: null as number | null, month: null as string | null };
  const month = months[months.length - 1]; const latest = Number(k.actuals[month]);
  const start = parseDate(yearStart)!.getTime(); const end = parseDate(targetDate)!.getTime();
  const expected = Math.min(1, Math.max(0.05, (today().getTime() - start) / (end - start)));
  let state: IndicatorState;
  if (k.measure === 'count') {
    const base = k.baseline ?? 0; const progress = (latest - base) / Math.max(1, k.target - base); const r = progress / expected;
    state = r >= 0.9 ? 'On track' : r >= 0.6 ? 'At risk' : 'Off track';
  } else {
    const r = latest / Math.max(1e-9, k.target);
    state = r >= 0.9 ? 'On track' : r >= 0.75 ? 'At risk' : 'Off track';
  }
  return { state, latest, month };
}
export const fmtIndicator = (k: Pick<Indicator, 'unit'>, v: number | null | undefined) =>
  v == null || Number.isNaN(v) ? '—' : k.unit === 'R' ? rand(v) : k.unit === '%' ? `${v}%` : Number(v).toLocaleString('en-ZA');

// ── Reminder rules and the schedule they produce ─────────────────────────────
export interface AutomationRule { key: string; name: string; when: string; to: string; channels: string[]; description: string }
export const AUTOMATIONS: AutomationRule[] = [
  { key: 'brief', name: 'Meeting brief', when: '24 hours before each meeting (Friday 15:00 for Monday)', to: 'Attendees', channels: ['Email', 'Calendar'], description: 'Agenda, Meet link and each person’s open items for that meeting.' },
  { key: 'join', name: 'Join reminder', when: '15 minutes before', to: 'Attendees', channels: ['Email', 'WhatsApp later'], description: 'One-tap Meet link.' },
  { key: 'assigned', name: 'Action assigned', when: 'Immediately', to: 'Action owner', channels: ['Email'], description: 'When an action is added in a meeting or a task changes owner.' },
  { key: 'minutes', name: 'Meeting minutes', when: 'When a meeting is closed', to: 'Attendees', channels: ['Email'], description: 'Decisions, new actions and carried-forward items.' },
  { key: 'duesoon', name: 'Due soon', when: '3 days and 1 day before, 08:00', to: 'Owner', channels: ['Email'], description: 'One message per person per day, however many items are due.' },
  { key: 'overdue', name: 'Overdue digest', when: 'Weekdays 08:00', to: 'Owner', channels: ['Email', 'WhatsApp later'], description: 'Everything overdue in one message.' },
  { key: 'monday', name: 'Monday briefing', when: 'Mondays 07:30', to: 'Everyone with open work', channels: ['Email'], description: 'Open tasks, this week’s top 3 and blockers before the 09:00 meeting.' },
  { key: 'blocked', name: 'Blocker escalation', when: 'Blocked for 2 working days', to: 'Meeting chair', channels: ['Email'], description: 'A blocked task gets help before Monday, not at Monday.' },
  { key: 'oppfollow', name: 'Opportunity follow-up', when: '2 days before target date, 08:00', to: 'Opportunity owner', channels: ['Email'], description: 'Internal nudge only. Partners are never emailed automatically.' },
  { key: 'reportweekly', name: 'Weekly management report', when: 'Mondays 07:00', to: 'Whole team', channels: ['Email'], description: 'Written from the trackers: completions, overdue work, blockers, project health, decisions.' },
  { key: 'reportmonthly', name: 'Monthly report pack', when: '1st working day, 08:00', to: 'Meeting chair', channels: ['Email'], description: 'Project reports drafted for review. Nothing goes to a funder until a person sends it.' },
  { key: 'inddata', name: 'Indicator data due', when: '25th of the month, 08:00', to: 'Owners of manually collected indicators', channels: ['Email'], description: 'Platform indicators fill themselves; this nudges survey and register owners.' },
];
export const ruleOn = (ws: Workspace, key: string) => ws.settings[0]?.automations?.[key] ?? true;

export interface ScheduledSend { at: Date; rule: string; to: string[]; subject: string }
export function upcomingSends(ws: Workspace, horizonDays: number): ScheduledSend[] {
  const now = new Date(); const end = new Date(today().getTime() + (horizonDays + 1) * 864e5); const out: ScheduledSend[] = [];
  const add = (at: Date, rule: string, to: string[], subject: string) => { if (at > now && at < end && to.length) out.push({ at, rule, to, subject }); };
  const workday8 = (d: Date, h = 8, mi = 0) => { const x = new Date(d); x.setHours(h, mi, 0, 0); if (x.getDay() === 6) x.setDate(x.getDate() - 1); if (x.getDay() === 0) x.setDate(x.getDate() - 2); return x; };
  const on = (k: string) => ruleOn(ws, k);

  ws.meeting.forEach((m) => {
    let o = nextOccurrence(m);
    for (let k = 0; k < 3; k++) {
      if (on('brief')) {
        const b = new Date(o.getTime() - 864e5); let label = 'Tomorrow';
        if (b.getDay() === 0 || b.getDay() === 6) { b.setDate(b.getDate() - (b.getDay() === 0 ? 2 : 1)); b.setHours(15, 0, 0, 0); label = m.day; }
        add(b, 'Meeting brief', m.attendees, `${label} ${m.time}: ${m.name}. Agenda and Meet link`);
      }
      if (on('join')) add(new Date(o.getTime() - 15 * 6e4), 'Join reminder', m.attendees, `Starting in 15 min: ${m.name}`);
      o = nextOccurrence(m, o);
    }
  });

  type Due = { title: string; owner: string; due: string };
  const openDue: Due[] = [
    ...ws.task.filter((t) => isOpen(t) && t.due),
    ...ws.initiative.filter((i) => itemOpen(i) && i.due && i.owner),
  ];
  if (on('duesoon')) {
    const groups = new Map<string, { at: Date; owner: string; n: number; items: Due[] }>();
    openDue.forEach((t) => [3, 1].forEach((n) => {
      const at = workday8(new Date(parseDate(t.due)!.getTime() - n * 864e5)); const key = `${t.owner}|${at.getTime()}`;
      if (!groups.has(key)) groups.set(key, { at, owner: t.owner, n, items: [] });
      groups.get(key)!.items.push(t);
    }));
    groups.forEach((g) => add(g.at, 'Due soon', [g.owner], g.items.length === 1 ? `Due in ${g.n} day${g.n > 1 ? 's' : ''}: ${g.items[0].title}` : `${g.items.length} items due in ${g.n} day${g.n > 1 ? 's' : ''}`));
  }
  if (on('overdue')) {
    for (const d = new Date(today()); d < end; d.setDate(d.getDate() + 1)) {
      if (d.getDay() === 0 || d.getDay() === 6) continue;
      const at = new Date(d); at.setHours(8, 0, 0, 0); const by = new Map<string, Due[]>();
      openDue.forEach((t) => { if (parseDate(t.due)! < d) by.set(t.owner, [...(by.get(t.owner) ?? []), t]); });
      by.forEach((list, owner) => add(at, 'Overdue digest', [owner], list.length === 1 ? `Overdue: ${list[0].title}` : `${list.length} overdue items`));
    }
  }
  if (on('monday')) add(nextOccurrence({ time: '07:30', dow: 1 }), 'Monday briefing', [...new Set(ws.task.filter(isOpen).map((t) => t.owner))], 'Your week: open tasks, top 3 priorities and blockers');
  if (on('oppfollow')) ws.opportunity.filter((o) => !['Won', 'Lost', 'On Hold'].includes(o.stage) && o.due)
    .forEach((o) => add(workday8(new Date(parseDate(o.due)!.getTime() - 2 * 864e5)), 'Opportunity follow-up', [o.owner], `${o.contact}: ${o.next} by ${fmtDate(o.due)}`));
  if (on('reportweekly')) { const d = nextOccurrence({ time: '07:00', dow: 1 }); while (d < end) { add(new Date(d), 'Weekly management report', ws.person.map((p) => p.id), 'Weekly management report'); d.setDate(d.getDate() + 7); } }
  const firstWorkday = (y: number, m: number) => { const d = new Date(y, m, 1, 8, 0); while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1); return d; };
  [0, 1].forEach((k) => {
    const b = new Date(today().getFullYear(), today().getMonth() + k, 1);
    const chair = ws.meeting[0]?.chair;
    if (on('reportmonthly') && chair) add(firstWorkday(b.getFullYear(), b.getMonth()), 'Monthly report pack', [chair], `${ws.project.length} project reports drafted for review`);
    if (on('inddata')) {
      const manual = ws.indicator.filter((x) => !x.source.startsWith('Platform'));
      add(workday8(new Date(b.getFullYear(), b.getMonth(), 25)), 'Indicator data due', [...new Set(manual.map((x) => x.owner))],
        `Enter ${b.toLocaleDateString('en-ZA', { month: 'long' })} results for ${manual.length} manually collected indicators`);
    }
  });
  return out.sort((a, b) => a.at.getTime() - b.at.getTime());
}

export const milestoneLate = (m: Milestone) => m.status !== 'Completed' && !!m.due && daysTo(m.due) < 0;
