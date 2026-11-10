import { useState, useEffect, useCallback, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Plus, Search, Edit2, Trash2, QrCode, LayoutGrid, List, Package, Filter, RefreshCw, Upload, ShieldAlert, FileText, ArrowRight, Lock, ShieldCheck, Warehouse, ChevronDown, Eye, EyeOff, Tag, X, CheckSquare } from 'lucide-react';
import toast from 'react-hot-toast';
import type { Product, ComplianceDoc, ProductFormData } from '../../types';
import { EmptyState } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import clsx from 'clsx';
import ProductFormModal from './ProductFormModal';
import BarcodeModal from './BarcodeModal';
import ProductCodesModal from './ProductCodesModal';
import BranchStockModal from './BranchStockModal';
import BulkUploadModal from './BulkUploadModal';
import { productsApi, profileApi } from '../../services/api';
import { Star } from 'lucide-react';

const REQUIRED_DOCS = [
  { docType: 'cipc_certificate', label: 'CIPC Certificate' },
  { docType: 'tax_clearance', label: 'Tax Clearance' },
  { docType: 'bee_certificate', label: 'BEE Certificate' },
  { docType: 'product_license', label: 'Product License' },
];

function ProductImage({ src, alt, className }: { src?: string; alt: string; className?: string }) {
  const [failed, setFailed] = useState(false);
  if (!src || failed) {
    return (
      <div className={clsx('bg-gray-100 text-gray-300 flex items-center justify-center', className)} aria-label={alt}>
        <Package size={20} />
      </div>
    );
  }
  return <img src={src} alt={alt} onError={() => setFailed(true)} className={clsx('object-cover bg-gray-50', className)} />;
}

const statusFilters = [
  { value: 'all',              label: 'All' },
  { value: 'active',           label: 'Active' },
  { value: 'pending_approval', label: 'Pending' },
  { value: 'archived',         label: 'Archived' },
];

const statusStyle: Record<string, string> = {
  active:           'bg-emerald-50 text-emerald-700 border border-emerald-200',
  pending_approval: 'bg-amber-50 text-amber-700 border border-amber-200',
  archived:         'bg-gray-100 text-gray-500 border border-gray-200',
  draft:            'bg-blue-50 text-blue-700 border border-blue-200',
};

