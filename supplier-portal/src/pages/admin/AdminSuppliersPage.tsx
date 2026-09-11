import { useState, useEffect, useCallback } from 'react';
import {
  Search, CheckCircle, Clock, Eye, ShieldCheck, Users, Store,
  X, Phone, Mail, MapPin, FileText, Calendar, Ban, ExternalLink, Download,
} from 'lucide-react';
import { format } from 'date-fns';
import clsx from 'clsx';
import toast from 'react-hot-toast';
import { adminSuppliersApi, adminSpazaOwnersApi, resolveUploadUrl } from '../../services/api';
import { TierBadge, Spinner } from '../../components/ui';

type MainTab = 'suppliers' | 'spaza-owners';
type FilterStatus = 'all' | 'verified' | 'pending';

interface SupplierDoc {
  docType: string;
  label: string;
  docUrl: string;
  status: 'pending' | 'approved' | 'rejected';
  uploadedAt: string;
  expiryDate?: string;
}

interface Supplier {
  id: string;
  companyName: string;
  contactName: string;
  email: string;
  phone: string;
  tier: string;
  isVerified: boolean;
  joinedAt: string;
  totalOrders: number;
  totalRevenue: number;
  documents: SupplierDoc[];
}

interface SpazaOwner {
  id: string;
  shopName: string;
  ownerName: string;
  email: string;
  phone: string;
  location: string;
  isVerified: boolean;
  joinedAt: string;
  totalOrders: number;
  totalSpent: number;
  documents: SupplierDoc[];
}

