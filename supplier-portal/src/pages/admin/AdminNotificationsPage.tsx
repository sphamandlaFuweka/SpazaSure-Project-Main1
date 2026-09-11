import { useState } from 'react';
import { Bell, CheckCircle, AlertTriangle, ShoppingCart, Users, X, Send, Plus } from 'lucide-react';
import { format, isToday, isYesterday } from 'date-fns';
import clsx from 'clsx';
import toast from 'react-hot-toast';

interface AdminNotification {
  id: string;
  type: 'system' | 'compliance' | 'order' | 'supplier';
  title: string;
  message: string;
  sentTo: 'all' | 'suppliers' | 'specific';
  createdAt: string;
  read: boolean;
  priority: 'high' | 'normal';
}

const typeConfig: Record<AdminNotification['type'], { icon: React.ReactNode; color: string; bg: string }> = {
  system:     { icon: <Bell size={15} />,           color: 'text-slate-600',  bg: 'bg-slate-100' },
  compliance: { icon: <AlertTriangle size={15} />,  color: 'text-amber-600',  bg: 'bg-amber-100' },
  order:      { icon: <ShoppingCart size={15} />,   color: 'text-blue-600',   bg: 'bg-blue-100' },
  supplier:   { icon: <Users size={15} />,          color: 'text-emerald-600', bg: 'bg-emerald-100' },
};

function getDateLabel(d: string) {
  const date = new Date(d);
  if (isToday(date)) return 'Today';
  if (isYesterday(date)) return 'Yesterday';
  return format(date, 'EEEE, d MMMM');
}

