import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Search, CheckCircle, XCircle, ToggleLeft, ToggleRight, Package, Clock,
  RefreshCw, Loader2, ChevronDown, ChevronRight, Users, Layers,
  Shield, AlertTriangle, Zap, Eye, X, Barcode, Download, Printer,
  DollarSign, Box, Tag, Calendar,
} from 'lucide-react';
import toast from 'react-hot-toast';
import clsx from 'clsx';
import { format } from 'date-fns';
import { adminProductsApi, resolveUploadUrl } from '../../services/api';
import type { Product } from '../../types';

type StatusFilter = 'all' | 'pending_approval' | 'active' | 'archived';
type ViewMode = 'flat' | 'grouped';

const statusStyle: Record<string, string> = {
  active:           'bg-emerald-50 text-emerald-700 border-emerald-200',
  pending_approval: 'bg-amber-50 text-amber-700 border-amber-200',
  archived:         'bg-gray-100 text-gray-500 border-gray-200',
  draft:            'bg-blue-50 text-blue-700 border-blue-200',
};

const statusFilters: { value: StatusFilter; label: string }[] = [
  { value: 'all',              label: 'All' },
  { value: 'pending_approval', label: 'Pending' },
  { value: 'active',           label: 'Active' },
  { value: 'archived',         label: 'Archived' },
];

interface SupplierGroup {
  supplierName: string;
  supplierId: string;
  products: Product[];
  totalRevenue: number;
  activeCount: number;
  pendingCount: number;
}

