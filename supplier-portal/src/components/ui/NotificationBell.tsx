import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, ShoppingCart, AlertTriangle, Package, X, CheckCheck, ArrowRight } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import clsx from 'clsx';
import { notificationsApi } from '../../services/api';

type NotifType = 'order' | 'compliance' | 'product' | 'system';

interface Notification {
  id: string;
  type: NotifType;
  title: string;
  message: string;
  createdAt: string;
  read: boolean;
  priority?: 'high' | 'normal';
}

const typeConfig: Record<NotifType, { icon: React.ReactNode; iconBg: string; iconColor: string }> = {
  order:      { icon: <ShoppingCart size={14} />,  iconBg: 'bg-blue-100',    iconColor: 'text-blue-600' },
  compliance: { icon: <AlertTriangle size={14} />, iconBg: 'bg-amber-100',   iconColor: 'text-amber-600' },
  product:    { icon: <Package size={14} />,       iconBg: 'bg-emerald-100', iconColor: 'text-emerald-600' },
  system:     { icon: <Bell size={14} />,          iconBg: 'bg-gray-100',    iconColor: 'text-gray-500' },
};

export default function NotificationBell({ variant = 'supplier' }: { variant?: 'supplier' | 'admin' }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [justOpened, setJustOpened] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  // Load notifications from API
  useEffect(() => {
    let mounted = true;
    const load = async () => {
      try {
        const data = await notificationsApi.list({ page: 1, pageSize: 10 });
        if (!mounted) return;
        const items = Array.isArray(data) ? data : data?.items ?? data?.data ?? [];
        setNotifications(items.map((n: any) => ({
          id: n.id ?? String(Math.random()),
          type: n.type ?? 'system',
          title: n.title ?? '',
          message: n.message ?? '',
          createdAt: n.createdAt ?? new Date().toISOString(),
          read: n.isRead ?? n.read ?? false,
          priority: n.priority ?? 'normal',
        })));
      } catch {
        // Silently fail — notifications are non-critical
      }
    };
    load();
    const interval = setInterval(load, 30000);
    return () => { mounted = false; clearInterval(interval); };
  }, []);

  const unread = notifications.filter((n) => !n.read);
  const unreadCount = unread.length;
  const preview = notifications.slice(0, 5);

  // Close on outside click
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  function handleOpen() {
    setOpen((o) => !o);
    setJustOpened(true);
    setTimeout(() => setJustOpened(false), 600);
  }

  function markRead(id: string) {
    notificationsApi.markRead(id).catch(() => {});
    setNotifications((prev) => prev.map((n) => n.id === id ? { ...n, read: true } : n));
  }

  function markAllRead() {
    notificationsApi.markAllRead().catch(() => {});
    setNotifications((prev) => prev.map((n) => ({ ...n, read: true })));
  }

  function dismiss(id: string, e: React.MouseEvent) {
    e.stopPropagation();
    setNotifications((prev) => prev.filter((n) => n.id !== id));
  }

  function goToAll() {
    setOpen(false);
    navigate(variant === 'admin' ? '/admin/notifications' : '/notifications');
  }

  const accentRing = variant === 'admin' ? 'hover:text-blue-600 hover:bg-blue-50' : 'hover:text-primary hover:bg-primary-50';
  const badgeBg    = variant === 'admin' ? 'bg-blue-500' : 'bg-red-500';

  return (
    <div ref={ref} className="relative">
      {/* Bell button */}
      <button
        onClick={handleOpen}
        className={clsx('relative p-2.5 text-gray-500 rounded-xl transition-all', accentRing, open && (variant === 'admin' ? 'text-blue-600 bg-blue-50' : 'text-primary bg-primary-50'))}
      >
        <Bell
          size={17}
          style={{
            animation: justOpened && unreadCount > 0 ? 'bellRing 0.5s cubic-bezier(0.36,0.07,0.19,0.97)' : 'none',
          }}
        />
        {/* Unread badge */}
        {unreadCount > 0 && (
          <span
            className={clsx('absolute -top-0.5 -right-0.5 min-w-[17px] h-[17px] flex items-center justify-center text-[10px] font-black text-white rounded-full px-1 ring-2 ring-white', badgeBg)}
            style={{ animation: 'badgePop 0.3s cubic-bezier(0.34,1.56,0.64,1)' }}
          >
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {/* Dropdown */}
      {open && (
        <div
          className="absolute right-0 top-full mt-2 w-96 bg-white rounded-2xl border border-gray-100 shadow-card-xl overflow-hidden z-50"
          style={{ animation: 'dropIn 0.2s cubic-bezier(0.16,1,0.3,1)' }}
        >
          {/* Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50/60">
            <div className="flex items-center gap-2">
              <Bell size={15} className={variant === 'admin' ? 'text-blue-600' : 'text-primary'} />
              <span className="font-bold text-gray-900 text-sm">Notifications</span>
              {unreadCount > 0 && (
                <span className={clsx('text-[10px] font-black text-white px-1.5 py-0.5 rounded-full', badgeBg)}>
                  {unreadCount} new
                </span>
              )}
            </div>
            {unreadCount > 0 && (
              <button
                onClick={markAllRead}
                className="flex items-center gap-1 text-xs font-semibold text-gray-400 hover:text-primary transition-colors"
              >
                <CheckCheck size={13} /> Mark all read
              </button>
            )}
          </div>

          {/* Notification list */}
          <div className="max-h-[360px] overflow-y-auto">
            {preview.length === 0 ? (
              <div className="py-12 text-center">
                <Bell size={28} className="mx-auto text-gray-200 mb-2" />
                <p className="text-sm font-semibold text-gray-400">All caught up! 🎉</p>
              </div>
            ) : (
              preview.map((n, i) => {
                const { icon, iconBg, iconColor } = typeConfig[n.type];
                return (
                  <div
                    key={n.id}
                    onClick={() => markRead(n.id)}
                    className={clsx(
                      'flex items-start gap-3 px-4 py-3 cursor-pointer transition-all duration-150 border-b border-gray-50 last:border-0',
                      !n.read ? 'bg-primary-50/40 hover:bg-primary-50/70' : 'hover:bg-gray-50',
                    )}
                    style={{ animation: `slideItem 0.2s ${i * 0.04}s both cubic-bezier(0.16,1,0.3,1)` }}
                  >
                    {/* Icon */}
                    <div className={clsx('w-8 h-8 rounded-xl flex items-center justify-center flex-shrink-0 mt-0.5', iconBg, iconColor)}>
                      {icon}
                    </div>

                    {/* Content */}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-start justify-between gap-1">
                        <div className="flex items-center gap-1.5 flex-wrap">
                          <p className={clsx('text-sm font-bold leading-tight', !n.read ? 'text-gray-900' : 'text-gray-600')}>
                            {n.title}
                          </p>
                          {n.priority === 'high' && !n.read && (
                            <span className="text-[9px] font-black bg-red-100 text-red-600 px-1.5 py-0.5 rounded-full uppercase tracking-wide">
                              Urgent
                            </span>
                          )}
                        </div>
                        <button
                          onClick={(e) => dismiss(n.id, e)}
                          className="text-gray-300 hover:text-gray-500 transition-colors flex-shrink-0 mt-0.5"
                        >
                          <X size={13} />
                        </button>
                      </div>
                      <p className="text-xs text-gray-500 mt-0.5 leading-relaxed line-clamp-2">{n.message}</p>
                      <p className="text-[11px] text-gray-400 mt-1 font-medium">
                        {(() => { try { return formatDistanceToNow(new Date(n.createdAt), { addSuffix: true }); } catch { return 'recently'; } })()}
                      </p>
                    </div>

                    {/* Unread dot */}
                    {!n.read && (
                      <div className="w-2 h-2 rounded-full bg-primary flex-shrink-0 mt-2 animate-pulse-soft" />
                    )}
                  </div>
                );
              })
            )}
          </div>

          {/* Footer — View all button */}
          <div className="border-t border-gray-100 p-3">
            <button
              onClick={goToAll}
              className={clsx(
                'w-full flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-bold transition-all',
                variant === 'admin'
                  ? 'bg-blue-50 text-blue-700 hover:bg-blue-100'
                  : 'bg-primary-50 text-primary hover:bg-primary-100'
              )}
            >
              View all notifications <ArrowRight size={14} />
            </button>
          </div>
        </div>
      )}

      <style>{`
        @keyframes bellRing {
          0%,100% { transform: rotate(0deg); }
          20%      { transform: rotate(-15deg); }
          40%      { transform: rotate(15deg); }
          60%      { transform: rotate(-10deg); }
          80%      { transform: rotate(8deg); }
        }
        @keyframes badgePop {
          from { transform: scale(0); opacity: 0; }
          to   { transform: scale(1); opacity: 1; }
        }
        @keyframes dropIn {
          from { opacity: 0; transform: translateY(-8px) scale(0.97); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
        @keyframes slideItem {
          from { opacity: 0; transform: translateX(8px); }
          to   { opacity: 1; transform: translateX(0); }
        }
      `}</style>
    </div>
  );
}