export default function ProductsPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [products, setProducts] = useState<Product[]>([]);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);
  useEffect(() => {
    productsApi.categories().then(setCategories).catch(() => {});
  }, []);
  // Ticked products, for the Actions menu
  const [selected, setSelected] = useState<Set<string>>(new Set());
  // Checkboxes stay hidden until the supplier chooses "Select" (or something is already ticked)
  const [selectMode, setSelectMode] = useState(false);
  const [actionsOpen, setActionsOpen] = useState(false);
  const [bulkCategoryOpen, setBulkCategoryOpen] = useState(false);
  const [bulkBusy, setBulkBusy] = useState(false);
  const actionsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!actionsOpen) return;
    const close = (e: MouseEvent) => {
      if (!actionsRef.current?.contains(e.target as Node)) setActionsOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [actionsOpen]);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('list');
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [barcodeProduct, setBarcodeProduct] = useState<Product | null>(null);
  const [codesProduct, setCodesProduct] = useState<Product | null>(null);
  const [stockProduct, setStockProduct] = useState<Product | null>(null);
  const [showBulkUpload, setShowBulkUpload] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  // Compliance state
  const [docs, setDocs] = useState<ComplianceDoc[]>([]);
  const [complianceChecked, setComplianceChecked] = useState(false);
  const [showComplianceAlert, setShowComplianceAlert] = useState(false);

  // Check which required docs are missing or not approved
  const missingDocs = REQUIRED_DOCS.filter(({ docType }) => {
    const doc = docs.find((d) => d.docType === docType);
    return !doc || doc.status === 'rejected';
  });
  // If the profile could not be loaded we don't know the document status, so
  // don't lock the supplier out (the backend still enforces compliance).
  const isCompliant = !complianceChecked || missingDocs.length === 0;

  const loadProducts = useCallback(async () => {
    setLoading(true);
    try {
      const [productsRes, profileRes] = await Promise.allSettled([
        // Load the whole catalogue: filtering happens on this page, so a short page hid products.
        productsApi.list({ pageSize: 500 }),
        profileApi.get(),
      ]);

      if (productsRes.status === 'fulfilled') {
        setProducts(productsRes.value.data);
      } else {
        toast.error('Failed to load products');
      }

      if (profileRes.status === 'fulfilled') {
        const profileData = (profileRes.value as any)?.data ?? profileRes.value;
        const rawDocs: any[] = profileData?.documents ?? [];
        setDocs(rawDocs.map((d: any) => ({
          id: d.id,
          docType: d.docType,
          docUrl: d.docUrl ?? '',
          status: d.status,
          expiryDate: d.expiryDate,
        })));
        setComplianceChecked(true);
      }
    } catch {
      toast.error('Failed to load data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadProducts(); }, [loadProducts]);

  // Show compliance popup on first load if not compliant
  useEffect(() => {
    if (complianceChecked && missingDocs.length > 0) {
      setShowComplianceAlert(true);
    }
  }, [complianceChecked, missingDocs.length]);

  // "Add Product" shortcut from the dashboard: /products?new=1
  useEffect(() => {
    if (loading || searchParams.get('new') !== '1') return;
    searchParams.delete('new');
    setSearchParams(searchParams, { replace: true });
    if (isCompliant) { setEditing(null); setShowModal(true); } else { setShowComplianceAlert(true); }
  }, [loading, searchParams, setSearchParams, isCompliant]);

  if (loading) return <PageLoader variant="cards" />;

  // Block action if not compliant
  const handleBlockedAction = () => {
    setShowComplianceAlert(true);
  };

  const filtered = products.filter((p) => {
    const matchSearch = (p.name ?? '').toLowerCase().includes(search.toLowerCase()) || (p.sku ?? '').toLowerCase().includes(search.toLowerCase());
    const matchStatus = statusFilter === 'all' || p.status === statusFilter;
    const matchCategory = categoryFilter === 'all'
      || (categoryFilter === 'none' ? !p.categoryId : p.categoryId === categoryFilter);
    return matchSearch && matchStatus && matchCategory;
  });

  const handleSave = async (data: Partial<Product>) => {
    setSaving(true);
    try {
      if (editing) {
        const updated = await productsApi.update(editing.id, data as any, editing);
        setProducts((prev) => prev.map((p) => p.id === editing.id ? updated : p));
        toast.success('Product updated');
      } else {
        // Product already created inside ProductFormModal — just add to list.
        // The modal stays open to show the generated barcode.
        setProducts((prev) => [data as Product, ...prev]);
        return;
      }
      setShowModal(false);
      setEditing(null);
    } catch {
      toast.error('Failed to save product');
    } finally {
      setSaving(false);
    }
  };

  // Actions apply to the ticked products the filters still show
  const selectedProducts = filtered.filter((p) => selected.has(p.id));
  const allVisibleSelected = filtered.length > 0 && selectedProducts.length === filtered.length;

  const toggleSelected = (id: string) => setSelected((prev) => {
    const next = new Set(prev);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });
  const selectAllVisible = () => setSelected((prev) => new Set([...prev, ...filtered.map((p) => p.id)]));
  const deselectAll = () => { setSelected(new Set()); setSelectMode(false); setActionsOpen(false); };
  const selecting = selectMode || selectedProducts.length > 0;

  const toggleOneAvailable = async (p: Product) => {
    try {
      await productsApi.toggleAvailability(p.id);
      const available = !p.isAvailable;
      setProducts((prev) => prev.map((x) => x.id === p.id
        ? { ...x, isAvailable: available, status: x.status === 'active' ? 'archived' : x.status === 'archived' ? 'active' : x.status }
        : x));
      toast.success(available ? 'Shown on the marketplace' : 'Hidden from the marketplace');
    } catch {
      toast.error('Could not update the product');
    }
  };

  const deleteOne = async (p: Product) => {
    if (!confirm(`Delete "${p.name}"? This cannot be undone.`)) return;
    try {
      await productsApi.delete(p.id);
      setProducts((prev) => prev.filter((x) => x.id !== p.id));
      setSelected((prev) => { const next = new Set(prev); next.delete(p.id); return next; });
      toast.success('Product deleted');
    } catch {
      toast.error('Could not delete the product');
    }
  };

  /** The buttons every product carries, in both the list and the grid. */
  const rowActions = (p: Product) => {
    const btn = 'p-1.5 rounded-lg transition-colors';
    return (
      <div className="flex items-center justify-end gap-0.5" onClick={(e) => e.stopPropagation()}>
        <button
          onClick={isCompliant ? () => { setEditing(p); setShowModal(true); } : handleBlockedAction}
          className={clsx(btn, 'text-gray-400 hover:text-primary hover:bg-primary-50')}
          title="Edit" aria-label={`Edit ${p.name}`}
        >
          {isCompliant ? <Edit2 size={15} /> : <Lock size={15} />}
        </button>
        <button onClick={() => setBarcodeProduct(p)} className={clsx(btn, 'text-gray-400 hover:text-emerald-600 hover:bg-emerald-50')} title="QR code & barcode" aria-label={`QR code and barcode for ${p.name}`}>
          <QrCode size={15} />
        </button>
        <button onClick={() => setStockProduct(p)} className={clsx(btn, 'text-gray-400 hover:text-gray-700 hover:bg-gray-100')} title="Stock per branch" aria-label={`Stock per branch for ${p.name}`}>
          <Warehouse size={15} />
        </button>
        <button onClick={() => setCodesProduct(p)} className={clsx(btn, 'text-gray-400 hover:text-primary hover:bg-primary-50')} title="Authenticity codes" aria-label={`Authenticity codes for ${p.name}`}>
          <ShieldCheck size={15} />
        </button>
        <button
          onClick={() => toggleOneAvailable(p)}
          className={clsx(btn, 'text-gray-400 hover:text-gray-700 hover:bg-gray-100')}
          title={p.isAvailable ? 'Hide from marketplace' : 'Show on marketplace'}
          aria-label={p.isAvailable ? `Hide ${p.name} from marketplace` : `Show ${p.name} on marketplace`}
        >
          {p.isAvailable ? <EyeOff size={15} /> : <Eye size={15} />}
        </button>
        <button
          onClick={isCompliant ? () => deleteOne(p) : handleBlockedAction}
          className={clsx(btn, 'text-gray-400 hover:text-red-600 hover:bg-red-50')}
          title="Delete" aria-label={`Delete ${p.name}`}
        >
          {isCompliant ? <Trash2 size={15} /> : <Lock size={15} />}
        </button>
      </div>
    );
  };

  /** Runs one request per product, a few at a time, and reports how many worked. */
  const runBulk = async (
    items: Product[],
    request: (p: Product) => Promise<unknown>,
    done: string,
  ): Promise<Set<string>> => {
    setActionsOpen(false);
    setBulkBusy(true);
    const succeeded = new Set<string>();
    try {
      for (let i = 0; i < items.length; i += 5) {
        const batch = items.slice(i, i + 5);
        const results = await Promise.allSettled(batch.map(request));
        results.forEach((r, j) => { if (r.status === 'fulfilled') succeeded.add(batch[j].id); });
      }
      const failed = items.length - succeeded.size;
      if (succeeded.size > 0) toast.success(`${succeeded.size} product${succeeded.size === 1 ? '' : 's'} ${done}`);
      if (failed > 0) toast.error(`${failed} product${failed === 1 ? '' : 's'} could not be updated`);
    } finally {
      setBulkBusy(false);
    }
    return succeeded;
  };

  const bulkSetAvailable = async (available: boolean) => {
    // The API toggles, so only products in the other state are sent
    const items = selectedProducts.filter((p) => p.isAvailable !== available);
    if (items.length === 0) {
      setActionsOpen(false);
      toast.success(`Already ${available ? 'shown on' : 'hidden from'} the marketplace`);
      return;
    }
    const ok = await runBulk(items, (p) => productsApi.toggleAvailability(p.id),
      available ? 'shown on the marketplace' : 'hidden from the marketplace');
    setProducts((prev) => prev.map((p) => ok.has(p.id)
      ? { ...p, isAvailable: available, status: p.status === 'active' ? 'archived' : p.status === 'archived' ? 'active' : p.status }
      : p));
  };

  const bulkSetCategory = async (categoryId: string) => {
    setBulkCategoryOpen(false);
    const category = categories.find((c) => c.id === categoryId);
    if (!category) return;
    const items = selectedProducts.filter((p) => p.categoryId !== categoryId);
    const updated = new Map<string, Product>();
    await runBulk(items, async (p) => {
      // The edit call overwrites every field, so the product's own values go with the new category
      const result = await productsApi.update(p.id, {
        name: p.name, description: p.description, sku: p.sku, price: p.price,
        categoryId, stockQuantity: p.stockQuantity, minOrderQty: p.minOrderQty,
        isFoodItem: p.isFoodItem, categoryName: category.name,
      } as Partial<ProductFormData>, p);
      updated.set(p.id, result);
    }, `moved to ${category.name}`);
    setProducts((prev) => prev.map((p) => updated.get(p.id) ?? p));
  };

  const bulkDelete = async () => {
    const items = selectedProducts;
    if (!confirm(`Delete ${items.length} product${items.length === 1 ? '' : 's'}? This cannot be undone.`)) {
      setActionsOpen(false);
      return;
    }
    const ok = await runBulk(items, (p) => productsApi.delete(p.id), 'deleted');
    setProducts((prev) => prev.filter((p) => !ok.has(p.id)));
    setSelected((prev) => new Set([...prev].filter((id) => !ok.has(id))));
  };

  const openNewProduct = isCompliant ? () => { setEditing(null); setShowModal(true); } : handleBlockedAction;
  const isFiltering = search.trim() !== '' || statusFilter !== 'all' || categoryFilter !== 'all';
  const emptyState = isFiltering ? (
    <EmptyState
      title="No matching products"
      description="Try a different search term, status or category."
      icon={<Search size={40} />}
      action={<button onClick={() => { setSearch(''); setStatusFilter('all'); }} className="btn-secondary">Clear filters</button>}
    />
  ) : (
    <EmptyState
      title="No products yet"
      description="Add your first product to start selling to spaza shops on SpazaSure."
      icon={<Package size={40} />}
      action={<button onClick={openNewProduct} className="btn-primary flex items-center gap-2"><Plus size={16} />Add Product</button>}
    />
  );

  const activeCount = products.filter((p) => p.status === 'active').length;
  const pendingCount = products.filter((p) => p.status === 'pending_approval').length;
  const outOfStock = products.filter((p) => p.stockQuantity === 0).length;

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Products</h1>
          <p className="page-subtitle">{products.length} product{products.length === 1 ? '' : 's'} in your catalog</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button onClick={loadProducts} className="btn-secondary flex items-center gap-2" title="Refresh" aria-label="Refresh products">
            <RefreshCw size={14} />
          </button>
          <button
            onClick={isCompliant ? () => setShowBulkUpload(true) : handleBlockedAction}
            className={clsx('btn-secondary flex items-center gap-2', !isCompliant && 'opacity-60 cursor-not-allowed')}
          >
            {!isCompliant && <Lock size={12} />}
            <Upload size={14} /> Bulk Upload
          </button>
          <button
            onClick={openNewProduct}
            className={clsx('btn-primary flex items-center gap-2', !isCompliant && 'opacity-70')}
          >
            {!isCompliant && <Lock size={12} />}
            <Plus size={16} /> Add Product
          </button>
        </div>
      </div>

      {/* Shops only see products that are in stock */}
      {outOfStock > 0 && (
        <div role="status" className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-brand-yellow-light p-4">
          <Package size={18} className="text-amber-600 flex-shrink-0 mt-0.5" />
          <p className="flex-1 text-sm text-amber-900">
            <strong>{outOfStock} product{outOfStock === 1 ? ' is' : 's are'} out of stock and hidden from shops.</strong>{' '}
            Edit {outOfStock === 1 ? 'it' : 'them'} and enter the stock you have, so retailers can see and order {outOfStock === 1 ? 'it' : 'them'} again.
          </p>
        </div>
      )}

      {/* Compliance Warning Banner (always visible when not compliant) */}
      {complianceChecked && !isCompliant && (
        <div className="bg-red-50 border-2 border-red-200 rounded-2xl p-5 flex flex-col sm:flex-row sm:items-center gap-4 animate-in">
          <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center flex-shrink-0">
            <ShieldAlert size={24} className="text-red-600" />
          </div>
          <div className="flex-1">
            <p className="font-bold text-red-900 text-sm">Account Restricted — Missing Compliance Documents</p>
            <p className="text-xs text-red-700 mt-1">You cannot add or edit products until all required documents are submitted and approved. Upload your documents to unlock full access.</p>
          </div>
          <button
            onClick={() => navigate('/profile')}
            className="flex items-center gap-2 bg-red-600 hover:bg-red-700 text-white text-sm font-bold px-4 py-2.5 rounded-xl transition-all flex-shrink-0"
          >
            Upload Documents <ArrowRight size={14} />
          </button>
        </div>
      )}

      {/* Summary Cards */}
      <div className="grid grid-cols-3 gap-3">
        <div className="card p-4 flex items-center gap-3">
          <div className="w-10 h-10 bg-emerald-50 rounded-xl flex items-center justify-center">
            <Package size={18} className="text-emerald-600" />
          </div>
          <div>
            <p className="text-xl font-black text-gray-900">{activeCount}</p>
            <p className="text-xs text-gray-500 font-medium">Active Products</p>
          </div>
        </div>
        <div className="card p-4 flex items-center gap-3">
          <div className="w-10 h-10 bg-amber-50 rounded-xl flex items-center justify-center">
            <Filter size={18} className="text-amber-600" />
          </div>
          <div>
            <p className="text-xl font-black text-gray-900">{pendingCount}</p>
            <p className="text-xs text-gray-500 font-medium">Pending Approval</p>
          </div>
        </div>
        <div className="card p-4 flex items-center gap-3">
          <div className="w-10 h-10 bg-red-50 rounded-xl flex items-center justify-center">
            <Package size={18} className="text-red-500" />
          </div>
          <div>
            <p className="text-xl font-black text-gray-900">{outOfStock}</p>
            <p className="text-xs text-gray-500 font-medium">Out of Stock</p>
          </div>
        </div>
      </div>

      {/* Filters Bar */}
      <div className="flex items-center gap-3 flex-wrap">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search size={15} className="absolute left-3.5 top-3 text-gray-400" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="input pl-10 focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
            placeholder="Search by name or SKU..."
          />
          {search && (
            <button onClick={() => setSearch('')} className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600 transition-colors">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
            </button>
          )}
        </div>

        <div className="flex items-center gap-1.5 bg-white border border-gray-200 rounded-xl p-1">
          {statusFilters.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => setStatusFilter(value)}
              className={clsx(
                'px-3 py-1.5 rounded-lg text-xs font-semibold transition-all',
                statusFilter === value ? 'bg-primary text-white shadow-sm' : 'text-gray-500 hover:text-gray-700 hover:bg-gray-50'
              )}
            >
              {label}
            </button>
          ))}
        </div>

        <select
          value={categoryFilter}
          onChange={(e) => setCategoryFilter(e.target.value)}
          className="select w-auto min-w-[180px]"
          aria-label="Filter by category"
        >
          <option value="all">All categories</option>
          {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          <option value="none">No category</option>
        </select>

        {filtered.length > 0 && !selecting && (
          <button onClick={() => setSelectMode(true)} className="btn-secondary text-sm ml-auto flex items-center gap-1.5">
            <CheckSquare size={14} /> Select
          </button>
        )}

        <div className={clsx('flex items-center gap-1 bg-white border border-gray-200 rounded-xl p-1', selecting || filtered.length === 0 ? 'ml-auto' : '')}>
          <button onClick={() => setViewMode('list')} className={clsx('p-2 rounded-lg transition-all', viewMode === 'list' ? 'bg-primary text-white' : 'text-gray-400 hover:text-gray-600')}>
            <List size={15} />
          </button>
          <button onClick={() => setViewMode('grid')} className={clsx('p-2 rounded-lg transition-all', viewMode === 'grid' ? 'bg-primary text-white' : 'text-gray-400 hover:text-gray-600')}>
            <LayoutGrid size={15} />
          </button>
        </div>
      </div>

      {/* Selection and bulk actions */}
      {filtered.length > 0 && selecting && (
        <div className="flex flex-wrap items-center gap-3 bg-white border border-gray-200 rounded-2xl px-4 py-2.5 animate-fade-in">
          <label className="flex items-center gap-2 text-sm font-semibold text-gray-700 cursor-pointer select-none">
            <input
              type="checkbox"
              className="w-4 h-4 accent-primary cursor-pointer"
              checked={allVisibleSelected}
              ref={(el) => { if (el) el.indeterminate = selectedProducts.length > 0 && !allVisibleSelected; }}
              onChange={allVisibleSelected ? deselectAll : selectAllVisible}
              aria-label="Select all products"
            />
            {selectedProducts.length > 0
              ? `${selectedProducts.length} selected`
              : `Select all (${filtered.length})`}
          </label>
          <button onClick={deselectAll} className="text-xs font-semibold text-gray-500 hover:text-gray-800 flex items-center gap-1">
            <X size={12} /> {selectedProducts.length > 0 ? 'Deselect all' : 'Cancel'}
          </button>

          {/* Bulk actions, once something is ticked */}
          {selectedProducts.length > 0 && (
          <div ref={actionsRef} className="relative ml-auto animate-fade-in">
            <button
              onClick={() => setActionsOpen((o) => !o)}
              disabled={bulkBusy}
              className="btn-primary text-sm py-1.5 flex items-center gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {bulkBusy && <RefreshCw size={14} className="animate-spin" />}
              Actions ({selectedProducts.length}) <ChevronDown size={14} />
            </button>
            {actionsOpen && (
              <div className="absolute right-0 mt-2 w-60 bg-white border border-gray-200 rounded-xl shadow-lg z-30 py-1 animate-fade-in">
                {/* Things that only make sense for one product */}
                {selectedProducts.length === 1 && (() => {
                  const only = selectedProducts[0];
                  const open = (action: () => void) => () => { setActionsOpen(false); action(); };
                  return (
                    <>
                      <button
                        onClick={isCompliant ? open(() => { setEditing(only); setShowModal(true); }) : handleBlockedAction}
                        className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2"
                      >
                        {isCompliant ? <Edit2 size={14} className="text-primary" /> : <Lock size={14} />} Edit
                      </button>
                      <button onClick={open(() => setBarcodeProduct(only))} className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2">
                        <QrCode size={14} className="text-emerald-600" /> QR code & barcode
                      </button>
                      <button onClick={open(() => setStockProduct(only))} className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2">
                        <Warehouse size={14} className="text-gray-500" /> Stock per branch
                      </button>
                      <button onClick={open(() => setCodesProduct(only))} className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2">
                        <ShieldCheck size={14} className="text-primary" /> Authenticity codes
                      </button>
                      <div className="border-t border-gray-100 my-1" />
                    </>
                  );
                })()}
                <button onClick={() => bulkSetAvailable(true)} className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2">
                  <Eye size={14} className="text-emerald-600" /> Show on marketplace
                </button>
                <button onClick={() => bulkSetAvailable(false)} className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2">
                  <EyeOff size={14} className="text-gray-500" /> Hide from marketplace
                </button>
                <button
                  onClick={isCompliant ? () => { setActionsOpen(false); setBulkCategoryOpen(true); } : handleBlockedAction}
                  className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2"
                >
                  {isCompliant ? <Tag size={14} className="text-primary" /> : <Lock size={14} />} Change category
                </button>
                <div className="border-t border-gray-100 my-1" />
                <button
                  onClick={isCompliant ? bulkDelete : handleBlockedAction}
                  className="w-full px-4 py-2 text-left text-sm text-red-600 hover:bg-red-50 flex items-center gap-2"
                >
                  {isCompliant ? <Trash2 size={14} /> : <Lock size={14} />} Delete
                </button>
              </div>
            )}
          </div>
          )}
        </div>
      )}

      {/* Grid View */}
      {viewMode === 'grid' && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          {filtered.length === 0 ? (
            <div className="col-span-full card">{emptyState}</div>
          ) : filtered.map((p) => (
            <div
              key={p.id}
              onClick={selecting ? () => toggleSelected(p.id) : undefined}
              className={clsx('card overflow-hidden hover:shadow-card-hover transition-all duration-200 group', selecting && 'cursor-pointer', selected.has(p.id) && 'ring-2 ring-primary')}
            >
              <div className="relative">
                <ProductImage src={p.imageUrl} alt={p.name} className="w-full h-36" />
                {selecting && (
                  <label onClick={(e) => e.stopPropagation()} className="absolute top-2 left-2 z-10 bg-white/90 rounded-lg p-1.5 shadow-sm cursor-pointer flex">
                    <input
                      type="checkbox"
                      className="w-4 h-4 accent-primary cursor-pointer"
                      checked={selected.has(p.id)}
                      onChange={() => toggleSelected(p.id)}
                      aria-label={`Select ${p.name}`}
                    />
                  </label>
                )}
                <div className="absolute top-2 right-2 flex gap-1">
                  <span className={clsx('text-[10px] font-bold px-2 py-0.5 rounded-full capitalize', statusStyle[p.status] ?? 'bg-gray-100 text-gray-500')}>
                    {p.status.replace('_', ' ')}
                  </span>
                </div>
                {!p.isAvailable && (
                  <div className="absolute inset-0 bg-black/40 flex items-center justify-center">
                    <span className="text-white text-xs font-bold bg-black/60 px-2 py-1 rounded-lg">Unavailable</span>
                  </div>
                )}
              </div>
              <div className="p-3">
                <p className="font-bold text-gray-900 text-sm truncate">{p.name}</p>
                <p className="text-xs text-gray-400 font-mono mt-0.5">{p.sku}</p>
                <div className="flex items-center justify-between mt-2">
                  <p className="text-base font-black text-primary">R{p.price.toFixed(2)}</p>
                  <span className={clsx('text-xs font-semibold', p.stockQuantity === 0 ? 'text-red-500' : p.stockQuantity < 50 ? 'text-amber-600' : 'text-gray-500')}>
                    {p.stockQuantity === 0 ? 'Out of stock' : `${p.stockQuantity} units`}
                  </span>
                </div>
                <div className="mt-2 pt-2 border-t border-gray-100">{rowActions(p)}</div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* List View */}
      {viewMode === 'list' && (
        <div className="card overflow-hidden">
          {filtered.length === 0 ? emptyState : (
            <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[820px]">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50/80">
                  {selecting && (
                    <th className="table-header w-10">
                      <input
                        type="checkbox"
                        className="w-4 h-4 accent-primary cursor-pointer"
                        checked={allVisibleSelected}
                        onChange={allVisibleSelected ? () => setSelected(new Set()) : selectAllVisible}
                        aria-label="Select all products"
                      />
                    </th>
                  )}
                  <th className="table-header">Product</th>
                  <th className="table-header">SKU</th>
                  <th className="table-header">Category</th>
                  <th className="table-header text-right">Price</th>
                  <th className="table-header text-right">Stock</th>
                  <th className="table-header text-center">Status</th>
                  <th className="table-header text-center">Available</th>
                  <th className="table-header text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((p) => (
                  <tr
                    key={p.id}
                    onClick={selecting ? () => toggleSelected(p.id) : undefined}
                    className={clsx('table-row', selecting && 'cursor-pointer', selected.has(p.id) && 'bg-primary-50/60')}
                  >
                    {selecting && (
                      <td className="table-cell w-10" onClick={(e) => e.stopPropagation()}>
                        <input
                          type="checkbox"
                          className="w-4 h-4 accent-primary cursor-pointer"
                          checked={selected.has(p.id)}
                          onChange={() => toggleSelected(p.id)}
                          aria-label={`Select ${p.name}`}
                        />
                      </td>
                    )}
                    <td className="table-cell">
                      <div className="flex items-center gap-3">
                        <ProductImage src={p.imageUrl} alt={p.name} className="w-11 h-11 rounded-xl flex-shrink-0" />
                        <div>
                          <p className="font-semibold text-gray-900">{p.name}</p>
                          <div className="flex items-center gap-1 mt-0.5">
                            {[1, 2, 3, 4, 5].map((i) => <Star key={i} size={12} className={i <= Math.round(p.rating) ? 'fill-accent text-accent' : 'text-gray-300'} />)}
                            <span className="text-[10px] text-gray-400">({p.reviewCount})</span>
                          </div>
                        </div>
                      </div>
                    </td>
                    <td className="table-cell text-gray-400 font-mono text-xs">{p.sku}</td>
                    <td className="table-cell">
                      {p.categoryName
                        ? <span className="text-xs bg-gray-100 text-gray-600 px-2 py-1 rounded-lg font-medium">{p.categoryName}</span>
                        : <span className="text-xs text-gray-300">—</span>}
                    </td>
                    <td className="table-cell text-right">
                      <p className="font-bold text-gray-900">R{p.price.toFixed(2)}</p>
                      {p.discountPrice && <p className="text-xs text-gray-400 line-through">R{p.discountPrice.toFixed(2)}</p>}
                    </td>
                    <td className="table-cell text-right">
                      <span className={clsx('font-bold text-sm', p.stockQuantity === 0 ? 'text-red-500' : p.stockQuantity < 50 ? 'text-amber-600' : 'text-gray-700')}>
                        {p.stockQuantity}
                      </span>
                      <p className="text-[10px] text-gray-400">Min: {p.minOrderQty}</p>
                    </td>
                    <td className="table-cell text-center">
                      <span className={clsx('text-xs font-semibold px-2.5 py-1 rounded-full capitalize', statusStyle[p.status] ?? 'bg-gray-100 text-gray-500')}>
                        {p.status.replace('_', ' ')}
                      </span>
                    </td>
                    <td className="table-cell text-center">
                      <span className={clsx('text-xs font-semibold', p.isAvailable ? 'text-emerald-600' : 'text-gray-400')}>
                        {p.isAvailable ? 'Shown' : 'Hidden'}
                      </span>
                    </td>
                    <td className="table-cell text-right">{rowActions(p)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          )}
        </div>
      )}

      {bulkCategoryOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setBulkCategoryOpen(false)} />
          <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-sm p-5 animate-in">
            <h3 className="font-bold text-gray-900">Change category</h3>
            <p className="text-sm text-gray-500 mt-1">
              Move {selectedProducts.length} product{selectedProducts.length === 1 ? '' : 's'} to:
            </p>
            <select
              defaultValue=""
              onChange={(e) => e.target.value && bulkSetCategory(e.target.value)}
              className="select w-full mt-4"
              aria-label="New category"
            >
              <option value="" disabled>Choose a category</option>
              {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
            <button onClick={() => setBulkCategoryOpen(false)} className="btn-secondary w-full mt-3">Cancel</button>
          </div>
        </div>
      )}

      {showModal && isCompliant && (
        <ProductFormModal
          product={editing}
          onSave={handleSave}
          onClose={() => { setShowModal(false); setEditing(null); }}
        />
      )}

      {stockProduct && (
        <BranchStockModal product={stockProduct} onClose={() => setStockProduct(null)} onSaved={loadProducts} />
      )}
      {barcodeProduct && (
        <BarcodeModal
          product={barcodeProduct}
          onClose={() => setBarcodeProduct(null)}
        />
      )}

      {codesProduct && (
        <ProductCodesModal product={codesProduct} onClose={() => setCodesProduct(null)} />
      )}

      {showBulkUpload && isCompliant && (
        <BulkUploadModal
          onClose={() => setShowBulkUpload(false)}
          onComplete={loadProducts}
        />
      )}

      {/* Compliance Alert Modal */}
      {showComplianceAlert && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setShowComplianceAlert(false)} />
          <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-md max-h-[90vh] overflow-hidden flex flex-col animate-scale-in">
            {/* Red gradient header */}
            <div className="bg-gradient-to-br from-red-600 to-red-700 p-6 text-center flex-shrink-0">
              <div className="w-16 h-16 bg-white/20 rounded-2xl flex items-center justify-center mx-auto mb-4">
                <ShieldAlert size={32} className="text-white" />
              </div>
              <h2 className="text-xl font-black text-white">Documents Required</h2>
              <p className="text-red-100 text-sm mt-2">Your account is restricted until compliance documents are submitted.</p>
            </div>

            {/* Missing documents list */}
            <div className="p-6 overflow-y-auto flex-1 overscroll-contain">
              <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Missing Documents:</p>
              <div className="space-y-2.5">
                {missingDocs.map(({ docType, label }) => {
                  const doc = docs.find((d) => d.docType === docType);
                  const isRejected = doc?.status === 'rejected';
                  return (
                    <div key={docType} className={clsx('flex items-center gap-3 p-3 rounded-xl border-2', isRejected ? 'bg-red-50 border-red-200' : 'bg-gray-50 border-gray-200')}>
                      <FileText size={18} className={isRejected ? 'text-red-500' : 'text-gray-400'} />
                      <div className="flex-1">
                        <p className="text-sm font-bold text-gray-900">{label}</p>
                        <p className="text-xs text-gray-500">{isRejected ? 'Rejected — please re-upload' : 'Not uploaded'}</p>
                      </div>
                      <span className={clsx('text-[10px] font-bold px-2 py-0.5 rounded-full', isRejected ? 'bg-red-100 text-red-700' : 'bg-gray-200 text-gray-600')}>
                        {isRejected ? 'Rejected' : 'Missing'}
                      </span>
                    </div>
                  );
                })}
              </div>

              <div className="mt-6 space-y-3">
                <button
                  onClick={() => { setShowComplianceAlert(false); navigate('/profile'); }}
                  className="w-full flex items-center justify-center gap-2 bg-primary hover:bg-primary-700 text-white font-bold py-3 rounded-xl transition-all"
                >
                  <FileText size={16} /> Upload Documents Now
                </button>
                <button
                  onClick={() => setShowComplianceAlert(false)}
                  className="w-full text-sm font-semibold text-gray-500 hover:text-gray-700 py-2 transition-colors"
                >
                  I'll do this later (read-only mode)
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
