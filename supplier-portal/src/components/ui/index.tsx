import { type ReactNode, useEffect, useState } from 'react';
import { TrendingUp, TrendingDown, X, Zap, Medal, Award, Crown } from 'lucide-react';
import clsx from 'clsx';
import type { OrderStatus, PaymentStatus } from '../../types';

// ─── Stat Card ────────────────────────────────────────────────────────────────
// Soft tinted KPI card (brand palette) with an icon chip and an optional trend chip.
type StatTone = 'default' | 'green' | 'amber' | 'blue' | 'red';

const STAT_TONES: Record<StatTone, { card: string; chip: string; label: string }> = {
  default: { card: 'bg-white border-brand-blue-light',                                   chip: 'bg-brand-blue-light text-brand-blue',   label: 'text-ink-3' },
  blue:    { card: 'bg-gradient-to-br from-brand-blue-light to-white border-brand-blue-light', chip: 'bg-brand-blue text-white',        label: 'text-brand-blue/70' },
  green:   { card: 'bg-gradient-to-br from-brand-green-light to-white border-emerald-100', chip: 'bg-brand-green text-white',         label: 'text-brand-green/80' },
  amber:   { card: 'bg-gradient-to-br from-brand-yellow-light to-white border-amber-100', chip: 'bg-brand-yellow text-brand-blue',    label: 'text-accent-700/90' },
  red:     { card: 'bg-gradient-to-br from-brand-red-light to-white border-red-100',      chip: 'bg-brand-red text-white',           label: 'text-brand-red/80' },
};

interface StatCardProps {
  title: string;
  value: string | number;
  change?: number;
  icon: ReactNode;
  prefix?: string;
  suffix?: string;
  variant?: StatTone;
  description?: string;
}

export function StatCard({ title, value, change, icon, prefix = '', suffix = '', variant = 'default', description }: StatCardProps) {
  const isPositive = (change ?? 0) >= 0;
  const tone = STAT_TONES[variant];

  return (
    <div className={clsx('rounded-3xl border p-5 shadow-card animate-in', tone.card)}>
      <div className="flex items-center justify-between gap-3">
        <div className={clsx('w-10 h-10 rounded-2xl flex items-center justify-center flex-shrink-0 [&>svg]:w-[18px] [&>svg]:h-[18px]', tone.chip)}>
          {icon}
        </div>
        {change !== undefined && (
          <span className={isPositive ? 'metric-up' : 'metric-down'}>
            {isPositive ? <TrendingUp size={11} /> : <TrendingDown size={11} />}
            {isPositive ? '+' : '-'}{Math.abs(change)}%
          </span>
        )}
      </div>
      <p className={clsx('mt-4 text-xs font-extrabold uppercase tracking-wider', tone.label)}>{title}</p>
      <p className="kpi-value text-gray-900 mt-1">
        {prefix}{typeof value === 'number' ? value.toLocaleString() : value}{suffix}
      </p>
      {(description || change !== undefined) && (
        <p className="text-xs font-semibold text-ink-3 mt-1">{description ?? 'vs last month'}</p>
      )}
    </div>
  );
}

// ─── Order Status Badge ───────────────────────────────────────────────────────
const orderStatusMap: Record<OrderStatus, { label: string; className: string; dot: string }> = {
  pending:    { label: 'Pending',    className: 'bg-brand-yellow-light text-accent-700',  dot: 'bg-brand-yellow' },
  confirmed:  { label: 'Confirmed',  className: 'bg-brand-blue-light text-brand-blue',   dot: 'bg-brand-blue' },
  processing: { label: 'Processing', className: 'bg-violet-50 text-violet-700 border border-violet-200',  dot: 'bg-violet-400' },
  dispatched: { label: 'Dispatched', className: 'bg-indigo-50 text-indigo-700 border border-indigo-200',  dot: 'bg-indigo-400' },
  delivered:  { label: 'Delivered',  className: 'bg-brand-green-light text-brand-green', dot: 'bg-brand-green' },
  cancelled:  { label: 'Cancelled',  className: 'bg-gray-100 text-gray-600 border border-gray-200',       dot: 'bg-gray-400' },
  disputed:   { label: 'Disputed',   className: 'bg-brand-red-light text-brand-red',     dot: 'bg-brand-red' },
};

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const { label, className, dot } = orderStatusMap[status] ?? { label: status, className: 'bg-gray-100 text-gray-600', dot: 'bg-gray-400' };
  return (
    <span className={clsx('inline-flex items-center gap-1.5 text-xs font-bold px-2.5 py-1 rounded-full whitespace-nowrap', className)}>
      <span className={clsx('w-1.5 h-1.5 rounded-full', dot)} />
      {label}
    </span>
  );
}

