import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, CheckCircle, AlertTriangle, ShoppingCart, Users, X, Loader2, RefreshCw, Package, ChevronRight } from 'lucide-react';
import { format, isToday, isYesterday } from 'date-fns';
import clsx from 'clsx';
import toast from 'react-hot-toast';
import { notificationsApi } from '../../services/api';
import { notificationRoute } from '../../components/ui/notificationTypes';

type NotifType = 'system' | 'compliance' | 'order' | 'supplier' | 'product';

interface AdminNotification {
  id: string;
  type: NotifType;
  title: string;
  message: string;
  createdAt: string;
  read: boolean;
  priority: 'high' | 'normal';
  referenceId?: string;
}

const typeConfig: Record<NotifType, { icon: React.ReactNode; color: string; bg: string; label: string }> = {
  system:     { icon: <Bell size={15} />,          color: 'text-slate-600',   bg: 'bg-slate-100',   label: 'System' },
  compliance: { icon: <AlertTriangle size={15} />, color: 'text-amber-600',   bg: 'bg-amber-100',   label: 'Compliance' },
  order:      { icon: <ShoppingCart size={15} />,  color: 'text-blue-600',    bg: 'bg-blue-100',    label: 'Orders' },
  supplier:   { icon: <Users size={15} />,         color: 'text-emerald-600', bg: 'bg-emerald-100', label: 'Suppliers' },
  product:    { icon: <Package size={15} />,       color: 'text-violet-600',  bg: 'bg-violet-100',  label: 'Products' },
};

// Where clicking a notification takes the admin
function getDateLabel(d: string) {
  const date = new Date(d);
  if (isToday(date)) return 'Today';
  if (isYesterday(date)) return 'Yesterday';
  return format(date, 'EEEE, d MMMM');
}

