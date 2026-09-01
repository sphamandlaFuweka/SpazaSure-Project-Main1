import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Package, ShoppingCart, DollarSign, TrendingUp, AlertTriangle,
  CheckCircle, ArrowRight, Clock, Plus, BarChart2, Bell,
  Zap, ArrowUpRight, Activity, Sparkles, Target, Flame,
  TrendingDown, Eye,
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

const quickActions = [
  { label: 'Add Product',   icon: Plus,        to: '/products',      color: 'bg-primary-50 text-primary hover:bg-primary-100 border-primary-100' },
  { label: 'View Orders',   icon: ShoppingCart, to: '/orders',        color: 'bg-blue-50 text-blue-600 hover:bg-blue-100 border-blue-100' },
  { label: 'Analytics',     icon: BarChart2,    to: '/analytics',     color: 'bg-violet-50 text-violet-600 hover:bg-violet-100 border-violet-100' },
  { label: 'Notifications', icon: Bell,         to: '/notifications', color: 'bg-amber-50 text-amber-600 hover:bg-amber-100 border-amber-100' },
];

const PIPELINE_META = [
  { status: 'pending',    label: 'Awaiting Acceptance', color: 'bg-amber-400' },
  { status: 'processing', label: 'Being Prepared',      color: 'bg-violet-400' },
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
            subtotal:       Number(o.subtotal ?? o.totalAmount ?? 0),
            deliveryFee:    Number(o.deliveryFee ?? 0),
            platformCommission: Number(o.platformCommission ?? 0),
            total:          Number(o.totalAmount ?? 0),
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
          mapped.forEach((o) => { if (o.status in counts) (counts as any)[o.status]++; });
          setPipelineCounts(counts);
        }

        if (profileRes.status === 'fulfilled') {
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
  }, []);

  // Animated counters for hero stats (must be called before any conditional returns!)
  const animRevenue = useAnimatedCounter(summary?.totalRevenue ?? 0, 2000, 500);
  const animOrders = useAnimatedCounter(summary?.totalOrders ?? 0, 1500, 700);
  const animProducts = useAnimatedCounter(summary?.activeProducts ?? 0, 1200, 900);

  if (loading) return <PageLoader variant="dashboard" />;

  const s = summary;
  const pendingCount = pipelineCounts.pending;
  const pipelineTotal = Object.values(pipelineCounts).reduce((a, b) => a + b, 0);
  const orderTimeline = PIPELINE_META.map((m) => ({ ...m, count: pipelineCounts[m.status] ?? 0 }));
  const beePending = docs.some((d) => d.docType === 'bee_certificate' && d.status === 'pending');

  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';

  return (
    <div className="p-6 space-y-6 animate-in">
      {/* ══════════════════════════════════════════════════════════════
          HERO WELCOME BANNER — World Class with Animations
         ══════════════════════════════════════════════════════════════ */}
      <div className="bg-gradient-to-br from-primary-800 via-primary-700 to-primary-600 rounded-3xl p-8 text-white relative overflow-hidden shadow-2xl group">
        {/* Animated floating particles */}
        <div className="absolute inset-0 pointer-events-none overflow-hidden">
          <div className="absolute top-6 right-20 w-3 h-3 bg-green-400/30 rounded-full animate-float-slow" />
          <div className="absolute top-16 right-48 w-2 h-2 bg-emerald-300/20 rounded-full animate-float-medium" />
          <div className="absolute bottom-8 right-36 w-4 h-4 bg-accent/20 rounded-full animate-float-fast" />
          <div className="absolute top-1/2 right-8 w-2 h-2 bg-white/10 rounded-full animate-float-slow" style={{ animationDelay: '1s' }} />
          <div className="absolute bottom-12 left-1/3 w-2 h-2 bg-green-300/15 rounded-full animate-float-medium" style={{ animationDelay: '0.5s' }} />
        </div>

        {/* Decorative gradient orbs */}
        <div className="absolute -top-20 -right-20 w-80 h-80 bg-gradient-to-br from-green-400/10 to-transparent rounded-full blur-3xl group-hover:scale-110 transition-transform duration-1000" />
        <div className="absolute -bottom-16 -left-16 w-60 h-60 bg-gradient-to-tr from-accent/10 to-transparent rounded-full blur-2xl" />
        <div className="absolute top-0 left-1/2 w-96 h-1 bg-gradient-to-r from-transparent via-green-400/30 to-transparent" />

        <div className="relative z-10">
          {/* Top bar with date and live indicator */}
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center gap-3">
              <span className="text-xs font-bold text-green-300/80 uppercase tracking-[0.2em]">
                {format(new Date(), 'EEEE, d MMMM yyyy')}
              </span>
              <div className="flex items-center gap-1.5 bg-emerald-500/20 border border-emerald-400/30 rounded-full px-2.5 py-1">
                <span className="relative flex h-2 w-2">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
                  <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-400" />
                </span>
                <span className="text-[10px] font-bold text-emerald-300 uppercase tracking-wider">Live</span>
              </div>
            </div>
            <div className="hidden md:flex items-center gap-2.5">
              <button
                onClick={() => navigate('/products')}
                className="flex items-center gap-2 bg-white/10 hover:bg-white/20 text-white text-sm font-semibold px-4 py-2.5 rounded-xl transition-all duration-300 border border-white/15 backdrop-blur-sm hover:scale-105 hover:shadow-lg"
              >
                <Plus size={15} /> Add Product
              </button>
              <button
                onClick={() => navigate('/orders')}
                className="flex items-center gap-2 bg-white text-primary text-sm font-bold px-5 py-2.5 rounded-xl hover:bg-green-50 transition-all duration-300 shadow-lg hover:shadow-xl hover:scale-105"
              >
                View Orders <ArrowRight size={15} className="group-hover:translate-x-0.5 transition-transform" />
              </button>
            </div>
          </div>

          {/* Main greeting */}
          <div className="mb-8">
            <h1 className="text-4xl md:text-5xl font-black text-white leading-tight tracking-tight">
              {greeting}, <span className="bg-gradient-to-r from-green-200 via-emerald-200 to-green-300 bg-clip-text text-transparent">{user?.companyName?.split(' ')[0]}</span> 👋
            </h1>
            <p className="text-green-200/70 text-base mt-2 max-w-lg">Here's your real-time business performance. Everything is looking great.</p>
          </div>

          {/* Hero Stats Row — Big animated numbers */}
          <div className="grid grid-cols-3 gap-6">
            <div className="bg-white/[0.07] backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/[0.12] transition-all duration-300 hover:scale-[1.02] group/stat">
              <div className="flex items-center gap-2 mb-3">
                <div className="p-2 bg-emerald-500/20 rounded-lg">
                  <DollarSign size={16} className="text-emerald-300" />
                </div>
                <span className="text-xs font-semibold text-green-200/60 uppercase tracking-wider">Revenue</span>
              </div>
              <p className="text-3xl font-black text-white tabular-nums">
                R{animRevenue.toLocaleString()}
              </p>
              {s?.revenueChange !== undefined && (
                <div className="flex items-center gap-1.5 mt-2">
                  <span className={`flex items-center gap-1 text-xs font-bold px-2 py-0.5 rounded-full ${s.revenueChange >= 0 ? 'bg-emerald-500/20 text-emerald-300' : 'bg-red-500/20 text-red-300'}`}>
                    {s.revenueChange >= 0 ? <TrendingUp size={10} /> : <TrendingDown size={10} />}
                    {Math.abs(s.revenueChange)}%
                  </span>
                  <span className="text-[11px] text-green-200/50">this month</span>
                </div>
              )}
            </div>

            <div className="bg-white/[0.07] backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/[0.12] transition-all duration-300 hover:scale-[1.02]">
              <div className="flex items-center gap-2 mb-3">
                <div className="p-2 bg-blue-500/20 rounded-lg">
                  <ShoppingCart size={16} className="text-blue-300" />
                </div>
                <span className="text-xs font-semibold text-green-200/60 uppercase tracking-wider">Orders</span>
              </div>
              <p className="text-3xl font-black text-white tabular-nums">{animOrders}</p>
              <div className="flex items-center gap-1.5 mt-2">
                <span className="flex items-center gap-1 text-xs font-bold px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-300">
                  <Flame size={10} /> {pendingCount} pending
                </span>
              </div>
            </div>

            <div className="bg-white/[0.07] backdrop-blur-sm border border-white/10 rounded-2xl p-5 hover:bg-white/[0.12] transition-all duration-300 hover:scale-[1.02]">
              <div className="flex items-center gap-2 mb-3">
                <div className="p-2 bg-violet-500/20 rounded-lg">
                  <Package size={16} className="text-violet-300" />
                </div>
                <span className="text-xs font-semibold text-green-200/60 uppercase tracking-wider">Products</span>
              </div>
              <p className="text-3xl font-black text-white tabular-nums">{animProducts}</p>
              <div className="flex items-center gap-1.5 mt-2">
                <span className="flex items-center gap-1 text-xs font-bold px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300">
                  <Target size={10} /> Active
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Compliance Alert */}
      {!alertDismissed && beePending && (
        <AlertBanner
          type="warning"
          message="⚠️ Your BEE Certificate is pending review. Upload it to maintain full compliance and avoid account restrictions."
          onDismiss={() => setAlertDismissed(true)}
        />
      )}

      {/* Quick Actions — Premium Cards */}
      <div className="grid grid-cols-4 gap-4">
        {quickActions.map(({ label, icon: Icon, to, color }, i) => (
          <button
            key={label}
            onClick={() => navigate(to)}
            className={`group relative flex flex-col items-start gap-3 p-5 rounded-2xl font-semibold text-sm transition-all duration-300 hover:scale-[1.03] active:scale-[0.97] border hover:shadow-xl ${color}`}
            style={{ animationDelay: `${i * 0.05}s` }}
          >
            <div className="p-2.5 bg-white/80 rounded-xl shadow-sm group-hover:shadow-md transition-all duration-300 group-hover:scale-110">
              <Icon size={20} />
            </div>
            <span className="font-bold">{label}</span>
            <ArrowRight size={14} className="absolute top-5 right-5 opacity-0 group-hover:opacity-100 transition-all duration-300 group-hover:translate-x-0.5" />
          </button>
        ))}
      </div>

      {/* Charts Row — Enhanced */}
      <div className="grid grid-cols-3 gap-5">
        {/* Revenue Chart */}
        <div className="card p-6 col-span-2 group hover:shadow-xl transition-shadow duration-300">
          <div className="flex items-center justify-between mb-6">
            <div>
              <h3 className="text-lg font-black text-gray-900">Revenue Trend</h3>
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
          <div className="animate-chart-reveal">
            <ResponsiveContainer width="100%" height={240}>
              <AreaChart data={revenue}>
                <defs>
                  <linearGradient id="revGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#1B4332" stopOpacity={0.15} />
                    <stop offset="95%" stopColor="#1B4332" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
                <XAxis dataKey="month" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={(v) => `R${(v / 1000).toFixed(0)}k`} />
                <Tooltip
                  contentStyle={{ borderRadius: '16px', border: '1px solid #E5E7EB', boxShadow: '0 12px 40px rgba(0,0,0,0.12)', fontSize: '13px', padding: '12px 16px' }}
                  formatter={(v: number) => [`R${v.toLocaleString()}`, 'Revenue']}
                />
                <Area type="monotone" dataKey="revenue" stroke="#1B4332" fill="url(#revGrad)" strokeWidth={3} dot={false} activeDot={{ r: 6, fill: '#1B4332', strokeWidth: 3, stroke: '#fff' }} />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Order Pipeline — Enhanced */}
        <div className="card p-6 hover:shadow-xl transition-shadow duration-300">
          <div className="flex items-center justify-between mb-6">
            <div>
              <h3 className="text-lg font-black text-gray-900">Order Pipeline</h3>
              <p className="text-xs text-gray-400 mt-1">Active order flow</p>
            </div>
            <div className="flex items-center gap-1.5 bg-gray-100 px-3 py-1.5 rounded-full">
              <Activity size={12} className="text-gray-500" />
              <span className="text-xs font-black text-gray-700 tabular-nums">{pipelineTotal}</span>
            </div>
          </div>
          <div className="space-y-4">
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

          <div className="mt-6 pt-5 border-t border-gray-100">
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
      <div className="grid grid-cols-3 gap-4">
        {/* Top Products */}
        <div className="card p-5">
          <div className="flex items-center justify-between mb-4">
            <h3 className="font-bold text-gray-900">Top Products</h3>
            <button onClick={() => navigate('/products')} className="text-xs text-primary font-semibold hover:underline">View all</button>
          </div>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={topProducts} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" horizontal={false} />
              <XAxis type="number" tick={{ fontSize: 10, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
              <YAxis type="category" dataKey="productName" tick={{ fontSize: 10, fill: '#6B7280' }} width={85} axisLine={false} tickLine={false} />
              <Tooltip
                contentStyle={{ borderRadius: '12px', border: '1px solid #E5E7EB', fontSize: '12px' }}
                formatter={(v: number) => [v, 'Units Sold']}
              />
              <Bar dataKey="totalSold" fill="#2E7D52" radius={[0, 6, 6, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        {/* Recent Orders */}
        <div className="card col-span-2">
          <SectionHeader
            title="Recent Orders"
            action={
              <button onClick={() => navigate('/orders')} className="text-sm text-primary font-semibold hover:underline flex items-center gap-1">
                View all <ArrowRight size={13} />
              </button>
            }
          />
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
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
                  <tr key={order.id} className="table-row cursor-pointer animate-table-row" onClick={() => navigate('/orders')}>
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
        </div>
      </div>

      {/* Compliance Status — Enhanced */}
      <div className="card p-6 hover:shadow-xl transition-shadow duration-300">
        <div className="flex items-center justify-between mb-5">
          <div>
            <h3 className="text-lg font-black text-gray-900">Compliance Status</h3>
            <p className="text-xs text-gray-400 mt-1">Keep your documents up to date to avoid restrictions</p>
          </div>
          <button onClick={() => navigate('/profile')} className="flex items-center gap-2 text-sm text-primary font-bold bg-primary-50 px-4 py-2 rounded-xl border border-primary-100 hover:bg-primary-100 transition-colors">
            Manage <ArrowRight size={13} />
          </button>
        </div>
        <div className="grid grid-cols-4 gap-4">
          {DOC_META.map(({ docType, label }) => {
            const doc = docs.find((d) => d.docType === docType);
            const status = doc?.status ?? 'missing';
            const expiry = doc?.expiryDate ? format(new Date(doc.expiryDate), 'dd MMM yyyy') : null;
            return (
            <div
              key={docType}
              className={`compliance-card p-5 rounded-2xl border-2 cursor-default ${
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

      {/* Upgrade CTA Banner — Premium with shimmer */}
      <div className="rounded-3xl bg-gradient-to-r from-primary-800 via-primary-700 to-primary-600 p-6 flex items-center justify-between relative overflow-hidden shadow-2xl animate-upgrade-shimmer group">
        <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/5 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500" />
        <div className="relative z-10 flex items-center gap-5">
          <div className="w-12 h-12 bg-accent/20 rounded-2xl flex items-center justify-center flex-shrink-0 group-hover:scale-110 transition-transform duration-300">
            <Sparkles size={24} className="text-accent" />
          </div>
          <div>
            <p className="font-black text-white text-base">Upgrade to Gold — Save R1,462/month in commission</p>
            <p className="text-green-200/70 text-sm mt-1">At your current volume, Gold tier (2%) saves you significantly vs Silver (3%)</p>
          </div>
        </div>
        <button
          onClick={() => navigate('/subscription')}
          className="relative z-10 flex items-center gap-2 bg-accent hover:bg-accent-600 text-white text-sm font-bold px-6 py-3 rounded-xl transition-all duration-300 shadow-lg hover:shadow-xl hover:scale-105 flex-shrink-0"
        >
          Upgrade Now <ArrowRight size={14} />
        </button>
      </div>
    </div>
  );
}