export default function AdminSuppliersPage() {
  const [mainTab, setMainTab] = useState<MainTab>('suppliers');
  const [loading, setLoading] = useState(true);

  // Supplier state
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [supplierSearch, setSupplierSearch] = useState('');
  const [supplierFilter, setSupplierFilter] = useState<FilterStatus>('all');
  const [selectedSupplier, setSelectedSupplier] = useState<Supplier | null>(null);

  // Spaza Owner state
  const [spazaOwners, setSpazaOwners] = useState<SpazaOwner[]>([]);
  const [spazaSearch, setSpazaSearch] = useState('');
  const [spazaFilter, setSpazaFilter] = useState<FilterStatus>('all');
  const [selectedSpaza, setSelectedSpaza] = useState<SpazaOwner | null>(null);

  // Suspend modal state
  const [suspendTarget, setSuspendTarget] = useState<{ id: string; type: 'supplier' | 'spaza' } | null>(null);
  const [suspendReason, setSuspendReason] = useState('');

  // ─── Data Fetching ────────────────────────────────────────────────────────────
  const fetchSuppliers = useCallback(async () => {
    try {
      setLoading(true);
      const params: Record<string, string> = {};
      if (supplierSearch) params.search = supplierSearch;
      if (supplierFilter !== 'all') params.status = supplierFilter;
      const res = await adminSuppliersApi.list(params);
      const raw = res?.data ?? res;
      const items = Array.isArray(raw) ? raw : raw?.items ?? raw?.suppliers ?? [];
      setSuppliers(items.map((s: any) => ({
        id: s.id,
        companyName: s.companyName ?? '',
        contactName: s.contactPerson ?? s.contactName ?? '',
        email: s.email ?? '',
        phone: s.phone ?? '',
        tier: s.tier ?? 'basic',
        isVerified: s.isVerified ?? s.status === 'verified',
        joinedAt: s.joinedAt ?? s.createdAt ?? new Date().toISOString(),
        totalOrders: s.totalOrders ?? 0,
        totalRevenue: s.totalRevenue ?? 0,
        documents: (s.documents ?? []).map((d: any) => ({
          docType: d.docType,
          label: d.docType,
          docUrl: d.docUrl ?? '',
          status: d.status,
          uploadedAt: d.createdAt ?? d.uploadedAt ?? new Date().toISOString(),
          expiryDate: d.expiryDate,
        })),
      })));
    } catch {
      toast.error('Failed to load suppliers');
    } finally {
      setLoading(false);
    }
  }, [supplierSearch, supplierFilter]);

  const fetchSpazaOwners = useCallback(async () => {
    try {
      setLoading(true);
      const params: Record<string, string> = {};
      if (spazaSearch) params.search = spazaSearch;
      if (spazaFilter !== 'all') params.status = spazaFilter;
      const res = await adminSpazaOwnersApi.list(params);
      const data = res?.data ?? res;
      const items = Array.isArray(data) ? data : data?.items ?? data?.owners ?? [];
      // Map backend fields to frontend interface
      setSpazaOwners(items.map((o: any) => ({
        id: o.id,
        shopName: o.shopName ?? '',
        ownerName: o.ownerName ?? '',
        email: o.email ?? '',
        phone: o.phone ?? '',
        location: [o.address, o.city, o.province].filter(Boolean).join(', ') || o.location || '',
        isVerified: o.isVerified ?? o.status === 'verified',
        joinedAt: o.joinedAt ?? o.createdAt ?? '',
        totalOrders: o.totalOrders ?? 0,
        totalSpent: o.totalSpent ?? 0,
        documents: (o.documents ?? []).map((d: any) => ({
          id: d.id,
          docType: d.docType,
          docUrl: d.docUrl,
          status: d.status,
          expiryDate: d.expiryDate,
          rejectionReason: d.rejectionNote ?? d.rejectionReason,
          uploadedAt: d.createdAt ?? d.uploadedAt ?? new Date().toISOString(),
          createdAt: d.createdAt,
        })),
      })));
    } catch {
      toast.error('Failed to load spaza owners');
    } finally {
      setLoading(false);
    }
  }, [spazaSearch, spazaFilter]);

  useEffect(() => {
    if (mainTab === 'suppliers') fetchSuppliers();
    else fetchSpazaOwners();
  }, [mainTab, fetchSuppliers, fetchSpazaOwners]);

  // ─── Actions ──────────────────────────────────────────────────────────────────
  const verifySupplier = async (id: string) => {
    try {
      await adminSuppliersApi.verify(id);
      toast.success('Supplier verified successfully');
      setSelectedSupplier(null);
      fetchSuppliers();
    } catch {
      toast.error('Failed to verify supplier');
    }
  };

  const verifySpazaOwner = async (id: string) => {
    try {
      await adminSpazaOwnersApi.verify(id);
      toast.success('Spaza owner verified successfully');
      setSelectedSpaza(null);
      fetchSpazaOwners();
    } catch {
      toast.error('Failed to verify spaza owner');
    }
  };

  const handleSuspend = async () => {
    if (!suspendTarget) return;
    try {
      if (suspendTarget.type === 'supplier') {
        await adminSuppliersApi.suspend(suspendTarget.id, suspendReason);
        toast.success('Supplier suspended');
        setSelectedSupplier(null);
        fetchSuppliers();
      } else {
        await adminSpazaOwnersApi.suspend(suspendTarget.id, suspendReason);
        toast.success('Spaza owner suspended');
        setSelectedSpaza(null);
        fetchSpazaOwners();
      }
    } catch {
      toast.error('Failed to suspend account');
    } finally {
      setSuspendTarget(null);
      setSuspendReason('');
    }
  };

  // ─── Filtering ────────────────────────────────────────────────────────────────
  const filteredSuppliers = suppliers.filter((s) => {
    const matchSearch = !supplierSearch ||
      s.companyName.toLowerCase().includes(supplierSearch.toLowerCase()) ||
      s.email.toLowerCase().includes(supplierSearch.toLowerCase());
    const matchFilter = supplierFilter === 'all' ||
      (supplierFilter === 'verified' ? s.isVerified : !s.isVerified);
    return matchSearch && matchFilter;
  });

  const filteredSpazaOwners = spazaOwners.filter((o) => {
    const matchSearch = !spazaSearch ||
      o.shopName.toLowerCase().includes(spazaSearch.toLowerCase()) ||
      o.ownerName.toLowerCase().includes(spazaSearch.toLowerCase()) ||
      o.email.toLowerCase().includes(spazaSearch.toLowerCase());
    const matchFilter = spazaFilter === 'all' ||
      (spazaFilter === 'verified' ? o.isVerified : !o.isVerified);
    return matchSearch && matchFilter;
  });

  const totalSupplierVerified = suppliers.filter((s) => s.isVerified).length;
  const totalSupplierPending = suppliers.filter((s) => !s.isVerified).length;
  const totalSpazaVerified = spazaOwners.filter((o) => o.isVerified).length;
  const totalSpazaPending = spazaOwners.filter((o) => !o.isVerified).length;

  if (loading && suppliers.length === 0 && spazaOwners.length === 0) {
    return <div className="flex items-center justify-center h-64"><Spinner /></div>;
  }

  return (
    <>
      <style>{`
        @keyframes slideUp {
          from { opacity: 0; transform: translateY(16px) scale(0.97); }
          to { opacity: 1; transform: translateY(0) scale(1); }
        }
        .modal-slide-up { animation: slideUp 0.25s cubic-bezier(0.16, 1, 0.3, 1) forwards; }
      `}</style>

      <div className="p-6 space-y-6 animate-in">
        {/* ─── Header ─── */}
        <div className="flex items-center gap-4">
          <div className="w-11 h-11 bg-gradient-to-br from-blue-600 to-indigo-700 rounded-xl flex items-center justify-center shadow-lg shadow-blue-600/20">
            <Users size={20} className="text-white" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Suppliers & Spaza Owners</h1>
            <p className="text-sm text-gray-500">Manage suppliers and spaza shop owners on the platform</p>
          </div>
        </div>

        {/* ─── Tab Toggle ─── */}
        <div className="bg-gray-100/80 rounded-2xl p-1.5 w-fit">
          <div className="flex items-center gap-1">
            <button
              onClick={() => setMainTab('suppliers')}
              className={clsx(
                'flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-semibold transition-all',
                mainTab === 'suppliers'
                  ? 'bg-white text-gray-900 shadow-sm ring-1 ring-gray-200/50'
                  : 'text-gray-500 hover:text-gray-700'
              )}
            >
              <Users size={16} />
              Suppliers
            </button>
            <button
              onClick={() => setMainTab('spaza-owners')}
              className={clsx(
                'flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-semibold transition-all',
                mainTab === 'spaza-owners'
                  ? 'bg-white text-gray-900 shadow-sm ring-1 ring-gray-200/50'
                  : 'text-gray-500 hover:text-gray-700'
              )}
            >
              <Store size={16} />
              Spaza Owners
            </button>
          </div>
        </div>

        {/* ─── SUPPLIERS TAB ─── */}
        {mainTab === 'suppliers' && (
          <>
            {/* Stats Cards */}
            <div className="grid grid-cols-3 gap-4">
              <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-blue-50 to-transparent rounded-bl-[40px]" />
                <div className="relative flex items-center gap-3">
                  <div className="w-10 h-10 bg-blue-50 rounded-xl flex items-center justify-center">
                    <Users size={18} className="text-blue-600" />
                  </div>
                  <div>
                    <p className="text-2xl font-black text-gray-900 tabular-nums">{suppliers.length}</p>
                    <p className="text-xs text-gray-500 font-semibold">Total Suppliers</p>
                  </div>
                </div>
              </div>
              <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-emerald-50 to-transparent rounded-bl-[40px]" />
                <div className="relative flex items-center gap-3">
                  <div className="w-10 h-10 bg-emerald-50 rounded-xl flex items-center justify-center">
                    <CheckCircle size={18} className="text-emerald-600" />
                  </div>
                  <div>
                    <p className="text-2xl font-black text-gray-900 tabular-nums">{totalSupplierVerified}</p>
                    <p className="text-xs text-gray-500 font-semibold">Verified</p>
                  </div>
                </div>
              </div>
              <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-amber-50 to-transparent rounded-bl-[40px]" />
                <div className="relative flex items-center gap-3">
                  <div className="w-10 h-10 bg-amber-50 rounded-xl flex items-center justify-center">
                    <Clock size={18} className="text-amber-600" />
                  </div>
                  <div>
                    <p className="text-2xl font-black text-gray-900 tabular-nums">{totalSupplierPending}</p>
                    <p className="text-xs text-gray-500 font-semibold">Pending Verification</p>
                  </div>
                </div>
              </div>
            </div>

            {/* Search & Filters */}
            <div className="flex items-center gap-3">
              <div className="relative flex-1 max-w-sm">
                <Search size={15} className="absolute left-4 top-3.5 text-gray-300" />
                <input
                  value={supplierSearch}
                  onChange={(e) => setSupplierSearch(e.target.value)}
                  className="w-full border border-gray-200 rounded-2xl pl-11 pr-4 py-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-100 focus:border-blue-300 transition-all placeholder:text-gray-300 font-medium"
                  placeholder="Search by name or email..."
                />
              </div>
              <div className="flex items-center gap-1.5">
                {(['all', 'verified', 'pending'] as FilterStatus[]).map((f) => (
                  <button
                    key={f}
                    onClick={() => setSupplierFilter(f)}
                    className={clsx(
                      'px-4 py-2.5 rounded-xl text-xs font-semibold capitalize transition-all border',
                      supplierFilter === f
                        ? 'bg-gray-900 text-white border-gray-900 shadow-sm'
                        : 'bg-white text-gray-500 border-gray-200 hover:border-gray-300'
                    )}
                  >
                    {f}
                  </button>
                ))}
              </div>
            </div>

            {/* Table */}
            <div className="rounded-2xl bg-white border border-gray-100 shadow-sm overflow-hidden">
              {filteredSuppliers.length === 0 ? (
                <div className="p-16 text-center">
                  <div className="w-14 h-14 bg-gray-50 rounded-2xl flex items-center justify-center mx-auto mb-4">
                    <Users size={24} className="text-gray-300" />
                  </div>
                  <p className="font-bold text-gray-600">No suppliers found</p>
                  <p className="text-sm text-gray-400 mt-1">Try adjusting your filters</p>
                </div>
              ) : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100 bg-gray-50/40">
                      <th className="text-left px-5 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Supplier</th>
                      <th className="text-left px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Tier</th>
                      <th className="text-right px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Orders</th>
                      <th className="text-right px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Revenue</th>
                      <th className="text-center px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Documents</th>
                      <th className="text-center px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Status</th>
                      <th className="text-left px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Joined</th>
                      <th className="text-center px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredSuppliers.map((s) => (
                      <tr key={s.id} className="group border-b border-gray-50 last:border-0 hover:bg-gray-50/60 transition-colors">
                        <td className="px-5 py-3.5">
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-2xl bg-gradient-to-br from-slate-100 to-slate-50 ring-1 ring-gray-200/60 flex items-center justify-center font-bold text-slate-600 text-sm flex-shrink-0">
                              {s.companyName.charAt(0)}
                            </div>
                            <div>
                              <p className="font-semibold text-gray-900">{s.companyName}</p>
                              <p className="text-xs text-gray-400">{s.email}</p>
                            </div>
                          </div>
                        </td>
                        <td className="px-4 py-3.5"><TierBadge tier={s.tier} /></td>
                        <td className="px-4 py-3.5 text-right font-bold tabular-nums text-gray-700">{s.totalOrders}</td>
                        <td className="px-4 py-3.5 text-right font-bold text-emerald-700 tabular-nums">R{(s.totalRevenue ?? 0).toLocaleString()}</td>
                        <td className="px-4 py-3.5 text-center">
                          {(() => {
                            const required = ['cipc_certificate', 'tax_clearance', 'bee_certificate', 'product_license'];
                            const docs = s.documents ?? [];
                            const submitted = docs.length;
                            const approved = docs.filter((d) => d.status === 'approved').length;
                            const pending = docs.filter((d) => d.status === 'pending').length;
                            const rejected = docs.filter((d) => d.status === 'rejected').length;
                            const outstanding = required.length - submitted;
                            return (
                              <div className="flex flex-col items-center gap-1">
                                <div className="flex items-center gap-1">
                                  <span className="text-[10px] font-bold text-gray-500">{submitted}/{required.length}</span>
                                  <span className="text-[10px] text-gray-400">submitted</span>
                                </div>
                                <div className="flex items-center gap-1 flex-wrap justify-center">
                                  {approved > 0 && (
                                    <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-1.5 py-0.5 rounded-full">{approved} ✓</span>
                                  )}
                                  {pending > 0 && (
                                    <span className="text-[10px] font-bold text-amber-700 bg-amber-50 border border-amber-200 px-1.5 py-0.5 rounded-full">{pending} ⏳</span>
                                  )}
                                  {rejected > 0 && (
                                    <span className="text-[10px] font-bold text-red-600 bg-red-50 border border-red-200 px-1.5 py-0.5 rounded-full">{rejected} ✗</span>
                                  )}
                                  {outstanding > 0 && (
                                    <span className="text-[10px] font-bold text-slate-600 bg-slate-100 border border-slate-200 px-1.5 py-0.5 rounded-full">{outstanding} missing</span>
                                  )}
                                </div>
                              </div>
                            );
                          })()}
                        </td>
                        <td className="px-4 py-3.5 text-center">
                          {s.isVerified ? (
                            <span className="inline-flex items-center gap-1 text-xs font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-2.5 py-1 rounded-full">
                              <CheckCircle size={11} /> Verified
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 text-xs font-bold text-amber-700 bg-amber-50 border border-amber-200 px-2.5 py-1 rounded-full">
                              <Clock size={11} /> Pending
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3.5 text-xs text-gray-400 font-medium">{format(new Date(s.joinedAt), 'dd MMM yyyy')}</td>
                        <td className="px-4 py-3.5">
                          <div className="flex items-center justify-center gap-1">
                            <button
                              onClick={() => setSelectedSupplier(s)}
                              className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
                              title="View Details"
                            >
                              <Eye size={14} />
                            </button>
                            {!s.isVerified && (
                              <button
                                onClick={() => verifySupplier(s.id)}
                                className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-emerald-600 hover:bg-emerald-50 transition-all"
                                title="Verify Supplier"
                              >
                                <ShieldCheck size={14} />
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </>
        )}

        {/* ─── SPAZA OWNERS TAB ─── */}
        {mainTab === 'spaza-owners' && (
          <>
            {/* Stats Cards */}
            <div className="grid grid-cols-3 gap-4">
              <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-purple-50 to-transparent rounded-bl-[40px]" />
                <div className="relative flex items-center gap-3">
                  <div className="w-10 h-10 bg-purple-50 rounded-xl flex items-center justify-center">
                    <Store size={18} className="text-purple-600" />
                  </div>
                  <div>
                    <p className="text-2xl font-black text-gray-900 tabular-nums">{spazaOwners.length}</p>
                    <p className="text-xs text-gray-500 font-semibold">Total Spaza Owners</p>
                  </div>
                </div>
              </div>
              <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-emerald-50 to-transparent rounded-bl-[40px]" />
                <div className="relative flex items-center gap-3">
                  <div className="w-10 h-10 bg-emerald-50 rounded-xl flex items-center justify-center">
                    <CheckCircle size={18} className="text-emerald-600" />
                  </div>
                  <div>
                    <p className="text-2xl font-black text-gray-900 tabular-nums">{totalSpazaVerified}</p>
                    <p className="text-xs text-gray-500 font-semibold">Verified</p>
                  </div>
                </div>
              </div>
              <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow group">
                <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-amber-50 to-transparent rounded-bl-[40px]" />
                <div className="relative flex items-center gap-3">
                  <div className="w-10 h-10 bg-amber-50 rounded-xl flex items-center justify-center">
                    <Clock size={18} className="text-amber-600" />
                  </div>
                  <div>
                    <p className="text-2xl font-black text-gray-900 tabular-nums">{totalSpazaPending}</p>
                    <p className="text-xs text-gray-500 font-semibold">Pending Verification</p>
                  </div>
                </div>
              </div>
            </div>

            {/* Search & Filters */}
            <div className="flex items-center gap-3">
              <div className="relative flex-1 max-w-sm">
                <Search size={15} className="absolute left-4 top-3.5 text-gray-300" />
                <input
                  value={spazaSearch}
                  onChange={(e) => setSpazaSearch(e.target.value)}
                  className="w-full border border-gray-200 rounded-2xl pl-11 pr-4 py-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-100 focus:border-blue-300 transition-all placeholder:text-gray-300 font-medium"
                  placeholder="Search by shop name, owner or email..."
                />
              </div>
              <div className="flex items-center gap-1.5">
                {(['all', 'verified', 'pending'] as FilterStatus[]).map((f) => (
                  <button
                    key={f}
                    onClick={() => setSpazaFilter(f)}
                    className={clsx(
                      'px-4 py-2.5 rounded-xl text-xs font-semibold capitalize transition-all border',
                      spazaFilter === f
                        ? 'bg-gray-900 text-white border-gray-900 shadow-sm'
                        : 'bg-white text-gray-500 border-gray-200 hover:border-gray-300'
                    )}
                  >
                    {f}
                  </button>
                ))}
              </div>
            </div>

            {/* Table */}
            <div className="rounded-2xl bg-white border border-gray-100 shadow-sm overflow-hidden">
              {filteredSpazaOwners.length === 0 ? (
                <div className="p-16 text-center">
                  <div className="w-14 h-14 bg-gray-50 rounded-2xl flex items-center justify-center mx-auto mb-4">
                    <Store size={24} className="text-gray-300" />
                  </div>
                  <p className="font-bold text-gray-600">No spaza owners found</p>
                  <p className="text-sm text-gray-400 mt-1">Try adjusting your filters</p>
                </div>
              ) : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100 bg-gray-50/40">
                      <th className="text-left px-5 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Shop</th>
                      <th className="text-left px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Owner</th>
                      <th className="text-left px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Location</th>
                      <th className="text-right px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Orders</th>
                      <th className="text-right px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Total Spent</th>
                      <th className="text-center px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Documents</th>
                      <th className="text-center px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Status</th>
                      <th className="text-left px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Joined</th>
                      <th className="text-center px-4 py-3.5 text-[11px] font-bold text-gray-400 uppercase tracking-wider">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredSpazaOwners.map((o) => (
                      <tr key={o.id} className="group border-b border-gray-50 last:border-0 hover:bg-gray-50/60 transition-colors">
                        <td className="px-5 py-3.5">
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-2xl bg-gradient-to-br from-purple-50 to-purple-100/50 ring-1 ring-purple-200/60 flex items-center justify-center font-bold text-purple-600 text-sm flex-shrink-0">
                              {o.shopName.charAt(0)}
                            </div>
                            <div>
                              <p className="font-semibold text-gray-900">{o.shopName}</p>
                              <p className="text-xs text-gray-400">{o.email}</p>
                            </div>
                          </div>
                        </td>
                        <td className="px-4 py-3.5 font-medium text-gray-700">{o.ownerName}</td>
                        <td className="px-4 py-3.5 text-xs text-gray-500">{o.location}</td>
                        <td className="px-4 py-3.5 text-right font-bold tabular-nums text-gray-700">{o.totalOrders}</td>
                        <td className="px-4 py-3.5 text-right font-bold text-emerald-700 tabular-nums">R{(o.totalSpent ?? 0).toLocaleString()}</td>
                        <td className="px-4 py-3.5 text-center">
                          {(() => {
                            const required = ['cipc_certificate', 'tax_clearance', 'bee_certificate', 'product_license'];
                            const docs = o.documents ?? [];
                            const submitted = docs.length;
                            const approved = docs.filter((d) => d.status === 'approved').length;
                            const pending = docs.filter((d) => d.status === 'pending').length;
                            const rejected = docs.filter((d) => d.status === 'rejected').length;
                            const outstanding = required.length - submitted;
                            return (
                              <div className="flex flex-col items-center gap-1">
                                <div className="flex items-center gap-1">
                                  <span className="text-[10px] font-bold text-gray-500">{submitted}/{required.length}</span>
                                  <span className="text-[10px] text-gray-400">submitted</span>
                                </div>
                                <div className="flex items-center gap-1 flex-wrap justify-center">
                                  {approved > 0 && (
                                    <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-1.5 py-0.5 rounded-full">{approved} ✓</span>
                                  )}
                                  {pending > 0 && (
                                    <span className="text-[10px] font-bold text-amber-700 bg-amber-50 border border-amber-200 px-1.5 py-0.5 rounded-full">{pending} ⏳</span>
                                  )}
                                  {rejected > 0 && (
                                    <span className="text-[10px] font-bold text-red-600 bg-red-50 border border-red-200 px-1.5 py-0.5 rounded-full">{rejected} ✗</span>
                                  )}
                                  {outstanding > 0 && (
                                    <span className="text-[10px] font-bold text-slate-600 bg-slate-100 border border-slate-200 px-1.5 py-0.5 rounded-full">{outstanding} missing</span>
                                  )}
                                </div>
                              </div>
                            );
                          })()}
                        </td>
                        <td className="px-4 py-3.5 text-center">
                          {o.isVerified ? (
                            <span className="inline-flex items-center gap-1 text-xs font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-2.5 py-1 rounded-full">
                              <CheckCircle size={11} /> Verified
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 text-xs font-bold text-amber-700 bg-amber-50 border border-amber-200 px-2.5 py-1 rounded-full">
                              <Clock size={11} /> Pending
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3.5 text-xs text-gray-400 font-medium">{format(new Date(o.joinedAt), 'dd MMM yyyy')}</td>
                        <td className="px-4 py-3.5">
                          <div className="flex items-center justify-center gap-1">
                            <button
                              onClick={() => setSelectedSpaza(o)}
                              className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
                              title="View Details"
                            >
                              <Eye size={14} />
                            </button>
                            {!o.isVerified && (
                              <button
                                onClick={() => verifySpazaOwner(o.id)}
                                className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-emerald-600 hover:bg-emerald-50 transition-all"
                                title="Verify Spaza Owner"
                              >
                                <ShieldCheck size={14} />
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </>
        )}

        {/* ─── SUPPLIER DETAIL MODAL ─── */}
        {selectedSupplier && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
            <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setSelectedSupplier(null)} />
            <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-2xl max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
              {/* Gradient Header */}
              <div className="relative bg-gradient-to-r from-blue-600 to-indigo-700 px-6 py-5 flex-shrink-0">
                <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAiIGhlaWdodD0iNDAiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PGNpcmNsZSBjeD0iMjAiIGN5PSIyMCIgcj0iMSIgZmlsbD0icmdiYSgyNTUsMjU1LDI1NSwwLjA1KSIvPjwvc3ZnPg==')] opacity-60" />
                <div className="relative flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-11 h-11 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center font-bold text-white text-lg ring-1 ring-white/30">
                      {selectedSupplier.companyName.charAt(0)}
                    </div>
                    <div>
                      <p className="font-bold text-white text-lg">{selectedSupplier.companyName}</p>
                      <p className="text-sm text-blue-100">{selectedSupplier.contactName}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {selectedSupplier.isVerified ? (
                      <span className="inline-flex items-center gap-1 text-xs font-bold text-white bg-white/20 backdrop-blur-sm border border-white/30 px-2.5 py-1 rounded-full">
                        <CheckCircle size={11} /> Verified
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-xs font-bold text-white bg-white/20 backdrop-blur-sm border border-white/30 px-2.5 py-1 rounded-full">
                        <Clock size={11} /> Pending
                      </span>
                    )}
                    <button onClick={() => setSelectedSupplier(null)} className="p-2 rounded-lg text-white/70 hover:text-white hover:bg-white/10 transition-all">
                      <X size={16} />
                    </button>
                  </div>
                </div>
              </div>

              {/* Body */}
              <div className="flex-1 overflow-y-auto p-6 space-y-6">
                {/* Contact Info */}
                <div className="grid grid-cols-2 gap-3">
                  <div className="flex items-center gap-3 p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                    <Mail size={15} className="text-gray-400" />
                    <div>
                      <p className="text-[11px] text-gray-400 font-semibold uppercase tracking-wide">Email</p>
                      <p className="text-sm font-medium text-gray-900">{selectedSupplier.email}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                    <Phone size={15} className="text-gray-400" />
                    <div>
                      <p className="text-[11px] text-gray-400 font-semibold uppercase tracking-wide">Phone</p>
                      <p className="text-sm font-medium text-gray-900">{selectedSupplier.phone}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                    <Calendar size={15} className="text-gray-400" />
                    <div>
                      <p className="text-[11px] text-gray-400 font-semibold uppercase tracking-wide">Joined</p>
                      <p className="text-sm font-medium text-gray-900">{format(new Date(selectedSupplier.joinedAt), 'dd MMM yyyy')}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                    <Users size={15} className="text-gray-400" />
                    <div>
                      <p className="text-[11px] text-gray-400 font-semibold uppercase tracking-wide">Tier</p>
                      <TierBadge tier={selectedSupplier.tier} />
                    </div>
                  </div>
                </div>

                {/* Stats */}
                <div className="grid grid-cols-2 gap-3">
                  <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 text-center shadow-sm">
                    <div className="absolute top-0 right-0 w-16 h-16 bg-gradient-to-bl from-blue-50 to-transparent rounded-bl-[32px]" />
                    <p className="relative text-2xl font-black text-gray-900 tabular-nums">{selectedSupplier.totalOrders}</p>
                    <p className="relative text-xs text-gray-500 font-semibold mt-1">Total Orders</p>
                  </div>
                  <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 text-center shadow-sm">
                    <div className="absolute top-0 right-0 w-16 h-16 bg-gradient-to-bl from-emerald-50 to-transparent rounded-bl-[32px]" />
                    <p className="relative text-2xl font-black text-emerald-700 tabular-nums">R{(selectedSupplier.totalRevenue ?? 0).toLocaleString()}</p>
                    <p className="relative text-xs text-gray-500 font-semibold mt-1">Total Revenue</p>
                  </div>
                </div>

                {/* Documents */}
                {selectedSupplier.documents && selectedSupplier.documents.length > 0 && (
                  <div>
                    <h3 className="text-sm font-bold text-gray-900 mb-3 flex items-center gap-2">
                      <FileText size={14} className="text-gray-400" /> Documents ({selectedSupplier.documents.length}/4 submitted)
                    </h3>
                    <div className="space-y-2">
                      {selectedSupplier.documents.map((doc, i) => {
                        const docLabels: Record<string, string> = {
                          cipc_certificate: 'CIPC Certificate',
                          tax_clearance: 'Tax Clearance Certificate',
                          bee_certificate: 'BEE Certificate',
                          product_license: 'Product License',
                        };
                        const resolvedUrl = resolveUploadUrl(doc.docUrl);
                        return (
                          <div key={i} className="flex items-center justify-between p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                            <div className="flex items-center gap-3">
                              <div className="w-9 h-9 bg-white rounded-lg flex items-center justify-center border border-gray-200 shadow-sm">
                                <FileText size={14} className="text-gray-400" />
                              </div>
                              <div>
                                <p className="text-sm font-semibold text-gray-900">{docLabels[doc.docType] ?? doc.label ?? doc.docType}</p>
                                <p className="text-xs text-gray-400">Uploaded {format(new Date(doc.uploadedAt), 'dd MMM yyyy')}</p>
                              </div>
                            </div>
                            <div className="flex items-center gap-2">
                              <span className={clsx('text-xs font-bold px-2.5 py-1 rounded-full border capitalize',
                                doc.status === 'approved' && 'bg-emerald-50 text-emerald-700 border-emerald-200',
                                doc.status === 'pending' && 'bg-amber-50 text-amber-700 border-amber-200',
                                doc.status === 'rejected' && 'bg-red-50 text-red-700 border-red-200',
                              )}>
                                {doc.status}
                              </span>
                              {resolvedUrl && (
                                <>
                                  <a
                                    href={resolvedUrl}
                                    target="_blank"
                                    rel="noreferrer"
                                    className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
                                    title="View Document"
                                  >
                                    <Eye size={14} />
                                  </a>
                                  <a
                                    href={resolvedUrl}
                                    download
                                    className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
                                    title="Download"
                                  >
                                    <Download size={14} />
                                  </a>
                                </>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>

              {/* Footer Actions */}
              <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-gray-100 flex-shrink-0 bg-gray-50/40">
                {selectedSupplier.isVerified && (
                  <button
                    onClick={() => setSuspendTarget({ id: selectedSupplier.id, type: 'supplier' })}
                    className="flex items-center gap-1.5 text-sm font-semibold bg-red-500 text-white px-5 py-2.5 rounded-xl hover:bg-red-600 transition-colors shadow-sm"
                  >
                    <Ban size={14} /> Suspend
                  </button>
                )}
                {!selectedSupplier.isVerified && (
                  <button
                    onClick={() => verifySupplier(selectedSupplier.id)}
                    className="flex items-center gap-1.5 text-sm font-semibold bg-emerald-600 text-white px-5 py-2.5 rounded-xl hover:bg-emerald-700 transition-colors shadow-sm"
                  >
                    <ShieldCheck size={14} /> Verify Supplier
                  </button>
                )}
              </div>
            </div>
          </div>
        )}

        {/* ─── SPAZA OWNER DETAIL MODAL ─── */}
        {selectedSpaza && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
            <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setSelectedSpaza(null)} />
            <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-2xl max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
              {/* Gradient Header */}
              <div className="relative bg-gradient-to-r from-purple-600 to-indigo-700 px-6 py-5 flex-shrink-0">
                <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAiIGhlaWdodD0iNDAiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PGNpcmNsZSBjeD0iMjAiIGN5PSIyMCIgcj0iMSIgZmlsbD0icmdiYSgyNTUsMjU1LDI1NSwwLjA1KSIvPjwvc3ZnPg==')] opacity-60" />
                <div className="relative flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-11 h-11 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center font-bold text-white text-lg ring-1 ring-white/30">
                      {selectedSpaza.shopName.charAt(0)}
                    </div>
                    <div>
                      <p className="font-bold text-white text-lg">{selectedSpaza.shopName}</p>
                      <p className="text-sm text-purple-100">{selectedSpaza.ownerName}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {selectedSpaza.isVerified ? (
                      <span className="inline-flex items-center gap-1 text-xs font-bold text-white bg-white/20 backdrop-blur-sm border border-white/30 px-2.5 py-1 rounded-full">
                        <CheckCircle size={11} /> Verified
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-xs font-bold text-white bg-white/20 backdrop-blur-sm border border-white/30 px-2.5 py-1 rounded-full">
                        <Clock size={11} /> Pending
                      </span>
                    )}
                    <button onClick={() => setSelectedSpaza(null)} className="p-2 rounded-lg text-white/70 hover:text-white hover:bg-white/10 transition-all">
                      <X size={16} />
                    </button>
                  </div>
                </div>
              </div>

              {/* Body */}
              <div className="flex-1 overflow-y-auto p-6 space-y-6">
                {/* Contact Info */}
                <div className="grid grid-cols-2 gap-3">
                  <div className="flex items-center gap-3 p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                    <Mail size={15} className="text-gray-400" />
                    <div>
                      <p className="text-[11px] text-gray-400 font-semibold uppercase tracking-wide">Email</p>
                      <p className="text-sm font-medium text-gray-900">{selectedSpaza.email}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                    <Phone size={15} className="text-gray-400" />
                    <div>
                      <p className="text-[11px] text-gray-400 font-semibold uppercase tracking-wide">Phone</p>
                      <p className="text-sm font-medium text-gray-900">{selectedSpaza.phone}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                    <MapPin size={15} className="text-gray-400" />
                    <div>
                      <p className="text-[11px] text-gray-400 font-semibold uppercase tracking-wide">Location</p>
                      <p className="text-sm font-medium text-gray-900">{selectedSpaza.location}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                    <Calendar size={15} className="text-gray-400" />
                    <div>
                      <p className="text-[11px] text-gray-400 font-semibold uppercase tracking-wide">Joined</p>
                      <p className="text-sm font-medium text-gray-900">{selectedSpaza.joinedAt ? format(new Date(selectedSpaza.joinedAt), 'dd MMM yyyy') : 'N/A'}</p>
                    </div>
                  </div>
                </div>

                {/* Stats */}
                <div className="grid grid-cols-2 gap-3">
                  <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 text-center shadow-sm">
                    <div className="absolute top-0 right-0 w-16 h-16 bg-gradient-to-bl from-purple-50 to-transparent rounded-bl-[32px]" />
                    <p className="relative text-2xl font-black text-gray-900 tabular-nums">{selectedSpaza.totalOrders}</p>
                    <p className="relative text-xs text-gray-500 font-semibold mt-1">Total Orders</p>
                  </div>
                  <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 text-center shadow-sm">
                    <div className="absolute top-0 right-0 w-16 h-16 bg-gradient-to-bl from-emerald-50 to-transparent rounded-bl-[32px]" />
                    <p className="relative text-2xl font-black text-emerald-700 tabular-nums">R{(selectedSpaza.totalSpent ?? 0).toLocaleString()}</p>
                    <p className="relative text-xs text-gray-500 font-semibold mt-1">Total Spent</p>
                  </div>
                </div>

                {/* Documents */}
                {selectedSpaza.documents && selectedSpaza.documents.length > 0 ? (
                  <div>
                    <h3 className="text-sm font-bold text-gray-900 mb-3 flex items-center gap-2">
                      <FileText size={14} className="text-gray-400" /> Documents
                    </h3>
                    <div className="space-y-2">
                      {selectedSpaza.documents.map((doc, i) => {
                        const docLabels: Record<string, string> = {
                          cipc_certificate: 'CIPC Certificate',
                          tax_clearance: 'Tax Clearance Certificate',
                          bee_certificate: 'BEE Certificate',
                          product_license: 'Product License',
                          business_permit: 'Business Permit',
                          health_cert: 'Health Certificate',
                          lease: 'Lease Agreement',
                          id_document: 'ID Document',
                          proof_of_address: 'Proof of Address',
                        };
                        const resolvedUrl = resolveUploadUrl(doc.docUrl);
                        return (
                          <div key={i} className="flex items-center justify-between p-3.5 bg-gray-50/80 rounded-xl border border-gray-100">
                            <div className="flex items-center gap-3">
                              <div className="w-9 h-9 bg-white rounded-lg flex items-center justify-center border border-gray-200 shadow-sm">
                                <FileText size={14} className="text-gray-400" />
                              </div>
                              <div>
                                <p className="text-sm font-semibold text-gray-900">{docLabels[doc.docType] ?? doc.label ?? doc.docType}</p>
                                <p className="text-xs text-gray-400">Uploaded {format(new Date(doc.uploadedAt), 'dd MMM yyyy')}</p>
                              </div>
                            </div>
                            <div className="flex items-center gap-2">
                              <span className={clsx('text-xs font-bold px-2.5 py-1 rounded-full border capitalize',
                                doc.status === 'approved' && 'bg-emerald-50 text-emerald-700 border-emerald-200',
                                doc.status === 'pending' && 'bg-amber-50 text-amber-700 border-amber-200',
                                doc.status === 'rejected' && 'bg-red-50 text-red-700 border-red-200',
                              )}>
                                {doc.status}
                              </span>
                              {resolvedUrl && (
                                <>
                                  <a
                                    href={resolvedUrl}
                                    target="_blank"
                                    rel="noreferrer"
                                    className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
                                    title="View Document"
                                  >
                                    <Eye size={14} />
                                  </a>
                                  <a
                                    href={resolvedUrl}
                                    download
                                    className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
                                    title="Download"
                                  >
                                    <Download size={14} />
                                  </a>
                                </>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ) : (
                  <div>
                    <h3 className="text-sm font-bold text-gray-900 mb-3 flex items-center gap-2">
                      <FileText size={14} className="text-gray-400" /> Documents
                    </h3>
                    <p className="text-sm text-gray-400 italic">No documents uploaded yet</p>
                  </div>
                )}
              </div>

              {/* Footer Actions */}
              <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-gray-100 flex-shrink-0 bg-gray-50/40">
                {selectedSpaza.isVerified && (
                  <button
                    onClick={() => setSuspendTarget({ id: selectedSpaza.id, type: 'spaza' })}
                    className="flex items-center gap-1.5 text-sm font-semibold bg-red-500 text-white px-5 py-2.5 rounded-xl hover:bg-red-600 transition-colors shadow-sm"
                  >
                    <Ban size={14} /> Suspend
                  </button>
                )}
                {!selectedSpaza.isVerified && (
                  <button
                    onClick={() => verifySpazaOwner(selectedSpaza.id)}
                    className="flex items-center gap-1.5 text-sm font-semibold bg-emerald-600 text-white px-5 py-2.5 rounded-xl hover:bg-emerald-700 transition-colors shadow-sm"
                  >
                    <ShieldCheck size={14} /> Verify Spaza Owner
                  </button>
                )}
              </div>
            </div>
          </div>
        )}

        {/* ─── SUSPEND MODAL ─── */}
        {suspendTarget && (
          <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 sm:p-6">
            <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setSuspendTarget(null)} />
            <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-md max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
              {/* Red Gradient Header */}
              <div className="relative bg-gradient-to-r from-red-600 to-rose-700 px-6 py-5">
                <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAiIGhlaWdodD0iNDAiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+PGNpcmNsZSBjeD0iMjAiIGN5PSIyMCIgcj0iMSIgZmlsbD0icmdiYSgyNTUsMjU1LDI1NSwwLjA1KSIvPjwvc3ZnPg==')] opacity-60" />
                <div className="relative flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center ring-1 ring-white/30">
                    <Ban size={18} className="text-white" />
                  </div>
                  <div>
                    <h2 className="font-bold text-white text-lg">Suspend Account</h2>
                    <p className="text-sm text-red-100">Provide a reason for suspending this account</p>
                  </div>
                </div>
              </div>

              {/* Body */}
              <div className="p-6 space-y-4">
                <div>
                  <label className="block text-xs font-bold text-gray-500 uppercase tracking-wide mb-2">Reason</label>
                  <textarea
                    value={suspendReason}
                    onChange={(e) => setSuspendReason(e.target.value)}
                    className="w-full border border-gray-200 rounded-xl px-4 py-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-red-100 focus:border-red-300 transition-all placeholder:text-gray-300 font-medium resize-none"
                    rows={3}
                    placeholder="e.g. Fraudulent activity, violation of terms..."
                  />
                </div>
                <div className="flex gap-3 justify-end pt-2">
                  <button
                    onClick={() => setSuspendTarget(null)}
                    className="px-5 py-2.5 rounded-xl text-sm font-semibold text-gray-600 bg-gray-100 hover:bg-gray-200 transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleSuspend}
                    disabled={!suspendReason.trim()}
                    className="flex items-center gap-1.5 px-5 py-2.5 rounded-xl text-sm font-semibold bg-red-600 text-white hover:bg-red-700 transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <Ban size={14} /> Suspend Account
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </>
  );
}
