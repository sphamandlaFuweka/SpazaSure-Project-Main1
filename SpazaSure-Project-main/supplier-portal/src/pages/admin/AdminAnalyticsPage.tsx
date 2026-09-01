import { useState, useEffect } from 'react';
import { DollarSign, ShoppingCart, Users, TrendingUp, Download, ArrowUpRight, Loader2 } from 'lucide-react';
import {
  AreaChart, Area, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, PieChart, Pie, Cell, Legend,
} from 'recharts';
import { adminOrdersApi, adminSuppliersApi, adminAnalyticsApi } from '../../services/api';
import toast from 'react-hot-toast';

const COLORS = ['#1B4332', '#2E7D52', '#4CAF50', '#81C784', '#A5D6A7'];

const CustomTooltip = ({ active, payload, label }: any) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-gray-100 rounded-xl shadow-card-lg p-3 text-sm">
      <p className="font-bold text-gray-700 mb-1">{label}</p>
      {payload.map((p: any) => (
        <p key={p.name} style={{ color: p.color }} className="font-semibold">
          {p.name === 'revenue' || p.name === 'fees' ? `R${Number(p.value).toLocaleString()}` : p.value} {p.name}
        </p>
      ))}
    </div>
  );
};

export default function AdminAnalyticsPage() {
  const [period, setPeriod] = useState<'week' | 'month' | 'year'>('month');
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState<any>({});
  const [orders, setOrders] = useState<any[]>([]);
  const [suppliers, setSuppliers] = useState<any[]>([]);
  const [revenueData, setRevenueData] = useState<any[]>([]);

  useEffect(() => {
    loadData();
  }, [period]);

  const loadData = async () => {
    setLoading(true);
    try {
      const [ordersRes, suppliersRes, analyticsRes] = await Promise.allSettled([
        adminOrdersApi.list({ page: 1, pageSize: 1000 }),
        adminSuppliersApi.list({ page: 1, pageSize: 100 }),
        adminAnalyticsApi.revenue(period),
      ]);

      if (ordersRes.status === 'fulfilled') {
        const allOrders = ordersRes.value.data ?? [];
        setOrders(allOrders);
        setStats(ordersRes.value.summary ?? {});
      }

      if (suppliersRes.status === 'fulfilled') {
        const data = suppliersRes.value?.data ?? suppliersRes.value;
        const items = data?.items ?? [];
        setSuppliers(items);
      }

      if (analyticsRes.status === 'fulfilled') {
        const data = analyticsRes.value;
        if (Array.isArray(data)) setRevenueData(data);
      }
    } catch {
      toast.error('Failed to load analytics');
    } finally {
      setLoading(false);
    }
  };

  // Calculate stats from real order data
  const totalRevenue = stats.deliveredRevenue ?? orders.filter((o) => o.status === 'delivered').reduce((s: number, o: any) => s + Number(o.totalAmount ?? 0), 0);
  const totalOrders = stats.totalOrders ?? orders.length;
  const totalPlatformFees = stats.totalPlatformFees ?? orders.reduce((s: number, o: any) => s + Number(o.platformCommission ?? 0), 0);

  // Orders by status for pie chart
  const ordersByStatus = [
    { name: 'Delivered',   value: orders.filter((o) => o.status === 'delivered').length },
    { name: 'Processing',  value: orders.filter((o) => o.status === 'processing').length },
    { name: 'Dispatched',  value: orders.filter((o) => o.status === 'dispatched').length },
    { name: 'Pending',     value: orders.filter((o) => o.status === 'pending').length },
    { name: 'Cancelled',   value: orders.filter((o) => o.status === 'cancelled').length },
  ].filter((s) => s.value > 0);

  // Revenue chart data
  const feeIncome = revenueData.map((r: any) => ({ ...r, fees: Math.round(Number(r.revenue ?? 0) * 0.035) }));

  // Supplier breakdown (from real data)
  const topSupplierRevenue = suppliers.length > 0
    ? Math.max(...suppliers.map((s: any) => Number(s.totalRevenue ?? s.totalOrders ?? 1)))
    : 1;

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-[400px]">
        <Loader2 size={36} className="text-gray-300 animate-spin" />
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6 animate-in">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="page-title">Platform Analytics</h1>
          <p className="page-subtitle">Full platform performance overview</p>
        </div>
        <button onClick={() => toast.success('Report export started')} className="btn-secondary flex items-center gap-2">
          <Download size={15} /> Export Report
        </button>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          { label: 'Platform GMV',     value: `R${(Number(totalRevenue) / 1000).toFixed(0)}k`, icon: <DollarSign size={20} />, color: 'stat-card-green' },
          { label: 'Total Orders',     value: totalOrders,  icon: <ShoppingCart size={20} />, color: 'card p-5' },
          { label: 'Active Suppliers', value: suppliers.length, icon: <Users size={20} />,       color: 'card p-5' },
          { label: 'Fee Income',       value: `R${(Number(totalPlatformFees) / 1000).toFixed(1)}k`, icon: <TrendingUp size={20} />, color: 'stat-card-amber' },
        ].map(({ label, value, icon, color }) => (
          <div key={label} className={`${color} animate-in`}>
            <div className="flex items-start justify-between">
              <div>
                <p className="text-xs font-bold uppercase tracking-wider opacity-70">{label}</p>
                <p className="text-3xl font-black mt-1.5 tabular-nums">{value}</p>
              </div>
              <div className="p-2.5 bg-white/15 rounded-xl ring-1 ring-white/10">{icon}</div>
            </div>
          </div>
        ))}
      </div>

      {/* Period Toggle */}
      <div className="flex items-center gap-1.5 bg-white border border-gray-200 rounded-xl p-1 w-fit">
        {(['week', 'month', 'year'] as const).map((p) => (
          <button key={p} onClick={() => setPeriod(p)}
            className={`px-4 py-1.5 rounded-lg text-sm font-semibold capitalize transition-all ${period === p ? 'bg-slate-800 text-white shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}>
            {p}
          </button>
        ))}
      </div>

      {/* Charts Row 1 */}
      <div className="grid grid-cols-3 gap-4">
        <div className="card p-5 col-span-2">
          <div className="flex items-center justify-between mb-5">
            <div>
              <h3 className="font-bold text-gray-900">Platform GMV Trend</h3>
              <p className="text-xs text-gray-400 mt-0.5">Total gross merchandise value</p>
            </div>
          </div>
          {revenueData.length > 0 ? (
            <ResponsiveContainer width="100%" height={240}>
              <AreaChart data={revenueData}>
                <defs>
                  <linearGradient id="adminGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#1E293B" stopOpacity={0.12} />
                    <stop offset="95%" stopColor="#1E293B" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
                <XAxis dataKey="date" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={(v) => `R${(v / 1000).toFixed(0)}k`} />
                <Tooltip content={<CustomTooltip />} />
                <Area type="monotone" dataKey="revenue" stroke="#1E293B" fill="url(#adminGrad)" strokeWidth={2.5} dot={false} activeDot={{ r: 5, fill: '#1E293B', strokeWidth: 2, stroke: '#fff' }} />
              </AreaChart>
            </ResponsiveContainer>
          ) : (
            <div className="h-[240px] flex items-center justify-center text-gray-400 text-sm">No revenue data yet</div>
          )}
        </div>

        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Orders by Status</h3>
          {ordersByStatus.length > 0 ? (
            <ResponsiveContainer width="100%" height={240}>
              <PieChart>
                <Pie data={ordersByStatus} cx="50%" cy="45%" innerRadius={55} outerRadius={85} dataKey="value" paddingAngle={3}>
                  {ordersByStatus.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
                </Pie>
                <Tooltip contentStyle={{ borderRadius: '12px', border: '1px solid #E5E7EB', fontSize: '12px' }} />
                <Legend iconType="circle" iconSize={8} wrapperStyle={{ fontSize: '12px' }} />
              </PieChart>
            </ResponsiveContainer>
          ) : (
            <div className="h-[240px] flex items-center justify-center text-gray-400 text-sm">No orders yet</div>
          )}
        </div>
      </div>

      {/* Charts Row 2 */}
      <div className="grid grid-cols-2 gap-4">
        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Monthly Orders</h3>
          {revenueData.length > 0 ? (
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={revenueData}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
                <XAxis dataKey="date" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <Tooltip content={<CustomTooltip />} />
                <Bar dataKey="orders" fill="#1E293B" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          ) : (
            <div className="h-[200px] flex items-center justify-center text-gray-400 text-sm">No data yet</div>
          )}
        </div>

        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Platform Fee Income</h3>
          {feeIncome.length > 0 ? (
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={feeIncome}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
                <XAxis dataKey="date" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={(v) => `R${(v / 1000).toFixed(0)}k`} />
                <Tooltip content={<CustomTooltip />} />
                <Bar dataKey="fees" fill="#7C3AED" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          ) : (
            <div className="h-[200px] flex items-center justify-center text-gray-400 text-sm">No data yet</div>
          )}
        </div>
      </div>

      {/* Supplier Revenue Breakdown */}
      {suppliers.length > 0 && (
        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-4">Supplier Breakdown</h3>
          <div className="space-y-4">
            {suppliers.slice(0, 10).map((s: any, i: number) => (
              <div key={s.id}>
                <div className="flex items-center justify-between mb-1.5">
                  <div className="flex items-center gap-2">
                    <span className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-black text-white ${i === 0 ? 'bg-amber-500' : i === 1 ? 'bg-gray-400' : 'bg-orange-400'}`}>{i + 1}</span>
                    <span className="text-sm font-semibold text-gray-900">{s.companyName}</span>
                    <span className="text-xs text-gray-400">({s.tier ?? 'basic'})</span>
                  </div>
                  <span className="font-bold text-gray-700 tabular-nums">{s.isVerified ? '✓ Verified' : 'Pending'}</span>
                </div>
                <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-slate-700 to-slate-500 transition-all duration-700"
                    style={{ width: `${Math.max(10, (Number(s.totalRevenue ?? s.totalOrders ?? 1) / topSupplierRevenue) * 100)}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
