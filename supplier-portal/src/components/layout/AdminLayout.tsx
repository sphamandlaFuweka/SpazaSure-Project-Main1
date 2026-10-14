import { useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Menu, Shield } from 'lucide-react';
import AdminSidebar from './AdminSidebar';
import PageTransition from '../ui/PageTransition';
import GlobalSearch from '../ui/GlobalSearch';
import NotificationBell from '../ui/NotificationBell';
import { useAuthStore } from '../../store/authStore';

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
  settings:      'Settings',
};

export default function AdminLayout() {
  const { user } = useAuthStore();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const segment = location.pathname.split('/').filter(Boolean)[1];
  const currentPage = pageTitles[segment] ?? 'Dashboard';

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
                  <p className="text-sm font-extrabold text-gray-900 leading-tight">Platform Admin</p>
                  <p className="text-[11px] font-bold text-ink-3 leading-tight truncate">{user?.email}</p>
                </div>
              </div>
            </div>
          </div>
        </header>

        <main className="flex-1 min-w-0 overflow-y-auto overflow-x-hidden">
          <div className="mx-auto w-full max-w-[1440px] sm:px-2 lg:px-4 pb-8">
            <PageTransition>
              <Outlet />
            </PageTransition>
          </div>
        </main>
      </div>
    </div>
  );
}
