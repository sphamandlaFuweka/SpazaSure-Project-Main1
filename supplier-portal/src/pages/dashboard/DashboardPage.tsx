import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Package, ShoppingCart, DollarSign, TrendingUp, AlertTriangle,
  CheckCircle, ArrowRight, Clock, Plus, BarChart2, Bell,
  Zap, ArrowUpRight, Activity, Sparkles, Target, Flame,
  TrendingDown, Eye, RefreshCw,
} from 'lucide-react';
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, BarChart, Bar,
} from 'recharts';
import { StatCard, OrderStatusBadge, AlertBanner, SectionHeader } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import { analyticsApi, ordersApi, profileApi } from '../../services/api';
import { useAuthStore } from '../../store/authStore';
import { format } from 'date-fns';
import type { AnalyticsSummary, RevenueDataPoint, TopProduct, Order, ComplianceDoc } from '../../types';

// ─── Animated Counter Hook ────────────────────────────────────────────────────
function useAnimatedCounter(target: number, duration = 1500, delay = 300) {
  const [count, setCount] = useState(0);
  const ref = useRef<number>(0);

  useEffect(() => {
    const timeout = setTimeout(() => {
      const startTime = performance.now();
      const animate = (currentTime: number) => {
        const elapsed = currentTime - startTime;
        const progress = Math.min(elapsed / duration, 1);
        const eased = 1 - Math.pow(1 - progress, 4); // easeOutQuart
        const current = Math.round(eased * target);
        setCount(current);
        if (progress < 1) {
          ref.current = requestAnimationFrame(animate);
        }
      };
      ref.current = requestAnimationFrame(animate);
    }, delay);

    return () => {
      clearTimeout(timeout);
      cancelAnimationFrame(ref.current);
    };
  }, [target, duration, delay]);

  return count;
}

function ChartEmpty({ height, message }: { height: number; message: string }) {
  return (
    <div style={{ height }} className="flex flex-col items-center justify-center text-center rounded-xl border border-dashed border-gray-200 bg-gray-50/50 px-6">
      <BarChart2 size={28} className="text-gray-300 mb-2" />
      <p className="text-xs text-gray-400 max-w-[220px]">{message}</p>
    </div>
  );
}

const quickActions = [
  { label: 'Add product',   hint: 'List a new item',        icon: Plus,         to: '/products?new=1', tone: 'bg-brand-blue-light text-brand-blue' },
  { label: 'Orders',        hint: 'Accept & dispatch',      icon: ShoppingCart, to: '/orders',         tone: 'bg-brand-yellow-light text-accent-700' },
  { label: 'Analytics',     hint: 'Sales & commission',     icon: BarChart2,    to: '/analytics',      tone: 'bg-brand-green-light text-brand-green' },
  { label: 'Notifications', hint: 'Updates & approvals',    icon: Bell,         to: '/notifications',  tone: 'bg-brand-red-light text-brand-red' },
];

const PIPELINE_META = [
  { status: 'pending',    label: 'Awaiting Acceptance', color: 'bg-amber-400' },
  { status: 'processing', label: 'Being Prepared',      color: 'bg-violet-400' }, // includes confirmed
  { status: 'dispatched', label: 'Out for Delivery',    color: 'bg-indigo-400' },
  { status: 'delivered',  label: 'Completed',           color: 'bg-emerald-400' },
];

const DOC_META: { docType: string; label: string }[] = [
  { docType: 'cipc_certificate', label: 'CIPC Certificate' },
  { docType: 'tax_clearance',    label: 'Tax Clearance' },
  { docType: 'bee_certificate',  label: 'BEE Certificate' },
  { docType: 'product_license',  label: 'Product License' },
];

