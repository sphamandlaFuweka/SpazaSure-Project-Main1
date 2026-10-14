import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import {
  LayoutDashboard, Package, ShoppingCart, BarChart2, User,
  LogOut, ChevronLeft, ChevronRight, Crown, Users, Bell, Sparkles, BadgeCheck,
} from 'lucide-react';
import clsx from 'clsx';
import { useEffect } from 'react';
import { useAuthStore } from '../../store/authStore';
import { useOrderStore } from '../../store/orderStore';
import { TierBadge } from '../ui';
import { resolveUploadUrl } from '../../services/api';

type NavItem = { to: string; icon: typeof LayoutDashboard; label: string };

const navSections: { title: string; items: NavItem[] }[] = [
  {
    title: 'Main menu',
    items: [
      { to: '/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
      { to: '/orders',    icon: ShoppingCart,    label: 'Orders' },
      { to: '/products',  icon: Package,         label: 'Products' },
      { to: '/group-buy', icon: Users,           label: 'Group Buy' },
    ],
  },
  {
    title: 'Business',
    items: [
      { to: '/analytics',     icon: BarChart2, label: 'Analytics' },
      { to: '/notifications', icon: Bell,      label: 'Notifications' },
      { to: '/subscription',  icon: Crown,     label: 'Subscription' },
      { to: '/profile',       icon: User,      label: 'Profile & Documents' },
    ],
  },
];

interface SidebarProps {
  collapsed: boolean;
  onToggle: () => void;
  mobileOpen: boolean;
  onClose: () => void;
}

export default function Sidebar({ collapsed, onToggle, mobileOpen, onClose }: SidebarProps) {
  const { user, logout } = useAuthStore();
  const { pendingCount, fetchPendingCount } = useOrderStore();
  const navigate = useNavigate();
  const location = useLocation();

  // Fetch pending order count on mount and poll every 60 seconds
  useEffect(() => {
    fetchPendingCount();
    const interval = setInterval(fetchPendingCount, 60_000);
    return () => clearInterval(interval);
  }, [fetchPendingCount]);

  const handleLogout = () => { logout(); navigate('/login'); };
  const onTopTier = user?.tier === 'gold';

  return (
    <aside
      className={clsx(
        'fixed inset-y-0 left-0 z-50 flex h-[100dvh] w-[264px] flex-col flex-shrink-0 bg-white border-r border-brand-blue-light',
        'transition-transform duration-300 ease-in-out lg:sticky lg:top-0 lg:translate-x-0 lg:transition-[width] lg:duration-300',
        mobileOpen ? 'translate-x-0 shadow-card-xl' : '-translate-x-full',
        collapsed ? 'lg:w-[84px]' : 'lg:w-[264px]'
      )}
    >
      {/* Collapse toggle */}
      <button
        onClick={onToggle}
        aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        className="absolute -right-3 top-8 z-10 hidden w-6 h-6 bg-white border border-brand-blue-light rounded-full lg:flex items-center justify-center shadow-card text-ink-3 hover:text-brand-blue transition-colors"
      >
        {collapsed ? <ChevronRight size={12} /> : <ChevronLeft size={12} />}
      </button>

      {/* Logo */}
      <div className={clsx('flex items-center gap-3 h-[76px] flex-shrink-0', collapsed ? 'justify-center px-3' : 'px-6')}>
        <img src="/spazasure_logo.jpg" alt="SpazaSure" className="w-10 h-10 rounded-xl object-cover flex-shrink-0" />
        {!collapsed && (
          <div className="min-w-0">
            <p className="text-lg font-black text-brand-blue tracking-tight leading-none">SpazaSure</p>
            <p className="text-[11px] font-bold text-ink-3 mt-1">Supplier Portal</p>
          </div>
        )}
      </div>

      {/* Nav */}
      <nav className={clsx('flex-1 overflow-y-auto pb-4', collapsed ? 'px-3' : 'px-4')} aria-label="Main navigation">
        {navSections.map((section) => (
          <div key={section.title} className="mt-4 first:mt-1">
            {!collapsed && (
              <p className="px-3 mb-2 text-[11px] font-extrabold uppercase tracking-wider text-ink-3">{section.title}</p>
            )}
            <div className="space-y-1">
              {section.items.map(({ to, icon: Icon, label }) => {
                const isActive = location.pathname.startsWith(to);
                const badge = to === '/orders' && pendingCount > 0 ? pendingCount : undefined;
                return (
                  <NavLink
                    key={to}
                    to={to}
                    onClick={onClose}
                    title={collapsed ? label : undefined}
                    aria-current={isActive ? 'page' : undefined}
                    className={clsx(
                      'relative flex items-center rounded-2xl text-sm font-bold transition-colors duration-150',
                      collapsed ? 'justify-center h-11' : 'gap-3 px-3 h-11',
                      isActive
                        ? 'bg-brand-blue text-white shadow-glow'
                        : 'text-ink-2 hover:bg-brand-blue-light/60 hover:text-brand-blue'
                    )}
                  >
                    <Icon size={18} className="flex-shrink-0" />
                    {!collapsed && <span className="flex-1 truncate">{label}</span>}
                    {!collapsed && badge && (
                      <span className={clsx(
                        'text-[11px] font-black px-2 py-0.5 rounded-full min-w-[22px] text-center',
                        isActive ? 'bg-white text-brand-blue' : 'bg-brand-yellow text-brand-blue'
                      )}>
                        {badge}
                      </span>
                    )}
                    {collapsed && badge && (
                      <span className="absolute top-1.5 right-1.5 w-2.5 h-2.5 bg-brand-yellow rounded-full ring-2 ring-white" />
                    )}
                  </NavLink>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      {/* Upgrade card (reference: "Upgrade to Pro") */}
      {!collapsed && !onTopTier && (
        <div className="mx-4 mb-3 rounded-3xl bg-gradient-hero p-4 text-white relative overflow-hidden">
          <div className="absolute -right-6 -top-6 w-24 h-24 rounded-full bg-white/10" aria-hidden="true" />
          <Sparkles size={18} className="text-brand-yellow" />
          <p className="mt-2 text-sm font-black leading-snug">Pay less commission</p>
          <p className="text-xs text-white/70 mt-1 leading-relaxed">Upgrade your plan to keep more of every order.</p>
          <button
            onClick={() => { onClose(); navigate('/subscription'); }}
            className="mt-3 w-full rounded-full bg-brand-yellow text-brand-blue text-xs font-extrabold py-2 hover:bg-accent-400 transition-colors"
          >
            View plans
          </button>
        </div>
      )}

      {/* Account */}
      <div className={clsx('border-t border-brand-blue-light py-3', collapsed ? 'px-3' : 'px-4')}>
        {user && !collapsed && (
          <div className="flex items-center gap-3 px-2 mb-2">
            <div className="w-10 h-10 rounded-full overflow-hidden bg-brand-blue-light flex items-center justify-center font-black text-brand-blue flex-shrink-0">
              {user.logoUrl
                ? <img src={resolveUploadUrl(user.logoUrl)} alt="" className="w-full h-full object-cover" />
                : user.companyName.charAt(0).toUpperCase()}
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-extrabold text-gray-900 truncate flex items-center gap-1">
                {user.companyName}
                {user.isVerified && <BadgeCheck size={14} className="text-brand-green flex-shrink-0" aria-label="Verified" />}
              </p>
              <div className="mt-0.5"><TierBadge tier={user.tier} /></div>
            </div>
          </div>
        )}
        <button
          onClick={handleLogout}
          title={collapsed ? 'Sign out' : undefined}
          className={clsx(
            'flex items-center rounded-2xl text-sm font-bold text-ink-2 hover:bg-brand-red-light hover:text-brand-red transition-colors w-full h-10',
            collapsed ? 'justify-center' : 'gap-3 px-3'
          )}
        >
          <LogOut size={17} className="flex-shrink-0" />
          {!collapsed && <span>Sign out</span>}
        </button>
      </div>
    </aside>
  );
}
