import { useState, useEffect, useMemo } from 'react';
import { DollarSign, ShoppingCart, Users, TrendingUp, Download, Loader2, AlertTriangle, RefreshCw, BarChart2 } from 'lucide-react';
import {
  AreaChart, Area, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, PieChart, Pie, Cell, Legend,
} from 'recharts';
import { format, startOfDay, startOfMonth, subDays, subMonths } from 'date-fns';
import { adminOrdersApi, adminSuppliersApi } from '../../services/api';
import { formatCurrency } from '../../utils';
import toast from 'react-hot-toast';

const COLORS = ['#1B4332', '#2E7D52', '#4CAF50', '#81C784', '#A5D6A7', '#94A3B8'];

type Period = 'week' | 'month' | 'year';

const PERIOD_INFO: Record<Period, string> = {
  week: 'Last 7 days',
  month: 'Last 6 months',
  year: 'By year',
};

interface Bucket { label: string; revenue: number; fees: number; orders: number }

const shortRand = (v: number) => (v >= 1000 ? `R${(v / 1000).toFixed(v >= 10000 ? 0 : 1)}k` : `R${Math.round(v)}`);

const CustomTooltip = ({ active, payload, label }: any) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-gray-100 rounded-xl shadow-card-lg p-3 text-sm">
      <p className="font-bold text-gray-700 mb-1">{label}</p>
      {payload.map((p: any) => (
        <p key={p.name} style={{ color: p.color }} className="font-semibold">
          {p.name === 'revenue' ? `GMV ${formatCurrency(Number(p.value))}`
            : p.name === 'fees' ? `Fees ${formatCurrency(Number(p.value))}`
            : `${p.value} orders`}
        </p>
      ))}
    </div>
  );
};

function ChartEmpty({ height, message }: { height: number; message: string }) {
  return (
    <div style={{ height }} className="flex flex-col items-center justify-center text-center rounded-xl border border-dashed border-gray-200 bg-gray-50/50 px-6">
      <BarChart2 size={28} className="text-gray-300 mb-2" />
      <p className="text-xs text-gray-400">{message}</p>
    </div>
  );
}

/** Group non-cancelled orders into time buckets for the selected period */
function buildBuckets(orders: any[], period: Period): Bucket[] {
  const now = new Date();
  let keys: { key: string; label: string }[];
  let keyOf: (d: Date) => string;

  if (period === 'week') {
    keys = Array.from({ length: 7 }, (_, i) => {
      const d = startOfDay(subDays(now, 6 - i));
      return { key: format(d, 'yyyy-MM-dd'), label: format(d, 'EEE d') };
    });
    keyOf = (d) => format(d, 'yyyy-MM-dd');
  } else if (period === 'month') {
    keys = Array.from({ length: 6 }, (_, i) => {
      const d = startOfMonth(subMonths(now, 5 - i));
      return { key: format(d, 'yyyy-MM'), label: format(d, 'MMM yyyy') };
    });
    keyOf = (d) => format(d, 'yyyy-MM');
  } else {
    const years = Array.from(new Set(orders.map((o) => new Date(o.createdAt).getFullYear()))).sort();
    if (years.length === 0) years.push(now.getFullYear());
    keys = years.map((y) => ({ key: String(y), label: String(y) }));
    keyOf = (d) => String(d.getFullYear());
  }

  const map = new Map<string, Bucket>(keys.map(({ key, label }) => [key, { label, revenue: 0, fees: 0, orders: 0 }]));
  for (const o of orders) {
    if (o.status === 'cancelled' || !o.createdAt) continue;
    const bucket = map.get(keyOf(new Date(o.createdAt)));
    if (!bucket) continue;
    bucket.revenue += Number(o.totalAmount ?? 0);
    bucket.fees += Number(o.platformCommission ?? 0);
    bucket.orders += 1;
  }
  return keys.map(({ key }) => map.get(key)!);
}

