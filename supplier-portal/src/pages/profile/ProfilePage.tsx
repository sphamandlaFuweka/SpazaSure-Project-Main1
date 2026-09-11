import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { CheckCircle, Clock, XCircle, Upload, AlertTriangle, Zap, Shield, Building2, CreditCard, Camera, Loader2, Eye, FileText, X, Download, ExternalLink, MapPin } from 'lucide-react';
import toast from 'react-hot-toast';
import type { SupplierProfile, ComplianceDoc } from '../../types';
import { TierBadge, AlertBanner } from '../../components/ui';
import PageLoader from '../../components/ui/PageLoader';
import { profileApi, resolveUploadUrl } from '../../services/api';
import { useAuthStore } from '../../store/authStore';
import clsx from 'clsx';
import { differenceInDays, parseISO, format } from 'date-fns';

const docLabels: Record<string, string> = {
  cipc_certificate: 'CIPC Certificate',
  tax_clearance:    'Tax Clearance Certificate',
  bee_certificate:  'BEE Certificate',
  product_license:  'Product License',
};

const docDescriptions: Record<string, string> = {
  cipc_certificate: 'Company registration from CIPC',
  tax_clearance:    'Valid tax clearance from SARS',
  bee_certificate:  'Black Economic Empowerment certificate',
  product_license:  'Product manufacturing/distribution license',
};

const tierFeatures: Record<string, { price: string; features: string[]; commission: string; color: string }> = {
  basic:  { price: 'Free',       commission: '5%', color: 'border-gray-200 bg-gray-50',    features: ['10 product listings', 'Basic analytics', 'Standard support'] },
  bronze: { price: 'R500/mo',    commission: '4%', color: 'border-orange-200 bg-orange-50', features: ['50 product listings', 'Standard analytics', 'Email support'] },
  silver: { price: 'R1,500/mo',  commission: '3%', color: 'border-slate-300 bg-slate-50',  features: ['Unlimited listings', 'Advanced analytics', 'Featured placement', 'Promotional tools'] },
  gold:   { price: 'R5,500/mo',  commission: '2%', color: 'border-amber-300 bg-amber-50',  features: ['Unlimited listings', 'Premium analytics + API', 'Priority placement', 'Dedicated support', 'Early payout (3 days)'] },
};

