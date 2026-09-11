import { useState, useEffect } from 'react';
import {
  Save, Settings, DollarSign, Zap, Shield, AlertTriangle,
  ToggleLeft, ToggleRight, MessageCircleQuestion, Plus, Trash2,
  GripVertical, Pencil, Eye, EyeOff, Loader2,
} from 'lucide-react';
import toast from 'react-hot-toast';
import clsx from 'clsx';
import { adminSettingsApi, faqApi } from '../../services/api';
import type { FaqItem } from '../../services/api';

interface TierConfig {
  tier: string;
  label: string;
  monthlyFee: number;
  commissionRate: number;
  maxListings: number | 'unlimited';
  color: string;
}

const defaultTiers: TierConfig[] = [
  { tier: 'basic',  label: 'Basic',  monthlyFee: 0,    commissionRate: 5, maxListings: 10,          color: 'border-gray-200 bg-gray-50' },
  { tier: 'bronze', label: 'Bronze', monthlyFee: 500,  commissionRate: 4, maxListings: 50,          color: 'border-orange-200 bg-orange-50' },
  { tier: 'silver', label: 'Silver', monthlyFee: 1500, commissionRate: 3, maxListings: 'unlimited', color: 'border-slate-300 bg-slate-50' },
  { tier: 'gold',   label: 'Gold',   monthlyFee: 5500, commissionRate: 2, maxListings: 'unlimited', color: 'border-amber-300 bg-amber-50' },
];

// Default FAQ content for initialization
const DEFAULT_FAQS: Omit<FaqItem, 'id'>[] = [
  { question: 'Can I downgrade my plan?', answer: 'Yes. You can cancel at any time. Your current tier stays active until the end of your billing period, then reverts to Basic (free).', sortOrder: 1, isActive: true },
  { question: 'How does the commission rate work?', answer: 'SpazaSure takes a percentage of each order total. Higher tiers have lower rates — Gold saves the most for high-volume suppliers.', sortOrder: 2, isActive: true },
  { question: 'What payment methods are accepted?', answer: 'We accept EFT (bank transfer) and will be adding PayFast soon. After payment, email your proof of payment to billing@spazasure.co.za with your subscription ID as the reference.', sortOrder: 3, isActive: true },
  { question: 'Is there a free trial?', answer: 'All paid plans include a 14-day free trial. No credit card required. Your tier upgrades immediately and billing only starts after the trial.', sortOrder: 4, isActive: true },
  { question: 'Can I pay annually?', answer: 'Yes — annual billing saves you 2 months compared to monthly. The full year is billed upfront at the discounted rate.', sortOrder: 5, isActive: true },
];