// ─── Payment Status Badge ─────────────────────────────────────────────────────
const paymentStatusMap: Record<PaymentStatus, { label: string; className: string }> = {
  initiated: { label: 'Initiated',  className: 'bg-gray-100 text-gray-600 border border-gray-200' },
  pending:   { label: 'Pending',    className: 'bg-amber-50 text-amber-700 border border-amber-200' },
  paid:      { label: 'Paid',       className: 'bg-emerald-50 text-emerald-700 border border-emerald-200' },
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
  basic:  { className: 'bg-gray-100 text-gray-600 border border-gray-200',            label: 'Basic' },
  bronze: { className: 'bg-primary-50 text-primary-600 border border-primary-100',     label: 'Bronze' },
  silver: { className: 'bg-primary-100 text-primary-800 border border-primary-200',    label: 'Silver' },
  gold:   { className: 'bg-accent-50 text-accent-700 border border-accent-400',        label: 'Gold' },
};

const tierIcons = { basic: Zap, bronze: Medal, silver: Award, gold: Crown } as const;

export function TierIcon({ tier, size = 14, className }: { tier: string; size?: number; className?: string }) {
  const Icon = tierIcons[tier as keyof typeof tierIcons] ?? Zap;
  return <Icon size={size} className={className} />;
}

export function TierBadge({ tier }: { tier: string }) {
  const { className, label } = tierMap[tier] ?? tierMap.basic;
  return (
    <span className={clsx('inline-flex items-center gap-1 text-xs font-bold px-2.5 py-1 rounded-full', className)}>
      <TierIcon tier={tier} size={12} /> {label}
    </span>
  );
}

// ─── Empty State ──────────────────────────────────────────────────────────────
export function EmptyState({ title, description, action, icon }: {
  title: string; description: string; action?: ReactNode; icon?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center px-6">
      {icon && <div className="mb-4 w-16 h-16 rounded-3xl bg-brand-blue-light text-brand-blue flex items-center justify-center [&>svg]:w-7 [&>svg]:h-7">{icon}</div>}
      <p className="text-base font-extrabold text-gray-900">{title}</p>
      <p className="text-sm text-ink-3 mt-1.5 max-w-xs leading-relaxed">{description}</p>
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
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6" role="dialog" aria-modal="true" aria-label={title}>
      <div className="absolute inset-0 bg-brand-blue-dark/40 backdrop-blur-sm animate-fade-in" onClick={onClose} />
      <div className={clsx(
        'relative bg-white rounded-3xl shadow-card-xl w-full flex flex-col max-h-[90vh] overflow-hidden',
        'animate-scale-in',
        widths[size]
      )}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-brand-blue-light flex-shrink-0">
          <h2 className="text-lg font-black text-gray-900">{title}</h2>
          <button onClick={onClose} className="btn-icon" aria-label="Close dialog"><X size={18} /></button>
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
  const [failedSrc, setFailedSrc] = useState<string | null>(null);
  if (src && failedSrc !== src) {
    return (
      <div className={clsx('rounded-full overflow-hidden ring-2 ring-primary-100 ring-offset-1 flex-shrink-0', sizes[size])}>
        {/* On load failure fall back to initials (handled in React state, not by editing the DOM) */}
        <img src={src} alt={name} className="w-full h-full object-cover" onError={() => setFailedSrc(src)} />
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
    <div className="flex items-center justify-between px-5 sm:px-6 py-4 border-b border-brand-blue-light">
      <h3 className="section-title">{title}</h3>
      {action}
    </div>
  );
}

// ─── Alert Banner ─────────────────────────────────────────────────────────────
export function AlertBanner({ type, message, onDismiss, action }: {
  type: 'info' | 'warning' | 'success' | 'error'; message: string; onDismiss?: () => void; action?: ReactNode;
}) {
  const styles = {
    info:    'bg-blue-50 border-blue-200 text-blue-800',
    warning: 'bg-amber-50 border-amber-200 text-amber-800',
    success: 'bg-emerald-50 border-emerald-200 text-emerald-800',
    error:   'bg-red-50 border-red-200 text-red-800',
  };
  return (
    <div role={type === 'error' ? 'alert' : 'status'} className={clsx('flex items-center justify-between gap-3 px-4 py-3 rounded-xl border text-sm font-medium', styles[type])}>
      <span>{message}</span>
      <div className="flex items-center gap-2 flex-shrink-0">
        {action}
        {onDismiss && <button onClick={onDismiss} aria-label="Dismiss" className="opacity-60 hover:opacity-100"><X size={14} /></button>}
      </div>
    </div>
  );
}