export default function AdminNotificationsPage() {
  const navigate = useNavigate();
  const [notifications, setNotifications] = useState<AdminNotification[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [typeFilter, setTypeFilter] = useState<'all' | NotifType>('all');

  const load = async () => {
    setLoading(true);
    setLoadError(false);
    try {
      const data: any = await notificationsApi.listStrict({ page: 1, pageSize: 50 });
      const items: any[] = Array.isArray(data) ? data : data?.items ?? data?.data ?? [];
      setNotifications(items.map((n) => ({
        id: n.id,
        type: (n.type in typeConfig ? n.type : 'system') as NotifType,
        title: n.title ?? '',
        message: n.message ?? n.body ?? '',
        createdAt: n.createdAt ?? new Date().toISOString(),
        read: n.isRead ?? n.read ?? false,
        priority: n.priority === 'high' ? 'high' : 'normal',
        referenceId: n.referenceId ?? undefined,
      })));
    } catch {
      setNotifications([]);
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const markRead = (id: string) => {
    setNotifications((prev) => prev.map((n) => n.id === id ? { ...n, read: true } : n));
    notificationsApi.markRead(id).catch(() => {});
  };

  const markAllRead = async () => {
    try {
      await notificationsApi.markAllRead();
      setNotifications((prev) => prev.map((n) => ({ ...n, read: true })));
      toast.success('All notifications marked as read');
    } catch {
      // error toast handled by api interceptor
    }
  };

  const dismiss = (n: AdminNotification) => {
    if (!n.read) markRead(n.id);
    setNotifications((prev) => prev.filter((x) => x.id !== n.id));
  };

  const open = (n: AdminNotification) => {
    if (!n.read) markRead(n.id);
    const target = notificationRoute(n.type, 'admin', n.referenceId);
    if (target) navigate(target);
  };

  const filtered = notifications.filter((n) => typeFilter === 'all' || n.type === typeFilter);
  const unreadCount = notifications.filter((n) => !n.read).length;
  const presentTypes = Array.from(new Set(notifications.map((n) => n.type)));

  const grouped = filtered.reduce<Record<string, AdminNotification[]>>((acc, n) => {
    const label = getDateLabel(n.createdAt);
    if (!acc[label]) acc[label] = [];
    acc[label].push(n);
    return acc;
  }, {});

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-[300px]">
        <Loader2 size={32} className="text-gray-300 animate-spin" />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in max-w-3xl">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-black text-gray-900">Notifications</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {loadError ? 'Could not load notifications' : unreadCount > 0 ? `${unreadCount} unread` : 'All caught up!'}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {unreadCount > 0 && (
            <button onClick={markAllRead} className="flex items-center gap-1.5 text-sm font-semibold text-slate-700 bg-white border border-gray-200 hover:bg-gray-50 px-3 py-2 rounded-xl">
              <CheckCircle size={14} /> Mark all read
            </button>
          )}
          <button onClick={load} aria-label="Refresh notifications" title="Refresh" className="p-2 rounded-xl text-gray-500 hover:bg-gray-100">
            <RefreshCw size={16} />
          </button>
        </div>
      </div>

      {loadError && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
          <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-red-800">We couldn't load notifications. Check your connection and try again.</p>
          <button onClick={load} className="text-sm font-semibold bg-white border border-red-200 rounded-xl px-3 py-1.5 hover:bg-red-50">Try again</button>
        </div>
      )}

      {presentTypes.length > 1 && (
        <div className="flex items-center gap-1.5 flex-wrap">
          {(['all', ...presentTypes] as const).map((t) => (
            <button key={t} onClick={() => setTypeFilter(t)}
              className={clsx('px-3 py-1.5 rounded-xl text-xs font-semibold transition-all border',
                typeFilter === t ? 'bg-brand-blue text-white border-brand-blue' : 'bg-white text-gray-500 border-gray-200 hover:border-slate-400 hover:text-slate-700')}>
              {t === 'all' ? 'All' : typeConfig[t as NotifType]?.label ?? t.replace('_', ' ').replace(/^./, (c) => c.toUpperCase())}
            </button>
          ))}
        </div>
      )}

      {loadError ? null : Object.keys(grouped).length === 0 ? (
        <div className="bg-white rounded-2xl border border-gray-100 p-10 sm:p-16 text-center">
          <Bell size={36} className="text-gray-200 mx-auto mb-4" />
          <p className="font-bold text-gray-600">No notifications</p>
          <p className="text-gray-400 text-sm mt-1">Platform alerts for your admin account will appear here.</p>
        </div>
      ) : (
        Object.entries(grouped).map(([dateLabel, items]) => (
          <div key={dateLabel}>
            <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2 px-1">{dateLabel}</p>
            <div className="space-y-2">
              {items.map((n) => {
                const { icon, color, bg } = typeConfig[n.type as NotifType] ?? typeConfig.system;
                return (
                  <div key={n.id} onClick={() => open(n)} role="button" tabIndex={0}
                    onKeyDown={(e) => { if (e.key === 'Enter') open(n); }}
                    className={clsx('bg-white rounded-2xl border border-gray-100 p-4 flex items-start gap-3 sm:gap-4 cursor-pointer hover:shadow-md transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-400/40',
                      !n.read && 'border-l-4 border-l-blue-500',
                      n.priority === 'high' && !n.read && 'bg-amber-50/30')}>
                    <div className={clsx('p-2.5 rounded-xl flex-shrink-0', bg, color)}>{icon}</div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex items-center gap-2 flex-wrap">
                          <p className={clsx('text-sm font-bold', !n.read ? 'text-gray-900' : 'text-gray-600')}>{n.title}</p>
                          {n.priority === 'high' && !n.read && (
                            <span className="text-[10px] font-black bg-red-100 text-red-600 px-1.5 py-0.5 rounded-full">URGENT</span>
                          )}
                        </div>
                        <div className="flex items-center gap-1.5 flex-shrink-0">
                          <p className="text-xs text-gray-400 whitespace-nowrap">{format(new Date(n.createdAt), 'HH:mm')}</p>
                          <button onClick={(e) => { e.stopPropagation(); dismiss(n); }} aria-label="Hide notification" title="Hide" className="p-0.5 text-gray-300 hover:text-gray-500">
                            <X size={14} />
                          </button>
                        </div>
                      </div>
                      <p className="text-sm text-gray-500 mt-0.5 leading-relaxed">{n.message}</p>
                    </div>
                    {notificationRoute(n.type, 'admin') && <ChevronRight size={16} className="text-gray-300 flex-shrink-0 self-center" />}
                  </div>
                );
              })}
            </div>
          </div>
        ))
      )}
    </div>
  );
}