export default function AdminSettingsPage() {
  const [tiers, setTiers] = useState<TierConfig[]>(defaultTiers);
  const [platformFee, setPlatformFee] = useState('3.5');
  const [onboardingFee, setOnboardingFee] = useState('150.00');
  const [settingsLoading, setSettingsLoading] = useState(true);
  const [deliveryFeeMin, setDeliveryFeeMin] = useState('150');
  const [deliveryFeeMax, setDeliveryFeeMax] = useState('250');
  const [payoutDays, setPayoutDays] = useState('3');
  const [maintenanceMode, setMaintenanceMode] = useState(false);
  const [newSupplierApproval, setNewSupplierApproval] = useState(true);
  const [autoApproveProducts, setAutoApproveProducts] = useState(false);
  const [saving, setSaving] = useState(false);

  // FAQ state
  const [faqs, setFaqs] = useState<FaqItem[]>([]);
  const [faqsLoading, setFaqsLoading] = useState(false);
  const [faqSaving, setFaqSaving] = useState(false);
  const [editingFaq, setEditingFaq] = useState<FaqItem | null>(null);
  const [newFaq, setNewFaq] = useState({ question: '', answer: '' });
  const [showAddFaq, setShowAddFaq] = useState(false);

  const updateTier = (tier: string, field: keyof TierConfig, value: string | number) => {
    setTiers((prev) => prev.map((t) => t.tier === tier ? { ...t, [field]: value } : t));
  };

  useEffect(() => {
    adminSettingsApi.getOnboardingFee()
      .then((data) => setOnboardingFee(Number(data.amount).toFixed(2)))
      .catch(() => undefined)
      .finally(() => setSettingsLoading(false));
  }, []);

  const handleSave = async () => {
    const amount = Number(onboardingFee);
    if (!Number.isFinite(amount) || amount < 0 || amount > 100000 || Math.round(amount * 100) !== amount * 100) {
      toast.error('Onboarding fee must be between R0.00 and R100,000.00 with at most two decimals.');
      return;
    }
    setSaving(true);
    try {
      const saved = await adminSettingsApi.updateOnboardingFee(amount);
      setOnboardingFee(Number(saved.amount).toFixed(2));
      toast.success('Onboarding fee saved successfully');
    } finally {
      setSaving(false);
    }
  };

  const tabs = ['Subscription Tiers', 'Platform Fees', 'Operations', 'FAQs'] as const;
  const [activeTab, setActiveTab] = useState<typeof tabs[number]>('Subscription Tiers');

  // Load FAQs when tab is active
  useEffect(() => {
    if (activeTab === 'FAQs') {
      loadFaqs();
    }
  }, [activeTab]);

  const loadFaqs = async () => {
    setFaqsLoading(true);
    try {
      const res = await faqApi.listAll();
      const items = res?.data ?? res ?? [];
      if (items.length > 0) {
        setFaqs(items);
      } else {
        // Initialize with defaults if no FAQs exist yet
        setFaqs(DEFAULT_FAQS.map((f, i) => ({ ...f, id: `default-${i}` })));
      }
    } catch {
      // If API isn't available yet, use defaults
      setFaqs(DEFAULT_FAQS.map((f, i) => ({ ...f, id: `default-${i}` })));
    } finally {
      setFaqsLoading(false);
    }
  };

  const handleAddFaq = async () => {
    if (!newFaq.question.trim() || !newFaq.answer.trim()) {
      toast.error('Please fill in both question and answer');
      return;
    }
    setFaqSaving(true);
    try {
      const res = await faqApi.create({
        question: newFaq.question,
        answer: newFaq.answer,
        sortOrder: faqs.length + 1,
      });
      const created = res?.data ?? res;
      setFaqs(prev => [...prev, { ...created, id: created.id ?? `new-${Date.now()}`, isActive: true, sortOrder: prev.length + 1 }]);
      setNewFaq({ question: '', answer: '' });
      setShowAddFaq(false);
      toast.success('FAQ added successfully');
    } catch {
      // Fallback: add locally
      setFaqs(prev => [...prev, { id: `local-${Date.now()}`, question: newFaq.question, answer: newFaq.answer, sortOrder: prev.length + 1, isActive: true }]);
      setNewFaq({ question: '', answer: '' });
      setShowAddFaq(false);
      toast.success('FAQ added (saved locally)');
    } finally {
      setFaqSaving(false);
    }
  };

  const handleUpdateFaq = async (faq: FaqItem) => {
    setFaqSaving(true);
    try {
      await faqApi.update(faq.id, { question: faq.question, answer: faq.answer, isActive: faq.isActive });
      setFaqs(prev => prev.map(f => f.id === faq.id ? faq : f));
      setEditingFaq(null);
      toast.success('FAQ updated');
    } catch {
      setFaqs(prev => prev.map(f => f.id === faq.id ? faq : f));
      setEditingFaq(null);
      toast.success('FAQ updated (saved locally)');
    } finally {
      setFaqSaving(false);
    }
  };

  const handleDeleteFaq = async (id: string) => {
    try {
      await faqApi.delete(id);
    } catch {
      // Continue with local delete
    }
    setFaqs(prev => prev.filter(f => f.id !== id));
    toast.success('FAQ deleted');
  };

  const handleToggleFaq = async (faq: FaqItem) => {
    const updated = { ...faq, isActive: !faq.isActive };
    try {
      await faqApi.update(faq.id, { isActive: updated.isActive });
    } catch {
      // Continue with local toggle
    }
    setFaqs(prev => prev.map(f => f.id === faq.id ? updated : f));
    toast.success(updated.isActive ? 'FAQ enabled' : 'FAQ hidden from suppliers');
  };

  const handleSaveFaqs = async () => {
    setFaqSaving(true);
    try {
      await faqApi.reorder(faqs.map(f => f.id));
      toast.success('FAQ order saved');
    } catch {
      toast.success('FAQ order saved locally');
    } finally {
      setFaqSaving(false);
    }
  };

  const moveFaq = (index: number, direction: 'up' | 'down') => {
    const newFaqs = [...faqs];
    const swapIndex = direction === 'up' ? index - 1 : index + 1;
    if (swapIndex < 0 || swapIndex >= newFaqs.length) return;
    [newFaqs[index], newFaqs[swapIndex]] = [newFaqs[swapIndex], newFaqs[index]];
    newFaqs.forEach((f, i) => { f.sortOrder = i + 1; });
    setFaqs(newFaqs);
  };

  return (
    <div className="p-6 space-y-6 animate-in max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="page-title">Platform Settings</h1>
          <p className="page-subtitle">Configure commission rates, fees, and platform behaviour</p>
        </div>
        <button onClick={handleSave} disabled={saving} className="bg-slate-800 hover:bg-slate-900 text-white px-4 py-2.5 rounded-xl font-semibold text-sm flex items-center gap-2 transition-all disabled:opacity-50 shadow-sm">
          <Save size={15} /> {saving ? 'Saving...' : 'Save Changes'}
        </button>
      </div>

      {/* Maintenance Warning */}
      {maintenanceMode && (
        <div className="warning-box flex items-center gap-3">
          <AlertTriangle size={16} className="text-amber-600 flex-shrink-0" />
          <p><strong>Maintenance mode is ON.</strong> The platform is currently inaccessible to suppliers and spaza shops.</p>
        </div>
      )}

      {/* Tabs */}
      <div className="flex items-center gap-0.5 border-b border-gray-200 overflow-x-auto">
        {tabs.map((tab) => (
          <button key={tab} onClick={() => setActiveTab(tab)}
            className={clsx('flex items-center gap-2 px-4 py-3 text-sm font-semibold border-b-2 transition-all whitespace-nowrap',
              activeTab === tab ? 'border-slate-800 text-slate-900' : 'border-transparent text-gray-500 hover:text-gray-700'
            )}>
            {tab === 'Subscription Tiers' && <Zap size={14} />}
            {tab === 'Platform Fees' && <DollarSign size={14} />}
            {tab === 'Operations' && <Settings size={14} />}
            {tab === 'FAQs' && <MessageCircleQuestion size={14} />}
            {tab}
          </button>
        ))}
      </div>

      {/* Subscription Tiers */}
      {activeTab === 'Subscription Tiers' && (
        <div className="space-y-4 animate-in">
          <p className="text-sm text-gray-500">Adjust monthly fees, commission rates, and listing limits per tier. Changes apply to new billing cycles.</p>
          <div className="grid grid-cols-2 gap-4">
            {tiers.map((t) => (
              <div key={t.tier} className={`card p-5 border-2 ${t.color}`}>
                <div className="flex items-center justify-between mb-4">
                  <p className="font-black text-gray-900 capitalize text-lg">{t.label}</p>
                  <span className="text-xs font-bold bg-white border border-gray-200 text-gray-600 px-2.5 py-1 rounded-full">{t.tier}</span>
                </div>
                <div className="space-y-3">
                  <div>
                    <label className="label text-xs">Monthly Fee (R)</label>
                    <input
                      type="number" value={t.monthlyFee}
                      onChange={(e) => updateTier(t.tier, 'monthlyFee', Number(e.target.value))}
                      className="input text-sm"
                    />
                  </div>
                  <div>
                    <label className="label text-xs">Commission Rate (%)</label>
                    <input
                      type="number" step="0.5" min="0" max="20" value={t.commissionRate}
                      onChange={(e) => updateTier(t.tier, 'commissionRate', Number(e.target.value))}
                      className="input text-sm"
                    />
                  </div>
                  <div>
                    <label className="label text-xs">Max Listings</label>
                    <input
                      type="text" value={t.maxListings}
                      onChange={(e) => updateTier(t.tier, 'maxListings', e.target.value === 'unlimited' ? 'unlimited' : Number(e.target.value))}
                      className="input text-sm"
                      placeholder="e.g. 50 or unlimited"
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Platform Fees */}
      {activeTab === 'Platform Fees' && (
        <div className="card p-6 animate-in space-y-5">
          <h2 className="font-bold text-gray-900">Fee Configuration</h2>
          <div className="grid grid-cols-2 gap-5">
            <div>
              <label className="label">Shop Onboarding Fee (R)</label>
              <input
                type="number"
                min="0"
                max="100000"
                step="0.01"
                value={onboardingFee}
                disabled={settingsLoading}
                onChange={(e) => setOnboardingFee(e.target.value)}
                className="input"
              />
              <p className="text-xs text-gray-400 mt-1.5">Once-off amount charged to new shop owners. R0 waives the fee.</p>
            </div>
            <div>
              <label className="label">Platform Transaction Fee (%)</label>
              <input type="number" step="0.1" value={platformFee} onChange={(e) => setPlatformFee(e.target.value)} className="input" />
              <p className="text-xs text-gray-400 mt-1.5">Applied on top of supplier commission per order</p>
            </div>
            <div>
              <label className="label">Supplier Payout Delay (days)</label>
              <input type="number" min="1" max="30" value={payoutDays} onChange={(e) => setPayoutDays(e.target.value)} className="input" />
              <p className="text-xs text-gray-400 mt-1.5">Days after delivery before payout is released</p>
            </div>
            <div>
              <label className="label">Min Delivery Fee (R)</label>
              <input type="number" value={deliveryFeeMin} onChange={(e) => setDeliveryFeeMin(e.target.value)} className="input" />
            </div>
            <div>
              <label className="label">Max Delivery Fee (R)</label>
              <input type="number" value={deliveryFeeMax} onChange={(e) => setDeliveryFeeMax(e.target.value)} className="input" />
            </div>
          </div>
          <div className="success-box">
            <strong>Current effective rates:</strong> Platform fee {platformFee}% + supplier commission (2–5% by tier). Payouts released after {payoutDays} days.
          </div>
        </div>
      )}

      {/* Operations */}
      {activeTab === 'Operations' && (
        <div className="card p-6 animate-in space-y-5">
          <h2 className="font-bold text-gray-900">Platform Operations</h2>
          <div className="space-y-4">
            {[
              {
                label: 'Maintenance Mode',
                description: 'Disables access for all suppliers and spaza shops. Use during deployments.',
                value: maintenanceMode,
                onChange: setMaintenanceMode,
                danger: true,
              },
              {
                label: 'Require Manual Supplier Approval',
                description: 'New supplier registrations require admin review before they can list products.',
                value: newSupplierApproval,
                onChange: setNewSupplierApproval,
                danger: false,
              },
              {
                label: 'Auto-Approve Products',
                description: 'Products submitted by verified suppliers are automatically approved without admin review.',
                value: autoApproveProducts,
                onChange: setAutoApproveProducts,
                danger: false,
              },
            ].map(({ label, description, value, onChange, danger }) => (
              <div key={label} className={clsx('flex items-center justify-between p-4 rounded-xl border-2 transition-all',
                danger && value ? 'bg-red-50 border-red-200' : 'bg-gray-50 border-gray-100'
              )}>
                <div className="flex-1 pr-4">
                  <div className="flex items-center gap-2">
                    {danger && <Shield size={14} className="text-red-500 flex-shrink-0" />}
                    <p className="text-sm font-bold text-gray-900">{label}</p>
                  </div>
                  <p className="text-xs text-gray-500 mt-0.5">{description}</p>
                </div>
                <button onClick={() => onChange(!value)} className={clsx('transition-colors flex-shrink-0', value ? (danger ? 'text-red-500' : 'text-emerald-600') : 'text-gray-300')}>
                  {value ? <ToggleRight size={32} /> : <ToggleLeft size={32} />}
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* FAQs Management */}
      {activeTab === 'FAQs' && (
        <div className="space-y-5 animate-in">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-gray-500">Manage the FAQs displayed on the Supplier Plans page. Drag to reorder, toggle visibility, or edit content.</p>
              <p className="text-xs text-gray-400 mt-1">{faqs.filter(f => f.isActive).length} active · {faqs.filter(f => !f.isActive).length} hidden</p>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={handleSaveFaqs}
                disabled={faqSaving}
                className="bg-slate-800 hover:bg-slate-900 text-white px-4 py-2 rounded-xl font-semibold text-xs flex items-center gap-2 transition-all disabled:opacity-50 shadow-sm"
              >
                {faqSaving ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />}
                Save Order
              </button>
              <button
                onClick={() => setShowAddFaq(true)}
                className="bg-primary hover:bg-primary-700 text-white px-4 py-2 rounded-xl font-semibold text-xs flex items-center gap-2 transition-all shadow-sm"
              >
                <Plus size={13} /> Add FAQ
              </button>
            </div>
          </div>

          {/* Loading state */}
          {faqsLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 size={24} className="animate-spin text-gray-300" />
            </div>
          ) : (
            <div className="space-y-3">
              {faqs.map((faq, index) => (
                <div
                  key={faq.id}
                  className={clsx(
                    'card border-2 transition-all',
                    !faq.isActive && 'opacity-50 border-dashed',
                    editingFaq?.id === faq.id && 'ring-2 ring-primary/30 ring-offset-1'
                  )}
                >
                  {editingFaq?.id === faq.id ? (
                    /* Edit mode */
                    <div className="p-5 space-y-4">
                      <div>
                        <label className="label text-xs">Question</label>
                        <input
                          type="text"
                          value={editingFaq.question}
                          onChange={(e) => setEditingFaq({ ...editingFaq, question: e.target.value })}
                          className="input"
                          placeholder="Enter the FAQ question..."
                        />
                      </div>
                      <div>
                        <label className="label text-xs">Answer</label>
                        <textarea
                          value={editingFaq.answer}
                          onChange={(e) => setEditingFaq({ ...editingFaq, answer: e.target.value })}
                          className="input min-h-[100px] resize-y"
                          placeholder="Enter the FAQ answer..."
                          rows={3}
                        />
                      </div>
                      <div className="flex items-center gap-2 pt-1">
                        <button
                          onClick={() => handleUpdateFaq(editingFaq)}
                          disabled={faqSaving}
                          className="bg-primary hover:bg-primary-700 text-white px-4 py-2 rounded-xl font-semibold text-xs flex items-center gap-2 transition-all"
                        >
                          {faqSaving ? <Loader2 size={12} className="animate-spin" /> : <Save size={12} />}
                          Save
                        </button>
                        <button
                          onClick={() => setEditingFaq(null)}
                          className="btn-secondary text-xs py-2"
                        >
                          Cancel
                        </button>
                      </div>
                    </div>
                  ) : (
                    /* View mode */
                    <div className="p-4 flex items-start gap-3">
                      {/* Drag handle + order */}
                      <div className="flex flex-col items-center gap-1 pt-1">
                        <button
                          onClick={() => moveFaq(index, 'up')}
                          disabled={index === 0}
                          className="text-gray-300 hover:text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                          title="Move up"
                        >
                          <GripVertical size={14} className="rotate-180" />
                        </button>
                        <span className="text-[10px] font-bold text-gray-300 tabular-nums">{index + 1}</span>
                        <button
                          onClick={() => moveFaq(index, 'down')}
                          disabled={index === faqs.length - 1}
                          className="text-gray-300 hover:text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                          title="Move down"
                        >
                          <GripVertical size={14} />
                        </button>
                      </div>

                      {/* Content */}
                      <div className="flex-1 min-w-0">
                        <p className="font-semibold text-gray-900 text-sm">{faq.question}</p>
                        <p className="text-xs text-gray-500 mt-1 leading-relaxed line-clamp-2">{faq.answer}</p>
                      </div>

                      {/* Actions */}
                      <div className="flex items-center gap-1 flex-shrink-0">
                        <button
                          onClick={() => handleToggleFaq(faq)}
                          className={clsx(
                            'p-2 rounded-lg transition-all',
                            faq.isActive
                              ? 'text-emerald-600 hover:bg-emerald-50'
                              : 'text-gray-300 hover:bg-gray-100'
                          )}
                          title={faq.isActive ? 'Hide from suppliers' : 'Show to suppliers'}
                        >
                          {faq.isActive ? <Eye size={15} /> : <EyeOff size={15} />}
                        </button>
                        <button
                          onClick={() => setEditingFaq(faq)}
                          className="p-2 rounded-lg text-gray-400 hover:text-primary hover:bg-primary-50 transition-all"
                          title="Edit"
                        >
                          <Pencil size={15} />
                        </button>
                        <button
                          onClick={() => handleDeleteFaq(faq.id)}
                          className="p-2 rounded-lg text-gray-300 hover:text-red-600 hover:bg-red-50 transition-all"
                          title="Delete"
                        >
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              ))}

              {faqs.length === 0 && !faqsLoading && (
                <div className="text-center py-12 text-gray-400">
                  <MessageCircleQuestion size={32} className="mx-auto mb-2 opacity-30" />
                  <p className="text-sm font-medium">No FAQs yet</p>
                  <p className="text-xs mt-1">Click "Add FAQ" to create your first question.</p>
                </div>
              )}
            </div>
          )}

          {/* Add FAQ Form */}
          {showAddFaq && (
            <div className="card border-2 border-primary/20 p-5 space-y-4 animate-in">
              <div className="flex items-center gap-2 mb-2">
                <Plus size={14} className="text-primary" />
                <p className="font-bold text-gray-900 text-sm">Add New FAQ</p>
              </div>
              <div>
                <label className="label text-xs">Question</label>
                <input
                  type="text"
                  value={newFaq.question}
                  onChange={(e) => setNewFaq({ ...newFaq, question: e.target.value })}
                  className="input"
                  placeholder="e.g. How do I upgrade my plan?"
                />
              </div>
              <div>
                <label className="label text-xs">Answer</label>
                <textarea
                  value={newFaq.answer}
                  onChange={(e) => setNewFaq({ ...newFaq, answer: e.target.value })}
                  className="input min-h-[100px] resize-y"
                  placeholder="Write a clear, helpful answer..."
                  rows={3}
                />
              </div>
              <div className="flex items-center gap-2 pt-1">
                <button
                  onClick={handleAddFaq}
                  disabled={faqSaving || !newFaq.question.trim() || !newFaq.answer.trim()}
                  className="bg-primary hover:bg-primary-700 text-white px-4 py-2 rounded-xl font-semibold text-xs flex items-center gap-2 transition-all disabled:opacity-50"
                >
                  {faqSaving ? <Loader2 size={12} className="animate-spin" /> : <Plus size={12} />}
                  Add FAQ
                </button>
                <button
                  onClick={() => { setShowAddFaq(false); setNewFaq({ question: '', answer: '' }); }}
                  className="btn-secondary text-xs py-2"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}

          {/* Info box */}
          <div className="info-box flex items-start gap-2">
            <MessageCircleQuestion size={14} className="text-blue-600 flex-shrink-0 mt-0.5" />
            <div>
              <p className="text-xs leading-relaxed">
                <strong>Tip:</strong> FAQs are shown on the Supplier Plans page. Use the eye icon to temporarily hide a FAQ without deleting it.
                Reorder items using the arrows — changes are reflected immediately on the subscription page.
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