export default function ProfilePage() {
  const navigate = useNavigate();
  const [profile, setProfile] = useState<SupplierProfile | null>(null);
  const [loading, setLoading]     = useState(true);
  const [saving, setSaving]       = useState(false);
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [logoUploading, setLogoUploading] = useState(false);
  const [logoDragOver, setLogoDragOver]   = useState(false);
  const logoInputRef = useRef<HTMLInputElement>(null);
  const [activeTab, setActiveTab] = useState<'company' | 'banking' | 'documents' | 'subscription'>('company');
  const [previewDoc, setPreviewDoc] = useState<ComplianceDoc & { label: string } | null>(null);
  const { register, handleSubmit, reset } = useForm<SupplierProfile>({ values: profile || undefined });

  useEffect(() => {
    profileApi.get()
      .then((data) => {
        setProfile(data);
        reset(data);
        // Sync logo to auth store on load (in case it was updated externally)
        const { user, setUser } = useAuthStore.getState();
        if (user && data.logoUrl && data.logoUrl !== user.logoUrl) {
          setUser({ ...user, logoUrl: data.logoUrl });
        }
      })
      .catch(() => toast.error('Failed to load profile'))
      .finally(() => setLoading(false));
  }, []);

  const onSubmit = async (data: SupplierProfile) => {
    setSaving(true);
    try {
      await profileApi.update(data);
      // Re-fetch so the hero card and form reflect saved values
      const updated = await profileApi.get();
      setProfile(updated);
      reset(updated);
      // Sync relevant fields back to auth store for header/sidebar
      const { user, setUser } = useAuthStore.getState();
      if (user) {
        setUser({
          ...user,
          companyName: updated.companyName || user.companyName,
          logoUrl: updated.logoUrl || user.logoUrl,
        });
      }
      toast.success('Profile updated successfully');
    } catch {
      toast.error('Failed to save profile');
    } finally {
      setSaving(false);
    }
  };

  const handleDocUpload = async (docType: string, file: File) => {
    try {
      await profileApi.uploadDocument(docType, file);
      toast.success(`${docLabels[docType]} uploaded successfully`);
      // Refresh profile to get updated doc status
      const data = await profileApi.get();
      setProfile(data);
    } catch {
      toast.error(`Failed to upload ${docLabels[docType]}`);
    }
  };

  const handleLogoFile = async (file: File) => {
    if (!file.type.startsWith('image/')) { toast.error('Please select an image file'); return; }
    if (file.size > 5 * 1024 * 1024) { toast.error('Image must be under 5MB'); return; }
    // Show local preview immediately
    const reader = new FileReader();
    reader.onload = (e) => setLogoPreview(e.target?.result as string);
    reader.readAsDataURL(file);
    // Upload to backend
    setLogoUploading(true);
    try {
      const res: any = await profileApi.uploadLogo(file);
      const newLogoUrl = res.data?.logoUrl ?? res.logoUrl ?? null;
      if (newLogoUrl) {
        setProfile((prev) => prev ? { ...prev, logoUrl: newLogoUrl } : prev);
        // Sync the logo to global auth store so header/sidebar update immediately
        const { user, setUser } = useAuthStore.getState();
        if (user) {
          setUser({ ...user, logoUrl: newLogoUrl });
        }
      } else {
        // Backend didn't return a URL — use the data URL preview as fallback for immediate display
        const dataUrl = logoPreview;
        if (dataUrl) {
          const { user, setUser } = useAuthStore.getState();
          if (user) {
            setUser({ ...user, logoUrl: dataUrl });
          }
        }
      }
      toast.success('Profile photo updated!');
    } catch {
      toast.error('Failed to upload photo');
      setLogoPreview(null);
    } finally {
      setLogoUploading(false);
    }
  };

  if (loading) return <PageLoader variant="cards" />;
  if (!profile) return null;

  const getExpiryInfo = (expiryDate?: string) => {
    if (!expiryDate) return null;
    const days = differenceInDays(parseISO(expiryDate), new Date());
    if (days < 0) return { label: 'Expired', color: 'text-red-600' };
    if (days <= 14) return { label: `Expires in ${days} days`, color: 'text-red-500' };
    if (days <= 30) return { label: `Expires in ${days} days`, color: 'text-amber-600' };
    return { label: `Expires ${expiryDate}`, color: 'text-gray-400' };
  };

  const tabs = [
    { id: 'company',      label: 'Company Info',   icon: Building2 },
    { id: 'banking',      label: 'Banking',         icon: CreditCard },
    { id: 'documents',    label: 'Documents',       icon: Shield },
    { id: 'subscription', label: 'Subscription',    icon: Zap },
  ] as const;

  return (
    <div className="p-6 space-y-6 animate-in max-w-5xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="page-title">Supplier Profile</h1>
          <p className="page-subtitle">Manage your company information and compliance</p>
        </div>
      </div>

      {/* Profile Hero Card */}
      <div className="card p-6">
        <div className="flex items-center gap-5">

          {/* Logo uploader */}
          <div className="relative flex-shrink-0 group">
            <div
              className={clsx(
                'w-24 h-24 rounded-2xl overflow-hidden border-2 transition-all duration-200 cursor-pointer',
                logoDragOver ? 'border-primary scale-105 shadow-glow' : 'border-gray-200 hover:border-primary hover:shadow-glow'
              )}
              onClick={() => logoInputRef.current?.click()}
              onDragOver={(e) => { e.preventDefault(); setLogoDragOver(true); }}
              onDragLeave={() => setLogoDragOver(false)}
              onDrop={(e) => { e.preventDefault(); setLogoDragOver(false); const f = e.dataTransfer.files[0]; if (f) handleLogoFile(f); }}
            >
              {/* Image or fallback */}
              {(logoPreview ?? resolveUploadUrl(profile.logoUrl)) ? (
                <img
                  src={logoPreview ?? resolveUploadUrl(profile.logoUrl)}
                  alt={profile.companyName}
                  className="w-full h-full object-cover"
                />
              ) : (
                <div className="w-full h-full bg-gradient-card flex items-center justify-center text-white text-3xl font-black">
                  {profile.companyName.charAt(0)}
                </div>
              )}

              {/* Uploading spinner overlay */}
              {logoUploading && (
                <div className="absolute inset-0 bg-black/50 flex items-center justify-center rounded-2xl">
                  <Loader2 size={22} className="text-white animate-spin" />
                </div>
              )}

              {/* Hover overlay */}
              {!logoUploading && (
                <div className="absolute inset-0 bg-black/0 group-hover:bg-black/40 flex items-center justify-center transition-all duration-200 rounded-2xl">
                  <div className="opacity-0 group-hover:opacity-100 transition-opacity flex flex-col items-center gap-1">
                    <Camera size={18} className="text-white" />
                    <span className="text-white text-[10px] font-bold">Change</span>
                  </div>
                </div>
              )}
            </div>

            {/* Hidden file input */}
            <input
              ref={logoInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
              onChange={(e) => { const f = e.target.files?.[0]; if (f) handleLogoFile(f); e.target.value = ''; }}
            />

            {/* Camera badge */}
            <button
              onClick={() => logoInputRef.current?.click()}
              className="absolute -bottom-1.5 -right-1.5 w-7 h-7 bg-primary rounded-full flex items-center justify-center shadow-md border-2 border-white hover:bg-primary-700 transition-colors"
            >
              <Camera size={12} className="text-white" />
            </button>
          </div>

          {/* Info */}
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-3 flex-wrap">
              <h2 className="text-xl font-black text-gray-900">{profile.companyName}</h2>
              <TierBadge tier={profile.tier} />
              {profile.isVerified && (
                <span className="badge-verified"><CheckCircle size={12} /> Verified Supplier</span>
              )}
            </div>
            <p className="text-sm text-gray-500 mt-1">{profile.email} · {profile.phone}</p>
            <p className="text-xs text-gray-400 mt-0.5">{profile.address}</p>
            <p className="text-[11px] text-gray-400 mt-2">
              Click the photo or drag & drop an image to update · JPG, PNG, WEBP · max 5MB
            </p>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-0.5 border-b border-gray-200">
        {tabs.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            onClick={() => setActiveTab(id)}
            className={clsx(
              'flex items-center gap-2 px-4 py-3 text-sm font-semibold border-b-2 transition-all',
              activeTab === id
                ? 'border-primary text-primary'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            )}
          >
            <Icon size={15} />
            {label}
          </button>
        ))}
      </div>

      {/* Company Info Tab */}
      {activeTab === 'company' && (
        <div className="card p-6 animate-in">
          <h2 className="font-bold text-gray-900 mb-5">Company Information</h2>
          <form onSubmit={handleSubmit(onSubmit)} className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <label className="label">Company Name</label>
              <input {...register('companyName')} className="input" />
            </div>
            <div>
              <label className="label">CIPC Registration Number</label>
              <input {...register('registrationNumber')} className="input font-mono" />
            </div>
            <div>
              <label className="label">Tax Number</label>
              <input {...register('taxNumber')} className="input font-mono" />
            </div>
            <div>
              <label className="label">Contact Person</label>
              <input {...register('contactName')} className="input" />
            </div>
            <div>
              <label className="label">Email Address</label>
              <input {...register('email')} type="email" className="input" />
            </div>
            <div>
              <label className="label">Phone Number</label>
              <input {...register('phone')} className="input" />
            </div>
            <div>
              <label className="label">Business Address</label>
              <input {...register('address')} className="input" />
            </div>
            <div className="col-span-2 rounded-xl border border-blue-100 bg-blue-50/60 p-4">
              <div className="flex items-start gap-3 mb-3">
                <MapPin size={17} className="text-blue-600 mt-0.5 flex-shrink-0" />
                <div>
                  <p className="text-sm font-bold text-blue-900">Dispatch coordinates (optional)</p>
                  <p className="text-xs text-blue-700 mt-0.5">Used to calculate kilometre-based delivery for group-buy orders.</p>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="label">Latitude</label>
                  <input {...register('latitude', { valueAsNumber: true })} type="number" step="any" min="-90" max="90" placeholder="e.g. -26.2041" className="input font-mono" />
                </div>
                <div>
                  <label className="label">Longitude</label>
                  <input {...register('longitude', { valueAsNumber: true })} type="number" step="any" min="-180" max="180" placeholder="e.g. 28.0473" className="input font-mono" />
                </div>
              </div>
            </div>
            <div className="col-span-2 flex justify-end pt-2">
              <button type="submit" disabled={saving} className="btn-primary min-w-[140px]">
                {saving ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Banking Tab */}
      {activeTab === 'banking' && (
        <div className="card p-6 animate-in">
          <div className="flex items-center gap-2 mb-5">
            <h2 className="font-bold text-gray-900">Banking Details</h2>
            <span className="text-xs bg-blue-50 text-blue-700 border border-blue-200 px-2 py-0.5 rounded-full font-semibold">Secure</span>
          </div>
          <div className="flex items-start gap-3 bg-blue-50 border border-blue-100 rounded-xl p-4 mb-5">
            <span className="text-blue-500 mt-0.5">ℹ️</span>
            <p className="text-sm text-blue-800">
              Your banking details are used for order payouts. Changes require admin verification and may take 2–3 business days.
            </p>
          </div>
          <form onSubmit={handleSubmit(onSubmit)} className="grid grid-cols-3 gap-4">
            <div>
              <label className="label">Bank Name</label>
              <input {...register('bankName')} placeholder="e.g. FNB" className="input" />
            </div>
            <div>
              <label className="label">Account Number</label>
              <input {...register('bankAccount')} placeholder="e.g. 62000000000" className="input font-mono" />
            </div>
            <div>
              <label className="label">Branch Code</label>
              <input {...register('bankBranchCode')} placeholder="e.g. 250655" className="input font-mono" />
            </div>
            <div className="col-span-3 flex justify-end pt-2">
              <button type="submit" disabled={saving} className="btn-primary min-w-[140px]">
                {saving ? 'Saving...' : 'Update Banking'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Documents Tab */}
      {activeTab === 'documents' && (
        <div className="card p-6 animate-in">
          <h2 className="font-bold text-gray-900 mb-4">Compliance Documents</h2>

          {/* Success banner when all docs are approved */}
          {(() => {
            const allDocs = ['cipc_certificate', 'tax_clearance', 'bee_certificate', 'product_license'] as const;
            const allApproved = allDocs.every((dt) => profile.documents.find((d) => d.docType === dt)?.status === 'approved');
            const hasPending = allDocs.some((dt) => profile.documents.find((d) => d.docType === dt)?.status === 'pending');
            const hasRejected = allDocs.some((dt) => profile.documents.find((d) => d.docType === dt)?.status === 'rejected');
            const missingCount = allDocs.filter((dt) => !profile.documents.find((d) => d.docType === dt)).length;

            if (allApproved) {
              return (
                <div className="flex items-center gap-3 bg-emerald-50 border border-emerald-200 rounded-2xl p-4 mb-5">
                  <div className="w-10 h-10 bg-emerald-100 rounded-xl flex items-center justify-center flex-shrink-0">
                    <CheckCircle size={20} className="text-emerald-600" />
                  </div>
                  <div>
                    <p className="text-sm font-bold text-emerald-800">All documents approved ✓</p>
                    <p className="text-xs text-emerald-600 mt-0.5">Your compliance is fully up to date. No action needed.</p>
                  </div>
                </div>
              );
            }
            if (hasRejected) {
              return (
                <div className="flex items-center gap-3 bg-red-50 border border-red-200 rounded-2xl p-4 mb-5">
                  <div className="w-10 h-10 bg-red-100 rounded-xl flex items-center justify-center flex-shrink-0">
                    <XCircle size={20} className="text-red-500" />
                  </div>
                  <div>
                    <p className="text-sm font-bold text-red-800">Some documents were rejected</p>
                    <p className="text-xs text-red-600 mt-0.5">Please review the rejection reasons below and re-upload corrected documents.</p>
                  </div>
                </div>
              );
            }
            if (hasPending) {
              return (
                <div className="flex items-center gap-3 bg-amber-50 border border-amber-200 rounded-2xl p-4 mb-5">
                  <div className="w-10 h-10 bg-amber-100 rounded-xl flex items-center justify-center flex-shrink-0">
                    <Clock size={20} className="text-amber-600" />
                  </div>
                  <div>
                    <p className="text-sm font-bold text-amber-800">Documents under review</p>
                    <p className="text-xs text-amber-600 mt-0.5">Your documents have been submitted and are being reviewed. This usually takes 1-2 business days.</p>
                  </div>
                </div>
              );
            }
            if (missingCount > 0) {
              return (
                <div className="flex items-center gap-3 bg-gray-50 border border-gray-200 rounded-2xl p-4 mb-5">
                  <div className="w-10 h-10 bg-gray-100 rounded-xl flex items-center justify-center flex-shrink-0">
                    <AlertTriangle size={20} className="text-gray-500" />
                  </div>
                  <div>
                    <p className="text-sm font-bold text-gray-800">{missingCount} document{missingCount > 1 ? 's' : ''} still required</p>
                    <p className="text-xs text-gray-500 mt-0.5">Upload all required documents to complete your verification.</p>
                  </div>
                </div>
              );
            }
            return null;
          })()}

          <div className="space-y-3">
            {(['cipc_certificate', 'tax_clearance', 'bee_certificate', 'product_license'] as const).map((docType) => {
              const doc = profile.documents.find((d) => d.docType === docType);
              const expiry = doc?.expiryDate ? getExpiryInfo(doc.expiryDate) : null;
              return (
                <div key={docType} className={clsx(
                  'flex items-center justify-between p-4 rounded-2xl border-2 transition-all',
                  doc?.status === 'approved' ? 'border-emerald-100 bg-emerald-50/50' :
                  doc?.status === 'pending'  ? 'border-amber-100 bg-amber-50/50' :
                  doc?.status === 'rejected' ? 'border-red-100 bg-red-50/50' :
                  'border-gray-100 bg-gray-50/50'
                )}>
                  <div className="flex items-center gap-4">
                    <div className={clsx('w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0',
                      doc?.status === 'approved' ? 'bg-emerald-100' :
                      doc?.status === 'pending'  ? 'bg-amber-100' :
                      doc?.status === 'rejected' ? 'bg-red-100' :
                      'bg-gray-100'
                    )}>
                      {doc?.status === 'approved' ? <CheckCircle size={18} className="text-emerald-600" /> :
                       doc?.status === 'pending'  ? <Clock size={18} className="text-amber-500" /> :
                       doc?.status === 'rejected' ? <XCircle size={18} className="text-red-500" /> :
                       <AlertTriangle size={18} className="text-gray-400" />}
                    </div>
                    <div>
                      <p className="font-bold text-sm text-gray-900">{docLabels[docType]}</p>
                      <p className="text-xs text-gray-400 mt-0.5">{docDescriptions[docType]}</p>
                      {expiry && <p className={clsx('text-xs font-semibold mt-0.5', expiry.color)}>{expiry.label}</p>}
                      {doc?.rejectionReason && (
                        <div className="mt-1.5 flex items-start gap-1.5 bg-red-50 border border-red-100 rounded-lg px-2.5 py-1.5">
                          <XCircle size={11} className="text-red-400 flex-shrink-0 mt-0.5" />
                          <p className="text-xs text-red-600 font-medium">Reason: {doc.rejectionReason}</p>
                        </div>
                      )}
                      {!doc && <p className="text-xs text-gray-400 mt-0.5">Not uploaded yet</p>}
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {doc && (
                      <span className={clsx('text-xs font-bold px-2.5 py-1 rounded-full capitalize',
                        doc.status === 'approved' ? 'bg-emerald-100 text-emerald-700' :
                        doc.status === 'pending'  ? 'bg-amber-100 text-amber-700' :
                        'bg-red-100 text-red-700'
                      )}>
                        {doc.status}
                      </span>
                    )}
                    {doc && (doc.status === 'approved' || doc.status === 'rejected') && (
                      <button
                        onClick={() => setPreviewDoc({ ...doc, label: docLabels[docType] })}
                        className="flex items-center gap-1.5 text-sm font-semibold text-blue-600 border border-blue-200 bg-blue-50 px-3 py-2 rounded-xl hover:bg-blue-100 transition-colors"
                        title="Preview document"
                      >
                        <Eye size={13} /> Preview
                      </button>
                    )}
                    {doc?.status !== 'approved' && (
                      <label className="flex items-center gap-1.5 text-sm font-semibold text-primary border border-primary px-3 py-2 rounded-xl hover:bg-primary-50 transition-colors cursor-pointer">
                        <Upload size={13} /> {doc ? 'Replace' : 'Upload'}
                        <input type="file" className="hidden" accept=".pdf,.jpg,.jpeg,.png"
                          onChange={(e) => { const f = e.target.files?.[0]; if (f) handleDocUpload(docType, f); }} />
                      </label>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Subscription Tab */}
      {activeTab === 'subscription' && (
        <div className="animate-in space-y-4">
          <div className="grid grid-cols-4 gap-3">
            {(['basic', 'bronze', 'silver', 'gold'] as const).map((tier) => {
              const { price, commission, color, features } = tierFeatures[tier];
              const isCurrent = profile.tier === tier;
              return (
                <div key={tier} className={clsx('card p-5 border-2 transition-all', color, isCurrent && 'ring-2 ring-primary')}>
                  <div className="flex items-center justify-between mb-3">
                    <TierBadge tier={tier} />
                    {isCurrent && <span className="text-[10px] font-black bg-primary text-white px-2 py-0.5 rounded-full">CURRENT</span>}
                  </div>
                  <p className="text-2xl font-black text-gray-900 mt-2">{price}</p>
                  <p className="text-xs text-gray-500 mb-3">Commission: <strong>{commission}</strong></p>
                  <ul className="space-y-1.5 mb-4">
                    {features.map((f) => (
                      <li key={f} className="flex items-start gap-1.5 text-xs text-gray-600">
                        <CheckCircle size={11} className="text-emerald-500 flex-shrink-0 mt-0.5" />
                        {f}
                      </li>
                    ))}
                  </ul>
                  {!isCurrent && (
                    <button onClick={() => navigate('/subscription')} className="w-full btn-primary text-xs py-2">
                      {tier === 'basic' ? 'Downgrade' : 'Upgrade'}
                    </button>
                  )}
                </div>
              );
            })}
          </div>
          <div className="success-box">
            <strong>💡 Tip:</strong> Upgrading to Gold saves you 3% commission per order. At your current volume of R48,750/month, Gold would save you approximately <strong>R1,462/month</strong> in commission fees.
          </div>
        </div>
      )}

      {/* Document Preview Modal */}
      {previewDoc && (() => {
        const resolvedUrl = resolveUploadUrl(previewDoc.docUrl);
        const isPdf = resolvedUrl?.match(/\.(pdf)$/i);
        const isImage = resolvedUrl?.match(/\.(jpg|jpeg|png|webp|gif)$/i);
        const statusBadge = {
          approved: { className: 'bg-emerald-50 text-emerald-700 border-emerald-200', icon: <CheckCircle size={13} />, label: 'Approved' },
          rejected: { className: 'bg-red-50 text-red-700 border-red-200',             icon: <XCircle size={13} />,     label: 'Rejected' },
          pending:  { className: 'bg-amber-50 text-amber-700 border-amber-200',        icon: <Clock size={13} />,       label: 'Pending' },
        }[previewDoc.status];

        return (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
            <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={() => setPreviewDoc(null)} />
            <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-3xl max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">

              {/* Header */}
              <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 flex-shrink-0">
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 bg-slate-100 rounded-xl flex items-center justify-center">
                    <FileText size={16} className="text-slate-600" />
                  </div>
                  <div>
                    <p className="font-bold text-gray-900 text-sm">{previewDoc.label}</p>
                    <p className="text-xs text-gray-400">{profile.companyName}</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className={clsx('inline-flex items-center gap-1.5 text-xs font-bold px-2.5 py-1 rounded-full border', statusBadge.className)}>
                    {statusBadge.icon}
                    {statusBadge.label}
                  </span>
                  {resolvedUrl && (
                    <>
                      <a href={resolvedUrl} download className="p-2 rounded-lg text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all" title="Download">
                        <Download size={15} />
                      </a>
                      <a href={resolvedUrl} target="_blank" rel="noreferrer" className="p-2 rounded-lg text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-all" title="Open in new tab">
                        <ExternalLink size={15} />
                      </a>
                    </>
                  )}
                  <button onClick={() => setPreviewDoc(null)} className="p-2 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-all">
                    <X size={15} />
                  </button>
                </div>
              </div>

              {/* Info Bar */}
              <div className="flex items-center gap-6 px-6 py-3 bg-gray-50 border-b border-gray-100 text-xs text-gray-500 flex-shrink-0">
                {previewDoc.expiryDate && (
                  <span><strong className="text-gray-700">Expires:</strong> {format(parseISO(previewDoc.expiryDate), 'dd MMM yyyy')}</span>
                )}
                {previewDoc.status === 'rejected' && previewDoc.rejectionReason && (
                  <span className="text-red-600"><strong className="text-red-700">Rejection reason:</strong> {previewDoc.rejectionReason}</span>
                )}
                {previewDoc.status === 'approved' && (
                  <span className="text-emerald-600 font-semibold flex items-center gap-1"><CheckCircle size={11} /> This document has been verified and approved</span>
                )}
              </div>

              {/* Preview Area */}
              <div className="flex-1 overflow-auto p-6 min-h-0">
                {!resolvedUrl ? (
                  <div className="h-full min-h-[360px] flex flex-col items-center justify-center bg-gradient-to-br from-slate-50 to-gray-100 rounded-2xl border-2 border-dashed border-gray-200">
                    <FileText size={32} className="text-slate-400 mb-3" />
                    <p className="font-bold text-gray-600 text-sm">Document not available for preview</p>
                    <p className="text-xs text-gray-400 mt-1">The file may not have been uploaded yet</p>
                  </div>
                ) : isPdf ? (
                  <iframe
                    src={resolvedUrl}
                    className="w-full h-full min-h-[500px] rounded-xl border border-gray-200"
                    title={previewDoc.label}
                  />
                ) : isImage ? (
                  <img
                    src={resolvedUrl}
                    alt={previewDoc.label}
                    className="max-w-full mx-auto rounded-xl border border-gray-200 shadow-sm"
                  />
                ) : (
                  <div className="h-full min-h-[360px] flex flex-col items-center justify-center bg-gradient-to-br from-slate-50 to-gray-100 rounded-2xl border-2 border-dashed border-gray-200">
                    <FileText size={32} className="text-slate-400 mb-3" />
                    <p className="font-bold text-gray-700 text-sm">{previewDoc.label}</p>
                    <p className="text-xs text-gray-400 mt-1 mb-4">Preview not available for this file type</p>
                    <a href={resolvedUrl} target="_blank" rel="noreferrer"
                      className="flex items-center gap-2 text-sm font-semibold text-blue-600 border border-blue-200 bg-blue-50 px-4 py-2 rounded-xl hover:bg-blue-100 transition-colors">
                      <ExternalLink size={13} /> Open Document
                    </a>
                  </div>
                )}
              </div>

              {/* Footer */}
              <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-gray-100 flex-shrink-0">
                <button onClick={() => setPreviewDoc(null)} className="btn-secondary">Close</button>
              </div>
            </div>
          </div>
        );
      })()}
    </div>
  );
}
