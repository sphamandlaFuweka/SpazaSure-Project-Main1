import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import {
  LayoutDashboard, Users, ShieldCheck, Package, ShoppingCart,
  BarChart2, Settings, LogOut, ChevronLeft, ChevronRight,
} from 'lucide-react';
import clsx from 'clsx';
import { useEffect } from 'react';
import { useAuthStore } from '../../store/authStore';
import { useAdminBadgeStore } from '../../store/adminBadgeStore';

interface AdminSidebarProps {
  collapsed: boolean;
  onToggle: () => void;
}

export default function AdminSidebar({ collapsed, onToggle }: AdminSidebarProps) {
  const { logout } = useAuthStore();
  const { pendingSuppliers, pendingDocuments, fetchBadges } = useAdminBadgeStore();
  const navigate = useNavigate();
  const location = useLocation();

  // Fetch badge counts on mount and poll every 60 seconds
  useEffect(() => {
    fetchBadges();
    const interval = setInterval(fetchBadges, 60_000);
    return () => clearInterval(interval);
  }, [fetchBadges]);

  const handleLogout = () => { logout(); navigate('/login'); };

  const navItems = [
    { to: '/admin/dashboard',     icon: LayoutDashboard, label: 'Dashboard' },
    { to: '/admin/suppliers',     icon: Users,           label: 'Suppliers & Spaza Owner', badge: pendingSuppliers > 0 ? pendingSuppliers : undefined },
    { to: '/admin/documents',     icon: ShieldCheck,     label: 'Documents',    badge: pendingDocuments > 0 ? pendingDocuments : undefined },
    { to: '/admin/products',      icon: Package,         label: 'Products' },
    { to: '/admin/orders',        icon: ShoppingCart,    label: 'Orders' },
    { to: '/admin/analytics',     icon: BarChart2,       label: 'Analytics' },
    { to: '/admin/settings',      icon: Settings,        label: 'Settings' },
  ];

  return (
    <aside
      className={clsx(
        'relative flex flex-col h-screen sticky top-0 transition-all duration-300 ease-in-out flex-shrink-0 overflow-hidden',
        collapsed ? 'w-[72px]' : 'w-64'
      )}
      style={{ background: 'linear-gradient(160deg, #0F172A 0%, #1E293B 50%, #0F172A 100%)', boxShadow: '4px 0 24px rgba(0,0,0,0.20)' }}
    >
      {/* Toggle */}
      <button
        onClick={onToggle}
        className="absolute -right-3 top-6 z-10 w-6 h-6 bg-white border border-gray-200 rounded-full flex items-center justify-center shadow-md hover:shadow-lg transition-shadow text-gray-500 hover:text-slate-800"
      >
        {collapsed ? <ChevronRight size={12} /> : <ChevronLeft size={12} />}
      </button>

      {/* Logo */}
      <div className={clsx('px-5 py-5 border-b border-white/10', collapsed && 'px-3')}>
        {collapsed ? (
          <div className="w-9 h-9 bg-white/10 rounded-xl flex items-center justify-center font-black text-white text-lg">A</div>
        ) : (
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 bg-white/10 rounded-xl flex items-center justify-center font-black text-white text-sm">A</div>
            <div>
              <h1 className="text-base font-black text-white tracking-tight leading-none">SpazaSure</h1>
              <p className="text-[10px] text-blue-400 font-bold mt-0.5 tracking-widest uppercase">Admin Portal</p>
            </div>
          </div>
        )}
      </div>

      {/* Admin Badge */}
      {!collapsed && (
        <div className="px-4 py-3 border-b border-white/10">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-blue-500/20 border border-blue-400/30 flex items-center justify-center font-bold text-blue-300 text-sm flex-shrink-0">
              AD
            </div>
            <div className="min-w-0">
              <p className="text-sm font-bold text-white truncate">Platform Admin</p>
              <p className="text-[10px] text-blue-400 font-semibold">Full Access</p>
            </div>
          </div>
        </div>
      )}

      {/* Nav */}
      <nav className={clsx('flex-1 py-4 space-y-0.5 overflow-y-auto', collapsed ? 'px-2' : 'px-3')}>
        {navItems.map(({ to, icon: Icon, label, badge }) => {
          const isActive = location.pathname.startsWith(to);
          return (
            <NavLink
              key={to}
              to={to}
              title={collapsed ? label : undefined}
              className={clsx(
                'flex items-center rounded-xl text-sm font-medium transition-all duration-150 relative group',
                collapsed ? 'justify-center p-3' : 'gap-3 px-3.5 py-2.5',
                isActive
                  ? 'bg-blue-500/20 text-blue-300 ring-1 ring-blue-400/30'
                  : 'text-slate-400 hover:bg-white/5 hover:text-white'
              )}
            >
              {isActive && (
                <span className="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-5 bg-blue-400 rounded-r-full" />
              )}
              <Icon size={18} className="flex-shrink-0" />
              {!collapsed && <span className="flex-1">{label}</span>}
              {!collapsed && badge && (
                <span className="bg-blue-500 text-white text-[10px] font-bold px-1.5 py-0.5 rounded-full min-w-[18px] text-center animate-pulse shadow-sm shadow-blue-400/50">
                  {badge}
                </span>
              )}
              {collapsed && badge && (
                <span className="absolute top-1 right-1 w-2.5 h-2.5 bg-blue-400 rounded-full animate-pulse ring-2 ring-white/20" />
              )}
              {collapsed && (
                <span className="absolute left-full ml-3 px-2.5 py-1.5 bg-gray-900 text-white text-xs rounded-lg opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none whitespace-nowrap z-50 shadow-lg">
                  {label}
                </span>
              )}
            </NavLink>
          );
        })}
      </nav>

      {/* Logout */}
      <div className={clsx('border-t border-white/10 py-3', collapsed ? 'px-2' : 'px-3')}>
        <button
          onClick={handleLogout}
          title={collapsed ? 'Sign Out' : undefined}
          className={clsx(
            'flex items-center rounded-xl text-sm text-slate-400 hover:bg-white/5 hover:text-white transition-all duration-150 w-full group relative',
            collapsed ? 'justify-center p-3' : 'gap-3 px-3.5 py-2.5'
          )}
        >
          <LogOut size={18} className="flex-shrink-0" />
          {!collapsed && <span>Sign Out</span>}
          {collapsed && (
            <span className="absolute left-full ml-3 px-2.5 py-1.5 bg-gray-900 text-white text-xs rounded-lg opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none whitespace-nowrap z-50 shadow-lg">
              Sign Out
            </span>
          )}
        </button>
      </div>
    </aside>
  );
}