export default function AdminNotificationsPage() {
  const [notifications, setNotifications] = useState<AdminNotification[]>([]);
  const [typeFilter, setTypeFilter] = useState<'all' | AdminNotification['type']>('all');
  const [showCompose, setShowCompose] = useState(false);
  const [compose, setCompose] = useState({ title: '', message: '', sentTo: 'all' as 'all' | 'suppliers' });

  const markAllRead = () => setNotifications((prev) => prev.map((n) => ({ ...n, read: true })));
  const dismiss = (id: string) => setNotifications((prev) => prev.filter((n) => n.id !== id));
  const markRead = (id: string) => setNotifications((prev) => prev.map((n) => n.id === id ? { ...n, read: true } : n));

  const filtered = notifications.filter((n) => typeFilter === 'all' || n.type === typeFilter);
  const unreadCount = notifications.filter((n) => !n.read).length;

  const grouped = filtered.reduce<Record<string, AdminNotification[]>>((acc, n) => {
    const label = getDateLabel(n.createdAt);
    if (!acc[label]) acc[label] = [];
    acc[label].push(n);
    return acc;
  }, {});

  const sendNotification = () => {
    if (!compose.title.trim() || !compose.message.trim()) {
      toast.error('Title and message are required');
      return;
    }
    const newNotif: AdminNotification = {
      id: `an${Date.now()}`, type: 'system', title: compose.title,
      message: compose.message, sentTo: compose.sentTo,
      createdAt: new Date().toISOString(), read: false, priority: 'normal',
    };
    setNotifications((prev) => [newNotif, ...prev]);
    toast.success(`Notification sent to ${compose.sentTo === 'all' ? 'everyone' : 'all suppliers'}`);
    setShowCompose(false);
    setCompose({ title: '', message: '', sentTo: 'all' });
  };

  const typeFilters: { value: 'all' | AdminNotification['type']; label: string }[] = [
    { value: 'all',        label: 'All' },
    { value: 'system',     label: 'System' },
    { value: 'compliance', label: 'Compliance' },
    { value: 'order',      label: 'Orders' },
    { value: 'supplier',   label: 'Suppliers' },
  ];

  return (
    <div className="p-6 space-y-5 animate-in max-w-3xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="page-title">Notifications</h1>
          <p className="page-subtitle">{unreadCount > 0 ? `${unreadCount} unread` : 'All caught up'}</p>
        </div>
        <div className="flex items-center gap-2">
          {unreadCount > 0 && (
            <button onClick={markAllRead} className="btn-secondary text-sm flex items-center gap-1.5">
              <CheckCircle size={14} /> Mark all read
            </button>
          )}
          <button onClick={() => setShowCompose(true)} className="btn-primary flex items-center gap-2">
            <Plus size={15} /> Send Notification
          </button>
        </div>
      </div>

      {/* Type Filters */}
      <div className="flex items-center gap-1.5 flex-wrap">
        {typeFilters.map(({ value, label }) => (
          <button key={value} onClick={() => setTypeFilter(value)}
            className={clsx('px-3 py-1.5 rounded-xl text-xs font-semibold transition-all border',
              typeFilter === value ? 'bg-slate-800 text-white border-slate-800' : 'bg-white text-gray-500 border-gray-200 hover:border-slate-400 hover:text-slate-700'
            )}>
            {label}
          </button>
        ))}
      </div>

      {/* Notifications */}
      {Object.keys(grouped).length === 0 ? (
        <div className="card p-16 text-center">
          <Bell size={36} className="text-gray-200 mx-auto mb-4" />
          <p className="font-bold text-gray-600">No notifications</p>
        </div>
      ) : (
        Object.entries(grouped).map(([dateLabel, items]) => (
          <div key={dateLabel}>
            <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2 px-1">{dateLabel}</p>
            <div className="space-y-2">
              {items.map((n) => {
                const { icon, color, bg } = typeConfig[n.type];
                return (
                  <div key={n.id} onClick={() => markRead(n.id)}
                    className={clsx('card p-4 flex items-start gap-4 cursor-pointer hover:shadow-card-hover transition-all duration-200',
                      !n.read && 'border-l-4 border-l-slate-700',
                      n.priority === 'high' && !n.read && 'bg-amber-50/20'
                    )}>
                    <div className={clsx('p-2.5 rounded-xl flex-shrink-0', bg, color)}>{icon}</div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex items-center gap-2 flex-wrap">
                          <p className={clsx('text-sm font-bold', !n.read ? 'text-gray-900' : 'text-gray-600')}>{n.title}</p>
                          {n.priority === 'high' && !n.read && (
                            <span className="text-[10px] font-black bg-red-100 text-red-600 px-1.5 py-0.5 rounded-full">URGENT</span>
                          )}
                          <span className="text-[10px] font-semibold bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded-full capitalize">
                            → {n.sentTo === 'all' ? 'Everyone' : n.sentTo}
                          </span>
                        </div>
                        <div className="flex items-center gap-1.5 flex-shrink-0">
                          <p className="text-xs text-gray-400 whitespace-nowrap">{format(new Date(n.createdAt), 'HH:mm')}</p>
                          <button onClick={(e) => { e.stopPropagation(); dismiss(n.id); }} className="p-0.5 text-gray-300 hover:text-gray-500 transition-colors">
                            <X size={14} />
                          </button>
                        </div>
                      </div>
                      <p className="text-sm text-gray-500 mt-0.5 leading-relaxed">{n.message}</p>
                    </div>
                    {!n.read && <div className="w-2 h-2 rounded-full bg-slate-700 flex-shrink-0 mt-1.5 animate-pulse-soft" />}
                  </div>
                );
              })}
            </div>
          </div>
        ))
      )}

      {/* Compose Modal */}
      {showCompose && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setShowCompose(false)} />
          <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-md max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 flex-shrink-0">
              <h2 className="font-bold text-gray-900">Send Platform Notification</h2>
              <button onClick={() => setShowCompose(false)} className="btn-icon"><X size={18} /></button>
            </div>
            <div className="p-6 space-y-4 overflow-y-auto flex-1 overscroll-contain">
              <div>
                <label className="label">Send To</label>
                <div className="flex gap-2">
                  {(['all', 'suppliers'] as const).map((v) => (
                    <button key={v} onClick={() => setCompose((c) => ({ ...c, sentTo: v }))}
                      className={clsx('flex-1 py-2 rounded-xl text-sm font-semibold border transition-all capitalize',
                        compose.sentTo === v ? 'bg-slate-800 text-white border-slate-800' : 'bg-white text-gray-500 border-gray-200 hover:border-slate-400'
                      )}>
                      {v === 'all' ? 'Everyone' : 'Suppliers Only'}
                    </button>
                  ))}
                </div>
              </div>
              <div>
                <label className="label">Title</label>
                <input value={compose.title} onChange={(e) => setCompose((c) => ({ ...c, title: e.target.value }))} className="input" placeholder="Notification title..." />
              </div>
              <div>
                <label className="label">Message</label>
                <textarea value={compose.message} onChange={(e) => setCompose((c) => ({ ...c, message: e.target.value }))} className="input resize-none" rows={3} placeholder="Write your message..." />
              </div>
              <div className="flex gap-3 justify-end pt-1">
                <button onClick={() => setShowCompose(false)} className="btn-secondary">Cancel</button>
                <button onClick={sendNotification} className="bg-slate-800 hover:bg-slate-900 text-white px-4 py-2.5 rounded-xl font-semibold text-sm flex items-center gap-2 transition-all">
                  <Send size={14} /> Send Now
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
