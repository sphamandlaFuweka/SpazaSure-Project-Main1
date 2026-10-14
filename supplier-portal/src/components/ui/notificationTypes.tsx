import { Bell, ShoppingCart, AlertTriangle, Package, Wallet, BadgeCheck, Flag, UsersRound } from 'lucide-react';

/** Look and destination for each notification type the backend sends. Unknown types fall back to "system". */
export const NOTIFICATION_TYPES: Record<string, { icon: React.ReactNode; iconBg: string; iconColor: string }> = {
  order:      { icon: <ShoppingCart size={14} />,  iconBg: 'bg-blue-100',    iconColor: 'text-blue-600' },
  delivery:   { icon: <ShoppingCart size={14} />,  iconBg: 'bg-blue-100',    iconColor: 'text-blue-600' },
  compliance: { icon: <AlertTriangle size={14} />, iconBg: 'bg-amber-100',   iconColor: 'text-amber-600' },
  product:    { icon: <Package size={14} />,       iconBg: 'bg-emerald-100', iconColor: 'text-emerald-600' },
  wallet:     { icon: <Wallet size={14} />,        iconBg: 'bg-emerald-100', iconColor: 'text-emerald-600' },
  payout:     { icon: <Wallet size={14} />,        iconBg: 'bg-emerald-100', iconColor: 'text-emerald-600' },
  account:    { icon: <BadgeCheck size={14} />,    iconBg: 'bg-blue-100',    iconColor: 'text-blue-600' },
  report:     { icon: <Flag size={14} />,          iconBg: 'bg-red-100',     iconColor: 'text-red-600' },
  group_buy:  { icon: <UsersRound size={14} />,    iconBg: 'bg-amber-100',   iconColor: 'text-amber-600' },
  system:     { icon: <Bell size={14} />,          iconBg: 'bg-gray-100',    iconColor: 'text-gray-500' },
};

export const notificationType = (type: string | undefined) => NOTIFICATION_TYPES[type ?? ''] ?? NOTIFICATION_TYPES.system;

const SUPPLIER_ROUTES: Record<string, string> = {
  order: '/orders', delivery: '/orders', product: '/products', compliance: '/profile',
  payout: '/analytics', account: '/dashboard', group_buy: '/group-buy',
};

const ADMIN_ROUTES: Record<string, string> = {
  order: '/admin/orders', product: '/admin/products', compliance: '/admin/documents',
  wallet: '/admin/deposits', account: '/admin/suppliers', supplier: '/admin/suppliers', report: '/admin/reports', group_buy: '/admin/group-buys',
};

/** Page a notification opens, or undefined if it has none. */
export const notificationRoute = (type: string | undefined, variant: 'supplier' | 'admin') =>
  (variant === 'admin' ? ADMIN_ROUTES : SUPPLIER_ROUTES)[type ?? ''];
