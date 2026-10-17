import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { Menu, ChevronDown } from 'lucide-react';
import Sidebar from './Sidebar';
import { useAuthStore } from '../../store/authStore';
import { Avatar } from '../ui';
import { resolveUploadUrl } from '../../services/api';
import PageTransition from '../ui/PageTransition';
import GlobalSearch from '../ui/GlobalSearch';
import NotificationBell from '../ui/NotificationBell';

const pageTitles: Record<string, string> = {
  dashboard: 'Dashboard',
  products: 'Products',
  orders: 'Orders',
  analytics: 'Analytics',
  notifications: 'Notifications',
  profile: 'Profile & Documents',
  'group-buy': 'Group Buy Requests',
  subscription: 'Subscription',
};

export default function AppLayout() {
  const { user } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const segment = location.pathname.split('/').filter(Boolean)[0];
  const currentPage = pageTitles[segment] ?? 'Dashboard';
  const tierLabel = user?.tier ? `${user.tier.charAt(0).toUpperCase()}${user.tier.slice(1)} supplier` : 'Supplier';

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
      <Sidebar
        collapsed={collapsed}
        onToggle={() => setCollapsed((c) => !c)}
        mobileOpen={mobileMenuOpen}
        onClose={() => setMobileMenuOpen(false)}
      />

      <div className="flex-1 flex flex-col min-w-0 overflow-x-hidden">
        {/* Top Header */}
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
              {/* Page name only on small screens, where the search box is hidden */}
              <p className="md:hidden truncate text-lg font-black text-gray-900">{currentPage}</p>
              <GlobalSearch />
            </div>

            <div className="flex flex-shrink-0 items-center gap-2 sm:gap-3">
              <NotificationBell variant="supplier" />
              <button
                type="button"
                aria-label="Open profile"
                className="flex items-center gap-2.5 rounded-full bg-white border border-brand-blue-light pl-1 pr-1 sm:pr-3 py-1 hover:border-brand-blue/40 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-blue/30"
                onClick={() => navigate('/profile')}
              >
                <Avatar name={user?.companyName ?? 'S'} size="md" src={user?.logoUrl ? resolveUploadUrl(user.logoUrl) : undefined} />
                <div className="hidden md:block text-left max-w-[160px]">
                  <p className="text-sm font-extrabold text-gray-900 leading-tight truncate">{user?.companyName}</p>
                  <p className="text-[11px] font-bold text-ink-3 leading-tight">{tierLabel}</p>
                </div>
                <ChevronDown size={14} className="hidden md:block text-ink-3" />
              </button>
            </div>
          </div>
        </header>

        {/* Main Content */}
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
