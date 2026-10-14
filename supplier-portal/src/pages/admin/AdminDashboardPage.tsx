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

      {/* Brand hero */}
      <section className="hero-banner p-6 sm:p-8">
        <div className="absolute -right-16 -top-16 w-64 h-64 rounded-full border-[28px] border-white/[0.06]" aria-hidden="true" />
        <div className="relative">
          <span className="hero-pill">
            <span className="w-2 h-2 rounded-full bg-brand-yellow" />
            Admin console · {format(new Date(), 'EEEE, d MMMM')}
          </span>
          <h1 className="mt-4 text-3xl sm:text-[40px] font-black leading-[1.08] text-white">
            {greeting}, <span className="text-brand-yellow">Admin</span>
          </h1>
          <p className="mt-3 text-[15px] leading-relaxed text-white/75 max-w-xl">
            {failed.length > 0
              ? 'Here is your platform overview for today.'
              : d.pendingVerifications + d.pendingDocuments + d.pendingProducts + d.pendingReports > 0
              ? 'Here is what needs a decision today to keep the marketplace safe and trusted.'
              : 'Everything is reviewed. The marketplace is up to date.'}
          </p>

          {/* Work queue pills */}
          <div className="mt-6 flex flex-wrap gap-2.5">
            {[
              { count: d.pendingVerifications, label: 'suppliers to verify', path: '/admin/suppliers' },
              { count: d.pendingDocuments, label: 'documents to review', path: '/admin/documents' },
              { count: d.pendingProducts, label: 'products to approve', path: '/admin/products' },
              { count: d.pendingReports, label: 'new reports', path: '/admin/reports' },
            ].filter((p) => p.count > 0).map(({ count, label, path }, i) => (
              <button
                key={label}
                onClick={() => navigate(path)}
                className={clsx(
                  'inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-extrabold transition-all hover:-translate-y-0.5',
                  i === 0 ? 'bg-brand-yellow text-brand-blue' : 'bg-white/12 text-white border border-white/25 hover:bg-white/20'
                )}
              >
                <span className="tabular-nums">{count}</span> {label} <ChevronRight size={14} />
              </button>
            ))}
          </div>
        </div>
      </section>

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
          { label: 'Total Suppliers',       value: d.totalSuppliers.toLocaleString(),  icon: Users,        color: 'text-white',       bg: 'bg-brand-blue',    ring: 'hover:ring-blue-200',    path: '/admin/suppliers', sub: `${d.verifiedSuppliers.toLocaleString()} verified` },
          { label: 'Pending Verifications', value: d.pendingVerifications.toLocaleString(), icon: ShieldCheck, color: 'text-brand-blue', bg: 'bg-brand-yellow', ring: 'hover:ring-amber-200', path: '/admin/suppliers', urgent: d.pendingVerifications > 0 },
          { label: 'Pending Documents',     value: d.pendingDocuments.toLocaleString(), icon: FileText,    color: 'text-white',       bg: 'bg-brand-red',  ring: 'hover:ring-orange-200',  path: '/admin/documents', urgent: d.pendingDocuments > 0 },
          { label: 'Live Products',         value: d.totalProducts.toLocaleString(),    icon: Package,     color: 'text-white',       bg: 'bg-brand-green', ring: 'hover:ring-emerald-200', path: '/admin/products', sub: d.pendingProducts > 0 ? `${d.pendingProducts} awaiting approval` : undefined },
          { label: 'Total Orders',          value: d.totalOrders.toLocaleString(),      icon: ShoppingCart, color: 'text-brand-blue', bg: 'bg-brand-blue-light',  ring: 'hover:ring-violet-200',  path: '/admin/orders', sub: d.pendingOrders > 0 ? `${d.pendingOrders} pending` : undefined },
          { label: 'Platform Fees Earned',  value: formatCurrency(d.platformFees),      icon: DollarSign,  color: 'text-brand-green', bg: 'bg-brand-green-light',   ring: 'hover:ring-green-200',   path: '/admin/orders', sub: `${formatCurrency(d.deliveredRevenue)} delivered GMV` },
        ] as { label: string; value: string; icon: typeof Users; color: string; bg: string; ring: string; path: string; urgent?: boolean; sub?: string }[]).map(({ label, value, icon: Icon, color, bg, ring, path, urgent, sub }) => (
          <button
            key={label}
            onClick={() => navigate(path)}
            className={clsx(
              'group relative overflow-hidden rounded-3xl bg-white border border-brand-blue-light p-4 sm:p-5 text-left transition-all duration-200',
              'shadow-sm hover:shadow-lg hover:-translate-y-0.5 hover:ring-2 focus:outline-none focus-visible:ring-2',
              ring,
              urgent && 'bg-gradient-to-br from-brand-yellow-light to-white border-amber-100'
            )}
          >
            <div className="relative flex items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="text-[11px] font-bold text-gray-400 uppercase tracking-wider">{label}</p>
                <p className="text-xl sm:text-3xl font-black text-gray-900 mt-2 tabular-nums truncate">{value}</p>
                {urgent ? (
                  <span className="tag-yellow mt-2.5 !text-[10px] !px-2 !py-0.5">
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

      {/* ═══ TWO COLUMN: RECENT SUPPLIERS + ACTIVITY ═══ */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 sm:gap-5">
        {/* Recent Suppliers */}
        <div className="card overflow-hidden">
          <div className="flex items-center justify-between px-5 py-4 border-b border-brand-blue-light">
            <div>
              <h3 className="section-title">Recent Suppliers</h3>
              <p className="text-xs text-gray-400 mt-0.5">Latest registrations</p>
            </div>
            <button onClick={() => navigate('/admin/suppliers')} className="text-xs text-brand-blue font-extrabold hover:underline flex items-center gap-1 transition-colors">
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
        <div className="card overflow-hidden">
          <div className="flex items-center justify-between px-5 py-4 border-b border-brand-blue-light">
            <div>
              <h3 className="section-title">Needs Your Attention</h3>
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