export default function AdminAnalyticsPage() {
  const [period, setPeriod] = useState<Period>('month');
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [stats, setStats] = useState<any>({});
  const [orders, setOrders] = useState<any[]>([]);
  const [supplierTotal, setSupplierTotal] = useState(0);
  const [ordersTruncated, setOrdersTruncated] = useState(false);

  const loadData = async () => {
    setLoading(true);
    setLoadError(false);
    const [ordersRes, suppliersRes] = await Promise.allSettled([
      adminOrdersApi.list({ page: 1, pageSize: 1000 }),
      adminSuppliersApi.list({ page: 1, pageSize: 1 }),
    ]);

    if (ordersRes.status === 'fulfilled') {
      const allOrders = ordersRes.value.data ?? [];
      setOrders(allOrders);
      setStats(ordersRes.value.summary ?? {});
      setOrdersTruncated((ordersRes.value.total ?? 0) > allOrders.length);
    } else {
      setLoadError(true);
    }

    if (suppliersRes.status === 'fulfilled') {
      const data = suppliersRes.value?.data ?? suppliersRes.value;
      setSupplierTotal(Number(data?.total ?? data?.items?.length ?? 0));
    }
    setLoading(false);
  };

  useEffect(() => { loadData(); }, []);

  const buckets = useMemo(() => buildBuckets(orders, period), [orders, period]);
  const hasTrend = buckets.some((b) => b.orders > 0);

  const totalGmv = Number(stats.deliveredRevenue ?? orders.filter((o) => o.status === 'delivered').reduce((s, o) => s + Number(o.totalAmount ?? 0), 0));
  const totalOrders = Number(stats.totalOrders ?? orders.length);
  const totalPlatformFees = Number(stats.totalPlatformFees ?? orders.reduce((s, o) => s + Number(o.platformCommission ?? 0), 0));

  const STATUS_ORDER = ['delivered', 'dispatched', 'processing', 'confirmed', 'pending', 'cancelled'];
  const ordersByStatus = STATUS_ORDER
    .map((status) => ({ name: status.charAt(0).toUpperCase() + status.slice(1), value: orders.filter((o) => o.status === status).length }))
    .filter((s) => s.value > 0);

  // Real per-supplier performance from orders (excluding cancelled)
  const supplierBreakdown = useMemo(() => {
    const map = new Map<string, { name: string; gmv: number; orders: number; fees: number }>();
    for (const o of orders) {
      if (o.status === 'cancelled') continue;
      const id = o.supplierId ?? o.supplierName ?? 'unknown';
      const row = map.get(id) ?? { name: o.supplierName ?? 'Unknown supplier', gmv: 0, orders: 0, fees: 0 };
      row.gmv += Number(o.totalAmount ?? 0);
      row.fees += Number(o.platformCommission ?? 0);
      row.orders += 1;
      map.set(id, row);
    }
    return Array.from(map.values()).sort((a, b) => b.gmv - a.gmv).slice(0, 10);
  }, [orders]);
  const activeSuppliers = new Set(orders.filter((o) => o.status !== 'cancelled').map((o) => o.supplierId ?? o.supplierName)).size;

  const handleExport = () => {
    if (orders.length === 0) { toast.error('There is no order data to export yet.'); return; }
    const rows: (string | number)[][] = [
      ['Order', 'Date', 'Status', 'Supplier', 'Shop', 'Total', 'Platform fee', 'Payment'],
      ...orders.map((o) => [
        o.orderNumber, o.createdAt ? format(new Date(o.createdAt), 'yyyy-MM-dd HH:mm') : '', o.status,
        o.supplierName ?? '', o.shopName ?? '', Number(o.totalAmount ?? 0).toFixed(2),
        Number(o.platformCommission ?? 0).toFixed(2), o.paymentStatus ?? '',
      ]),
    ];
    const csv = rows.map((r) => r.map((v) => {
      const s = String(v ?? '');
      return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
    }).join(',')).join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `spazasure-platform-orders-${format(new Date(), 'yyyy-MM-dd')}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    toast.success(`Exported ${orders.length} orders`);
  };

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-[400px]">
        <Loader2 size={36} className="text-gray-300 animate-spin" />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 space-y-5 sm:space-y-6 animate-in">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Platform Analytics</h1>
          <p className="page-subtitle">Full platform performance overview</p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={loadData} className="btn-secondary flex items-center gap-2" aria-label="Refresh analytics">
            <RefreshCw size={14} />
          </button>
          <button onClick={handleExport} className="btn-secondary flex items-center gap-2">
            <Download size={15} /> Export CSV
          </button>
        </div>
      </div>

      {loadError && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
          <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-red-800">We couldn't load platform orders, so the figures below are empty.</p>
          <button onClick={loadData} className="btn-secondary text-sm">Try again</button>
        </div>
      )}
      {ordersTruncated && (
        <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-xl px-3 py-2">
          Charts are based on the most recent 1,000 orders. Headline totals include all orders.
        </p>
      )}

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          { label: 'Delivered GMV',     value: formatCurrency(totalGmv), icon: <DollarSign size={20} />, color: 'stat-card-green' },
          { label: 'Total Orders',      value: totalOrders.toLocaleString(), icon: <ShoppingCart size={20} />, color: 'card p-5' },
          { label: 'Suppliers Selling', value: `${activeSuppliers} / ${supplierTotal}`, icon: <Users size={20} />, color: 'card p-5' },
          { label: 'Platform Fees',     value: formatCurrency(totalPlatformFees), icon: <TrendingUp size={20} />, color: 'stat-card-amber' },
        ].map(({ label, value, icon, color }) => (
          <div key={label} className={`${color} animate-in`}>
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="text-xs font-bold uppercase tracking-wider opacity-70">{label}</p>
                <p className="text-2xl sm:text-3xl font-black mt-1.5 tabular-nums truncate">{value}</p>
              </div>
              <div className="p-2.5 bg-white/15 rounded-xl ring-1 ring-white/10 flex-shrink-0">{icon}</div>
            </div>
          </div>
        ))}
      </div>

      {/* Period Toggle */}
      <div className="flex items-center gap-1.5 bg-white border border-gray-200 rounded-xl p-1 w-fit" role="tablist" aria-label="Trend period">
        {(['week', 'month', 'year'] as const).map((p) => (
          <button key={p} role="tab" aria-selected={period === p} onClick={() => setPeriod(p)}
            className={`px-4 py-1.5 rounded-lg text-sm font-semibold capitalize transition-all ${period === p ? 'bg-slate-800 text-white shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}>
            {p}
          </button>
        ))}
      </div>

      {/* Charts Row 1 */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="card p-4 sm:p-5 lg:col-span-2">
          <div className="mb-5">
            <h3 className="font-bold text-gray-900">GMV Trend</h3>
            <p className="text-xs text-gray-400 mt-0.5">Order value excluding cancelled orders · {PERIOD_INFO[period]}</p>
          </div>
          {hasTrend ? (
            <ResponsiveContainer width="100%" height={240}>
              <AreaChart data={buckets}>
                <defs>
                  <linearGradient id="adminGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#1E293B" stopOpacity={0.12} />
                    <stop offset="95%" stopColor="#1E293B" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
                <XAxis dataKey="label" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={shortRand} />
                <Tooltip content={<CustomTooltip />} />
                <Area type="monotone" dataKey="revenue" stroke="#1E293B" fill="url(#adminGrad)" strokeWidth={2.5} dot={false} activeDot={{ r: 5, fill: '#1E293B', strokeWidth: 2, stroke: '#fff' }} />
              </AreaChart>
            </ResponsiveContainer>
          ) : (
            <ChartEmpty height={240} message="No orders in this period yet." />
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
            <ChartEmpty height={240} message="No orders yet." />
          )}
        </div>
      </div>

      {/* Charts Row 2 */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Orders</h3>
          {hasTrend ? (
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={buckets}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
                <XAxis dataKey="label" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <Tooltip content={<CustomTooltip />} />
                <Bar dataKey="orders" fill="#1E293B" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          ) : (
            <ChartEmpty height={200} message="No orders in this period yet." />
          )}
        </div>

        <div className="card p-5">
          <h3 className="font-bold text-gray-900 mb-5">Platform Fee Income</h3>
          {hasTrend ? (
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={buckets}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f3f4f6" />
                <XAxis dataKey="label" tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#9CA3AF' }} axisLine={false} tickLine={false} tickFormatter={shortRand} />
                <Tooltip content={<CustomTooltip />} />
                <Bar dataKey="fees" fill="#7C3AED" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          ) : (
            <ChartEmpty height={200} message="No fee income in this period yet." />
          )}
        </div>
      </div>

      {/* Supplier performance */}
      <div className="card p-5">
        <h3 className="font-bold text-gray-900">Top Suppliers by GMV</h3>
        <p className="text-xs text-gray-400 mt-0.5 mb-4">Based on non-cancelled orders</p>
        {supplierBreakdown.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-6">No supplier sales yet.</p>
        ) : (
          <div className="space-y-4">
            {supplierBreakdown.map((s, i) => (
              <div key={s.name + i}>
                <div className="flex items-center justify-between gap-3 mb-1.5">
                  <div className="flex items-center gap-2 min-w-0">
                    <span className={`w-5 h-5 flex-shrink-0 rounded-full flex items-center justify-center text-[10px] font-black text-white ${i === 0 ? 'bg-amber-500' : i === 1 ? 'bg-gray-400' : 'bg-orange-400'}`}>{i + 1}</span>
                    <span className="text-sm font-semibold text-gray-900 truncate">{s.name}</span>
                    <span className="text-xs text-gray-400 whitespace-nowrap">{s.orders} order{s.orders === 1 ? '' : 's'}</span>
                  </div>
                  <span className="font-bold text-gray-700 tabular-nums whitespace-nowrap">{formatCurrency(s.gmv)}</span>
                </div>
                <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-slate-700 to-slate-500 transition-all duration-700"
                    style={{ width: `${supplierBreakdown[0].gmv > 0 ? Math.max(4, (s.gmv / supplierBreakdown[0].gmv) * 100) : 0}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
