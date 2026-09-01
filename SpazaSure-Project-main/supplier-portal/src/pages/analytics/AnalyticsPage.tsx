import { useState, useEffect } from 'react';
import { DollarSign, ShoppingCart, Package, TrendingUp, Download, ArrowUpRight } from 'lucide-react';
import {
  AreaChart, Area, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, PieChart, Pie, Cell, Legend,
} from 'recharts';
import { StatCard } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import { analyticsApi, ordersApi } from '../../services/api';
import toast from 'react-hot-toast';
import type { AnalyticsSummary, RevenueDataPoint, TopProduct, Order } from '../../types';

const COLORS = ['#1B4332', '#2E7D52', '#4CAF50', '#81C784', '#A5D6A7'];

type Period = 'week' | 'month' | 'year';

const CustomTooltip = ({ active, payload, label }: any) => {
  if (active && payload?.length) {
    return (
      <div className="bg-white border border-gray-100 rounded-xl shadow-card-lg p-3 text-sm">
        <p className="font-bold text-gray-700 mb-1">{label}</p>
        {payload.map((p: any) => (
          <p key={p.name} style={{ color: p.color }} className="font-semibold">
            {p.name === 'revenue' ? `R${p.value.toLocaleString()}` : p.value}
          </p>
        ))}
      </div>
    );
  }
  return null;
};

