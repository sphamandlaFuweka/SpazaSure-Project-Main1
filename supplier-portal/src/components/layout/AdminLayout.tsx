import { useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { ChevronRight, Menu, Shield } from 'lucide-react';
import AdminSidebar from './AdminSidebar';
import PageTransition from '../ui/PageTransition';
import GlobalSearch from '../ui/GlobalSearch';
import LiveClock from '../ui/LiveClock';
import NotificationBell from '../ui/NotificationBell';
import { useAuthStore } from '../../store/authStore';

const breadcrumbMap: Record<string, string> = {
  dashboard:     'Dashboard',
  suppliers:     'Suppliers',
  'shops-map':   'Shops Map',
  'group-buys':  'Group Buys',
  documents:     'Document Verification',
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

  const segments = location.pathname.split('/').filter(Boolean);
  const currentPage = breadcrumbMap[segments[1]] ?? 'Dashboard';

  return (
    <div className="flex min-h-[100dvh] bg-slate-50">
      {mobileMenuOpen && (
        <button
          type="button"
          aria-label="Close navigation menu"
          className="fixed inset-0 z-40 bg-slate-950/50 lg:hidden"
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
        {/* Header */}
        <header
          className="bg-white/95 lg:bg-white/80 backdrop-blur-xl border-b border-gray-100/80 px-3 sm:px-4 lg:px-6 py-2.5 lg:py-3 flex items-center justify-between gap-2 flex-shrink-0 sticky top-0 z-30"
          style={{ boxShadow: '0 1px 3px rgba(0,0,0,0.03), 0 1px 0 rgba(0,0,0,0.02)' }}
        >
          <div className="flex items-center gap-2 min-w-0 text-sm">
            <button
              type="button"
              aria-label="Open navigation menu"
              className="lg:hidden inline-flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl text-slate-600 hover:bg-slate-100"
              onClick={() => setMobileMenuOpen(true)}
            >
              <Menu size={20} />
            </button>
            <div className="hidden sm:flex items-center gap-2 bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-100/60 px-2.5 py-1 rounded-lg">
              <div className="w-4 h-4 bg-gradient-to-br from-blue-600 to-indigo-600 rounded flex items-center justify-center">
                <Shield size={9} className="text-white" />
              </div>
              <span className="text-xs font-bold text-blue-700">Admin</span>
            </div>
            <ChevronRight size={13} className="hidden sm:block flex-shrink-0 text-gray-300" />
            <span className="truncate font-bold text-gray-900 text-sm">{currentPage}</span>
          </div>

          <div className="flex flex-shrink-0 items-center gap-1 sm:gap-2 md:gap-3">
            {/* Live Clock */}
            <LiveClock variant="admin" />

            {/* Search */}
            <GlobalSearch variant="admin" />

            {/* Notifications */}
            <NotificationBell variant="admin" />

            {/* Admin Avatar */}
            <div className="flex items-center gap-2 pl-2 sm:pl-3 ml-1 border-l border-gray-200/60">
              <div className="w-8 h-8 rounded-xl bg-gradient-to-br from-blue-600 to-indigo-600 flex items-center justify-center text-white text-xs font-black shadow-sm shadow-blue-200 ring-2 ring-blue-100">
                {(user?.email ?? 'A').charAt(0).toUpperCase()}
              </div>
              <div className="hidden sm:block">
                <p className="text-sm font-bold text-gray-900 leading-tight">Platform Admin</p>
                <p className="text-[11px] text-gray-400 leading-tight">{user?.email}</p>
              </div>
            </div>
          </div>
        </header>

        <main className="flex-1 min-w-0 overflow-y-auto overflow-x-hidden">
          <PageTransition>
            <Outlet />
          </PageTransition>
        </main>
      </div>
    </div>
  );
}
