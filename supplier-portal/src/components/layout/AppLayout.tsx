import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { ChevronRight, Menu } from 'lucide-react';
import Sidebar from './Sidebar';
import { useAuthStore } from '../../store/authStore';
import { Avatar } from '../ui';
import { resolveUploadUrl } from '../../services/api';
import PageTransition from '../ui/PageTransition';
import GlobalSearch from '../ui/GlobalSearch';
import LiveClock from '../ui/LiveClock';
import NotificationBell from '../ui/NotificationBell';

const breadcrumbMap: Record<string, string> = {
  dashboard: 'Dashboard',
  products: 'Products',
  orders: 'Orders',
  analytics: 'Analytics',
  notifications: 'Notifications',
  profile: 'Profile',
  'group-buy': 'Group Buy',
  subscription: 'Upgrade',
};

export default function AppLayout() {
  const { user } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const segments = location.pathname.split('/').filter(Boolean);
  const currentPage = breadcrumbMap[segments[0]] ?? 'Dashboard';

  return (
    <div className="flex min-h-[100dvh] bg-surface">
      {mobileMenuOpen && (
        <button
          type="button"
          aria-label="Close navigation menu"
          className="fixed inset-0 z-40 bg-slate-950/50 lg:hidden"
          onClick={() => setMobileMenuOpen(false)}
        />
      )}
      <Sidebar
        collapsed={collapsed}
        onToggle={() => setCollapsed((c) => !c)}
        mobileOpen={mobileMenuOpen}
        onClose={() => setMobileMenuOpen(false)}
      />

      <div className="flex-1 flex flex-col min-w-0 overflow-x-hidden">
        {/* Top Header */}
        <header className="bg-white/95 lg:bg-white/80 backdrop-blur-xl border-b border-gray-100/80 px-3 sm:px-4 lg:px-6 py-2.5 lg:py-3 compact:py-2 flex items-center justify-between gap-2 flex-shrink-0 sticky top-0 z-30" style={{ boxShadow: '0 1px 3px rgba(0,0,0,0.03), 0 1px 0 rgba(0,0,0,0.02)' }}>
          {/* Breadcrumb */}
          <div className="flex items-center gap-2 min-w-0 text-sm">
            <button
              type="button"
              aria-label="Open navigation menu"
              className="lg:hidden inline-flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl text-gray-600 hover:bg-gray-100"
              onClick={() => setMobileMenuOpen(true)}
            >
              <Menu size={20} />
            </button>
            <div className="hidden sm:flex items-center gap-2 bg-gradient-to-r from-primary-50 to-emerald-50 border border-primary-100/60 px-2.5 py-1 rounded-lg">
              <div className="w-4 h-4 bg-gradient-to-br from-primary to-emerald-600 rounded flex items-center justify-center">
                <span className="text-[8px] font-black text-white">S</span>
              </div>
              <span className="text-xs font-bold text-primary">SpazaSure</span>
            </div>
            <ChevronRight size={13} className="hidden sm:block flex-shrink-0 text-gray-300" />
            <span className="truncate font-bold text-gray-900 text-sm">{currentPage}</span>
          </div>

          <div className="flex flex-shrink-0 items-center gap-1 sm:gap-2 md:gap-3">
            {/* Live Clock */}
            <LiveClock variant="supplier" />

            {/* Search */}
            <GlobalSearch />

            {/* Notifications */}
            <NotificationBell variant="supplier" />

            {/* User */}
            <button
              type="button"
              aria-label="Open profile"
              className="flex items-center gap-2 pl-2 sm:pl-3 ml-1 border-l border-gray-200/60 cursor-pointer group text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 rounded-lg"
              onClick={() => navigate('/profile')}
            >
              <Avatar name={user?.companyName ?? 'S'} size="sm" src={user?.logoUrl ? resolveUploadUrl(user.logoUrl) : undefined} />
              <div className="hidden sm:block">
                <p className="text-sm font-bold text-gray-900 leading-tight group-hover:text-primary transition-colors">{user?.companyName}</p>
                <p className="text-[11px] text-gray-400 leading-tight">{user?.email}</p>
              </div>
            </button>
          </div>
        </header>

        {/* Main Content */}
        <main className="flex-1 min-w-0 overflow-y-auto overflow-x-hidden">
          <PageTransition>
            <Outlet />
          </PageTransition>
        </main>
      </div>
    </div>
  );
}
