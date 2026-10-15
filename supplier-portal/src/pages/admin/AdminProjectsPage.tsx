import { useState } from 'react';
import { Navigate, NavLink, useNavigate, useParams } from 'react-router-dom';
import { ClipboardList, FileSpreadsheet, RefreshCw } from 'lucide-react';
import clsx from 'clsx';
import { EmptyState, Spinner } from '../../components/ui';
import { useWorkspace } from './projects/useWorkspace';
import { EditorProvider } from './projects/editors';
import OverviewTab from './projects/OverviewTab';
import PortfolioTab from './projects/PortfolioTab';
import { OpportunitiesTab, ProductTab, TasksTab } from './projects/TrackerTabs';
import ImpactTab from './projects/ImpactTab';
import ReportsTab from './projects/ReportsTab';
import MeetingsTab, { type LiveMeeting } from './projects/MeetingsTab';
import { PeopleTab, RemindersTab } from './projects/PeopleRemindersTabs';

// Projects & Impact: project management, monitoring, evaluation & learning, and reporting.
// Each tab has its own URL (/admin/projects/<tab>) so it can be linked and bookmarked.
const TABS = [
  { key: 'overview', label: 'Overview' },
  { key: 'portfolio', label: 'Portfolio' },
  { key: 'tasks', label: 'Tasks', day: 'Mon' },
  { key: 'opportunities', label: 'Opportunities', day: 'Wed' },
  { key: 'product', label: 'Product & Marketing', day: 'Fri' },
  { key: 'impact', label: 'Impact (MEL)' },
  { key: 'reports', label: 'Reports' },
  { key: 'meetings', label: 'Meetings' },
  { key: 'reminders', label: 'Reminders' },
  { key: 'people', label: 'People' },
] as const;
type TabKey = typeof TABS[number]['key'];

export default function AdminProjectsPage() {
  const { tab = 'overview' } = useParams<{ tab?: string }>();
  const navigate = useNavigate();
  const api = useWorkspace();
  const [live, setLive] = useState<LiveMeeting | null>(null);

  if (!TABS.some((t) => t.key === tab)) return <Navigate to="/admin/projects" replace />;
  const current = tab as TabKey;
  const empty = !api.loading && !api.error && api.ws.project.length === 0 && api.ws.person.length === 0;

  const runMeeting = (key: string) => {
    const m = api.ws.meeting.find((x) => x.key === key);
    if (!m) return;
    setLive({ key: m.key, date: new Date().toISOString().slice(0, 10), checked: m.agenda.map(() => false), decisions: [], actions: [], lessons: [] });
    navigate('/admin/projects/meetings');
  };

  return (
    <div className="p-6 compact:p-4 space-y-5 max-w-7xl mx-auto">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="page-title">Projects & Impact</h1>
          <p className="page-subtitle">Weekly management rhythm, project delivery, monitoring, evaluation & learning, and reporting in one place.</p>
        </div>
        {!empty && <button className="btn-ghost" onClick={api.reload} disabled={api.loading}><RefreshCw size={15} className={clsx(api.loading && 'animate-spin')} /> Refresh</button>}
      </div>

      <nav aria-label="Projects & Impact sections" className="flex gap-1 overflow-x-auto pb-1 -mx-1 px-1">
        {TABS.map((t) => (
          <NavLink key={t.key} to={`/admin/projects/${t.key === 'overview' ? '' : t.key}`} end
            className={() => clsx('inline-flex items-center gap-1.5 px-3.5 py-2 rounded-full text-sm font-bold whitespace-nowrap transition-colors',
              current === t.key ? 'bg-brand-blue text-white' : 'text-brand-blue hover:bg-brand-blue-light/60')}>
            {t.label}
            {'day' in t && <span className={clsx('text-[10px] font-extrabold', current === t.key ? 'text-white/70' : 'text-ink-3')}>{t.day}</span>}
            {t.key === 'meetings' && live && <span className="w-2 h-2 rounded-full bg-brand-red" aria-label="meeting in progress" />}
          </NavLink>
        ))}
      </nav>

      {api.loading && api.ws.project.length === 0 ? (
        <div className="card p-16 flex justify-center"><Spinner size="lg" /></div>
      ) : api.error ? (
        <div className="card"><EmptyState icon={<ClipboardList />} title="Couldn’t load Projects & Impact" description="The server didn’t respond. Check your connection and try again." action={<button className="btn-primary" onClick={api.reload}>Try again</button>} /></div>
      ) : empty ? (
        <div className="card"><EmptyState icon={<FileSpreadsheet />} title="Start from the weekly management workbook"
          description="Load the projects, tasks, people, meetings, risks and draft results framework from the SpazaSure workbooks. You can edit or delete anything afterwards."
          action={<button className="btn-primary" onClick={api.importStarter} disabled={api.importing}>{api.importing ? 'Importing…' : 'Import workbook data'}</button>} /></div>
      ) : (
        <EditorProvider api={api}>
          {current === 'overview' && <OverviewTab ws={api.ws} onRunMeeting={runMeeting} />}
          {current === 'portfolio' && <PortfolioTab ws={api.ws} />}
          {current === 'tasks' && <TasksTab api={api} />}
          {current === 'opportunities' && <OpportunitiesTab api={api} />}
          {current === 'product' && <ProductTab api={api} />}
          {current === 'impact' && <ImpactTab ws={api.ws} />}
          {current === 'reports' && <ReportsTab api={api} />}
          {current === 'meetings' && <MeetingsTab api={api} live={live} setLive={setLive} />}
          {current === 'reminders' && <RemindersTab api={api} />}
          {current === 'people' && <PeopleTab api={api} />}
        </EditorProvider>
      )}
    </div>
  );
}