export default function AdminProductsPage() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [viewMode, setViewMode] = useState<ViewMode>('grouped');
  const [expandedSuppliers, setExpandedSuppliers] = useState<Set<string>>(new Set());
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const pageSize = 50;

  const fetchProducts = useCallback(async () => {
    setLoading(true);
    try {
      const params: { page: number; pageSize: number; search?: string; status?: string } = {
        page,
        pageSize,
      };
      if (search.trim()) params.search = search.trim();
      if (statusFilter !== 'all') params.status = statusFilter;

      const result = await adminProductsApi.list(params);
      setProducts(result.data);
      setTotal(result.total);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load products');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, search, statusFilter]);

  useEffect(() => {
    fetchProducts();
  }, [fetchProducts]);

  useEffect(() => {
    setPage(1);
  }, [search, statusFilter]);

  // Group products by supplier
  const supplierGroups = useMemo((): SupplierGroup[] => {
    const groupMap = new Map<string, SupplierGroup>();

    products.forEach((p) => {
      const key = p.supplierId || p.supplierName || 'Unknown';
      if (!groupMap.has(key)) {
        groupMap.set(key, {
          supplierId: p.supplierId || key,
          supplierName: p.supplierName || 'Unknown Supplier',
          products: [],
          totalRevenue: 0,
          activeCount: 0,
          pendingCount: 0,
        });
      }
      const group = groupMap.get(key)!;
      group.products.push(p);
      group.totalRevenue += p.price * p.stockQuantity;
      if (p.status === 'active') group.activeCount++;
      if (p.status === 'pending_approval') group.pendingCount++;
    });

    return Array.from(groupMap.values()).sort((a, b) => b.products.length - a.products.length);
  }, [products]);

  // Auto-expand all groups on load
  useEffect(() => {
    if (supplierGroups.length > 0 && expandedSuppliers.size === 0) {
      setExpandedSuppliers(new Set(supplierGroups.map((g) => g.supplierId)));
    }
  }, [supplierGroups]);

  const toggleExpanded = (supplierId: string) => {
    setExpandedSuppliers((prev) => {
      // Accordion: if clicking the open one, close it. Otherwise open only the clicked one.
      if (prev.has(supplierId)) {
        return new Set();
      }
      return new Set([supplierId]);
    });
  };

  const expandAll = () => setExpandedSuppliers(new Set(supplierGroups.map((g) => g.supplierId)));
  const collapseAll = () => setExpandedSuppliers(new Set());

  const approve = async (id: string) => {
    setActionLoading(id);
    try {
      await adminProductsApi.approve(id);
      toast.success('Product approved and now live');
      fetchProducts();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to approve product');
    } finally {
      setActionLoading(null);
    }
  };

  const reject = async (id: string) => {
    setActionLoading(id);
    try {
      await adminProductsApi.reject(id);
      toast.error('Product rejected and archived');
      fetchProducts();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to reject product');
    } finally {
      setActionLoading(null);
    }
  };

  const toggleAvailability = async (id: string) => {
    setActionLoading(id);
    try {
      await adminProductsApi.toggleAvailability(id);
      toast.success('Availability updated');
      fetchProducts();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to update availability');
    } finally {
      setActionLoading(null);
    }
  };

  const pendingCount = products.filter((p) => p.status === 'pending_approval').length;
  const activeCount  = products.filter((p) => p.status === 'active').length;

  const renderProductRow = (p: Product) => (
    <tr key={p.id} className="group border-b border-gray-50 last:border-0 hover:bg-gray-50/60 transition-colors">
      <td className="px-5 py-4">
        <div className="flex items-center gap-3">
          <img src={p.imageUrl || '/placeholder-product.png'} alt={p.name} className="w-10 h-10 rounded-xl object-cover bg-gray-100 flex-shrink-0 ring-1 ring-gray-100 shadow-sm" />
          <div>
            <p className="font-bold text-gray-900 text-sm">{p.name}</p>
            <p className="text-[11px] text-gray-400 font-mono mt-0.5">{p.sku}</p>
          </div>
        </div>
      </td>
      {viewMode === 'flat' && (
        <td className="px-5 py-4 text-gray-600 text-xs font-bold">{p.supplierName}</td>
      )}
      <td className="px-5 py-4">
        <span className="text-[11px] bg-gray-100/80 text-gray-600 px-2.5 py-1 rounded-lg font-semibold">{p.categoryName}</span>
      </td>
      <td className="px-5 py-4 text-right font-black text-sm tabular-nums text-gray-800">R{p.price.toFixed(2)}</td>
      <td className="px-5 py-4 text-right">
        <span className={clsx('font-bold text-sm tabular-nums', p.stockQuantity === 0 ? 'text-red-500' : p.stockQuantity < 50 ? 'text-amber-600' : 'text-gray-700')}>
          {p.stockQuantity}
        </span>
      </td>
      <td className="px-5 py-4 text-center">
        <span className={clsx('text-[11px] font-bold px-2.5 py-1 rounded-full border capitalize inline-flex items-center gap-1', statusStyle[p.status] ?? 'bg-gray-100 text-gray-500 border-gray-200')}>
          {p.status === 'pending_approval' && <Clock size={10} />}
          {p.status === 'active' && <CheckCircle size={10} />}
          {p.status.replace('_', ' ')}
        </span>
      </td>
      <td className="px-5 py-4 text-center">
        <button
          onClick={() => toggleAvailability(p.id)}
          disabled={actionLoading === p.id}
          className={clsx('transition-all disabled:opacity-50 hover:scale-110', p.isAvailable ? 'text-emerald-600' : 'text-gray-300')}
        >
          {p.isAvailable ? <ToggleRight size={22} /> : <ToggleLeft size={22} />}
        </button>
      </td>
      <td className="px-5 py-4 text-xs text-gray-400 font-medium">{format(new Date(p.createdAt), 'dd MMM yyyy')}</td>
      <td className="px-5 py-4">
        <div className="flex items-center justify-center gap-1">
          {actionLoading === p.id ? (
            <Loader2 size={14} className="animate-spin text-gray-400" />
          ) : (
            <>
              <button onClick={() => setSelectedProduct(p)} title="View Details" className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all">
                <Eye size={15} />
              </button>
              {p.status === 'pending_approval' && (
                <>
                  <button onClick={() => approve(p.id)} title="Approve" className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-emerald-600 hover:bg-emerald-50 transition-all">
                    <CheckCircle size={15} />
                  </button>
                  <button onClick={() => reject(p.id)} title="Reject" className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-red-500 hover:bg-red-50 transition-all">
                    <XCircle size={15} />
                  </button>
                </>
              )}
              {p.status === 'active' && (
                <button onClick={() => reject(p.id)} title="Archive" className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-red-500 hover:bg-red-50 transition-all">
                  <XCircle size={15} />
                </button>
              )}
              {p.status === 'archived' && (
                <button onClick={() => approve(p.id)} title="Restore" className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-emerald-600 hover:bg-emerald-50 transition-all">
                  <CheckCircle size={15} />
                </button>
              )}
            </>
          )}
        </div>
      </td>
    </tr>
  );

  return (
    <div className="p-6 space-y-6 animate-in">

      {/* ═══ GRADIENT HEADER ═══ */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          <div className="w-11 h-11 bg-gradient-to-br from-blue-600 to-indigo-700 rounded-xl flex items-center justify-center shadow-lg shadow-blue-200">
            <Package size={20} className="text-white" />
          </div>
          <div>
            <h1 className="text-xl font-black text-gray-900 tracking-tight">Product Management</h1>
            <p className="text-sm text-gray-400 font-medium">Review and manage products across all suppliers</p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {pendingCount > 0 && (
            <div className="flex items-center gap-2.5 bg-gradient-to-r from-amber-50 to-orange-50 border border-amber-200/80 text-amber-800 text-sm font-bold px-4 py-2.5 rounded-2xl shadow-sm">
              <div className="w-6 h-6 bg-amber-100 rounded-full flex items-center justify-center">
                <AlertTriangle size={12} className="text-amber-600" />
              </div>
              {pendingCount} pending approval
            </div>
          )}
          <button
            onClick={fetchProducts}
            disabled={loading}
            className="flex items-center gap-2 px-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-bold text-gray-600 hover:bg-gray-50 hover:border-gray-300 transition-all disabled:opacity-50 shadow-sm"
          >
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Refresh
          </button>
        </div>
      </div>

      {/* ═══ PREMIUM STATS CARDS ═══ */}
      <div className="grid grid-cols-4 gap-4">
        <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-blue-50 to-transparent rounded-bl-[40px]" />
          <div className="relative">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
                <Package size={14} className="text-blue-600" />
              </div>
            </div>
            <p className="text-3xl font-black text-gray-900 tabular-nums">{total}</p>
            <p className="text-xs font-semibold text-gray-400 mt-1">Total Products</p>
          </div>
        </div>
        <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-emerald-50 to-transparent rounded-bl-[40px]" />
          <div className="relative">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 bg-emerald-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
                <CheckCircle size={14} className="text-emerald-600" />
              </div>
            </div>
            <p className="text-3xl font-black text-emerald-600 tabular-nums">{activeCount}</p>
            <p className="text-xs font-semibold text-gray-400 mt-1">Active</p>
          </div>
        </div>
        <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-amber-50 to-transparent rounded-bl-[40px]" />
          <div className="relative">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 bg-amber-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
                <Clock size={14} className="text-amber-600" />
              </div>
            </div>
            <p className="text-3xl font-black text-amber-600 tabular-nums">{pendingCount}</p>
            <p className="text-xs font-semibold text-gray-400 mt-1">Pending Approval</p>
          </div>
        </div>
        <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-purple-50 to-transparent rounded-bl-[40px]" />
          <div className="relative">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 bg-purple-100 rounded-lg flex items-center justify-center group-hover:scale-110 transition-transform">
                <Users size={14} className="text-purple-600" />
              </div>
            </div>
            <p className="text-3xl font-black text-purple-600 tabular-nums">{supplierGroups.length}</p>
            <p className="text-xs font-semibold text-gray-400 mt-1">Suppliers</p>
          </div>
        </div>
      </div>

      {/* ═══ FILTERS & VIEW MODE ═══ */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        {/* Search */}
        <div className="relative flex-1 min-w-[220px] max-w-sm">
          <Search size={15} className="absolute left-4 top-3.5 text-gray-300" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full border border-gray-200 rounded-2xl pl-11 pr-4 py-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-100 focus:border-blue-300 transition-all placeholder:text-gray-300 font-medium"
            placeholder="Search product, supplier, SKU..."
          />
        </div>

        {/* Status Filter Pills */}
        <div className="flex items-center gap-1.5">
          {statusFilters.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => setStatusFilter(value)}
              className={clsx(
                'flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold transition-all border',
                statusFilter === value
                  ? 'bg-gray-900 text-white border-gray-900 shadow-sm'
                  : 'bg-white text-gray-500 border-gray-200 hover:border-gray-300 hover:text-gray-700'
              )}
            >
              <span>{label}</span>
            </button>
          ))}
        </div>

        {/* View Mode Toggle */}
        <div className="flex items-center gap-1 bg-gray-100/80 rounded-2xl p-1.5">
          <button
            onClick={() => setViewMode('grouped')}
            className={clsx(
              'flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold transition-all',
              viewMode === 'grouped'
                ? 'bg-white text-gray-900 shadow-sm ring-1 ring-gray-200/50'
                : 'text-gray-500 hover:text-gray-700'
            )}
          >
            <Layers size={13} /> By Supplier
          </button>
          <button
            onClick={() => setViewMode('flat')}
            className={clsx(
              'flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold transition-all',
              viewMode === 'flat'
                ? 'bg-white text-gray-900 shadow-sm ring-1 ring-gray-200/50'
                : 'text-gray-500 hover:text-gray-700'
            )}
          >
            <Package size={13} /> All Products
          </button>
        </div>
      </div>

      {/* ═══ GROUPED VIEW ═══ */}
      {viewMode === 'grouped' && (
        <div className="space-y-4">
          {/* Expand/Collapse controls */}
          {supplierGroups.length > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm font-bold text-gray-500">
                {supplierGroups.length} supplier{supplierGroups.length !== 1 ? 's' : ''} found
              </p>
              <button
                onClick={() => expandedSuppliers.size === supplierGroups.length ? collapseAll() : expandAll()}
                className="text-xs font-bold text-blue-600 hover:text-blue-800 transition-colors px-3 py-1.5 rounded-lg hover:bg-blue-50"
              >
                {expandedSuppliers.size === supplierGroups.length ? 'Collapse All' : 'Expand All'}
              </button>
            </div>
          )}

          {loading ? (
            <div className="rounded-2xl border-2 border-dashed border-gray-200 flex items-center justify-center py-16">
              <Loader2 size={24} className="animate-spin text-gray-300" />
              <span className="ml-3 text-sm text-gray-400 font-medium">Loading products...</span>
            </div>
          ) : supplierGroups.length === 0 ? (
            <div className="rounded-2xl border-2 border-dashed border-gray-200 p-16 text-center">
              <div className="w-16 h-16 bg-gray-100 rounded-2xl flex items-center justify-center mx-auto mb-4">
                <Package size={28} className="text-gray-300" />
              </div>
              <p className="font-bold text-gray-600 text-lg">No products found</p>
              <p className="text-sm text-gray-400 mt-1">Try adjusting your search or filters</p>
            </div>
          ) : (
            supplierGroups.map((group) => {
              const isExpanded = expandedSuppliers.has(group.supplierId);
              const completionPct = group.products.length > 0
                ? Math.round((group.activeCount / group.products.length) * 100)
                : 0;

              return (
                <div key={group.supplierId} className="bg-white rounded-2xl border border-gray-100 shadow-sm hover:shadow-md transition-all overflow-hidden">
                  {/* Supplier Header */}
                  <button
                    onClick={() => toggleExpanded(group.supplierId)}
                    className="w-full flex items-center justify-between px-6 py-5 hover:bg-gray-50/50 transition-colors text-left"
                  >
                    <div className="flex items-center gap-4">
                      <div className="w-11 h-11 rounded-2xl bg-gradient-to-br from-slate-100 to-slate-50 flex items-center justify-center font-black text-base text-slate-700 ring-1 ring-slate-200/50 shadow-sm flex-shrink-0">
                        {group.supplierName.charAt(0)}
                      </div>
                      <div>
                        <p className="font-bold text-gray-900 text-sm">{group.supplierName}</p>
                        <p className="text-xs text-gray-400 font-medium mt-0.5">{group.products.length} product{group.products.length !== 1 ? 's' : ''}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-3">
                      {/* Progress ring */}
                      <div className="relative w-10 h-10">
                        <svg className="w-10 h-10 -rotate-90" viewBox="0 0 36 36">
                          <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                            fill="none" stroke="#f1f5f9" strokeWidth="3" />
                          <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                            fill="none" stroke={completionPct === 100 ? '#10b981' : completionPct >= 50 ? '#f59e0b' : '#94a3b8'}
                            strokeWidth="3" strokeDasharray={`${completionPct}, 100`} strokeLinecap="round" />
                        </svg>
                        <span className="absolute inset-0 flex items-center justify-center text-[10px] font-black text-gray-600">
                          {group.activeCount}/{group.products.length}
                        </span>
                      </div>

                      {/* Status pills */}
                      <div className="flex items-center gap-1.5">
                        {group.activeCount > 0 && (
                          <span className="flex items-center gap-1 text-[11px] font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-1 rounded-lg">
                            <CheckCircle size={10} /> {group.activeCount}
                          </span>
                        )}
                        {group.pendingCount > 0 && (
                          <span className="flex items-center gap-1 text-[11px] font-bold text-amber-700 bg-amber-50 border border-amber-200 px-2 py-1 rounded-lg">
                            <Clock size={10} /> {group.pendingCount}
                          </span>
                        )}
                      </div>

                      <ChevronRight size={16} className={clsx('text-gray-300 transition-transform duration-200', isExpanded && 'rotate-90')} />
                    </div>
                  </button>

                  {/* Products Table */}
                  {isExpanded && (
                    <div className="border-t border-gray-100" style={{ animation: 'expandIn 0.25s ease-out' }}>
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="border-b border-gray-100 bg-gray-50/60">
                            <th className="px-5 py-3 text-left text-[11px] font-bold text-gray-400 uppercase tracking-wider">Product</th>
                            <th className="px-5 py-3 text-left text-[11px] font-bold text-gray-400 uppercase tracking-wider">Category</th>
                            <th className="px-5 py-3 text-right text-[11px] font-bold text-gray-400 uppercase tracking-wider">Price</th>
                            <th className="px-5 py-3 text-right text-[11px] font-bold text-gray-400 uppercase tracking-wider">Stock</th>
                            <th className="px-5 py-3 text-center text-[11px] font-bold text-gray-400 uppercase tracking-wider">Status</th>
                            <th className="px-5 py-3 text-center text-[11px] font-bold text-gray-400 uppercase tracking-wider">Available</th>
                            <th className="px-5 py-3 text-left text-[11px] font-bold text-gray-400 uppercase tracking-wider">Added</th>
                            <th className="px-5 py-3 text-center text-[11px] font-bold text-gray-400 uppercase tracking-wider">Actions</th>
                          </tr>
                        </thead>
                        <tbody>
                          {group.products.map(renderProductRow)}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              );
            })
          )}
        </div>
      )}

      {/* ═══ FLAT VIEW ═══ */}
      {viewMode === 'flat' && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          {loading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 size={24} className="animate-spin text-gray-300" />
              <span className="ml-3 text-sm text-gray-400 font-medium">Loading products...</span>
            </div>
          ) : products.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-gray-400">
              <div className="w-16 h-16 bg-gray-100 rounded-2xl flex items-center justify-center mb-4">
                <Package size={28} className="text-gray-300" />
              </div>
              <p className="font-bold text-gray-600 text-lg">No products found</p>
              <p className="text-sm text-gray-400 mt-1">Try adjusting your search or filters</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50/60">
                  <th className="px-5 py-3 text-left text-[11px] font-bold text-gray-400 uppercase tracking-wider">Product</th>
                  <th className="px-5 py-3 text-left text-[11px] font-bold text-gray-400 uppercase tracking-wider">Supplier</th>
                  <th className="px-5 py-3 text-left text-[11px] font-bold text-gray-400 uppercase tracking-wider">Category</th>
                  <th className="px-5 py-3 text-right text-[11px] font-bold text-gray-400 uppercase tracking-wider">Price</th>
                  <th className="px-5 py-3 text-right text-[11px] font-bold text-gray-400 uppercase tracking-wider">Stock</th>
                  <th className="px-5 py-3 text-center text-[11px] font-bold text-gray-400 uppercase tracking-wider">Status</th>
                  <th className="px-5 py-3 text-center text-[11px] font-bold text-gray-400 uppercase tracking-wider">Available</th>
                  <th className="px-5 py-3 text-left text-[11px] font-bold text-gray-400 uppercase tracking-wider">Added</th>
                  <th className="px-5 py-3 text-center text-[11px] font-bold text-gray-400 uppercase tracking-wider">Actions</th>
                </tr>
              </thead>
              <tbody>
                {products.map(renderProductRow)}
              </tbody>
            </table>
          )}

          {/* Pagination */}
          {!loading && total > pageSize && (
            <div className="flex items-center justify-between px-6 py-4 border-t border-gray-100 bg-gray-50/30">
              <p className="text-xs text-gray-500 font-medium">
                Showing <span className="font-bold text-gray-700">{(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)}</span> of <span className="font-bold text-gray-700">{total}</span> products
              </p>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-4 py-2 text-xs font-bold rounded-xl border border-gray-200 disabled:opacity-40 hover:bg-white hover:border-gray-300 hover:shadow-sm transition-all bg-white"
                >
                  Previous
                </button>
                <span className="text-xs font-bold text-gray-500 px-2">Page {page}</span>
                <button
                  onClick={() => setPage((p) => p + 1)}
                  disabled={page * pageSize >= total}
                  className="px-4 py-2 text-xs font-bold rounded-xl border border-gray-200 disabled:opacity-40 hover:bg-white hover:border-gray-300 hover:shadow-sm transition-all bg-white"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ═══ PRODUCT DETAIL MODAL ═══ */}
      {selectedProduct && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setSelectedProduct(null)} />
          <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-2xl max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
            {/* Header */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 bg-gradient-to-r from-blue-50 to-white flex-shrink-0">
              <div className="flex items-center gap-4">
                <img
                  src={selectedProduct.imageUrl || '/placeholder-product.png'}
                  alt={selectedProduct.name}
                  className="w-14 h-14 rounded-2xl object-cover bg-gray-100 ring-1 ring-gray-200 shadow-sm"
                />
                <div>
                  <p className="font-black text-gray-900 text-base">{selectedProduct.name}</p>
                  <p className="text-xs text-gray-400 font-mono mt-0.5">{selectedProduct.sku}</p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <span className={clsx('text-[11px] font-bold px-2.5 py-1 rounded-full border capitalize inline-flex items-center gap-1', statusStyle[selectedProduct.status] ?? 'bg-gray-100 text-gray-500 border-gray-200')}>
                  {selectedProduct.status.replace('_', ' ')}
                </span>
                <button onClick={() => setSelectedProduct(null)} className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-all">
                  <X size={15} />
                </button>
              </div>
            </div>

            {/* Body */}
            <div className="flex-1 overflow-y-auto p-6 space-y-6">
              {/* Product Details Grid */}
              <div className="grid grid-cols-2 gap-4">
                <div className="flex items-center gap-3 p-3.5 bg-gray-50 rounded-xl">
                  <div className="w-9 h-9 bg-white rounded-lg flex items-center justify-center border border-gray-200 shadow-sm">
                    <DollarSign size={15} className="text-emerald-600" />
                  </div>
                  <div>
                    <p className="text-[10px] text-gray-400 font-bold uppercase tracking-wider">Price</p>
                    <p className="text-lg font-black text-gray-900">R{selectedProduct.price.toFixed(2)}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 p-3.5 bg-gray-50 rounded-xl">
                  <div className="w-9 h-9 bg-white rounded-lg flex items-center justify-center border border-gray-200 shadow-sm">
                    <Box size={15} className="text-blue-600" />
                  </div>
                  <div>
                    <p className="text-[10px] text-gray-400 font-bold uppercase tracking-wider">Stock</p>
                    <p className={clsx('text-lg font-black', selectedProduct.stockQuantity === 0 ? 'text-red-500' : 'text-gray-900')}>{selectedProduct.stockQuantity} units</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 p-3.5 bg-gray-50 rounded-xl">
                  <div className="w-9 h-9 bg-white rounded-lg flex items-center justify-center border border-gray-200 shadow-sm">
                    <Tag size={15} className="text-purple-600" />
                  </div>
                  <div>
                    <p className="text-[10px] text-gray-400 font-bold uppercase tracking-wider">Category</p>
                    <p className="text-sm font-bold text-gray-900">{selectedProduct.categoryName || 'Uncategorized'}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 p-3.5 bg-gray-50 rounded-xl">
                  <div className="w-9 h-9 bg-white rounded-lg flex items-center justify-center border border-gray-200 shadow-sm">
                    <Users size={15} className="text-indigo-600" />
                  </div>
                  <div>
                    <p className="text-[10px] text-gray-400 font-bold uppercase tracking-wider">Supplier</p>
                    <p className="text-sm font-bold text-gray-900">{selectedProduct.supplierName || 'Unknown'}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 p-3.5 bg-gray-50 rounded-xl">
                  <div className="w-9 h-9 bg-white rounded-lg flex items-center justify-center border border-gray-200 shadow-sm">
                    <Package size={15} className="text-amber-600" />
                  </div>
                  <div>
                    <p className="text-[10px] text-gray-400 font-bold uppercase tracking-wider">Min Order Qty</p>
                    <p className="text-sm font-bold text-gray-900">{selectedProduct.minOrderQty} units</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 p-3.5 bg-gray-50 rounded-xl">
                  <div className="w-9 h-9 bg-white rounded-lg flex items-center justify-center border border-gray-200 shadow-sm">
                    <Calendar size={15} className="text-gray-500" />
                  </div>
                  <div>
                    <p className="text-[10px] text-gray-400 font-bold uppercase tracking-wider">Added</p>
                    <p className="text-sm font-bold text-gray-900">{format(new Date(selectedProduct.createdAt), 'dd MMM yyyy')}</p>
                  </div>
                </div>
              </div>

              {/* Description */}
              {selectedProduct.description && (
                <div>
                  <h3 className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Description</h3>
                  <p className="text-sm text-gray-600 leading-relaxed bg-gray-50 rounded-xl p-4 border border-gray-100">{selectedProduct.description}</p>
                </div>
              )}

              {/* Barcode Section */}
              <div className="bg-gradient-to-br from-slate-50 to-white rounded-2xl border border-gray-200 p-5">
                <div className="flex items-center gap-2 mb-4">
                  <div className="w-8 h-8 bg-slate-100 rounded-lg flex items-center justify-center">
                    <Barcode size={15} className="text-slate-600" />
                  </div>
                  <div>
                    <h3 className="text-sm font-bold text-gray-900">Product Barcode</h3>
                    <p className="text-[11px] text-gray-400">EAN-13 barcode for this product</p>
                  </div>
                </div>

                {selectedProduct.qrCode ? (
                  <div className="flex items-center gap-5">
                    {/* Barcode visual */}
                    <div className="bg-white rounded-xl p-4 border border-gray-100 shadow-sm flex-shrink-0">
                      <div className="flex flex-col items-center">
                        {/* Simple barcode visualization */}
                        <div className="flex items-end gap-[1px] h-16 mb-2">
                          {selectedProduct.qrCode.split('').map((digit, i) => (
                            <div
                              key={i}
                              className="bg-gray-900 rounded-sm"
                              style={{ width: i % 3 === 0 ? 3 : 2, height: `${50 + parseInt(digit) * 5}%` }}
                            />
                          ))}
                        </div>
                        <p className="font-mono text-xs font-bold text-gray-800 tracking-[3px]">{selectedProduct.qrCode}</p>
                      </div>
                    </div>

                    {/* Barcode info + actions */}
                    <div className="flex-1">
                      <p className="text-xs text-gray-500 font-medium mb-1">Barcode Number</p>
                      <p className="font-mono text-lg font-black text-gray-900 tracking-widest mb-3">{selectedProduct.qrCode}</p>
                      <div className="flex gap-2">
                        <button
                          onClick={() => {
                            navigator.clipboard.writeText(selectedProduct.qrCode);
                            toast.success('Barcode copied!');
                          }}
                          className="flex items-center gap-1.5 px-3 py-2 bg-white border border-gray-200 text-gray-700 text-xs font-bold rounded-xl hover:border-gray-300 hover:shadow-sm transition-all"
                        >
                          📋 Copy
                        </button>
                        <button
                          onClick={() => {
                            const win = window.open('', '_blank', 'width=400,height=300');
                            if (!win) return;
                            win.document.write(`<html><head><title>Barcode - ${selectedProduct.name}</title><style>body{font-family:sans-serif;text-align:center;padding:40px}h3{margin:0 0 4px}p{margin:0;color:#666;font-size:13px}.code{font-family:monospace;font-size:18px;letter-spacing:3px;margin-top:12px;font-weight:bold}</style></head><body><h3>${selectedProduct.name}</h3><p>SKU: ${selectedProduct.sku}</p><p class="code">${selectedProduct.qrCode}</p><script>window.onload=()=>{window.print();window.close();}<\/script></body></html>`);
                            win.document.close();
                          }}
                          className="flex items-center gap-1.5 px-3 py-2 bg-gray-900 text-white text-xs font-bold rounded-xl hover:bg-gray-800 transition-all"
                        >
                          <Printer size={12} /> Print
                        </button>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="text-center py-6 bg-gray-50 rounded-xl border border-dashed border-gray-200">
                    <Barcode size={28} className="text-gray-300 mx-auto mb-2" />
                    <p className="text-sm font-medium text-gray-400">No barcode generated yet</p>
                    <p className="text-xs text-gray-300 mt-0.5">Barcode is generated when the supplier creates the product</p>
                  </div>
                )}
              </div>

              {/* Product Image */}
              {selectedProduct.imageUrl && (
                <div>
                  <h3 className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Product Image</h3>
                  <img
                    src={selectedProduct.imageUrl}
                    alt={selectedProduct.name}
                    className="w-full max-h-64 object-contain rounded-2xl border border-gray-100 bg-gray-50"
                  />
                </div>
              )}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-gray-100 bg-white flex-shrink-0">
              {selectedProduct.status === 'pending_approval' && (
                <>
                  <button
                    onClick={() => { approve(selectedProduct.id); setSelectedProduct(null); }}
                    className="flex items-center gap-2 text-sm font-bold bg-emerald-600 text-white px-5 py-2.5 rounded-xl hover:bg-emerald-700 transition-colors shadow-sm shadow-emerald-200"
                  >
                    <CheckCircle size={14} /> Approve Product
                  </button>
                  <button
                    onClick={() => { reject(selectedProduct.id); setSelectedProduct(null); }}
                    className="flex items-center gap-2 text-sm font-bold bg-red-500 text-white px-5 py-2.5 rounded-xl hover:bg-red-600 transition-colors shadow-sm shadow-red-200"
                  >
                    <XCircle size={14} /> Reject Product
                  </button>
                </>
              )}
              {selectedProduct.status === 'active' && (
                <button
                  onClick={() => { reject(selectedProduct.id); setSelectedProduct(null); }}
                  className="flex items-center gap-2 text-sm font-bold bg-red-500 text-white px-5 py-2.5 rounded-xl hover:bg-red-600 transition-colors shadow-sm shadow-red-200"
                >
                  <XCircle size={14} /> Archive Product
                </button>
              )}
              <button
                onClick={() => setSelectedProduct(null)}
                className="px-5 py-2.5 rounded-xl text-sm font-bold text-gray-600 bg-gray-100 hover:bg-gray-200 transition-colors"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ═══ KEYFRAMES ═══ */}
      <style>{`
        @keyframes expandIn {
          from {
            opacity: 0;
            max-height: 0;
            transform: translateY(-8px);
          }
          to {
            opacity: 1;
            max-height: 2000px;
            transform: translateY(0);
          }
        }
      `}</style>
    </div>
  );
}
