import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import {
  LayoutDashboard, Users, ShieldCheck, Package, ShoppingCart, Bell,
  BarChart2, Settings, LogOut, ChevronLeft, ChevronRight, Flag, MapPin, UsersRound, Wallet,
} from 'lucide-react';
import clsx from 'clsx';
import { useEffect } from 'react';
import { useAuthStore } from '../../store/authStore';
import { useAdminBadgeStore } from '../../store/adminBadgeStore';

interface AdminSidebarProps {
  collapsed: boolean;
  onToggle: () => void;
  mobileOpen: boolean;
  onClose: () => void;
}

export default function AdminSidebar({ collapsed, onToggle, mobileOpen, onClose }: AdminSidebarProps) {
  const { user, logout } = useAuthStore();
  const { pendingSuppliers, pendingDocuments, pendingReports, fetchBadges } = useAdminBadgeStore();
  const navigate = useNavigate();
  const location = useLocation();

  // Fetch badge counts on mount and poll every 60 seconds
  useEffect(() => {
    fetchBadges();
    const interval = setInterval(fetchBadges, 60_000);
    return () => clearInterval(interval);
  }, [fetchBadges]);

  const handleLogout = () => { logout(); navigate('/login'); };

  const sections: { title: string; items: { to: string; icon: typeof Users; label: string; badge?: number }[] }[] = [
    {
      title: 'Overview',
      items: [
        { to: '/admin/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
        { to: '/admin/analytics', icon: BarChart2,       label: 'Analytics' },
      ],
    },
    {
      title: 'Trust & safety',
      items: [
        { to: '/admin/suppliers', icon: Users,       label: 'Suppliers & Shops', badge: pendingSuppliers || undefined },
        { to: '/admin/documents', icon: ShieldCheck, label: 'Documents',         badge: pendingDocuments || undefined },
        { to: '/admin/reports',   icon: Flag,        label: 'Reports',           badge: pendingReports || undefined },
        { to: '/admin/shops-map', icon: MapPin,      label: 'Shops Map' },
      ],
    },
    {
      title: 'Marketplace',
      items: [
        { to: '/admin/products',   icon: Package,      label: 'Products' },
        { to: '/admin/orders',     icon: ShoppingCart, label: 'Orders' },
        { to: '/admin/group-buys', icon: UsersRound,   label: 'Group Buys' },
        { to: '/admin/payouts',    icon: Wallet,       label: 'Payouts' },
      ],
    },
    {
      title: 'System',
      items: [
        { to: '/admin/notifications', icon: Bell,     label: 'Notifications' },
        { to: '/admin/settings',      icon: Settings, label: 'Settings' },
      ],
    },
  ];

  return (
    <aside
      className={clsx(
        'fixed inset-y-0 left-0 z-50 flex h-[100dvh] w-[264px] flex-col flex-shrink-0 bg-white border-r border-brand-blue-light',
        'transition-transform duration-300 ease-in-out lg:sticky lg:top-0 lg:translate-x-0 lg:transition-[width] lg:duration-300',
        mobileOpen ? 'translate-x-0 shadow-card-xl' : '-translate-x-full',
        collapsed ? 'lg:w-[84px]' : 'lg:w-[264px]'
      )}
    >
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
            <p className="mt-1 inline-flex text-[10px] font-extrabold uppercase tracking-wider text-brand-red bg-brand-red-light px-2 py-0.5 rounded-full">
              Admin Console
            </p>
          </div>
        )}
      </div>

      {/* Nav */}
      <nav className={clsx('flex-1 overflow-y-auto pb-4', collapsed ? 'px-3' : 'px-4')} aria-label="Admin navigation">
        {sections.map((section) => (
          <div key={section.title} className="mt-4 first:mt-1">
            {!collapsed && (
              <p className="px-3 mb-2 text-[11px] font-extrabold uppercase tracking-wider text-ink-3">{section.title}</p>
            )}
            <div className="space-y-1">
              {section.items.map(({ to, icon: Icon, label, badge }) => {
                const isActive = location.pathname.startsWith(to);
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
                        isActive ? 'bg-white text-brand-blue' : 'bg-brand-red text-white'
                      )}>
                        {badge > 99 ? '99+' : badge}
                      </span>
                    )}
                    {collapsed && badge && (
                      <span className="absolute top-1.5 right-1.5 w-2.5 h-2.5 bg-brand-red rounded-full ring-2 ring-white" />
                    )}
                  </NavLink>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      {/* Account */}
      <div className={clsx('border-t border-brand-blue-light py-3', collapsed ? 'px-3' : 'px-4')}>
        {!collapsed && (
          <div className="flex items-center gap-3 px-2 mb-2">
            <div className="w-10 h-10 rounded-full bg-brand-blue text-white flex items-center justify-center font-black flex-shrink-0">
              {(user?.email ?? 'A').charAt(0).toUpperCase()}
            </div>
            <div className="min-w-0">
              <p className="text-sm font-extrabold text-gray-900 truncate">Platform Admin</p>
              <p className="text-[11px] font-semibold text-ink-3 truncate">{user?.email}</p>
            </div>
          </div>
        )}
        <button
          onClick={handleLogout}
          aria-label="Sign out"
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