export default function DashboardPage() {
  const { user } = useAuthStore();
  const navigate = useNavigate();
  const [alertDismissed, setAlertDismissed] = useState(false);
  const [loading, setLoading] = useState(true);

  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [revenue, setRevenue] = useState<RevenueDataPoint[]>([]);
  const [topProducts, setTopProducts] = useState<TopProduct[]>([]);
  const [recentOrders, setRecentOrders] = useState<Order[]>([]);
  const [pipelineCounts, setPipelineCounts] = useState<Record<string, number>>({ pending: 0, processing: 0, dispatched: 0, delivered: 0 });
  const [docs, setDocs] = useState<ComplianceDoc[]>([]);
  const [loadFailed, setLoadFailed] = useState(false);
  const [profileLoaded, setProfileLoaded] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const reload = useCallback(() => { setLoading(true); setReloadKey((k) => k + 1); }, []);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const [summaryRes, revenueRes, topRes, ordersRes, profileRes] = await Promise.allSettled([
          analyticsApi.summary(),
          analyticsApi.revenue('month'),
          analyticsApi.topProducts(),
          ordersApi.list({ pageSize: 50 }),
          profileApi.get(),
        ]);

        if (cancelled) return;
        setLoadFailed([summaryRes, revenueRes, topRes, ordersRes, profileRes].every((r) => r.status === 'rejected'));

        if (summaryRes.status === 'fulfilled') {
          const d = (summaryRes.value as any)?.data ?? summaryRes.value;
          setSummary({
            totalRevenue:   d.totalRevenue   ?? 0,
            totalOrders:    d.totalOrders    ?? 0,
            activeProducts: d.activeProducts ?? 0,
            avgOrderValue:  d.totalOrders > 0 ? Math.round(d.totalRevenue / d.totalOrders) : 0,
            revenueChange:  d.revenueGrowth  ?? d.revenueChange  ?? 0,
            ordersChange:   d.ordersGrowth   ?? d.ordersChange   ?? 0,
          });
        }

        if (revenueRes.status === 'fulfilled') {
          const raw: any[] = (revenueRes.value as any)?.data ?? revenueRes.value ?? [];
          setRevenue(raw.map((r) => ({
            month:   r.month ?? r.date ?? '',
            revenue: r.revenue ?? 0,
            orders:  r.orders  ?? 0,
          })));
        }

        if (topRes.status === 'fulfilled') {
          const raw: any[] = (topRes.value as any)?.data ?? topRes.value ?? [];
          setTopProducts(raw.map((r) => ({
            productId:   r.productId ?? r.id ?? '',
            productName: r.name ?? r.productName ?? '',
            totalSold:   r.unitsSold ?? r.totalSold ?? 0,
            revenue:     r.revenue ?? 0,
          })));
        }

        if (ordersRes.status === 'fulfilled') {
          const raw: any[] = (ordersRes.value as any)?.data?.items ?? (ordersRes.value as any)?.data ?? (ordersRes.value as any)?.items ?? [];
          const mapped: Order[] = raw.map((o: any) => ({
            id:             o.id,
            orderNumber:    o.orderNumber,
            shopId:         o.shop?.id ?? '',
            shopName:       o.shop?.shopName ?? o.shopName ?? '',
            shopAddress:    o.shop?.address ?? o.deliveryAddress ?? '',
            items:          (o.items ?? []).map((i: any) => ({
              productId:    i.productId,
              productName:  i.name ?? i.productName ?? '',
              productImage: '',
              quantity:     i.quantity,
              price:        i.unitPrice ?? i.price ?? 0,
            })),
            subtotal:       Number(o.subtotal ?? 0),
            total:          Number(o.subtotal ?? 0),
            status:         o.status,
            deliveryOption: o.deliveryType ?? 'standard',
            paymentMethod:  o.paymentMethod ?? 'eft',
            paymentStatus:  o.paymentStatus ?? 'held',
            createdAt:      o.createdAt,
            groupBuyId:     o.groupBuyId ?? undefined,
            isGroupBuy:     o.isGroupBuy ?? Boolean(o.groupBuyId),
            deliveryDistanceKm: o.deliveryDistanceKm == null ? undefined : Number(o.deliveryDistanceKm),
          }));
          setRecentOrders(mapped.slice(0, 5));
          const counts = { pending: 0, processing: 0, dispatched: 0, delivered: 0 };
          mapped.forEach((o) => {
            // "confirmed" orders are accepted but not yet packed, so count them as being prepared
            const bucket = o.status === 'confirmed' ? 'processing' : o.status;
            if (bucket in counts) (counts as any)[bucket]++;
          });
          setPipelineCounts(counts);
        }

        if (profileRes.status === 'fulfilled') {
          setProfileLoaded(true);
          const d = (profileRes.value as any)?.data ?? profileRes.value;
          const rawDocs: any[] = d?.documents ?? [];
          setDocs(rawDocs.map((d: any) => ({
            id:       d.id,
            docType:  d.docType,
            docUrl:   d.docUrl ?? '',
            status:   d.status,
            expiryDate: d.expiryDate,
          })));
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, [reloadKey]);

  // Animated counters for hero stats (must be called before any conditional returns!)
  const animRevenue = useAnimatedCounter(summary?.totalRevenue ?? 0, 2000, 500);
  const animOrders = useAnimatedCounter(summary?.totalOrders ?? 0, 1500, 700);
  const animProducts = useAnimatedCounter(summary?.activeProducts ?? 0, 1200, 900);

  if (loading) return <PageLoader variant="dashboard" />;

  const s = summary;
  const pendingCount = pipelineCounts.pending;
  const pipelineTotal = Object.values(pipelineCounts).reduce((a, b) => a + b, 0);
  const orderTimeline = PIPELINE_META.map((m) => ({ ...m, count: pipelineCounts[m.status] ?? 0 }));
  // Documents that are missing or were rejected block product and order actions
  const docsNeedingAction = profileLoaded
    ? DOC_META.filter(({ docType }) => {
        const doc = docs.find((d) => d.docType === docType);
        return !doc || doc.status === 'rejected';
      })
    : [];

  const compact = typeof window !== 'undefined' && window.matchMedia('(max-width: 1366px) and (max-height: 768px)').matches;

  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';

  return (
    <div className="p-4 sm:p-6 space-y-5 sm:space-y-6 compact:p-4 compact:space-y-3 animate-in">
      {/* Brand hero — mirrors the spazasure.co.za hero */}
      <section className="hero-banner p-6 sm:p-8">
        <div className="absolute -right-16 -top-16 w-64 h-64 rounded-full border-[28px] border-white/[0.06]" aria-hidden="true" />
        <img src="/spazasure_logo.png" alt="" aria-hidden="true" className="hidden md:block absolute right-8 top-1/2 -translate-y-1/2 w-36 h-36 object-contain opacity-[0.12]" />

        <div className="relative max-w-3xl">
          <span className="hero-pill">
            <span className="w-2 h-2 rounded-full bg-brand-yellow" />
            {format(new Date(), 'EEEE, d MMMM')}
          </span>
          <h1 className="mt-4 text-3xl sm:text-[40px] font-black leading-[1.08] text-white">
            {greeting}, <span className="text-brand-yellow">{user?.companyName?.split(' ')[0] ?? 'there'}</span>
          </h1>
          <p className="mt-3 text-[15px] leading-relaxed text-white/75 max-w-xl">
            {pendingCount > 0
              ? `You have ${pendingCount} new order${pendingCount === 1 ? '' : 's'} waiting for you. Accept them quickly to keep shops stocked.`
              : 'Your orders are up to date. Keep your catalogue fresh so spaza shops can find you.'}
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <button onClick={() => navigate('/orders')} className="btn-accent px-6 py-3">
              {pendingCount > 0 ? 'Review new orders' : 'View orders'} <ArrowRight size={15} />
            </button>
            <button
              onClick={() => navigate('/products?new=1')}
              className="inline-flex items-center gap-2 rounded-full border-2 border-white/35 px-6 py-2.5 text-sm font-bold text-white hover:border-white hover:bg-white/10 transition-colors"
            >
              <Plus size={15} /> Add product
            </button>
          </div>
        </div>

        {/* Stat row with coloured icon squares (website "hero-badges") */}
        <div className="relative mt-8 pt-6 border-t border-white/15 grid grid-cols-1 sm:grid-cols-3 gap-4 sm:gap-6">
          {[
            { label: 'Revenue', value: `R${animRevenue.toLocaleString()}`, icon: DollarSign, square: 'bg-brand-green text-white', change: s?.revenueChange },
            { label: 'Orders', value: animOrders.toLocaleString(), icon: ShoppingCart, square: 'bg-brand-yellow text-brand-blue', note: `${pendingCount} pending` },
            { label: 'Live products', value: animProducts.toLocaleString(), icon: Package, square: 'bg-brand-red text-white', note: 'On the marketplace' },
          ].map(({ label, value, icon: Icon, square, change, note }) => (
            <div key={label} className="flex items-center gap-3">
              <div className={`w-11 h-11 rounded-xl flex items-center justify-center flex-shrink-0 ${square}`}>
                <Icon size={18} />
              </div>
              <div className="min-w-0">
                <p className="text-2xl font-black leading-none tabular-nums">{value}</p>
                <p className="mt-1 text-[11px] font-bold uppercase tracking-wider text-white/55 flex items-center gap-1.5">
                  {label}
                  {change !== undefined ? (
                    <span className={change >= 0 ? 'text-emerald-300' : 'text-red-300'}>
                      {change >= 0 ? '▲' : '▼'} {Math.abs(change)}%
                    </span>
                  ) : note ? <span className="normal-case tracking-normal font-semibold text-white/45">· {note}</span> : null}
                </p>
              </div>
            </div>
          ))}
        </div>
      </section>

      {loadFailed && (
        <AlertBanner
          type="error"
          message="We couldn't load your dashboard data. The numbers below may be out of date."
          action={
            <button onClick={reload} className="flex items-center gap-1.5 text-xs font-bold bg-white border border-red-200 rounded-lg px-3 py-1.5 hover:bg-red-50">
              <RefreshCw size={12} /> Try again
            </button>
          }
        />
      )}

      {/* Compliance Alert */}
      {!alertDismissed && docsNeedingAction.length > 0 && (
        <AlertBanner
          type="warning"
          message={`${docsNeedingAction.map((d) => d.label).join(', ')} ${docsNeedingAction.length === 1 ? 'needs' : 'need'} to be uploaded before you can add products or process orders.`}
          onDismiss={() => setAlertDismissed(true)}
          action={
            <button onClick={() => navigate('/profile')} className="flex items-center gap-1.5 text-xs font-bold bg-white border border-amber-200 rounded-lg px-3 py-1.5 hover:bg-amber-50 whitespace-nowrap">
              Upload <ArrowRight size={12} />
            </button>
          }
        />
      )}

      {/* Quick actions */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
        {quickActions.map(({ label, hint, icon: Icon, to, tone }) => (
          <button
            key={label}
            onClick={() => navigate(to)}
            className="group card flex items-center gap-3 p-4 text-left hover:shadow-card-hover hover:border-brand-blue/20 transition-all"
          >
            <div className={`w-11 h-11 rounded-2xl flex items-center justify-center flex-shrink-0 ${tone}`}>
              <Icon size={19} />
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-extrabold text-gray-900 truncate">{label}</p>
              <p className="text-xs font-semibold text-ink-3 truncate">{hint}</p>
            </div>
            <ArrowRight size={15} className="text-ink-3 group-hover:text-brand-blue group-hover:translate-x-0.5 transition-all flex-shrink-0" />
          </button>
        ))}
      </div>

      {/* Charts Row — Enhanced */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 sm:gap-5 compact:gap-3">
        {/* Revenue Chart */}
        <div className="card p-4 sm:p-6 compact:p-4 lg:col-span-2 group hover:shadow-xl transition-shadow duration-300">
          <div className="flex items-center justify-between mb-6 compact:mb-3">
            <div>
              <h3 className="section-title">Revenue Trend</h3>
              <p className="text-xs text-gray-400 mt-1">Last 6 months performance overview</p>
            </div>
            <div className="flex items-center gap-3">
              {s?.revenueChange !== undefined && (
                <span className={`flex items-center gap-1 text-sm font-bold px-3 py-1.5 rounded-full ${s.revenueChange >= 0 ? 'bg-emerald-50 text-emerald-700 border border-emerald-100' : 'bg-red-50 text-red-700 border border-red-100'}`}>
                  <ArrowUpRight size={13} /> {s.revenueChange >= 0 ? '+' : ''}{s.revenueChange}%
                </span>
              )}
              <button
                onClick={() => navigate('/analytics')}
                className="flex items-center gap-1.5 text-xs text-primary font-bold hover:underline bg-primary-50 px-3 py-1.5 rounded-full border border-primary-100 hover:bg-primary-100 transition-colors"
              >
                <Eye size={12} /> Full Report
              </button>
            </div>
          </div>
          {revenue.length === 0 ? (
            <ChartEmpty height={compact ? 170 : 240} message="Revenue will show here once you complete your first orders." />
          ) : (
          <div className="animate-chart-reveal">
            <ResponsiveContainer width="100%" height={compact ? 170 : 240}>
              <AreaChart data={revenue}>
                <defs>
                  <linearGradient id="revGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#25449A" stopOpacity={0.15} />
                    <stop offset="95%" stopColor="#25449A" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
                <XAxis dataKey="month" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={(v) => `R${(v / 1000).toFixed(0)}k`} />
                <Tooltip
                  contentStyle={{ borderRadius: '16px', border: '1px solid #E5E7EB', boxShadow: '0 12px 40px rgba(0,0,0,0.12)', fontSize: '13px', padding: '12px 16px' }}
                  formatter={(v: number) => [`R${v.toLocaleString()}`, 'Revenue']}
                />
                <Area type="monotone" dataKey="revenue" stroke="#25449A" fill="url(#revGrad)" strokeWidth={3} dot={false} activeDot={{ r: 6, fill: '#25449A', strokeWidth: 3, stroke: '#fff' }} />
              </AreaChart>
            </ResponsiveContainer>
          </div>
          )}
        </div>

        {/* Order Pipeline — Enhanced */}
        <div className="card p-6 compact:p-4 hover:shadow-xl transition-shadow duration-300">
          <div className="flex items-center justify-between mb-6 compact:mb-3">
            <div>
              <h3 className="section-title">Order Pipeline</h3>
              <p className="text-xs text-gray-400 mt-1">Active order flow</p>
            </div>
            <div className="flex items-center gap-1.5 bg-gray-100 px-3 py-1.5 rounded-full">
              <Activity size={12} className="text-gray-500" />
              <span className="text-xs font-black text-gray-700 tabular-nums">{pipelineTotal}</span>
            </div>
          </div>
          <div className="space-y-4 compact:space-y-2">
            {orderTimeline.map(({ label, count, color }, i) => (
              <div key={label} className="group/pipeline">
                <div className="flex items-center gap-3">
                  <div className={`w-3 h-3 rounded-full flex-shrink-0 ${color} shadow-sm`} />
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center justify-between mb-2">
                      <p className="text-xs font-bold text-gray-700">{label}</p>
                      <span className="text-sm font-black text-gray-900 tabular-nums">{count}</span>
                    </div>
                    <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div
                        className={`h-full rounded-full ${color} animate-pipeline-bar`}
                        style={{ width: `${pipelineTotal > 0 ? (count / pipelineTotal) * 100 : 0}%`, animationDelay: `${0.5 + i * 0.15}s` }}
                      />
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-6 pt-5 compact:mt-3 compact:pt-3 border-t border-gray-100">
            {pendingCount > 0 && (
              <div className="flex items-center gap-2 bg-amber-50 border border-amber-100 rounded-xl p-3 animate-glow-pulse">
                <Clock size={14} className="text-amber-500" />
                <span className="text-xs font-bold text-amber-800">{pendingCount} order{pendingCount !== 1 ? 's' : ''} need your attention</span>
              </div>
            )}
            <button onClick={() => navigate('/orders')} className="mt-3 w-full flex items-center justify-center gap-2 text-sm text-primary font-bold hover:bg-primary-50 py-2.5 rounded-xl transition-colors">
              Review Orders <ArrowRight size={14} />
            </button>
          </div>
        </div>
      </div>

      {/* Bottom Row */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 compact:gap-3">
        {/* Top Products */}
        <div className="card p-5 compact:p-4">
          <div className="flex items-center justify-between mb-4">
            <h3 className="font-bold text-gray-900">Top Products</h3>
            <button onClick={() => navigate('/products')} className="text-xs text-primary font-semibold hover:underline">View all</button>
          </div>
          {topProducts.length === 0 ? (
            <ChartEmpty height={compact ? 140 : 180} message="Your best sellers will appear here." />
          ) : (
          <ResponsiveContainer width="100%" height={compact ? 140 : 180}>
            <BarChart data={topProducts} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" horizontal={false} />
              <XAxis type="number" tick={{ fontSize: 10, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
              <YAxis type="category" dataKey="productName" tick={{ fontSize: 10, fill: '#6B7280' }} width={85} axisLine={false} tickLine={false} />
              <Tooltip
                contentStyle={{ borderRadius: '12px', border: '1px solid #E5E7EB', fontSize: '12px' }}
                formatter={(v: number) => [v, 'Units Sold']}
              />
              <Bar dataKey="totalSold" fill="#078C50" radius={[0, 6, 6, 0]} />
            </BarChart>
          </ResponsiveContainer>
          )}
        </div>

        {/* Recent Orders */}
        <div className="card lg:col-span-2">
          <SectionHeader
            title="Recent Orders"
            action={
              <button onClick={() => navigate('/orders')} className="text-sm text-primary font-semibold hover:underline flex items-center gap-1">
                View all <ArrowRight size={13} />
              </button>
            }
          />
          {recentOrders.length === 0 ? (
            <div className="flex flex-col items-center justify-center text-center py-12 px-6">
              <ShoppingCart size={32} className="text-gray-300 mb-3" />
              <p className="text-sm font-semibold text-gray-700">No orders yet</p>
              <p className="text-xs text-gray-400 mt-1 max-w-xs">When spaza shops order your products, the newest ones will show up here.</p>
            </div>
          ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[560px]">
              <thead>
                <tr className="border-b border-gray-50 bg-gray-50/60">
                  <th className="table-header">Order #</th>
                  <th className="table-header">Shop</th>
                  <th className="table-header text-right">Total</th>
                  <th className="table-header text-center">Status</th>
                  <th className="table-header">Date</th>
                </tr>
              </thead>
              <tbody>
                {recentOrders.map((order, i) => (
                  <tr
                    key={order.id}
                    className="table-row cursor-pointer animate-table-row focus:outline-none focus-visible:bg-primary-50"
                    tabIndex={0}
                    onClick={() => navigate(`/orders?order=${order.id}`)}
                    onKeyDown={(e) => { if (e.key === 'Enter') navigate(`/orders?order=${order.id}`); }}
                  >
                    <td className="table-cell font-bold text-primary">{order.orderNumber}</td>
                    <td className="table-cell">
                      <div>
                        <p className="font-semibold text-gray-900">{order.shopName}</p>
                        <p className="text-xs text-gray-400">{order.shopAddress}</p>
                      </div>
                    </td>
                    <td className="table-cell text-right font-black text-gray-900 tabular-nums">R{order.total.toLocaleString()}</td>
                    <td className="table-cell text-center"><OrderStatusBadge status={order.status} /></td>
                    <td className="table-cell text-gray-400 text-xs">{format(new Date(order.createdAt), 'dd MMM yyyy')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          )}
        </div>
      </div>

      {/* Compliance Status — Enhanced */}
      <div className="card p-6 compact:p-4 hover:shadow-xl transition-shadow duration-300">
        <div className="flex items-center justify-between mb-5 compact:mb-3">
          <div>
            <h3 className="section-title">Compliance Status</h3>
            <p className="text-xs text-gray-400 mt-1">Keep your documents up to date to avoid restrictions</p>
          </div>
          <button onClick={() => navigate('/profile')} className="flex items-center gap-2 text-sm text-primary font-bold bg-primary-50 px-4 py-2 rounded-xl border border-primary-100 hover:bg-primary-100 transition-colors">
            Manage <ArrowRight size={13} />
          </button>
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 sm:gap-4 compact:gap-3">
          {DOC_META.map(({ docType, label }) => {
            const doc = docs.find((d) => d.docType === docType);
            const status = doc?.status ?? 'missing';
            const expiry = doc?.expiryDate ? format(new Date(doc.expiryDate), 'dd MMM yyyy') : null;
            return (
            <div
              key={docType}
              className={`compliance-card p-5 compact:p-3 rounded-2xl border-2 cursor-default ${
                status === 'approved' ? 'bg-emerald-50/80 border-emerald-200 hover:border-emerald-300' :
                status === 'pending'  ? 'bg-amber-50/80 border-amber-200 hover:border-amber-300' :
                'bg-gray-50/80 border-gray-200 hover:border-gray-300'
              }`}
            >
              <div className="flex items-center gap-2 mb-3">
                {status === 'approved' ? <CheckCircle size={18} className="text-emerald-600" /> :
                 status === 'pending'  ? <Clock size={18} className="text-amber-500 animate-pulse" /> :
                 <AlertTriangle size={18} className="text-gray-400" />}
                <span className={`text-xs font-bold capitalize ${
                  status === 'approved' ? 'text-emerald-700' :
                  status === 'pending'  ? 'text-amber-700' :
                  'text-gray-500'
                }`}>{status === 'missing' ? 'Not Uploaded' : status}</span>
              </div>
              <p className="text-sm font-bold text-gray-800">{label}</p>
              {expiry && <p className="text-[11px] text-gray-400 mt-1">Expires {expiry}</p>}
            </div>
            );
          })}
        </div>
      </div>

    </div>
  );
}
