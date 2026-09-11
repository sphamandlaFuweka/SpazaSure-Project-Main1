import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import {
  LayoutDashboard, Package, ShoppingCart, BarChart2, User,
  LogOut, CheckCircle, ChevronLeft, ChevronRight,
  AlertTriangle, Crown, Users,
} from 'lucide-react';
import clsx from 'clsx';
import { useEffect } from 'react';
import { useAuthStore } from '../../store/authStore';
import { useOrderStore } from '../../store/orderStore';
import { TierBadge } from '../ui';
import { resolveUploadUrl } from '../../services/api';

const baseNavItems = [
  { to: '/dashboard',     icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/products',      icon: Package,          label: 'Products' },
  { to: '/orders',        icon: ShoppingCart,     label: 'Orders' },
  { to: '/group-buy',     icon: Users,            label: 'Group Buy' },
  { to: '/analytics',     icon: BarChart2,        label: 'Analytics' },
  { to: '/subscription',  icon: Crown,            label: 'Upgrade',  highlight: true },
  { to: '/profile',       icon: User,             label: 'Profile' },
];

interface SidebarProps {
  collapsed: boolean;
  onToggle: () => void;
}

export default function Sidebar({ collapsed, onToggle }: SidebarProps) {
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

  // Build nav items with dynamic badge
  const navItems = baseNavItems.map((item) => ({
    ...item,
    badge: item.to === '/orders' && pendingCount > 0 ? pendingCount : undefined,
  }));

  return (
    <aside
      className={clsx(
        'relative flex flex-col bg-gradient-primary h-screen sticky top-0 transition-all duration-300 ease-in-out flex-shrink-0 overflow-hidden',
        collapsed ? 'w-[72px]' : 'w-64'
      )}
      style={{ boxShadow: '4px 0 24px rgba(0,0,0,0.15)' }}
    >
      {/* Toggle Button */}
      <button
        onClick={onToggle}
        className="absolute -right-3 top-6 z-10 w-6 h-6 bg-white border border-gray-200 rounded-full flex items-center justify-center shadow-md hover:shadow-lg transition-shadow text-gray-500 hover:text-primary"
      >
        {collapsed ? <ChevronRight size={12} /> : <ChevronLeft size={12} />}
      </button>

      {/* Logo */}
      <div className={clsx('px-5 py-5 border-b border-white/10', collapsed && 'px-3')}>
        {collapsed ? (
          <div className="w-9 h-9 bg-white/20 rounded-xl flex items-center justify-center font-black text-white text-lg">S</div>
        ) : (
          <div>
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 bg-white/20 rounded-xl flex items-center justify-center font-black text-white text-sm">S</div>
              <div>
                <h1 className="text-base font-black text-white tracking-tight leading-none">SpazaSure</h1>
                <p className="text-[10px] text-green-300 font-medium mt-0.5 tracking-wide uppercase">Supplier Portal</p>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Supplier Info */}
      {user && !collapsed && (
        <div className="px-4 py-4 border-b border-white/10">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl overflow-hidden bg-white/20 flex items-center justify-center font-bold text-white text-sm flex-shrink-0">
              {user.logoUrl ? (
                <img src={resolveUploadUrl(user.logoUrl)} alt={user.companyName} className="w-full h-full object-cover" />
              ) : (
                user.companyName.charAt(0)
              )}
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-semibold text-white truncate leading-tight">{user.companyName}</p>
              <div className="flex items-center gap-1.5 mt-1">
                <TierBadge tier={user.tier} />
                {user.isVerified && (
                  <span className="flex items-center gap-0.5 text-[10px] text-green-300 font-medium">
                    <CheckCircle size={10} /> Verified
                  </span>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Nav */}
      <nav className={clsx('flex-1 py-4 space-y-0.5 overflow-y-auto', collapsed ? 'px-2' : 'px-3')}>
        {navItems.map(({ to, icon: Icon, label, badge, highlight }) => {
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
                  ? 'bg-white/20 text-white shadow-sm ring-1 ring-white/20'
                  : highlight
                    ? 'text-accent hover:bg-accent/15 hover:text-accent border border-accent/20'
                    : 'text-green-100/80 hover:bg-white/10 hover:text-white'
              )}
            >
              {isActive && (
                <span className="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-5 bg-white rounded-r-full" />
              )}
              <Icon size={18} className="flex-shrink-0" />
              {!collapsed && <span className="flex-1">{label}</span>}
              {!collapsed && badge && (
                <span className="bg-accent text-white text-[10px] font-bold px-1.5 py-0.5 rounded-full min-w-[18px] text-center animate-pulse shadow-sm shadow-accent/50">
                  {badge}
                </span>
              )}
              {collapsed && badge && (
                <span className="absolute top-1 right-1 w-2.5 h-2.5 bg-accent rounded-full animate-pulse shadow-sm shadow-accent/50 ring-2 ring-white/20" />
              )}
              {/* Tooltip for collapsed */}
              {collapsed && (
                <span className="absolute left-full ml-3 px-2.5 py-1.5 bg-gray-900 text-white text-xs rounded-lg opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none whitespace-nowrap z-50 shadow-lg">
                  {label}
                </span>
              )}
            </NavLink>
          );
        })}
      </nav>

      {/* Compliance Alert — removed: this is now dynamic based on actual document status */}

      {/* Logout */}
      <div className={clsx('border-t border-white/10 py-3', collapsed ? 'px-2' : 'px-3')}>
        <button
          onClick={handleLogout}
          title={collapsed ? 'Sign Out' : undefined}
          className={clsx(
            'flex items-center rounded-xl text-sm text-green-100/70 hover:bg-white/10 hover:text-white transition-all duration-150 w-full group relative',
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
