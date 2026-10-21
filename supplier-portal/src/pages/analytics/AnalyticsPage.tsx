import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { DollarSign, ShoppingCart, Package, TrendingUp, TrendingDown, Download, ArrowUpRight, BarChart2 } from 'lucide-react';
import {
  AreaChart, Area, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, PieChart, Pie, Cell, Legend,
} from 'recharts';
import clsx from 'clsx';
import { StatCard, TierIcon } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import PayoutsCard from './PayoutsCard';
import { analyticsApi, ordersApi, subscriptionApi } from '../../services/api';
import { useAuthStore } from '../../store/authStore';
import { formatCurrency } from '../../utils';
import toast from 'react-hot-toast';
import type { AnalyticsSummary, RevenueDataPoint, TopProduct, Order, SubscriptionPlan } from '../../types';

const COLORS = ['#1B4332', '#2E7D52', '#4CAF50', '#81C784', '#A5D6A7', '#C8E6C9'];

const TIER_STYLE: Record<string, { color: string; text: string }> = {
  basic:  { color: 'bg-gray-50 border-gray-200',     text: 'text-gray-600' },
  bronze: { color: 'bg-orange-50 border-orange-200', text: 'text-orange-700' },
  silver: { color: 'bg-slate-50 border-slate-300',   text: 'text-slate-700' },
  gold:   { color: 'bg-amber-50 border-amber-300',   text: 'text-amber-700' },
};

type Period = 'week' | 'month' | 'year';

const PERIOD_LABEL: Record<Period, string> = {
  week: 'Daily revenue this week',
  month: 'Revenue by month',
  year: 'Revenue by year',
};

const CustomTooltip = ({ active, payload, label }: any) => {
  if (active && payload?.length) {
    return (
      <div className="bg-white border border-gray-100 rounded-xl shadow-card-lg p-3 text-sm">
        <p className="font-bold text-gray-700 mb-1">{label}</p>
        {payload.map((p: any) => (
          <p key={p.name} style={{ color: p.color }} className="font-semibold">
            {p.name === 'revenue' ? formatCurrency(p.value) : `${p.value} orders`}
          </p>
        ))}
      </div>
    );
  }
  return null;
};

function ChartEmpty({ height, message }: { height: number; message: string }) {
  return (
    <div style={{ height }} className="flex flex-col items-center justify-center text-center rounded-xl border border-dashed border-gray-200 bg-gray-50/50 px-6">
      <BarChart2 size={28} className="text-gray-300 mb-2" />
      <p className="text-xs text-gray-400 max-w-[240px]">{message}</p>
    </div>
  );
}

