import { useEffect, useState } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { Eye, Lock, Menu, Shield } from 'lucide-react';
import AdminSidebar from './AdminSidebar';
import PageTransition from '../ui/PageTransition';
import GlobalSearch from '../ui/GlobalSearch';
import NotificationBell from '../ui/NotificationBell';
import { useAuthStore } from '../../store/authStore';
import { adminTeamApi } from '../../services/api';
import { areaForAdminPath, areaLabel, canOpenAdminPath, levelFor } from '../../utils/adminAccess';

const pageTitles: Record<string, string> = {
  dashboard:     'Dashboard',
  suppliers:     'Suppliers & Shops',
  'shops-map':   'Shops Map',
  'group-buys':  'Group Buys',
  documents:     'Documents',
  products:      'Products',
  orders:        'Orders',
  reports:       'Reports',
  analytics:     'Analytics',
  notifications: 'Notifications',
  payouts:       'Payouts',
  settings:      'Settings',
  team:          'Team & Access',
};

export default function AdminLayout() {
  const { user, adminProfile, setAdminProfile } = useAuthStore();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  // Access can change while someone is signed in, so refresh it on load and when the tab regains focus.
  useEffect(() => {
    const refresh = () => adminTeamApi.me().then(setAdminProfile).catch(() => {});
    refresh();
    window.addEventListener('focus', refresh);
    return () => window.removeEventListener('focus', refresh);
  }, [setAdminProfile]);

  const segment = location.pathname.split('/').filter(Boolean)[1];
  const currentPage = pageTitles[segment] ?? 'Dashboard';
  const area = areaForAdminPath(location.pathname);
  const allowed = canOpenAdminPath(adminProfile, location.pathname);
  const viewOnlyArea = area !== 'any' && area !== 'master' && levelFor(adminProfile, area) === 'view' ? area : null;
  const name = adminProfile?.displayName || user?.email || 'Admin';
  const title = adminProfile?.title || (adminProfile?.isMaster ? 'Master admin' : 'Platform Admin');

  return (
    <div className="flex min-h-[100dvh] bg-surface">
      {mobileMenuOpen && (
        <button
          type="button"
          aria-label="Close navigation menu"
          className="fixed inset-0 z-40 bg-brand-blue-dark/40 backdrop-blur-[2px] lg:hidden"
          onClick={() => setMobileMenuOpen(false)}
        />
      )}
      <AdminSidebar
        collapsed={collapsed}
        onToggle={() => setCollapsed((c) => !c)}
        mobileOpen={mobileMenuOpen}
        onClose={() => setMobileMenuOpen(false)}
      />

      <div className="flex-1 flex flex-col min-w-0 overflow-x-hidden">
        <header className="sticky top-0 z-30 flex-shrink-0 bg-surface/85 backdrop-blur-xl">
          <div className="flex items-center justify-between gap-3 px-4 sm:px-6 lg:px-8 h-[76px]">
            <div className="flex flex-1 items-center gap-3 min-w-0">
              <button
                type="button"
                aria-label="Open navigation menu"
                className="lg:hidden inline-flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full bg-white border border-brand-blue-light text-ink-2"
                onClick={() => setMobileMenuOpen(true)}
              >
                <Menu size={19} />
              </button>
              <p className="md:hidden truncate text-lg font-black text-gray-900">{currentPage}</p>
              <GlobalSearch variant="admin" />
            </div>

            <div className="flex flex-shrink-0 items-center gap-2 sm:gap-3">
              <NotificationBell variant="admin" />
              <div className="flex items-center gap-2.5 rounded-full bg-white border border-brand-blue-light pl-1 pr-1 sm:pr-4 py-1">
                <div className="w-9 h-9 rounded-full bg-brand-blue text-white flex items-center justify-center">
                  <Shield size={16} />
                </div>
                <div className="hidden md:block max-w-[180px]">
                  <p className="text-sm font-extrabold text-gray-900 leading-tight truncate">{name}</p>
                  <p className="text-[11px] font-bold text-ink-3 leading-tight truncate">{title}</p>
                </div>
              </div>
            </div>
          </div>
        </header>

        <main className="flex-1 min-w-0 overflow-y-auto overflow-x-hidden">
          <div className="mx-auto w-full max-w-[1440px] sm:px-2 lg:px-4 pb-8">
            {allowed && viewOnlyArea && (
              <div className="mx-4 sm:mx-6 mt-4 flex items-start gap-3 rounded-2xl border border-brand-blue-light bg-white px-4 py-3 text-sm text-ink-2">
                <Eye size={17} className="mt-0.5 flex-shrink-0 text-brand-blue" />
                <span>You have view-only access to {areaLabel(viewOnlyArea)}. Changes you try to make here will be refused.</span>
              </div>
            )}
            <PageTransition>
              {allowed ? <Outlet /> : <NoAccess />}
            </PageTransition>
          </div>
        </main>
      </div>
    </div>
  );
}

function NoAccess() {
  return (
    <div className="p-6">
      <div className="card max-w-lg mx-auto mt-10 p-8 text-center">
        <div className="w-12 h-12 mx-auto rounded-2xl bg-brand-blue-light flex items-center justify-center text-brand-blue">
          <Lock size={22} />
        </div>
        <h1 className="mt-4 text-lg font-black text-gray-900">You don't have access to this page</h1>
        <p className="mt-2 text-sm text-ink-3">Your role doesn't include this part of the admin console. Ask a master admin if you need it.</p>
        <Link to="/admin/dashboard" className="btn-primary inline-flex mt-5">Back to dashboard</Link>
      </div>
    </div>
  );
}
