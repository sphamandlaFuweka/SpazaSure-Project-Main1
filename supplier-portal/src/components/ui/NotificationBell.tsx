import { useState, useRef, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, X, CheckCheck, ArrowRight } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import clsx from 'clsx';
import toast from 'react-hot-toast';
import { notificationsApi } from '../../services/api';
import { IS_ADMIN_APP } from '../../config/target';
import { notificationType, notificationRoute } from './notificationTypes';

/** How often new notifications are checked for (and popped up). */
const POLL_MS = 20_000;

interface Notification {
  id: string;
  type: string;
  title: string;
  message: string;
  createdAt: string;
  read: boolean;
  priority?: 'high' | 'normal';
}

export default function NotificationBell({ variant = 'supplier' }: { variant?: 'supplier' | 'admin' }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [justOpened, setJustOpened] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  // Ids already shown, so only notifications that arrive while the dashboard is open pop up
  const seen = useRef<Set<string> | null>(null);

  const open_ = useCallback((n: Notification) => {
    notificationsApi.markRead(n.id).catch(() => {});
    setNotifications((prev) => prev.map((x) => x.id === n.id ? { ...x, read: true } : x));
    const route = notificationRoute(n.type, variant);
    if (route) navigate(route);
  }, [navigate, variant]);

  const popUp = useCallback((n: Notification) => {
    const { icon, iconBg, iconColor } = notificationType(n.type);
    toast.custom((t) => (
      <div
        role="status"
        className={clsx('pointer-events-auto w-[min(22rem,calc(100vw-2rem))] flex items-start gap-3 rounded-2xl bg-white border border-brand-blue-light shadow-card-xl p-4 transition-all',
          t.visible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-2')}
      >
        <button className="flex flex-1 items-start gap-3 text-left min-w-0" onClick={() => { toast.dismiss(t.id); open_(n); }}>
          <span className={clsx('w-9 h-9 rounded-xl flex items-center justify-center flex-shrink-0', iconBg, iconColor)}>{icon}</span>
          <span className="flex-1 min-w-0">
            <span className="block text-sm font-bold text-gray-900">{n.title}</span>
            <span className="block text-xs text-ink-2 mt-0.5 line-clamp-3">{n.message}</span>
          </span>
        </button>
        <button onClick={() => toast.dismiss(t.id)} aria-label="Dismiss" className="text-gray-300 hover:text-gray-500"><X size={14} /></button>
      </div>
    ), { id: `notif-${n.id}`, duration: n.priority === 'high' ? 10_000 : 6_000, position: 'top-right' });

    // Desktop alert when the dashboard is in a background tab (only if the user allowed it)
    if (document.hidden && 'Notification' in window && window.Notification.permission === 'granted') {
      try {
        const desktop = new window.Notification(n.title, { body: n.message, tag: n.id, icon: '/spazasure_logo.png' });
        desktop.onclick = () => { window.focus(); open_(n); desktop.close(); };
      } catch { /* some browsers only allow this from a service worker */ }
    }
  }, [open_]);

  // Load notifications, and pop up any that are new since the last check
  useEffect(() => {
    let mounted = true;
    const load = async () => {
      try {
        const data = await notificationsApi.list({ page: 1, pageSize: 10 });
        if (!mounted) return;
        const items = Array.isArray(data) ? data : data?.items ?? data?.data ?? [];
        const mapped: Notification[] = items.map((n: any) => ({
          id: String(n.id ?? Math.random()),
          type: n.type ?? 'system',
          title: n.title ?? '',
          message: n.message ?? '',
          createdAt: n.createdAt ?? new Date().toISOString(),
          read: n.isRead ?? n.read ?? false,
          priority: n.priority ?? 'normal',
        }));
        if (seen.current) {
          const fresh = mapped.filter((n) => !n.read && !seen.current!.has(n.id));
          fresh.slice(0, 3).reverse().forEach(popUp);
        }
        seen.current = new Set([...(seen.current ?? []), ...mapped.map((n) => n.id)]);
        setNotifications(mapped);
      } catch {
        // Silently fail — notifications are non-critical
      }
    };
    load();
    const interval = setInterval(load, POLL_MS);
    // Check straight away when the user comes back to the tab
    const onVisible = () => { if (!document.hidden) load(); };
    document.addEventListener('visibilitychange', onVisible);
    return () => { mounted = false; clearInterval(interval); document.removeEventListener('visibilitychange', onVisible); };
  }, [popUp]);

  // Keep the tab title showing the unread count, so new notifications are noticed from other tabs
  const unreadForTitle = notifications.filter((n) => !n.read).length;
  useEffect(() => {
    const base = document.title.replace(/^\(\d+\+?\) /, '');
    document.title = unreadForTitle > 0 ? `(${unreadForTitle > 9 ? '9+' : unreadForTitle}) ${base}` : base;
  }, [unreadForTitle]);

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
    // Ask once for desktop alerts; this needs a user click in most browsers
    if ('Notification' in window && window.Notification.permission === 'default') {
      window.Notification.requestPermission().catch(() => {});
    }
    setOpen((o) => !o);
    setJustOpened(true);
    setTimeout(() => setJustOpened(false), 600);
  }

  function markRead(n: Notification) {
    setOpen(false);
    open_(n);
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
    navigate(IS_ADMIN_APP && variant === 'admin' ? '/admin/notifications' : '/notifications');
  }

    const badgeBg    = 'bg-brand-red';

  return (
    <div ref={ref} className="relative">
      {/* Bell button */}
      <button
        onClick={handleOpen}
        aria-label="Notifications"
        className={clsx('relative inline-flex h-11 w-11 items-center justify-center rounded-full bg-white border border-brand-blue-light text-ink-2 transition-colors hover:text-brand-blue hover:border-brand-blue/30', open && 'text-brand-blue border-brand-blue/40')}
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
          className="absolute right-0 top-full mt-2 w-[min(24rem,calc(100vw-1rem))] bg-white rounded-2xl border border-gray-100 shadow-card-xl overflow-hidden z-50"
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
                <p className="text-sm font-semibold text-gray-400">All caught up</p>
              </div>
            ) : (
              preview.map((n, i) => {
                const { icon, iconBg, iconColor } = notificationType(n.type);
                return (
                  <div
                    key={n.id}
                    onClick={() => markRead(n)}
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
