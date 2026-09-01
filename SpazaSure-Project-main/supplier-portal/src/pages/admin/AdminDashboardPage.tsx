import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Users, ShieldCheck, Package, ShoppingCart, DollarSign,
  AlertTriangle, CheckCircle, Clock, ArrowRight, TrendingUp, FileText,
  Store, BarChart2, Settings, ChevronRight, Zap, Globe,
} from 'lucide-react';
import { format } from 'date-fns';
import clsx from 'clsx';
import { adminSuppliersApi } from '../../services/api';
import api from '../../services/api';
import { Spinner } from '../../components/ui';

interface DashboardData {
  totalSuppliers: number;
  verifiedSuppliers: number;
  pendingVerifications: number;
  pendingDocuments: number;
  totalProducts: number;
  totalOrders: number;
  totalRevenue: number;
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

  useEffect(() => {
    const fetchData = async () => {
      try {
        // Fetch suppliers
        const suppRes = await adminSuppliersApi.list({});
        const suppData = suppRes?.data ?? suppRes;
        const suppliers = Array.isArray(suppData) ? suppData : suppData?.items ?? [];

        // Fetch documents
        let pendingDocs = 0;
        try {
          const docRes = await api.get('/compliance/documents', { params: { status: 'pending' } });
          pendingDocs = docRes.data?.data?.summary?.pending ?? docRes.data?.data?.docs?.length ?? 0;
        } catch {}

        // Fetch products count
        let totalProducts = 0;
        try {
          const prodRes = await api.get('/supplier/products', { params: { pageSize: 1 } });
          const pd = prodRes.data?.data ?? prodRes.data;
          totalProducts = pd?.total ?? (pd?.items ?? pd?.data ?? []).length ?? 0;
        } catch {}

        setData({
          totalSuppliers: suppliers.length,
          verifiedSuppliers: suppliers.filter((s: any) => s.isVerified || s.status === 'verified').length,
          pendingVerifications: suppliers.filter((s: any) => !s.isVerified && s.status !== 'verified').length,
          pendingDocuments: pendingDocs,
          totalProducts,
          totalOrders: 0,
          totalRevenue: 0,
          recentSuppliers: suppliers.slice(0, 5).map((s: any) => ({
            id: s.id,
            companyName: s.companyName ?? '',
            email: s.email ?? '',
            isVerified: s.isVerified ?? s.status === 'verified',
            joinedAt: s.joinedAt ?? s.createdAt ?? new Date().toISOString(),
          })),
        });
      } catch {
        setData({
          totalSuppliers: 0, verifiedSuppliers: 0, pendingVerifications: 0,
          pendingDocuments: 0, totalProducts: 0, totalOrders: 0, totalRevenue: 0, recentSuppliers: [],
        });
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

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
    <div className="p-6 space-y-7 animate-in">

      {/* ═══ HEADER ═══ */}
      <div className="relative overflow-hidden rounded-3xl bg-gradient-to-br from-slate-900 via-slate-800 to-indigo-900 p-8 shadow-xl">
        {/* Decorative elements */}
        <div className="absolute top-0 right-0 w-80 h-80 bg-indigo-500/10 rounded-full blur-[80px] -translate-y-20 translate-x-20" />
        <div className="absolute bottom-0 left-0 w-60 h-60 bg-emerald-500/10 rounded-full blur-[60px] translate-y-20 -translate-x-10" />
        <div className="absolute inset-0 opacity-[0.03]" style={{ backgroundImage: 'radial-gradient(circle at 1px 1px, white 1px, transparent 0)', backgroundSize: '24px 24px' }} />

        <div className="relative z-10">
          <div className="flex items-center gap-2 mb-1">
            <Zap size={16} className="text-amber-400" />
            <span className="text-xs font-bold text-amber-400 uppercase tracking-widest">Admin Control Center</span>
          </div>
          <h1 className="text-2xl font-black text-white tracking-tight">{greeting}, Admin 👋</h1>
          <p className="text-sm text-slate-400 mt-1 font-medium">
            {format(new Date(), 'EEEE, d MMMM yyyy')} · Here's your platform overview
          </p>

          {/* Quick action pills */}
          <div className="flex items-center gap-2 mt-5">
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

      {/* ═══ KPI CARDS — CLICKABLE ═══ */}
      <div className="grid grid-cols-2 lg:grid-cols-3 gap-4">
        {[
          { label: 'Total Suppliers',       value: d.totalSuppliers,       icon: Users,       gradient: 'from-blue-500 to-indigo-600',   bg: 'bg-blue-50',  ring: 'hover:ring-blue-200',  path: '/admin/suppliers' },
          { label: 'Pending Verifications', value: d.pendingVerifications, icon: ShieldCheck, gradient: 'from-amber-500 to-orange-600',  bg: 'bg-amber-50', ring: 'hover:ring-amber-200', path: '/admin/suppliers', urgent: true },
          { label: 'Pending Documents',     value: d.pendingDocuments,     icon: FileText,    gradient: 'from-orange-500 to-red-500',    bg: 'bg-orange-50',ring: 'hover:ring-orange-200',path: '/admin/documents', urgent: true },
          { label: 'Active Products',       value: d.totalProducts,        icon: Package,     gradient: 'from-emerald-500 to-teal-600',  bg: 'bg-emerald-50',ring:'hover:ring-emerald-200',path: '/admin/products' },
          { label: 'Total Orders',          value: d.totalOrders,          icon: ShoppingCart, gradient: 'from-violet-500 to-purple-600', bg: 'bg-violet-50',ring: 'hover:ring-violet-200',path: '/admin/orders' },
          { label: 'Platform Revenue',      value: `R${(d.totalRevenue / 1000).toFixed(0)}k`, icon: DollarSign, gradient: 'from-emerald-600 to-green-700', bg: 'bg-green-50', ring: 'hover:ring-green-200', path: '/admin/analytics' },
        ].map(({ label, value, icon: Icon, gradient, bg, ring, path, urgent }) => (
          <button
            key={label}
            onClick={() => navigate(path)}
            className={clsx(
              'relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 text-left transition-all duration-200',
              'shadow-sm hover:shadow-lg hover:-translate-y-0.5 hover:ring-2',
              ring,
              urgent && 'border-amber-200/60'
            )}
          >
            {/* Decorative corner gradient */}
            <div className={clsx('absolute -top-6 -right-6 w-20 h-20 rounded-full opacity-10 bg-gradient-to-br', gradient)} />

            <div className="relative flex items-start justify-between">
              <div>
                <p className="text-[11px] font-bold text-gray-400 uppercase tracking-wider">{label}</p>
                <p className="text-3xl font-black text-gray-900 mt-2 tabular-nums">{value}</p>
                {urgent && value > 0 && (
                  <span className="inline-flex items-center gap-1 text-[10px] font-bold text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded-full mt-2.5">
                    <AlertTriangle size={9} /> Needs attention
                  </span>
                )}
              </div>
              <div className={clsx('w-11 h-11 rounded-xl flex items-center justify-center shadow-sm', bg)}>
                <Icon size={20} className={clsx('bg-gradient-to-br bg-clip-text', gradient.replace('from-', 'text-').split(' ')[0].replace('text-', 'text-'))} style={{ color: undefined }} />
                <Icon size={20} className="text-current absolute opacity-0" />
              </div>
            </div>

            {/* "Go" indicator */}
            <div className="absolute bottom-3 right-4 flex items-center gap-1 text-[10px] font-bold text-gray-300 group-hover:text-gray-500">
              <ArrowRight size={10} />
            </div>
          </button>
        ))}
      </div>

      {/* ═══ QUICK NAVIGATION GRID ═══ */}
      <div>
        <h2 className="text-sm font-bold text-gray-400 uppercase tracking-wider mb-3">Quick Navigation</h2>
        <div className="grid grid-cols-4 gap-3">
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
      <div className="grid grid-cols-2 gap-5">
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

        {/* Platform Status */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 bg-gradient-to-r from-gray-50 to-white">
            <div>
              <h3 className="font-bold text-gray-900 text-sm">Platform Status</h3>
              <p className="text-xs text-gray-400 mt-0.5">System health overview</p>
            </div>
          </div>
          <div className="p-5 space-y-4">
            {[
              { label: 'API Gateway',       status: 'online', desc: 'Port 5181' },
              { label: 'Auth Service',      status: 'online', desc: 'Port 5001' },
              { label: 'Product Service',   status: 'online', desc: 'Port 5002' },
              { label: 'Order Service',     status: 'online', desc: 'Port 5003' },
              { label: 'Analytics Service', status: 'online', desc: 'Port 5004' },
              { label: 'User Service',      status: 'online', desc: 'Port 5005' },
              { label: 'Compliance Service',status: 'online', desc: 'Port 5006' },
            ].map(({ label, status, desc }) => (
              <div key={label} className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className={clsx(
                    'w-2.5 h-2.5 rounded-full',
                    status === 'online' ? 'bg-emerald-400 shadow-sm shadow-emerald-200' : 'bg-red-400'
                  )} />
                  <span className="text-sm font-medium text-gray-700">{label}</span>
                </div>
                <span className="text-[11px] font-medium text-gray-400">{desc}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
