import { type ReactNode } from 'react';
import { TrendingUp, TrendingDown, X } from 'lucide-react';
import clsx from 'clsx';
import type { OrderStatus, PaymentStatus } from '../../types';

// ─── Stat Card ────────────────────────────────────────────────────────────────
interface StatCardProps {
  title: string;
  value: string | number;
  change?: number;
  icon: ReactNode;
  prefix?: string;
  suffix?: string;
  variant?: 'default' | 'green' | 'amber';
  description?: string;
}

export function StatCard({ title, value, change, icon, prefix = '', suffix = '', variant = 'default', description }: StatCardProps) {
  const isPositive = (change ?? 0) >= 0;

  if (variant === 'green') {
    return (
      <div className="stat-card-green animate-in">
        <div className="absolute inset-0 bg-gradient-mesh opacity-30 pointer-events-none rounded-2xl" />
        <div className="flex items-start justify-between relative z-10">
          <div>
            <p className="text-green-100/80 text-xs font-semibold uppercase tracking-wider">{title}</p>
            <p className="kpi-value text-white mt-1.5">
              {prefix}{typeof value === 'number' ? value.toLocaleString() : value}{suffix}
            </p>
            {change !== undefined && (
              <div className="flex items-center gap-1 text-xs mt-2">
                <span className="flex items-center gap-1 bg-white/15 text-white font-bold px-2 py-0.5 rounded-full">
                  {isPositive ? <TrendingUp size={10} /> : <TrendingDown size={10} />}
                  {Math.abs(change)}%
                </span>
                <span className="text-green-200/70 text-[11px]">vs last month</span>
              </div>
            )}
            {description && <p className="text-xs text-green-200/60 mt-1">{description}</p>}
          </div>
          <div className="p-2.5 bg-white/15 rounded-xl ring-1 ring-white/10">{icon}</div>
        </div>
      </div>
    );
  }

  if (variant === 'amber') {
    return (
      <div className="stat-card-amber animate-in">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-amber-100/80 text-xs font-semibold uppercase tracking-wider">{title}</p>
            <p className="kpi-value text-white mt-1.5">
              {prefix}{typeof value === 'number' ? value.toLocaleString() : value}{suffix}
            </p>
            {change !== undefined && (
              <div className="flex items-center gap-1 text-xs mt-2">
                <span className="flex items-center gap-1 bg-white/15 text-white font-bold px-2 py-0.5 rounded-full">
                  {isPositive ? <TrendingUp size={10} /> : <TrendingDown size={10} />}
                  {Math.abs(change)}%
                </span>
                <span className="text-amber-200/70 text-[11px]">vs last month</span>
              </div>
            )}
          </div>
          <div className="p-2.5 bg-white/15 rounded-xl ring-1 ring-white/10">{icon}</div>
        </div>
      </div>
    );
  }

  return (
    <div className="card p-5 animate-in hover:shadow-card-hover transition-shadow duration-200">
      <div className="flex items-start justify-between">
        <div className="flex-1">
          <p className="text-xs text-gray-400 font-semibold uppercase tracking-wider">{title}</p>
          <p className="kpi-value text-gray-900 mt-1.5">
            {prefix}{typeof value === 'number' ? value.toLocaleString() : value}{suffix}
          </p>
          {change !== undefined && (
            <div className={clsx('flex items-center gap-1 text-xs mt-2 font-bold', isPositive ? 'text-emerald-600' : 'text-red-500')}>
              {isPositive
                ? <span className="metric-up"><TrendingUp size={10} /> {Math.abs(change)}%</span>
                : <span className="metric-down"><TrendingDown size={10} /> {Math.abs(change)}%</span>
              }
              <span className="text-gray-400 font-normal text-[11px]">vs last month</span>
            </div>
          )}
          {description && <p className="text-xs text-gray-400 mt-1">{description}</p>}
        </div>
        <div className="p-2.5 bg-primary-50 rounded-xl text-primary flex-shrink-0 ring-1 ring-primary-100">{icon}</div>
      </div>
    </div>
  );
}

