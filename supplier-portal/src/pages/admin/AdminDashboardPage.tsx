import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Users, ShieldCheck, Package, ShoppingCart, DollarSign,
  AlertTriangle, CheckCircle, Clock, ArrowRight, TrendingUp, FileText,
  Store, BarChart2, Settings, ChevronRight, Zap, Globe, Flag, RefreshCw,
} from 'lucide-react';
import { format } from 'date-fns';
import clsx from 'clsx';
import { adminSuppliersApi, adminProductsApi, adminOrdersApi, adminReportsApi } from '../../services/api';
import api from '../../services/api';
import { Spinner } from '../../components/ui';
import { formatCurrency } from '../../utils';

interface DashboardData {
  totalSuppliers: number;
  verifiedSuppliers: number;
  pendingVerifications: number;
  pendingDocuments: number;
  totalProducts: number;
  pendingProducts: number;
  pendingReports: number;
  totalOrders: number;
  pendingOrders: number;
  deliveredRevenue: number;
  platformFees: number;
  recentSuppliers: Array<{
    id: string;
    companyName: string;
    email: string;
    isVerified: boolean;
    joinedAt: string;
  }>;
}

export default function AdminDashboardPage() {
  const navigate = useNavigate();
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);

  const [failed, setFailed] = useState<string[]>([]);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    const fetchData = async () => {
      const supplierPage = (r: any) => {
        const d = r?.data ?? r;
        return { items: (Array.isArray(d) ? d : d?.items ?? []) as any[], total: Number(d?.total ?? (Array.isArray(d) ? d.length : d?.items?.length ?? 0)) };
      };

      // Each request is independent; one failing service shouldn't blank the whole dashboard
      const [suppRes, pendingSuppRes, docsRes, productsRes, pendingProductsRes, ordersRes, reportsRes] = await Promise.allSettled([
        adminSuppliersApi.list({ pageSize: 5 }),
        adminSuppliersApi.list({ status: 'pending', pageSize: 1 }),
        api.get('/compliance/documents', { params: { status: 'pending' } }),
        adminProductsApi.list({ status: 'active', pageSize: 1 }),
        adminProductsApi.list({ status: 'pending_approval', pageSize: 1 }),
        adminOrdersApi.list({ pageSize: 1 }),
        adminReportsApi.list({ status: 'submitted', pageSize: 1 }),
      ]);
      if (cancelled) return;

      const failures: string[] = [];
      const suppliers = suppRes.status === 'fulfilled' ? supplierPage(suppRes.value) : (failures.push('suppliers'), { items: [], total: 0 });
      const pendingSuppliers = pendingSuppRes.status === 'fulfilled' ? supplierPage(pendingSuppRes.value).total : 0;
      const pendingDocs = docsRes.status === 'fulfilled'
        ? Number(docsRes.value.data?.data?.summary?.pending ?? docsRes.value.data?.data?.docs?.length ?? 0)
        : (failures.push('documents'), 0);
      const totalProducts = productsRes.status === 'fulfilled' ? productsRes.value.total : (failures.push('products'), 0);
      const pendingProducts = pendingProductsRes.status === 'fulfilled' ? pendingProductsRes.value.total : 0;
      const orderSummary = ordersRes.status === 'fulfilled' ? ordersRes.value.summary as any : (failures.push('orders'), {});
      const pendingReports = reportsRes.status === 'fulfilled' ? reportsRes.value.total : 0;

      setFailed(failures);
      setData({
        totalSuppliers: suppliers.total,
        verifiedSuppliers: Math.max(0, suppliers.total - pendingSuppliers),
        pendingVerifications: pendingSuppliers,
        pendingDocuments: pendingDocs,
        totalProducts,
        pendingProducts,
        pendingReports,
        totalOrders: Number(orderSummary?.totalOrders ?? 0),
        pendingOrders: Number(orderSummary?.pendingCount ?? 0),
        deliveredRevenue: Number(orderSummary?.deliveredRevenue ?? 0),
        platformFees: Number(orderSummary?.totalPlatformFees ?? 0),
        recentSuppliers: suppliers.items.slice(0, 5).map((s: any) => ({
          id: s.id,
          companyName: s.companyName ?? '',
          email: s.email ?? '',
          isVerified: s.isVerified ?? s.status === 'verified',
          joinedAt: s.joinedAt ?? s.createdAt ?? new Date().toISOString(),
        })),
      });
      setLoading(false);
    };
    fetchData();
    return () => { cancelled = true; };
  }, [reloadKey]);

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <div className="text-center">
        <Spinner />
        <p className="text-sm text-gray-400 mt-3 font-medium">Loading dashboard...</p>
      </div>
    </div>
  );

  const d = data!;
  const greeting = (() => {
    const h = new Date().getHours();
    if (h < 12) return 'Good morning';
    if (h < 17) return 'Good afternoon';
    return 'Good evening';
  })();

  return (
    <div className="p-4 sm:p-6 space-y-5 sm:space-y-7 animate-in">

      {/* ═══ HEADER ═══ */}
      <div className="relative overflow-hidden rounded-2xl sm:rounded-3xl bg-gradient-to-br from-slate-900 via-slate-800 to-indigo-900 p-5 sm:p-8 shadow-xl">
        {/* Decorative elements */}
        <div className="absolute top-0 right-0 w-80 h-80 bg-indigo-500/10 rounded-full blur-[80px] -translate-y-20 translate-x-20" />
        <div className="absolute bottom-0 left-0 w-60 h-60 bg-emerald-500/10 rounded-full blur-[60px] translate-y-20 -translate-x-10" />
        <div className="absolute inset-0 opacity-[0.03]" style={{ backgroundImage: 'radial-gradient(circle at 1px 1px, white 1px, transparent 0)', backgroundSize: '24px 24px' }} />

        <div className="relative z-10">
          <div className="flex items-center gap-2 mb-1">
            <Zap size={16} className="text-amber-400" />
            <span className="text-xs font-bold text-amber-400 uppercase tracking-widest">Admin Control Center</span>
          </div>
          <h1 className="text-2xl font-black text-white tracking-tight">{greeting}, Admin</h1>
          <p className="text-sm text-slate-400 mt-1 font-medium">
            {format(new Date(), 'EEEE, d MMMM yyyy')} · Here's your platform overview
          </p>

          {/* Quick action pills */}
          <div className="flex flex-wrap items-start gap-2 mt-5">
            {d.pendingVerifications > 0 && (
              <button
                onClick={() => navigate('/admin/suppliers')}
                className="flex items-center gap-2 bg-amber-500/20 border border-amber-400/30 text-amber-300 text-xs font-bold px-3.5 py-2 rounded-xl hover:bg-amber-500/30 transition-colors"
              >
                <AlertTriangle size={12} />
                {d.pendingVerifications} suppliers need verification
                <ChevronRight size={12} />
              </button>
            )}
            {d.pendingDocuments > 0 && (
              <button
                onClick={() => navigate('/admin/documents')}
                className="flex items-center gap-2 bg-orange-500/20 border border-orange-400/30 text-orange-300 text-xs font-bold px-3.5 py-2 rounded-xl hover:bg-orange-500/30 transition-colors"
              >
                <FileText size={12} />
                {d.pendingDocuments} documents to review
                <ChevronRight size={12} />
              </button>
            )}
          </div>
        </div>
      </div>

      {failed.length > 0 && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4">
          <AlertTriangle size={18} className="text-amber-600 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-amber-900">
            Some figures couldn't be loaded ({failed.join(', ')}). They are shown as 0 until the service responds.
          </p>
          <button onClick={() => { setLoading(true); setReloadKey((k) => k + 1); }} className="flex items-center gap-1.5 text-sm font-semibold bg-white border border-amber-200 rounded-xl px-3 py-1.5 hover:bg-amber-100">
            <RefreshCw size={13} /> Try again
          </button>
        </div>
      )}

      {/* ═══ KPI CARDS — CLICKABLE ═══ */}
      <div className="grid grid-cols-2 lg:grid-cols-3 gap-3 sm:gap-4">
        {([
          { label: 'Total Suppliers',       value: d.totalSuppliers.toLocaleString(),  icon: Users,        color: 'text-blue-600',    bg: 'bg-blue-50',    ring: 'hover:ring-blue-200',    path: '/admin/suppliers', sub: `${d.verifiedSuppliers.toLocaleString()} verified` },
          { label: 'Pending Verifications', value: d.pendingVerifications.toLocaleString(), icon: ShieldCheck, color: 'text-amber-600', bg: 'bg-amber-50', ring: 'hover:ring-amber-200', path: '/admin/suppliers', urgent: d.pendingVerifications > 0 },
          { label: 'Pending Documents',     value: d.pendingDocuments.toLocaleString(), icon: FileText,    color: 'text-orange-600',  bg: 'bg-orange-50',  ring: 'hover:ring-orange-200',  path: '/admin/documents', urgent: d.pendingDocuments > 0 },
          { label: 'Live Products',         value: d.totalProducts.toLocaleString(),    icon: Package,     color: 'text-emerald-600', bg: 'bg-emerald-50', ring: 'hover:ring-emerald-200', path: '/admin/products', sub: d.pendingProducts > 0 ? `${d.pendingProducts} awaiting approval` : undefined },
          { label: 'Total Orders',          value: d.totalOrders.toLocaleString(),      icon: ShoppingCart, color: 'text-violet-600', bg: 'bg-violet-50',  ring: 'hover:ring-violet-200',  path: '/admin/orders', sub: d.pendingOrders > 0 ? `${d.pendingOrders} pending` : undefined },
          { label: 'Platform Fees Earned',  value: formatCurrency(d.platformFees),      icon: DollarSign,  color: 'text-green-700',   bg: 'bg-green-50',   ring: 'hover:ring-green-200',   path: '/admin/orders', sub: `${formatCurrency(d.deliveredRevenue)} delivered GMV` },
        ] as { label: string; value: string; icon: typeof Users; color: string; bg: string; ring: string; path: string; urgent?: boolean; sub?: string }[]).map(({ label, value, icon: Icon, color, bg, ring, path, urgent, sub }) => (
          <button
            key={label}
            onClick={() => navigate(path)}
            className={clsx(
              'group relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-4 sm:p-5 text-left transition-all duration-200',
              'shadow-sm hover:shadow-lg hover:-translate-y-0.5 hover:ring-2 focus:outline-none focus-visible:ring-2',
              ring,
              urgent && 'border-amber-200/60'
            )}
          >
            <div className="relative flex items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="text-[11px] font-bold text-gray-400 uppercase tracking-wider">{label}</p>
                <p className="text-xl sm:text-3xl font-black text-gray-900 mt-2 tabular-nums truncate">{value}</p>
                {urgent ? (
                  <span className="inline-flex items-center gap-1 text-[10px] font-bold text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded-full mt-2.5">
                    <AlertTriangle size={9} /> Needs attention
                  </span>
                ) : sub ? (
                  <p className="text-[11px] text-gray-400 font-medium mt-2 truncate">{sub}</p>
                ) : null}
              </div>
              <div className={clsx('w-10 h-10 sm:w-11 sm:h-11 rounded-xl flex items-center justify-center shadow-sm flex-shrink-0', bg)}>
                <Icon size={20} className={color} />
              </div>
            </div>
            <ArrowRight size={12} className="absolute bottom-3 right-4 text-gray-200 group-hover:text-gray-500 transition-colors" />
          </button>
        ))}
      </div>

      {/* ═══ QUICK NAVIGATION GRID ═══ */}
      <div>
        <h2 className="text-sm font-bold text-gray-400 uppercase tracking-wider mb-3">Quick Navigation</h2>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {[
            { label: 'Suppliers',   desc: 'Manage & verify',      icon: Users,       path: '/admin/suppliers',     color: 'from-blue-500 to-indigo-600' },
            { label: 'Documents',   desc: 'Review compliance',    icon: FileText,    path: '/admin/documents',     color: 'from-amber-500 to-orange-600' },
            { label: 'Products',    desc: 'Approve listings',     icon: Package,     path: '/admin/products',      color: 'from-emerald-500 to-teal-600' },
            { label: 'Orders',      desc: 'Monitor transactions', icon: ShoppingCart, path: '/admin/orders',       color: 'from-violet-500 to-purple-600' },
            { label: 'Analytics',   desc: 'Platform insights',    icon: BarChart2,   path: '/admin/analytics',     color: 'from-cyan-500 to-blue-600' },
            { label: 'Spaza Owners',desc: 'Shop management',      icon: Store,       path: '/admin/suppliers',     color: 'from-purple-500 to-pink-600' },
            { label: 'Notifications',desc:'System alerts',         icon: Globe,       path: '/admin/notifications', color: 'from-rose-500 to-red-600' },
            { label: 'Settings',    desc: 'Platform config',      icon: Settings,    path: '/admin/settings',      color: 'from-gray-500 to-slate-700' },
          ].map(({ label, desc, icon: Icon, path, color }) => (
            <button
              key={label}
              onClick={() => navigate(path)}
              className="group relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-4 text-left shadow-sm hover:shadow-md hover:-translate-y-0.5 transition-all duration-200"
            >
              <div className={clsx('w-9 h-9 rounded-xl bg-gradient-to-br flex items-center justify-center mb-3 shadow-sm group-hover:scale-110 transition-transform', color)}>
                <Icon size={16} className="text-white" />
              </div>
              <p className="text-sm font-bold text-gray-900">{label}</p>
              <p className="text-[11px] text-gray-400 font-medium mt-0.5">{desc}</p>
              <ChevronRight size={14} className="absolute top-4 right-4 text-gray-200 group-hover:text-gray-400 group-hover:translate-x-0.5 transition-all" />
            </button>
          ))}
        </div>
      </div>

      {/* ═══ TWO COLUMN: RECENT SUPPLIERS + ACTIVITY ═══ */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 sm:gap-5">
        {/* Recent Suppliers */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 bg-gradient-to-r from-gray-50 to-white">
            <div>
              <h3 className="font-bold text-gray-900 text-sm">Recent Suppliers</h3>
              <p className="text-xs text-gray-400 mt-0.5">Latest registrations</p>
            </div>
            <button onClick={() => navigate('/admin/suppliers')} className="text-xs text-indigo-600 font-bold hover:text-indigo-800 flex items-center gap-1 transition-colors">
              View all <ArrowRight size={11} />
            </button>
          </div>
          <div className="divide-y divide-gray-50">
            {d.recentSuppliers.length === 0 ? (
              <div className="p-10 text-center">
                <Users size={28} className="text-gray-200 mx-auto mb-2" />
                <p className="text-sm font-semibold text-gray-500">No suppliers yet</p>
              </div>
            ) : d.recentSuppliers.map((s) => (
              <button
                key={s.id}
                onClick={() => navigate('/admin/suppliers')}
                className="w-full flex items-center justify-between px-5 py-3.5 hover:bg-gray-50/70 transition-colors text-left"
              >
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-slate-100 to-slate-50 flex items-center justify-center font-bold text-slate-600 text-sm flex-shrink-0 ring-1 ring-slate-200/50">
                    {s.companyName.charAt(0)}
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-gray-900">{s.companyName}</p>
                    <p className="text-[11px] text-gray-400">{s.email}</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {s.isVerified ? (
                    <span className="w-2 h-2 bg-emerald-400 rounded-full" title="Verified" />
                  ) : (
                    <span className="w-2 h-2 bg-amber-400 rounded-full animate-pulse" title="Pending" />
                  )}
                  <ChevronRight size={14} className="text-gray-200" />
                </div>
              </button>
            ))}
          </div>
        </div>

        {/* Work queue — real counts of items waiting for an admin */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 bg-gradient-to-r from-gray-50 to-white">
            <div>
              <h3 className="font-bold text-gray-900 text-sm">Needs Your Attention</h3>
              <p className="text-xs text-gray-400 mt-0.5">Items waiting for an admin decision</p>
            </div>
          </div>
          <div className="divide-y divide-gray-50">
            {[
              { label: 'Suppliers to verify',          count: d.pendingVerifications, icon: ShieldCheck,  path: '/admin/suppliers' },
              { label: 'Compliance documents to review', count: d.pendingDocuments,   icon: FileText,     path: '/admin/documents' },
              { label: 'Products awaiting approval',   count: d.pendingProducts,      icon: Package,      path: '/admin/products' },
              { label: 'New customer reports',         count: d.pendingReports,       icon: Flag,         path: '/admin/reports' },
              { label: 'Orders still pending',         count: d.pendingOrders,        icon: Clock,        path: '/admin/orders' },
            ].map(({ label, count, icon: Icon, path }) => (
              <button
                key={label}
                onClick={() => navigate(path)}
                className="w-full flex items-center justify-between gap-3 px-5 py-3.5 hover:bg-gray-50/70 transition-colors text-left"
              >
                <div className="flex items-center gap-3 min-w-0">
                  <div className={clsx('w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0', count > 0 ? 'bg-amber-50 text-amber-600' : 'bg-emerald-50 text-emerald-600')}>
                    {count > 0 ? <Icon size={15} /> : <CheckCircle size={15} />}
                  </div>
                  <span className="text-sm font-medium text-gray-700 truncate">{label}</span>
                </div>
                <div className="flex items-center gap-2 flex-shrink-0">
                  <span className={clsx('text-sm font-black tabular-nums', count > 0 ? 'text-amber-700' : 'text-gray-300')}>{count}</span>
                  <ChevronRight size={14} className="text-gray-200" />
                </div>
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
