import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Search, Edit2, Trash2, ToggleLeft, ToggleRight, QrCode, LayoutGrid, List, Package, Filter, RefreshCw, Upload, ShieldAlert, FileText, ArrowRight, Lock } from 'lucide-react';
import toast from 'react-hot-toast';
import type { Product, ComplianceDoc } from '../../types';
import { EmptyState } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import clsx from 'clsx';
import ProductFormModal from './ProductFormModal';
import BarcodeModal from './BarcodeModal';
import BulkUploadModal from './BulkUploadModal';
import { productsApi, profileApi } from '../../services/api';

const REQUIRED_DOCS = [
  { docType: 'cipc_certificate', label: 'CIPC Certificate' },
  { docType: 'tax_clearance', label: 'Tax Clearance' },
  { docType: 'bee_certificate', label: 'BEE Certificate' },
  { docType: 'product_license', label: 'Product License' },
];

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
  const [products, setProducts] = useState<Product[]>([]);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('list');
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [barcodeProduct, setBarcodeProduct] = useState<Product | null>(null);
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
  const isCompliant = complianceChecked && missingDocs.length === 0;

  const loadProducts = useCallback(async () => {
    setLoading(true);
    try {
      const [productsRes, profileRes] = await Promise.allSettled([
        productsApi.list(),
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
      }

      setComplianceChecked(true);
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

  if (loading) return <PageLoader variant="cards" />;

  // Block action if not compliant
  const handleBlockedAction = () => {
    setShowComplianceAlert(true);
  };

  const filtered = products.filter((p) => {
    const matchSearch = (p.name ?? '').toLowerCase().includes(search.toLowerCase()) || (p.sku ?? '').toLowerCase().includes(search.toLowerCase());
    const matchStatus = statusFilter === 'all' || p.status === statusFilter;
    return matchSearch && matchStatus;
  });

  const handleToggle = async (id: string) => {
    try {
      await productsApi.toggleAvailability(id);
      setProducts((prev) => prev.map((p) => p.id === id ? { ...p, isAvailable: !p.isAvailable } : p));
      toast.success('Availability updated');
    } catch {
      toast.error('Failed to update availability');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Delete this product?')) return;
    try {
      await productsApi.delete(id);
      setProducts((prev) => prev.filter((p) => p.id !== id));
      toast.success('Product deleted');
    } catch {
      toast.error('Failed to delete product');
    }
  };

  const handleSave = async (data: Partial<Product>) => {
    setSaving(true);
    try {
      if (editing) {
        const updated = await productsApi.update(editing.id, data as any);
        setProducts((prev) => prev.map((p) => p.id === editing.id ? updated : p));
        toast.success('Product updated');
      } else {
        // Product already created inside ProductFormModal — just add to list
        setProducts((prev) => [data as Product, ...prev]);
      }
      setShowModal(false);
      setEditing(null);
    } catch {
      toast.error('Failed to save product');
    } finally {
      setSaving(false);
    }
  };

  const activeCount = products.filter((p) => p.status === 'active').length;
  const pendingCount = products.filter((p) => p.status === 'pending_approval').length;
  const outOfStock = products.filter((p) => p.stockQuantity === 0).length;

  return (
    <div className="p-6 space-y-5 animate-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="page-title">Products</h1>
          <p className="page-subtitle">{products.length} products in your catalog</p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={loadProducts} className="btn-secondary flex items-center gap-2" title="Refresh">
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
            onClick={isCompliant ? () => { setEditing(null); setShowModal(true); } : handleBlockedAction}
            className={clsx('btn-primary flex items-center gap-2', !isCompliant && 'opacity-70')}
          >
            {!isCompliant && <Lock size={12} />}
            <Plus size={16} /> Add Product
          </button>
        </div>
      </div>

      {/* Compliance Warning Banner (always visible when not compliant) */}
      {complianceChecked && !isCompliant && (
        <div className="bg-red-50 border-2 border-red-200 rounded-2xl p-5 flex items-center gap-4 animate-in">
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

        <div className="flex items-center gap-1 bg-white border border-gray-200 rounded-xl p-1 ml-auto">
          <button onClick={() => setViewMode('list')} className={clsx('p-2 rounded-lg transition-all', viewMode === 'list' ? 'bg-primary text-white' : 'text-gray-400 hover:text-gray-600')}>
            <List size={15} />
          </button>
          <button onClick={() => setViewMode('grid')} className={clsx('p-2 rounded-lg transition-all', viewMode === 'grid' ? 'bg-primary text-white' : 'text-gray-400 hover:text-gray-600')}>
            <LayoutGrid size={15} />
          </button>
        </div>
      </div>

      {/* Grid View */}
      {viewMode === 'grid' && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          {filtered.length === 0 ? (
            <div className="col-span-4">
              <EmptyState
                title="No products found"
                description="Add your first product to start selling on SpazaSure."
                icon={<Package size={40} />}
                action={<button onClick={() => setShowModal(true)} className="btn-primary flex items-center gap-2"><Plus size={16} />Add Product</button>}
              />
            </div>
          ) : filtered.map((p) => (
            <div key={p.id} className="card overflow-hidden hover:shadow-card-hover transition-all duration-200 group">
              <div className="relative">
                <img src={p.imageUrl} alt={p.name} className="w-full h-36 object-cover bg-gray-50" />
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
                <div className="flex items-center gap-1.5 mt-3 pt-3 border-t border-gray-100">
                  <button onClick={() => { setEditing(p); setShowModal(true); }} className="flex-1 btn-secondary text-xs py-1.5 flex items-center justify-center gap-1" disabled={!isCompliant}>
                    {isCompliant ? <Edit2 size={12} /> : <Lock size={12} />} Edit
                  </button>
                  <button onClick={() => setBarcodeProduct(p)} className="p-1.5 text-emerald-600 hover:bg-emerald-50 rounded-lg transition-colors" title="View QR & Barcode">
                    <QrCode size={16} />
                  </button>
                  <button onClick={() => handleToggle(p.id)} className={clsx('p-1.5 rounded-lg transition-colors', p.isAvailable ? 'text-emerald-600 hover:bg-emerald-50' : 'text-gray-400 hover:bg-gray-100')}>
                    {p.isAvailable ? <ToggleRight size={18} /> : <ToggleLeft size={18} />}
                  </button>
                  <button onClick={() => handleDelete(p.id)} className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors">
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* List View */}
      {viewMode === 'list' && (
        <div className="card overflow-hidden">
          {filtered.length === 0 ? (
            <EmptyState
              title="No products found"
              description="Add your first product to start selling on SpazaSure."
              icon={<Package size={40} />}
              action={<button onClick={() => setShowModal(true)} className="btn-primary flex items-center gap-2"><Plus size={16} />Add Product</button>}
            />
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50/80">
                  <th className="table-header">Product</th>
                  <th className="table-header">SKU</th>
                  <th className="table-header">Category</th>
                  <th className="table-header text-right">Price</th>
                  <th className="table-header text-right">Stock</th>
                  <th className="table-header text-center">Status</th>
                  <th className="table-header text-center">Available</th>
                  <th className="table-header text-center">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((p) => (
                  <tr key={p.id} className="table-row">
                    <td className="table-cell">
                      <div className="flex items-center gap-3">
                        <img src={p.imageUrl} alt={p.name} className="w-11 h-11 rounded-xl object-cover bg-gray-100 flex-shrink-0" />
                        <div>
                          <p className="font-semibold text-gray-900">{p.name}</p>
                          <div className="flex items-center gap-1 mt-0.5">
                            {'★'.repeat(Math.round(p.rating))}{'☆'.repeat(5 - Math.round(p.rating))}
                            <span className="text-[10px] text-gray-400">({p.reviewCount})</span>
                          </div>
                        </div>
                      </div>
                    </td>
                    <td className="table-cell text-gray-400 font-mono text-xs">{p.sku}</td>
                    <td className="table-cell">
                      <span className="text-xs bg-gray-100 text-gray-600 px-2 py-1 rounded-lg font-medium">{p.categoryName}</span>
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
                      <button onClick={() => handleToggle(p.id)} className={clsx('transition-colors', p.isAvailable ? 'text-emerald-600' : 'text-gray-300')}>
                        {p.isAvailable ? <ToggleRight size={24} /> : <ToggleLeft size={24} />}
                      </button>
                    </td>
                    <td className="table-cell">
                      <div className="flex items-center justify-center gap-1">
                        <button onClick={isCompliant ? () => { setEditing(p); setShowModal(true); } : handleBlockedAction} className="btn-icon hover:text-primary hover:bg-primary-50" title={isCompliant ? "Edit" : "Documents required"}>
                          {isCompliant ? <Edit2 size={14} /> : <Lock size={14} />}
                        </button>
                        <button onClick={() => setBarcodeProduct(p)} className="btn-icon hover:text-emerald-600 hover:bg-emerald-50" title="View QR & Barcode">
                          <QrCode size={14} />
                        </button>
                        <button onClick={isCompliant ? () => handleDelete(p.id) : handleBlockedAction} className="btn-icon hover:text-red-500 hover:bg-red-50" title={isCompliant ? "Delete" : "Documents required"}>
                          <Trash2 size={14} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {showModal && isCompliant && (
        <ProductFormModal
          product={editing}
          onSave={handleSave}
          onClose={() => { setShowModal(false); setEditing(null); }}
        />
      )}

      {barcodeProduct && (
        <BarcodeModal
          product={barcodeProduct}
          onClose={() => setBarcodeProduct(null)}
        />
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