// ─── Order Status Badge ───────────────────────────────────────────────────────
const orderStatusMap: Record<OrderStatus, { label: string; className: string; dot: string }> = {
  pending:    { label: 'Pending',    className: 'bg-amber-50 text-amber-700 border border-amber-200',     dot: 'bg-amber-400' },
  confirmed:  { label: 'Confirmed',  className: 'bg-blue-50 text-blue-700 border border-blue-200',        dot: 'bg-blue-400' },
  processing: { label: 'Processing', className: 'bg-violet-50 text-violet-700 border border-violet-200',  dot: 'bg-violet-400' },
  dispatched: { label: 'Dispatched', className: 'bg-indigo-50 text-indigo-700 border border-indigo-200',  dot: 'bg-indigo-400' },
  delivered:  { label: 'Delivered',  className: 'bg-emerald-50 text-emerald-700 border border-emerald-200', dot: 'bg-emerald-400' },
  cancelled:  { label: 'Cancelled',  className: 'bg-gray-100 text-gray-600 border border-gray-200',       dot: 'bg-gray-400' },
  disputed:   { label: 'Disputed',   className: 'bg-red-50 text-red-700 border border-red-200',           dot: 'bg-red-400' },
};

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const { label, className, dot } = orderStatusMap[status] ?? { label: status, className: 'bg-gray-100 text-gray-600 border border-gray-200', dot: 'bg-gray-400' };
  return (
    <span className={clsx('inline-flex items-center gap-1.5 text-xs font-semibold px-2.5 py-1 rounded-full', className)}>
      <span className={clsx('w-1.5 h-1.5 rounded-full', dot)} />
      {label}
    </span>
  );
}

// ─── Payment Status Badge ─────────────────────────────────────────────────────
const paymentStatusMap: Record<PaymentStatus, { label: string; className: string }> = {
  initiated: { label: 'Initiated',  className: 'bg-gray-100 text-gray-600 border border-gray-200' },
  pending:   { label: 'Pending',    className: 'bg-amber-50 text-amber-700 border border-amber-200' },
  held:      { label: 'In Escrow',  className: 'bg-blue-50 text-blue-700 border border-blue-200' },
  released:  { label: 'Released',   className: 'bg-emerald-50 text-emerald-700 border border-emerald-200' },
  failed:    { label: 'Failed',     className: 'bg-red-50 text-red-700 border border-red-200' },
  refunded:  { label: 'Refunded',   className: 'bg-orange-50 text-orange-700 border border-orange-200' },
};

export function PaymentStatusBadge({ status }: { status: PaymentStatus }) {
  const { label, className } = paymentStatusMap[status] ?? { label: status, className: 'bg-gray-100 text-gray-600 border border-gray-200' };
  return <span className={clsx('text-xs font-semibold px-2.5 py-1 rounded-full', className)}>{label}</span>;
}

// ─── Tier Badge ───────────────────────────────────────────────────────────────
const tierMap: Record<string, { className: string; label: string }> = {
  basic:  { className: 'bg-gray-100 text-gray-600 border border-gray-200',         label: 'Basic' },
  bronze: { className: 'bg-orange-50 text-orange-700 border border-orange-200',    label: '🥉 Bronze' },
  silver: { className: 'bg-slate-100 text-slate-700 border border-slate-300',      label: '🥈 Silver' },
  gold:   { className: 'bg-amber-50 text-amber-700 border border-amber-300',       label: '🥇 Gold' },
};

export function TierBadge({ tier }: { tier: string }) {
  const { className, label } = tierMap[tier] ?? tierMap.basic;
  return <span className={clsx('text-xs font-bold px-2.5 py-1 rounded-full', className)}>{label}</span>;
}