function downloadCsv(filename: string, rows: (string | number)[][]) {
  const escape = (v: string | number) => {
    const s = String(v ?? '');
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const csv = rows.map((r) => r.map(escape).join(',')).join('\n');
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export default function AnalyticsPage() {
  const navigate = useNavigate();
  const { user } = useAuthStore();
  const [period, setPeriod] = useState<Period>('month');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [revenue, setRevenue] = useState<RevenueDataPoint[]>([]);
  const [topProducts, setTopProducts] = useState<TopProduct[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [currentTier, setCurrentTier] = useState<string>(user?.tier ?? 'basic');

  // Everything except the revenue series is independent of the period, so load it once
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const [summaryRes, topRes, ordersRes, plansRes, currentRes] = await Promise.allSettled([
        analyticsApi.summary(),
        analyticsApi.topProducts(),
        ordersApi.list({ pageSize: 100 }),
        subscriptionApi.getPlans(),
        subscriptionApi.getCurrent(),
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
        setOrders(raw.map((o: any) => ({
          id:             o.id,
          orderNumber:    o.orderNumber,
          shopId:         o.shop?.id ?? '',
          shopName:       o.shop?.shopName ?? o.shopName ?? '',
          shopAddress:    o.shop?.address ?? '',
          items:          [],
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
        })));
      }

      if (plansRes.status === 'fulfilled') {
        const raw = (plansRes.value as any)?.data ?? plansRes.value ?? [];
        if (Array.isArray(raw)) setPlans([...raw].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)));
      }
      if (currentRes.status === 'fulfilled') {
        const d = (currentRes.value as any)?.data ?? currentRes.value;
        if (d?.currentTier) setCurrentTier(d.currentTier);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setRefreshing(true);
    analyticsApi.revenue(period)
      .then((res) => {
        if (cancelled) return;
        const raw: any[] = (res as any)?.data ?? res ?? [];
        setRevenue(raw.map((r) => ({
          month:   r.month ?? r.date ?? '',
          revenue: r.revenue ?? 0,
          orders:  r.orders  ?? 0,
        })));
      })
      .catch(() => { if (!cancelled) setRevenue([]); })
      .finally(() => { if (!cancelled) { setRefreshing(false); setLoading(false); } });
    return () => { cancelled = true; };
  }, [period]);

  if (loading) return <PageLoader variant="dashboard" />;

  // Derive orders-by-status for pie chart from live orders
  const statusCounts: Record<string, number> = {};
  orders.forEach((o) => { statusCounts[o.status] = (statusCounts[o.status] ?? 0) + 1; });
  const ordersByStatus = Object.entries(statusCounts).map(([name, value]) => ({
    name: name.charAt(0).toUpperCase() + name.slice(1),
    value,
  }));

  const productRevenue = topProducts.map((p) => ({ name: p.productName, revenue: p.revenue }));
  const recentTransactions = orders.filter((o) => o.status !== 'cancelled').slice(0, 10);

  // Commission savings for this calendar month, based on delivered orders
  const now = new Date();
  const monthRevenue = orders
    .filter((o) => o.status === 'delivered')
    .filter((o) => { const d = new Date(o.createdAt); return d.getMonth() === now.getMonth() && d.getFullYear() === now.getFullYear(); })
    .reduce((sum, o) => sum + o.subtotal, 0);
  const basicPlan = plans.find((p) => p.tier === 'basic');
  const currentPlan = plans.find((p) => p.tier === currentTier);
  const goldPlan = plans.find((p) => p.tier === 'gold');
  const savingVsBasic = basicPlan && currentPlan
    ? (monthRevenue * (basicPlan.commissionRate - currentPlan.commissionRate)) / 100
    : 0;
  const tierLabel = currentTier.charAt(0).toUpperCase() + currentTier.slice(1);

  const s = summary;
  const revenueUp = (s?.revenueChange ?? 0) >= 0;

  const handleExport = () => {
    if (!s && revenue.length === 0 && orders.length === 0) {
      toast.error('There is no data to export yet.');
      return;
    }
    const rows: (string | number)[][] = [
      ['SpazaSure analytics report', user?.companyName ?? ''],
      ['Generated', new Date().toLocaleString('en-ZA')],
      [],
      ['Summary'],
      ['Total revenue', s?.totalRevenue ?? 0],
      ['Total orders', s?.totalOrders ?? 0],
      ['Active products', s?.activeProducts ?? 0],
      ['Average order value', s?.avgOrderValue ?? 0],
      [],
      [`Revenue (${period})`],
      ['Period', 'Revenue', 'Orders'],
      ...revenue.map((r) => [r.month, r.revenue, r.orders ?? 0]),
      [],
      ['Top products'],
      ['Product', 'Units sold', 'Revenue'],
      ...topProducts.map((p) => [p.productName, p.totalSold, p.revenue]),
      [],
      ['Orders'],
      ['Order', 'Shop', 'Status', 'Date', 'Product value'],
      ...orders.map((o) => [
        o.orderNumber, o.shopName, o.status, new Date(o.createdAt).toLocaleDateString('en-ZA'),
        o.subtotal.toFixed(2),
      ]),
    ];
    downloadCsv(`spazasure-analytics-${new Date().toISOString().slice(0, 10)}.csv`, rows);
    toast.success('Report downloaded');
  };

  return (
    <div className="p-4 sm:p-6 space-y-6 animate-in">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Analytics</h1>
          <p className="page-subtitle">Performance overview for your supplier account</p>
        </div>
        <button onClick={handleExport} className="btn-secondary flex items-center gap-2">
          <Download size={15} /> Export CSV
        </button>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard title="Total Revenue" value={s?.totalRevenue ?? 0} prefix="R " change={s?.revenueChange} icon={<DollarSign size={20} />} variant="green" />
        <StatCard title="Total Orders" value={s?.totalOrders ?? 0} change={s?.ordersChange} icon={<ShoppingCart size={20} />} />
        <StatCard title="Active Products" value={s?.activeProducts ?? 0} icon={<Package size={20} />} />
        <StatCard title="Avg Order Value" value={s?.avgOrderValue ?? 0} prefix="R " icon={<TrendingUp size={20} />} variant="amber" />
      </div>

      <PayoutsCard />

      {/* Subscription Tier Commission Impact (real plans + this month's delivered orders) */}
      {plans.length > 0 && (
        <div className="card p-5">
          <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
            <div>
              <h3 className="font-bold text-gray-900">Commission by Plan</h3>
              <p className="text-xs text-gray-400 mt-0.5">What you pay SpazaSure on each plan</p>
            </div>
            <span className="text-xs font-bold text-slate-700 bg-slate-100 border border-slate-300 px-3 py-1.5 rounded-full inline-flex items-center gap-1">
              <TierIcon tier={currentTier} size={12} /> {tierLabel} Plan
            </span>
          </div>
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
            {plans.map((plan) => {
              const style = TIER_STYLE[plan.tier] ?? TIER_STYLE.basic;
              const active = plan.tier === currentTier;
              return (
                <div key={plan.tier} className={clsx('p-4 rounded-xl border-2', style.color, active && 'ring-2 ring-slate-400')}>
                  <div className="flex items-center justify-between mb-2">
                    <p className={clsx('font-bold text-sm', style.text)}>{plan.name}</p>
                    {active && <span className="text-[10px] font-bold bg-slate-600 text-white px-1.5 py-0.5 rounded-full">CURRENT</span>}
                  </div>
                  <p className={clsx('text-2xl font-black', style.text)}>{plan.commissionRate}%</p>
                  <p className="text-xs text-gray-500 mt-0.5">commission</p>
                  <p className="text-xs text-gray-400 mt-2">
                    {plan.monthlyPrice > 0 ? `${formatCurrency(plan.monthlyPrice)}/month` : 'Free'}
                  </p>
                </div>
              );
            })}
          </div>
          <div className="mt-4 p-3 bg-emerald-50 rounded-xl border border-emerald-100 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <p className="text-sm text-emerald-800 font-medium">
              {currentTier === 'basic' || !basicPlan || !currentPlan
                ? <>You delivered <strong>{formatCurrency(monthRevenue)}</strong> in orders this month.</>
                : <>On {tierLabel} ({currentPlan.commissionRate}%) you saved <strong>{formatCurrency(savingVsBasic)}</strong> vs Basic ({basicPlan.commissionRate}%) on this month's delivered orders.</>}
            </p>
            {currentTier !== 'gold' && goldPlan && (
              <button onClick={() => navigate('/subscription')} className="text-xs font-bold text-emerald-700 hover:underline flex items-center gap-1 whitespace-nowrap">
                Compare plans <ArrowUpRight size={12} />
              </button>
            )}
          </div>
        </div>
      )}

      {/* Period Toggle */}
      <div className="flex items-center gap-1.5 bg-white border border-gray-200 rounded-xl p-1 w-fit" role="tablist" aria-label="Revenue period">
        {(['week', 'month', 'year'] as Period[]).map((p) => (
          <button
            key={p}
            role="tab"
            aria-selected={period === p}
            onClick={() => setPeriod(p)}
            className={`px-4 py-1.5 rounded-lg text-sm font-semibold capitalize transition-all ${
              period === p ? 'bg-primary text-white shadow-sm' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {p}
          </button>
        ))}
      </div>

      {/* Charts Row 1 */}
      <div className={clsx('grid grid-cols-1 lg:grid-cols-3 gap-4 transition-opacity', refreshing && 'opacity-60')}>
        <div className="card p-5 lg:col-span-2">
          <div className="flex items-center justify-between mb-5">
            <div>
              <h3 className="font-bold text-gray-900">Revenue Trend</h3>
              <p className="text-xs text-gray-400 mt-0.5">{PERIOD_LABEL[period]}</p>
            </div>
            {s && (
              <span className={clsx(
                'text-xs font-semibold px-2.5 py-1 rounded-full border inline-flex items-center gap-1',
                revenueUp ? 'text-emerald-600 bg-emerald-50 border-emerald-100' : 'text-red-600 bg-red-50 border-red-100',
              )}>
                {revenueUp ? <TrendingUp size={12} /> : <TrendingDown size={12} />}
                {revenueUp ? '+' : ''}{s.revenueChange}%
              </span>
            )}
          </div>
          {revenue.length === 0 ? (
            <ChartEmpty height={240} message="No revenue recorded for this period yet." />
          ) : (
            <ResponsiveContainer width="100%" height={240}>
              <AreaChart data={revenue}>
                <defs>
                  <linearGradient id="grad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#1B4332" stopOpacity={0.15} />
                    <stop offset="95%" stopColor="#1B4332" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="month" tick={{ fontSize: 12, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 12, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={(v) => (v >= 1000 ? `R${(v / 1000).toFixed(0)}k` : `R${v}`)} />
                <Tooltip content={<CustomTooltip />} />
                <Area type="monotone" dataKey="revenue" stroke="#1B4332" fill="url(#grad)" strokeWidth={2.5} dot={false} activeDot={{ r: 5, fill: '#1B4332' }} />
              </AreaChart>
            </ResponsiveContainer>
          )}
        </div>

        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Orders by Status</h3>
          {ordersByStatus.length === 0 ? (
            <ChartEmpty height={240} message="No orders yet." />
          ) : (
            <ResponsiveContainer width="100%" height={240}>
              <PieChart>
                <Pie data={ordersByStatus} cx="50%" cy="45%" innerRadius={55} outerRadius={85} dataKey="value" paddingAngle={3}>
                  {ordersByStatus.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
                </Pie>
                <Tooltip contentStyle={{ borderRadius: '12px', border: '1px solid #E5E7EB' }} />
                <Legend iconType="circle" iconSize={8} wrapperStyle={{ fontSize: '12px' }} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>

      {/* Charts Row 2 */}
      <div className={clsx('grid grid-cols-1 lg:grid-cols-2 gap-4 transition-opacity', refreshing && 'opacity-60')}>
        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Orders per Period</h3>
          {revenue.length === 0 ? (
            <ChartEmpty height={200} message="No orders recorded for this period yet." />
          ) : (
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={revenue}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="month" tick={{ fontSize: 12, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis allowDecimals={false} tick={{ fontSize: 12, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <Tooltip content={<CustomTooltip />} />
                <Bar dataKey="orders" fill="#2E7D52" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>

        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Revenue by Product</h3>
          {productRevenue.length === 0 ? (
            <ChartEmpty height={200} message="Product revenue will appear after your first sales." />
          ) : (
            // A ranked list rather than a sideways chart: product names stay readable at any width
            <ol className="space-y-3">
              {productRevenue.slice(0, 6).map((p, i) => {
                const best = Math.max(...productRevenue.map((r) => r.revenue), 1);
                return (
                  <li key={`${p.name}-${i}`} title={`${p.name}: ${formatCurrency(p.revenue)}`}>
                    <div className="flex items-baseline gap-2 text-sm">
                      <span className="w-4 flex-shrink-0 text-xs font-bold text-gray-400 tabular-nums">{i + 1}</span>
                      <span className="flex-1 min-w-0 truncate font-semibold text-gray-800">{p.name}</span>
                      <span className="flex-shrink-0 text-xs font-bold text-gray-900 tabular-nums">{formatCurrency(p.revenue)}</span>
                    </div>
                    <div className="ml-6 mt-1 h-1.5 rounded-full bg-gray-100 overflow-hidden">
                      <div className="h-full rounded-full bg-[#1B4332]" style={{ width: `${Math.max(4, (p.revenue / best) * 100)}%` }} />
                    </div>
                  </li>
                );
              })}
            </ol>
          )}
        </div>
      </div>

      {/* Top Products + Transactions */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-4">Top Products by Revenue</h3>
          <div className="space-y-4">
            {topProducts.length === 0 && <p className="text-sm text-gray-400 text-center py-4">No product sales yet.</p>}
            {topProducts.map((p, i) => (
              <div key={p.productId || p.productName}>
                <div className="flex justify-between gap-3 text-sm mb-1.5">
                  <div className="flex items-center gap-2 min-w-0">
                    <span className={`w-5 h-5 flex-shrink-0 rounded-full flex items-center justify-center text-[10px] font-black text-white ${i === 0 ? 'bg-amber-500' : i === 1 ? 'bg-gray-400' : 'bg-orange-400'}`}>
                      {i + 1}
                    </span>
                    <span className="font-semibold text-gray-900 truncate">{p.productName}</span>
                  </div>
                  <span className="font-bold text-gray-700 whitespace-nowrap">{formatCurrency(p.revenue)}</span>
                </div>
                <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-primary-600 to-primary-400 transition-all duration-700"
                    style={{ width: `${topProducts[0].revenue > 0 ? (p.revenue / topProducts[0].revenue) * 100 : 0}%` }}
                  />
                </div>
                <p className="text-xs text-gray-400 mt-1">{p.totalSold} units sold</p>
              </div>
            ))}
          </div>
        </div>

        <div className="card">
          <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <h3 className="font-bold text-gray-900">Recent Transactions</h3>
            <button onClick={() => navigate('/orders')} className="text-xs text-primary font-semibold hover:underline">View all</button>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[420px]">
              <thead>
                <tr className="border-b border-gray-50 bg-gray-50/50">
                  <th className="table-header">Order</th>
                  <th className="table-header">Shop</th>
                  <th className="table-header text-right">Product value</th>
                </tr>
              </thead>
              <tbody>
                {recentTransactions.length === 0 && (
                  <tr><td colSpan={3} className="table-cell text-center text-gray-400">No transactions yet.</td></tr>
                )}
                {recentTransactions.map((o) => (
                  <tr key={o.id} className="table-row cursor-pointer" onClick={() => navigate(`/orders?order=${o.id}`)}>
                    <td className="table-cell font-bold text-primary">{o.orderNumber}</td>
                    <td className="table-cell text-gray-600">{o.shopName}</td>
                    <td className="table-cell text-right font-bold text-emerald-700">
                      {formatCurrency(o.subtotal)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
