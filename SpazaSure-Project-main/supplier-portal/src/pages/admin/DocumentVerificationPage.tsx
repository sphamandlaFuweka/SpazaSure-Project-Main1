import { useState, useEffect } from 'react';
import {
  CheckCircle, XCircle, Clock, Eye, FileText, Search,
  AlertTriangle, X, Download, ExternalLink, Store, Truck,
  Shield, ChevronRight, AlertCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { format } from 'date-fns';
import clsx from 'clsx';
import api, { resolveUploadUrl } from '../../services/api';
import { Spinner } from '../../components/ui';

type DocStatus = 'pending' | 'approved' | 'rejected';
type FilterStatus = 'all' | DocStatus;
type DocTab = 'supplier' | 'spaza';

interface SupplierDoc {
  id: string;
  docType: string;
  label: string;
  docUrl: string;
  status: DocStatus;
  expiryDate?: string;
  rejectionNote?: string;
  uploadedAt: string;
  supplierId: string;
  supplierName: string;
  supplierEmail: string;
}

const docLabels: Record<string, string> = {
  cipc_certificate: 'CIPC Certificate',
  tax_clearance: 'Tax Clearance Certificate',
  bee_certificate: 'BEE Certificate',
  product_license: 'Product License',
};

const docIcons: Record<string, string> = {
  cipc_certificate: '🏢',
  tax_clearance: '🧾',
  bee_certificate: '🤝',
  product_license: '📋',
};

const statusConfig: Record<DocStatus, { label: string; icon: React.ReactNode; className: string; dotColor: string }> = {
  pending:  { label: 'Pending Review', icon: <Clock size={12} />,       className: 'bg-amber-50 text-amber-700 border-amber-200', dotColor: 'bg-amber-400' },
  approved: { label: 'Approved',       icon: <CheckCircle size={12} />, className: 'bg-emerald-50 text-emerald-700 border-emerald-200', dotColor: 'bg-emerald-400' },
  rejected: { label: 'Rejected',       icon: <XCircle size={12} />,     className: 'bg-red-50 text-red-700 border-red-200', dotColor: 'bg-red-400' },
};

export default function DocumentVerificationPage() {
  const [activeTab, setActiveTab]   = useState<DocTab>('supplier');
  const [allDocs, setAllDocs]       = useState<SupplierDoc[]>([]);
  const [loading, setLoading]       = useState(true);
  const [summary, setSummary]       = useState({ pending: 0, approved: 0, rejected: 0 });
  const [search, setSearch]         = useState('');
  const [statusFilter, setStatusFilter] = useState<FilterStatus>('all');
  const [rejectReason, setRejectReason] = useState('');
  const [rejectTarget, setRejectTarget] = useState<string | null>(null);
  const [previewDoc, setPreviewDoc] = useState<SupplierDoc | null>(null);
  const [expandedSupplier, setExpandedSupplier] = useState<Set<string>>(new Set());

  const fetchDocs = async (status?: string, q?: string, tab?: DocTab) => {
    try {
      setLoading(true);
      const params: Record<string, string> = {};
      if (status && status !== 'all') params.status = status;
      if (q) params.search = q;
      const currentTab = tab ?? activeTab;
      const endpoint = currentTab === 'spaza' ? '/compliance/documents/shop' : '/compliance/documents';
      const res = await api.get(endpoint, { params });
      const { docs, summary: s } = res.data.data;
      setAllDocs(docs.map((d: any) => ({ ...d, label: docLabels[d.docType] ?? d.docType, uploadedAt: d.createdAt })));
      setSummary(s);
    } catch {
      toast.error('Failed to load documents');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchDocs(); }, []);

  const handleTabChange = (tab: DocTab) => {
    setActiveTab(tab);
    setSearch('');
    setStatusFilter('all');
    setExpandedSupplier(new Set());
    fetchDocs(undefined, undefined, tab);
  };

  const filtered = allDocs.filter((d) => {
    const matchSearch = !search ||
      d.supplierName.toLowerCase().includes(search.toLowerCase()) ||
      d.label.toLowerCase().includes(search.toLowerCase());
    const matchStatus = statusFilter === 'all' || d.status === statusFilter;
    return matchSearch && matchStatus;
  });

  const pendingCount  = summary.pending;
  const approvedCount = summary.approved;
  const rejectedCount = summary.rejected;
  const totalCount = pendingCount + approvedCount + rejectedCount;

  const updateDocStatus = async (id: string, status: DocStatus, reason?: string) => {
    try {
      if (status === 'approved') {
        await api.patch(`/compliance/documents/${id}/approve`);
      } else {
        await api.patch(`/compliance/documents/${id}/reject`, { reason });
      }
      toast.success(status === 'approved' ? '✅ Document approved' : '❌ Document rejected');
      setRejectTarget(null);
      setRejectReason('');
      setPreviewDoc(null);
      fetchDocs(statusFilter !== 'all' ? statusFilter : undefined, search || undefined, activeTab);
    } catch {
      toast.error('Failed to update document status');
    }
  };

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <div className="text-center">
        <Spinner />
        <p className="text-sm text-gray-400 mt-3 font-medium">Loading documents...</p>
      </div>
    </div>
  );

  // Group documents by supplier
  const grouped = new Map<string, { name: string; email: string; docs: typeof filtered }>();
  filtered.forEach((doc) => {
    const key = doc.supplierId;
    if (!grouped.has(key)) {
      grouped.set(key, { name: doc.supplierName, email: doc.supplierEmail, docs: [] });
    }
    grouped.get(key)!.docs.push(doc);
  });
  const requiredDocs = ['cipc_certificate', 'tax_clearance', 'bee_certificate', 'product_license'];

  return (
    <div className="p-6 space-y-6 animate-in">

      {/* ═══ HEADER ═══ */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <div className="w-10 h-10 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl flex items-center justify-center shadow-lg shadow-indigo-200">
              <Shield size={18} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl font-black text-gray-900 tracking-tight">Document Verification</h1>
              <p className="text-sm text-gray-400 font-medium">Review and verify compliance documents</p>
            </div>
          </div>
        </div>
        {pendingCount > 0 && (
          <div className="flex items-center gap-2.5 bg-gradient-to-r from-amber-50 to-orange-50 border border-amber-200/80 text-amber-800 text-sm font-bold px-4 py-2.5 rounded-2xl shadow-sm">
            <div className="w-6 h-6 bg-amber-100 rounded-full flex items-center justify-center">
              <AlertTriangle size={12} className="text-amber-600" />
            </div>
            {pendingCount} awaiting review
          </div>
        )}
      </div>

      {/* ═══ STATS CARDS ═══ */}
      <div className="grid grid-cols-4 gap-4">
        <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-blue-50 to-transparent rounded-bl-[40px]" />
          <div className="relative">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center">
                <FileText size={14} className="text-blue-600" />
              </div>
            </div>
            <p className="text-3xl font-black text-gray-900 tabular-nums">{totalCount}</p>
            <p className="text-xs font-semibold text-gray-400 mt-1">Total Documents</p>
          </div>
        </div>
        <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-amber-50 to-transparent rounded-bl-[40px]" />
          <div className="relative">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 bg-amber-100 rounded-lg flex items-center justify-center">
                <Clock size={14} className="text-amber-600" />
              </div>
            </div>
            <p className="text-3xl font-black text-amber-600 tabular-nums">{pendingCount}</p>
            <p className="text-xs font-semibold text-gray-400 mt-1">Pending Review</p>
          </div>
        </div>
        <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-emerald-50 to-transparent rounded-bl-[40px]" />
          <div className="relative">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 bg-emerald-100 rounded-lg flex items-center justify-center">
                <CheckCircle size={14} className="text-emerald-600" />
              </div>
            </div>
            <p className="text-3xl font-black text-emerald-600 tabular-nums">{approvedCount}</p>
            <p className="text-xs font-semibold text-gray-400 mt-1">Approved</p>
          </div>
        </div>
        <div className="relative overflow-hidden rounded-2xl bg-white border border-gray-100 p-5 shadow-sm hover:shadow-md transition-shadow">
          <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-bl from-red-50 to-transparent rounded-bl-[40px]" />
          <div className="relative">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-8 h-8 bg-red-100 rounded-lg flex items-center justify-center">
                <XCircle size={14} className="text-red-500" />
              </div>
            </div>
            <p className="text-3xl font-black text-red-500 tabular-nums">{rejectedCount}</p>
            <p className="text-xs font-semibold text-gray-400 mt-1">Rejected</p>
          </div>
        </div>
      </div>

      {/* ═══ TABS & FILTERS ═══ */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        {/* Tab Toggle */}
        <div className="flex items-center gap-1 bg-gray-100/80 rounded-2xl p-1.5">
          <button
            onClick={() => handleTabChange('supplier')}
            className={clsx(
              'flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-bold transition-all',
              activeTab === 'supplier'
                ? 'bg-white text-gray-900 shadow-sm ring-1 ring-gray-200/50'
                : 'text-gray-500 hover:text-gray-700'
            )}
          >
            <Truck size={15} />
            Suppliers
          </button>
          <button
            onClick={() => handleTabChange('spaza')}
            className={clsx(
              'flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-bold transition-all',
              activeTab === 'spaza'
                ? 'bg-white text-gray-900 shadow-sm ring-1 ring-gray-200/50'
                : 'text-gray-500 hover:text-gray-700'
            )}
          >
            <Store size={15} />
            Spaza Owners
          </button>
        </div>

        {/* Status Filter Pills */}
        <div className="flex items-center gap-1.5">
          {(['all', 'pending', 'approved', 'rejected'] as FilterStatus[]).map((value) => {
            const count = value === 'all' ? totalCount : value === 'pending' ? pendingCount : value === 'approved' ? approvedCount : rejectedCount;
            return (
              <button
                key={value}
                onClick={() => { setStatusFilter(value); fetchDocs(value !== 'all' ? value : undefined, search || undefined, activeTab); }}
                className={clsx(
                  'flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold transition-all border',
                  statusFilter === value
                    ? 'bg-gray-900 text-white border-gray-900 shadow-sm'
                    : 'bg-white text-gray-500 border-gray-200 hover:border-gray-300 hover:text-gray-700'
                )}
              >
                <span className="capitalize">{value}</span>
                <span className={clsx(
                  'text-[10px] font-black px-1.5 py-0.5 rounded-full min-w-[20px] text-center',
                  statusFilter === value ? 'bg-white/20' : 'bg-gray-100'
                )}>{count}</span>
              </button>
            );
          })}
        </div>
      </div>

      {/* ═══ SEARCH ═══ */}
      <div className="relative max-w-md">
        <Search size={15} className="absolute left-4 top-3.5 text-gray-300" />
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full border border-gray-200 rounded-2xl pl-11 pr-4 py-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-100 focus:border-indigo-300 transition-all placeholder:text-gray-300 font-medium"
          placeholder={activeTab === 'spaza' ? 'Search by shop name or document...' : 'Search by supplier name or document...'}
        />
        {search && (
          <button onClick={() => setSearch('')} className="absolute right-3 top-3 text-gray-300 hover:text-gray-500 transition-colors">
            <X size={16} />
          </button>
        )}
      </div>

      {/* ═══ GROUPED DOCUMENTS ═══ */}
      <div className="flex items-center justify-between">
        <p className="text-sm font-bold text-gray-500">
          {grouped.size} {activeTab === 'spaza' ? 'shop' : 'supplier'}{grouped.size !== 1 ? 's' : ''} found
        </p>
        <div className="flex items-center gap-2">
          <button
            onClick={() => {
              const allIds = Array.from(grouped.keys());
              setExpandedSupplier((prev) => prev.size === allIds.length ? new Set() : new Set(allIds));
            }}
            className="text-xs font-bold text-indigo-600 hover:text-indigo-800 transition-colors px-3 py-1.5 rounded-lg hover:bg-indigo-50"
          >
            {expandedSupplier.size === grouped.size ? 'Collapse All' : 'Expand All'}
          </button>
        </div>
      </div>

      <div className="space-y-4">
        {filtered.length === 0 ? (
          <div className="rounded-2xl border-2 border-dashed border-gray-200 p-16 text-center">
            <div className="w-16 h-16 bg-gray-100 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <FileText size={28} className="text-gray-300" />
            </div>
            <p className="font-bold text-gray-600 text-lg">No documents found</p>
            <p className="text-sm text-gray-400 mt-1">Try adjusting your search or filters</p>
          </div>
        ) : (
          Array.from(grouped.entries()).map(([supplierId, group]) => {
            const submittedTypes = group.docs.map((d) => d.docType);
            const outstanding = requiredDocs.filter((r) => !submittedTypes.includes(r));
            const grpApproved = group.docs.filter((d) => d.status === 'approved').length;
            const grpPending = group.docs.filter((d) => d.status === 'pending').length;
            const grpRejected = group.docs.filter((d) => d.status === 'rejected').length;
            const completionPct = Math.round((group.docs.length / requiredDocs.length) * 100);
            const isExpanded = expandedSupplier.has(supplierId);

            return (
              <div key={supplierId} className="bg-white rounded-2xl border border-gray-100 shadow-sm hover:shadow-md transition-all overflow-hidden">
                {/* ── Supplier Header ── */}
                <button
                  onClick={() => {
                    setExpandedSupplier((prev) => {
                      const next = new Set(prev);
                      if (next.has(supplierId)) next.delete(supplierId);
                      else next.add(supplierId);
                      return next;
                    });
                  }}
                  className="w-full flex items-center justify-between px-6 py-5 hover:bg-gray-50/50 transition-colors text-left"
                >
                  <div className="flex items-center gap-4">
                    <div className={clsx(
                      'w-12 h-12 rounded-2xl flex items-center justify-center font-black text-base shadow-sm',
                      activeTab === 'spaza'
                        ? 'bg-gradient-to-br from-purple-100 to-purple-50 text-purple-700 ring-1 ring-purple-200/50'
                        : 'bg-gradient-to-br from-slate-100 to-slate-50 text-slate-700 ring-1 ring-slate-200/50'
                    )}>
                      {group.name.charAt(0)}
                    </div>
                    <div>
                      <p className="font-bold text-gray-900 text-base">{group.name}</p>
                      <p className="text-xs text-gray-400 font-medium mt-0.5">{group.email}</p>
                    </div>
                  </div>

                  <div className="flex items-center gap-3">
                    {/* Progress ring */}
                    <div className="flex items-center gap-2.5">
                      <div className="relative w-10 h-10">
                        <svg className="w-10 h-10 -rotate-90" viewBox="0 0 36 36">
                          <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                            fill="none" stroke="#f1f5f9" strokeWidth="3" />
                          <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                            fill="none" stroke={completionPct === 100 ? '#10b981' : completionPct >= 50 ? '#f59e0b' : '#94a3b8'}
                            strokeWidth="3" strokeDasharray={`${completionPct}, 100`} strokeLinecap="round" />
                        </svg>
                        <span className="absolute inset-0 flex items-center justify-center text-[10px] font-black text-gray-600">
                          {group.docs.length}/{requiredDocs.length}
                        </span>
                      </div>
                    </div>

                    {/* Status pills */}
                    <div className="flex items-center gap-1.5">
                      {grpApproved > 0 && (
                        <span className="flex items-center gap-1 text-[11px] font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-1 rounded-lg">
                          <CheckCircle size={10} /> {grpApproved}
                        </span>
                      )}
                      {grpPending > 0 && (
                        <span className="flex items-center gap-1 text-[11px] font-bold text-amber-700 bg-amber-50 border border-amber-200 px-2 py-1 rounded-lg">
                          <Clock size={10} /> {grpPending}
                        </span>
                      )}
                      {grpRejected > 0 && (
                        <span className="flex items-center gap-1 text-[11px] font-bold text-red-600 bg-red-50 border border-red-200 px-2 py-1 rounded-lg">
                          <XCircle size={10} /> {grpRejected}
                        </span>
                      )}
                      {outstanding.length > 0 && (
                        <span className="flex items-center gap-1 text-[11px] font-bold text-gray-500 bg-gray-100 border border-gray-200 px-2 py-1 rounded-lg">
                          <AlertCircle size={10} /> {outstanding.length}
                        </span>
                      )}
                    </div>

                    <ChevronRight size={16} className={clsx('text-gray-300 transition-transform', isExpanded && 'rotate-90')} />
                  </div>
                </button>

                {/* ── Document List ── */}
                {isExpanded && (
                  <div className="border-t border-gray-100" style={{ animation: 'expandIn 0.25s ease-out' }}>
                    {group.docs.map((doc, idx) => (
                      <div
                        key={doc.id}
                        className={clsx(
                          'flex items-center justify-between px-6 py-4 transition-colors hover:bg-gray-50/70',
                          idx < group.docs.length - 1 || outstanding.length > 0 ? 'border-b border-gray-50' : ''
                        )}
                      >
                        <div className="flex items-center gap-4">
                          <div className="w-10 h-10 bg-gradient-to-br from-slate-50 to-white rounded-xl flex items-center justify-center border border-slate-100 text-lg shadow-sm">
                            {docIcons[doc.docType] ?? '📄'}
                          </div>
                          <div>
                            <p className="text-sm font-bold text-gray-900">{doc.label}</p>
                            <p className="text-[11px] text-gray-400 mt-0.5 font-medium">
                              Uploaded {format(new Date(doc.uploadedAt), 'dd MMM yyyy')}
                              {doc.expiryDate && (
                                <span className="text-amber-500"> · Expires {format(new Date(doc.expiryDate), 'dd MMM yyyy')}</span>
                              )}
                            </p>
                          </div>
                        </div>

                        <div className="flex items-center gap-2.5">
                          {/* Status badge */}
                          <span className={clsx(
                            'inline-flex items-center gap-1.5 text-[11px] font-bold px-2.5 py-1 rounded-lg border',
                            statusConfig[doc.status].className
                          )}>
                            {statusConfig[doc.status].icon}
                            {statusConfig[doc.status].label}
                          </span>

                          {/* Action buttons */}
                          <div className="flex items-center gap-1 ml-1">
                            <button
                              onClick={() => setPreviewDoc(doc)}
                              className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all"
                              title="View Document"
                            >
                              <Eye size={15} />
                            </button>
                            {doc.status !== 'approved' && (
                              <button
                                onClick={() => updateDocStatus(doc.id, 'approved')}
                                className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-emerald-600 hover:bg-emerald-50 transition-all"
                                title="Approve"
                              >
                                <CheckCircle size={15} />
                              </button>
                            )}
                            {doc.status !== 'rejected' && (
                              <button
                                onClick={() => setRejectTarget(doc.id)}
                                className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-red-500 hover:bg-red-50 transition-all"
                                title="Reject"
                              >
                                <XCircle size={15} />
                              </button>
                            )}
                          </div>
                        </div>
                      </div>
                    ))}

                    {/* Outstanding documents */}
                    {outstanding.map((docType) => (
                      <div key={docType} className="flex items-center justify-between px-6 py-4 bg-gray-50/50">
                        <div className="flex items-center gap-4">
                          <div className="w-10 h-10 bg-gray-100 rounded-xl flex items-center justify-center border-2 border-dashed border-gray-200 text-lg opacity-40">
                            {docIcons[docType] ?? '📄'}
                          </div>
                          <div>
                            <p className="text-sm font-medium text-gray-400">{docLabels[docType] ?? docType}</p>
                            <p className="text-[11px] text-gray-300 mt-0.5">Not yet submitted by {activeTab === 'spaza' ? 'shop owner' : 'supplier'}</p>
                          </div>
                        </div>
                        <span className="inline-flex items-center gap-1.5 text-[11px] font-bold text-gray-400 bg-gray-100 border border-gray-200 px-2.5 py-1 rounded-lg">
                          <AlertCircle size={10} />
                          Outstanding
                        </span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            );
          })
        )}
      </div>

      {/* ═══ DOCUMENT PREVIEW MODAL ═══ */}
      {previewDoc && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setPreviewDoc(null)} />
          <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-3xl max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
            {/* Header */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 bg-gradient-to-r from-slate-50 to-white flex-shrink-0">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-gradient-to-br from-indigo-100 to-indigo-50 rounded-xl flex items-center justify-center text-lg shadow-sm">
                  {docIcons[previewDoc.docType] ?? '📄'}
                </div>
                <div>
                  <p className="font-bold text-gray-900">{previewDoc.label}</p>
                  <p className="text-xs text-gray-400 font-medium">{previewDoc.supplierName}</p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <span className={clsx('inline-flex items-center gap-1.5 text-xs font-bold px-3 py-1.5 rounded-xl border', statusConfig[previewDoc.status].className)}>
                  {statusConfig[previewDoc.status].icon}
                  {statusConfig[previewDoc.status].label}
                </span>
                {previewDoc.docUrl && previewDoc.docUrl !== '#' && (
                  <>
                    <a href={resolveUploadUrl(previewDoc.docUrl)} download className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all" title="Download">
                      <Download size={15} />
                    </a>
                    <a href={resolveUploadUrl(previewDoc.docUrl)} target="_blank" rel="noreferrer" className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all" title="Open in new tab">
                      <ExternalLink size={15} />
                    </a>
                  </>
                )}
                <button onClick={() => setPreviewDoc(null)} className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-all">
                  <X size={15} />
                </button>
              </div>
            </div>

            {/* Info Bar */}
            <div className="flex items-center gap-6 px-6 py-3 bg-gray-50/80 border-b border-gray-100 text-xs text-gray-500 flex-shrink-0 font-medium">
              <span>📅 Uploaded: <strong className="text-gray-700">{format(new Date(previewDoc.uploadedAt), 'dd MMM yyyy')}</strong></span>
              {previewDoc.expiryDate && <span>⏰ Expires: <strong className="text-gray-700">{format(new Date(previewDoc.expiryDate), 'dd MMM yyyy')}</strong></span>}
              <span>📧 {previewDoc.supplierEmail}</span>
            </div>

            {/* Preview */}
            <div className="flex-1 overflow-auto p-6 min-h-0 bg-gray-50/30">
              {(() => {
                const resolvedUrl = resolveUploadUrl(previewDoc.docUrl);
                if (!resolvedUrl || previewDoc.docUrl === '#') {
                  return (
                    <div className="h-full min-h-[400px] flex flex-col items-center justify-center bg-white rounded-2xl border-2 border-dashed border-gray-200 shadow-sm">
                      <div className="w-16 h-16 bg-gray-100 rounded-2xl flex items-center justify-center mb-4 text-2xl">📄</div>
                      <p className="font-bold text-gray-700">{previewDoc.label}</p>
                      <p className="text-sm text-gray-400 mt-1 mb-5">No document file available for preview</p>
                      <div className="flex gap-2">
                        {previewDoc.status !== 'approved' && (
                          <button onClick={() => updateDocStatus(previewDoc.id, 'approved')}
                            className="flex items-center gap-1.5 text-sm font-bold bg-emerald-600 text-white px-5 py-2.5 rounded-xl hover:bg-emerald-700 transition-colors shadow-sm">
                            <CheckCircle size={14} /> Approve
                          </button>
                        )}
                        {previewDoc.status !== 'rejected' && (
                          <button onClick={() => { setPreviewDoc(null); setRejectTarget(previewDoc.id); }}
                            className="flex items-center gap-1.5 text-sm font-bold bg-red-500 text-white px-5 py-2.5 rounded-xl hover:bg-red-600 transition-colors shadow-sm">
                            <XCircle size={14} /> Reject
                          </button>
                        )}
                      </div>
                    </div>
                  );
                }
                if (resolvedUrl.match(/\.(pdf)$/i)) {
                  return <iframe src={resolvedUrl} className="w-full h-full min-h-[500px] rounded-2xl border border-gray-200 shadow-sm bg-white" title={previewDoc.label} />;
                }
                return <img src={resolvedUrl} alt={previewDoc.label} className="max-w-full mx-auto rounded-2xl border border-gray-200 shadow-sm" onError={(e) => { (e.target as HTMLImageElement).style.display = 'none'; }} />;
              })()}
            </div>

            {/* Footer */}
            {previewDoc.docUrl && previewDoc.docUrl !== '#' && resolveUploadUrl(previewDoc.docUrl) && (
              <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-gray-100 bg-white flex-shrink-0">
                {previewDoc.status !== 'approved' && (
                  <button onClick={() => updateDocStatus(previewDoc.id, 'approved')}
                    className="flex items-center gap-2 text-sm font-bold bg-emerald-600 text-white px-6 py-2.5 rounded-xl hover:bg-emerald-700 transition-colors shadow-sm shadow-emerald-200">
                    <CheckCircle size={14} /> Approve Document
                  </button>
                )}
                {previewDoc.status !== 'rejected' && (
                  <button onClick={() => { setPreviewDoc(null); setRejectTarget(previewDoc.id); }}
                    className="flex items-center gap-2 text-sm font-bold bg-red-500 text-white px-6 py-2.5 rounded-xl hover:bg-red-600 transition-colors shadow-sm shadow-red-200">
                    <XCircle size={14} /> Reject Document
                  </button>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ═══ REJECT MODAL ═══ */}
      {rejectTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setRejectTarget(null)} />
          <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-md max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
            <div className="px-6 py-5 border-b border-gray-100 bg-gradient-to-r from-red-50 to-white">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-red-100 rounded-xl flex items-center justify-center">
                  <XCircle size={18} className="text-red-500" />
                </div>
                <div>
                  <h2 className="font-bold text-gray-900">Reject Document</h2>
                  <p className="text-sm text-gray-500 mt-0.5">The supplier will be notified with this reason</p>
                </div>
              </div>
            </div>
            <div className="p-6 space-y-4">
              <div>
                <label className="block text-xs font-bold text-gray-500 uppercase tracking-wider mb-2">Rejection Reason</label>
                <textarea
                  value={rejectReason}
                  onChange={(e) => setRejectReason(e.target.value)}
                  className="w-full border border-gray-200 rounded-xl px-4 py-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-red-100 focus:border-red-300 transition-all placeholder:text-gray-300"
                  rows={3}
                  placeholder="e.g. Document is expired, illegible, or incorrect type..."
                />
              </div>
              <div className="flex gap-3 justify-end pt-2">
                <button onClick={() => setRejectTarget(null)} className="px-5 py-2.5 rounded-xl text-sm font-bold text-gray-600 bg-gray-100 hover:bg-gray-200 transition-colors">
                  Cancel
                </button>
                <button
                  onClick={() => updateDocStatus(rejectTarget, 'rejected', rejectReason)}
                  disabled={!rejectReason.trim()}
                  className="flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-bold bg-red-500 text-white hover:bg-red-600 transition-colors disabled:opacity-40 disabled:cursor-not-allowed shadow-sm shadow-red-200"
                >
                  <XCircle size={14} /> Reject Document
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      <style>{`
        @keyframes slideUp {
          from { opacity: 0; transform: translateY(16px) scale(0.98); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
        @keyframes expandIn {
          from { opacity: 0; max-height: 0; }
          to   { opacity: 1; max-height: 2000px; }
        }
      `}</style>
    </div>
  );
}