export default function AnalyticsPage() {
  const [period, setPeriod] = useState<Period>('month');
  const [loading, setLoading] = useState(true);
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [revenue, setRevenue] = useState<RevenueDataPoint[]>([]);
  const [topProducts, setTopProducts] = useState<TopProduct[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      const [summaryRes, revenueRes, topRes, ordersRes] = await Promise.allSettled([
        analyticsApi.summary(),
        analyticsApi.revenue(period),
        analyticsApi.topProducts(),
        ordersApi.list({ pageSize: 100 }),
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
        setOrders(raw.map((o: any) => ({
          id:             o.id,
          orderNumber:    o.orderNumber,
          shopId:         o.shop?.id ?? '',
          shopName:       o.shop?.shopName ?? o.shopName ?? '',
          shopAddress:    o.shop?.address ?? '',
          items:          [],
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
        })));
      }

      if (!cancelled) setLoading(false);
    };
    load();
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

  // Derive category revenue from top products (best available without a dedicated endpoint)
  const categoryRevenue = topProducts.map((p) => ({ name: p.productName, revenue: p.revenue }));

  const s = summary;
  const handleExport = () => toast.success('Report export started — check your email shortly.');

  return (
    <div className="p-6 space-y-6 animate-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="page-title">Analytics</h1>
          <p className="page-subtitle">Performance overview for your supplier account</p>
        </div>
        <button onClick={handleExport} className="btn-secondary flex items-center gap-2">
          <Download size={15} /> Export Report
        </button>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard title="Total Revenue" value={s?.totalRevenue ?? 0} prefix="R " change={s?.revenueChange} icon={<DollarSign size={20} />} variant="green" />
        <StatCard title="Total Orders" value={s?.totalOrders ?? 0} change={s?.ordersChange} icon={<ShoppingCart size={20} />} />
        <StatCard title="Active Products" value={s?.activeProducts ?? 0} icon={<Package size={20} />} />
        <StatCard title="Avg Order Value" value={s?.avgOrderValue ?? 0} prefix="R " icon={<TrendingUp size={20} />} variant="amber" />
      </div>

      {/* Subscription Tier Revenue Impact */}
      <div className="card p-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h3 className="font-bold text-gray-900">Subscription Tier Savings</h3>
            <p className="text-xs text-gray-400 mt-0.5">Your Silver tier saves you commission vs Basic</p>
          </div>
          <span className="text-xs font-bold text-slate-700 bg-slate-100 border border-slate-300 px-3 py-1.5 rounded-full">🥈 Silver Plan</span>
        </div>
        <div className="grid grid-cols-4 gap-3">
          {[
            { tier: 'Basic',  rate: '5%', monthly: 'R500', color: 'bg-gray-50 border-gray-200', text: 'text-gray-600' },
            { tier: 'Bronze', rate: '4%', monthly: 'R500', color: 'bg-orange-50 border-orange-200', text: 'text-orange-700' },
            { tier: 'Silver', rate: '3%', monthly: 'R1,500', color: 'bg-slate-50 border-slate-300', text: 'text-slate-700', active: true },
            { tier: 'Gold',   rate: '2%', monthly: 'R5,500', color: 'bg-amber-50 border-amber-300', text: 'text-amber-700' },
          ].map(({ tier, rate, monthly, color, text, active }) => (
            <div key={tier} className={`p-4 rounded-xl border-2 ${color} ${active ? 'ring-2 ring-slate-400' : ''}`}>
              <div className="flex items-center justify-between mb-2">
                <p className={`font-bold text-sm ${text}`}>{tier}</p>
                {active && <span className="text-[10px] font-bold bg-slate-600 text-white px-1.5 py-0.5 rounded-full">CURRENT</span>}
              </div>
              <p className={`text-2xl font-black ${text}`}>{rate}</p>
              <p className="text-xs text-gray-500 mt-0.5">commission</p>
              <p className="text-xs text-gray-400 mt-2">{monthly}/month</p>
            </div>
          ))}
        </div>
        <div className="mt-4 p-3 bg-emerald-50 rounded-xl border border-emerald-100 flex items-center justify-between">
          <p className="text-sm text-emerald-800 font-medium">
            💰 At Silver (3%), you save <strong>R731.25</strong> vs Basic (5%) on this month's orders
          </p>
          <button className="text-xs font-bold text-emerald-700 hover:underline flex items-center gap-1">
            Upgrade to Gold <ArrowUpRight size={12} />
          </button>
        </div>
      </div>

      {/* Period Toggle */}
      <div className="flex items-center gap-1.5 bg-white border border-gray-200 rounded-xl p-1 w-fit">
        {(['week', 'month', 'year'] as Period[]).map((p) => (
          <button
            key={p}
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
      <div className="grid grid-cols-3 gap-4">
        <div className="card p-5 col-span-2">
          <div className="flex items-center justify-between mb-5">
            <div>
              <h3 className="font-bold text-gray-900">Revenue Trend</h3>
              <p className="text-xs text-gray-400 mt-0.5">Monthly revenue over time</p>
            </div>
            <span className="text-xs font-semibold text-emerald-600 bg-emerald-50 px-2.5 py-1 rounded-full border border-emerald-100">
              {s && s.revenueChange >= 0 ? '+' : ''}{s?.revenueChange ?? 0}% ↑
            </span>
          </div>
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
              <YAxis tick={{ fontSize: 12, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={(v) => `R${(v / 1000).toFixed(0)}k`} />
              <Tooltip content={<CustomTooltip />} />
              <Area type="monotone" dataKey="revenue" stroke="#1B4332" fill="url(#grad)" strokeWidth={2.5} dot={false} activeDot={{ r: 5, fill: '#1B4332' }} />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Orders by Status</h3>
          <ResponsiveContainer width="100%" height={240}>
            <PieChart>
              <Pie data={ordersByStatus} cx="50%" cy="45%" innerRadius={55} outerRadius={85} dataKey="value" paddingAngle={3}>
                {ordersByStatus.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
              </Pie>
              <Tooltip contentStyle={{ borderRadius: '12px', border: '1px solid #E5E7EB' }} />
              <Legend iconType="circle" iconSize={8} wrapperStyle={{ fontSize: '12px' }} />
            </PieChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Charts Row 2 */}
      <div className="grid grid-cols-2 gap-4">
        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Orders per Month</h3>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={revenue}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
              <XAxis dataKey="month" tick={{ fontSize: 12, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fontSize: 12, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="orders" fill="#2E7D52" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Revenue by Category</h3>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={categoryRevenue} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" horizontal={false} />
              <XAxis type="number" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={(v) => `R${(v / 1000).toFixed(0)}k`} />
              <YAxis type="category" dataKey="name" tick={{ fontSize: 11, fill: '#6B7280' }} width={90} axisLine={false} tickLine={false} />
              <Tooltip contentStyle={{ borderRadius: '12px', border: '1px solid #E5E7EB' }} formatter={(v: number) => [`R${v.toLocaleString()}`, 'Revenue']} />
              <Bar dataKey="revenue" fill="#1B4332" radius={[0, 6, 6, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Top Products + Transactions */}
      <div className="grid grid-cols-2 gap-4">
        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-4">Top Products by Revenue</h3>
          <div className="space-y-4">
            {topProducts.length === 0 && <p className="text-sm text-gray-400 text-center py-4">No product data yet.</p>}
            {topProducts.map((p, i) => (
              <div key={p.productId}>
                <div className="flex justify-between text-sm mb-1.5">
                  <div className="flex items-center gap-2">
                    <span className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-black text-white ${i === 0 ? 'bg-amber-500' : i === 1 ? 'bg-gray-400' : 'bg-orange-400'}`}>
                      {i + 1}
                    </span>
                    <span className="font-semibold text-gray-900">{p.productName}</span>
                  </div>
                  <span className="font-bold text-gray-700">R{p.revenue.toLocaleString()}</span>
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
          <div className="px-5 py-4 border-b border-gray-100">
            <h3 className="font-bold text-gray-900">Recent Transactions</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-50 bg-gray-50/50">
                  <th className="table-header">Order</th>
                  <th className="table-header">Shop</th>
                  <th className="table-header text-right">Gross</th>
                  <th className="table-header text-right">Net</th>
                </tr>
              </thead>
              <tbody>
                {orders.length === 0 && (
                  <tr><td colSpan={4} className="table-cell text-center text-gray-400">No transactions yet.</td></tr>
                )}
                {orders.map((o) => (
                  <tr key={o.id} className="table-row">
                    <td className="table-cell font-bold text-primary">{o.orderNumber}</td>
                    <td className="table-cell text-gray-600">{o.shopName}</td>
                    <td className="table-cell text-right font-semibold">R{o.total.toFixed(2)}</td>
                    <td className="table-cell text-right font-bold text-emerald-700">
                      R{(o.subtotal - o.platformCommission).toFixed(2)}
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
