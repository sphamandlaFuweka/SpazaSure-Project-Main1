import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { ChevronRight, Shield } from 'lucide-react';
import AdminSidebar from './AdminSidebar';
import PageTransition from '../ui/PageTransition';
import GlobalSearch from '../ui/GlobalSearch';
import LiveClock from '../ui/LiveClock';
import NotificationBell from '../ui/NotificationBell';

const breadcrumbMap: Record<string, string> = {
  dashboard:     'Dashboard',
  suppliers:     'Suppliers',
  documents:     'Document Verification',
  products:      'Products',
  orders:        'Orders',
  analytics:     'Analytics',
  notifications: 'Notifications',
  settings:      'Settings',
};

export default function AdminLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);

  const segments = location.pathname.split('/').filter(Boolean);
  const currentPage = breadcrumbMap[segments[1]] ?? 'Dashboard';

  return (
    <div className="flex min-h-screen bg-slate-50">
      <AdminSidebar collapsed={collapsed} onToggle={() => setCollapsed((c) => !c)} />

      <div className="flex-1 flex flex-col overflow-hidden min-w-0">
        {/* Header */}
        <header
          className="bg-white/80 backdrop-blur-xl border-b border-gray-100/80 px-6 py-3 flex items-center justify-between flex-shrink-0 sticky top-0 z-30"
          style={{ boxShadow: '0 1px 3px rgba(0,0,0,0.03), 0 1px 0 rgba(0,0,0,0.02)' }}
        >
          <div className="flex items-center gap-2.5 text-sm">
            <div className="flex items-center gap-2 bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-100/60 px-2.5 py-1 rounded-lg">
              <div className="w-4 h-4 bg-gradient-to-br from-blue-600 to-indigo-600 rounded flex items-center justify-center">
                <Shield size={9} className="text-white" />
              </div>
              <span className="text-xs font-bold text-blue-700">Admin</span>
            </div>
            <ChevronRight size={13} className="text-gray-300" />
            <span className="font-bold text-gray-900 text-sm">{currentPage}</span>
          </div>

          <div className="flex items-center gap-3">
            {/* Live Clock */}
            <LiveClock variant="admin" />

            {/* Search */}
            <GlobalSearch />

            {/* Notifications */}
            <NotificationBell variant="admin" />

            {/* Admin Avatar */}
            <div className="flex items-center gap-2.5 pl-3 ml-1 border-l border-gray-200/60">
              <div className="w-8 h-8 rounded-xl bg-gradient-to-br from-blue-600 to-indigo-600 flex items-center justify-center text-white text-xs font-black shadow-sm shadow-blue-200 ring-2 ring-blue-100">
                A
              </div>
              <div className="hidden sm:block">
                <p className="text-sm font-bold text-gray-900 leading-tight">Platform Admin</p>
                <p className="text-[11px] text-gray-400 leading-tight">admin@spazasure.co.za</p>
              </div>
            </div>
          </div>
        </header>

        <main className="flex-1 overflow-auto">
          <PageTransition>
            <Outlet />
          </PageTransition>
        </main>
      </div>
    </div>
  );
}