// ─── Empty State ──────────────────────────────────────────────────────────────
export function EmptyState({ title, description, action, icon }: {
  title: string; description: string; action?: ReactNode; icon?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center px-6">
      {icon && <div className="mb-4 text-gray-300">{icon}</div>}
      <p className="text-base font-semibold text-gray-700">{title}</p>
      <p className="text-sm text-gray-400 mt-1.5 max-w-xs leading-relaxed">{description}</p>
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}

// ─── Spinner ──────────────────────────────────────────────────────────────────
export function Spinner({ size = 'md' }: { size?: 'sm' | 'md' | 'lg' }) {
  const s = { sm: 'h-4 w-4 border-2', md: 'h-6 w-6 border-2', lg: 'h-10 w-10 border-[3px]' }[size];
  return <div className={clsx('animate-spin rounded-full border-primary border-t-transparent', s)} />;
}

// ─── Modal ────────────────────────────────────────────────────────────────────
export function Modal({ title, children, onClose, size = 'md' }: {
  title: string; children: ReactNode; onClose: () => void; size?: 'sm' | 'md' | 'lg' | 'xl';
}) {
  const widths = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-2xl', xl: 'max-w-4xl' };
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={onClose} />
      <div className={clsx(
        'relative bg-white rounded-2xl shadow-card-lg w-full flex flex-col max-h-[90vh] overflow-hidden',
        'animate-scale-in',
        widths[size]
      )}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 flex-shrink-0">
          <h2 className="text-base font-bold text-gray-900">{title}</h2>
          <button onClick={onClose} className="btn-icon"><X size={18} /></button>
        </div>
        <div className="overflow-y-auto flex-1 overscroll-contain">{children}</div>
      </div>
    </div>
  );
}

// ─── Progress Bar ─────────────────────────────────────────────────────────────
export function ProgressBar({ value, max, color = 'primary' }: { value: number; max: number; color?: string }) {
  const pct = Math.min(100, (value / max) * 100);
  const colors: Record<string, string> = {
    primary: 'bg-primary',
    amber: 'bg-accent',
    red: 'bg-red-500',
    blue: 'bg-blue-500',
  };
  return (
    <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
      <div
        className={clsx('h-full rounded-full transition-all duration-500', colors[color] ?? colors.primary)}
        style={{ width: `${pct}%` }}
      />
    </div>
  );
}

// ─── Avatar ───────────────────────────────────────────────────────────────────
export function Avatar({ name, size = 'md', src }: { name: string; size?: 'sm' | 'md' | 'lg'; src?: string }) {
  const sizes = { sm: 'w-7 h-7 text-xs', md: 'w-9 h-9 text-sm', lg: 'w-12 h-12 text-base' };
  if (src) {
    return (
      <div className={clsx('rounded-full overflow-hidden ring-2 ring-primary-100 ring-offset-1 flex-shrink-0', sizes[size])}>
        <img
          src={src}
          alt={name}
          className="w-full h-full object-cover"
          onError={(e) => {
            // On image load failure, replace with initials
            const el = e.currentTarget;
            el.style.display = 'none';
            el.parentElement!.classList.add('bg-gradient-card', 'text-white', 'font-bold', 'flex', 'items-center', 'justify-center');
            el.parentElement!.textContent = name.charAt(0).toUpperCase();
          }}
        />
      </div>
    );
  }
  return (
    <div className={clsx('rounded-full bg-gradient-card text-white font-bold flex items-center justify-center flex-shrink-0', sizes[size])}>
      {name.charAt(0).toUpperCase()}
    </div>
  );
}

// ─── Section Header ───────────────────────────────────────────────────────────
export function SectionHeader({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
      <h3 className="font-bold text-gray-900">{title}</h3>
      {action}
    </div>
  );
}

// ─── Alert Banner ─────────────────────────────────────────────────────────────
export function AlertBanner({ type, message, onDismiss }: {
  type: 'info' | 'warning' | 'success' | 'error'; message: string; onDismiss?: () => void;
}) {
  const styles = {
    info:    'bg-blue-50 border-blue-200 text-blue-800',
    warning: 'bg-amber-50 border-amber-200 text-amber-800',
    success: 'bg-emerald-50 border-emerald-200 text-emerald-800',
    error:   'bg-red-50 border-red-200 text-red-800',
  };
  return (
    <div className={clsx('flex items-center justify-between px-4 py-3 rounded-xl border text-sm font-medium', styles[type])}>
      <span>{message}</span>
      {onDismiss && <button onClick={onDismiss} className="ml-3 opacity-60 hover:opacity-100"><X size={14} /></button>}
    </div>
  );
}
