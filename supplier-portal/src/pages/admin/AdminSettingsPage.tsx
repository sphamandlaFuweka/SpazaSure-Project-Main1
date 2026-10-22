import { useState, useEffect } from 'react';
import {
  Save, Settings, DollarSign, Zap, Shield, AlertTriangle,
  ToggleLeft, ToggleRight, MessageCircleQuestion, Plus, Trash2,
  GripVertical, Pencil, Eye, EyeOff, Loader2,
} from 'lucide-react';
import toast from 'react-hot-toast';
import clsx from 'clsx';
import { adminSettingsApi, adminFaqApi, subscriptionApi } from '../../services/api';
import type { FaqItem, PlatformRules } from '../../services/api';

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
const FAQ_UNAVAILABLE = 'The FAQ service is not available, so this change was not saved.';

/** Shown on settings that are displayed here but not yet stored by the backend */
function NotConnectedNote({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
      <AlertTriangle size={16} className="text-amber-600 flex-shrink-0 mt-0.5" />
      <p>{children}</p>
    </div>
  );
}

const DEFAULT_FAQS: Omit<FaqItem, 'id'>[] = [
  { question: 'Can I downgrade my plan?', answer: 'Yes. You can cancel at any time. Cancellation ends your paid plan immediately and reverts to Basic (free).', sortOrder: 1, isActive: true },
  { question: 'How does the commission rate work?', answer: 'SpazaSure takes a percentage of each order total. Higher tiers have lower rates â€” Gold saves the most for high-volume suppliers.', sortOrder: 2, isActive: true },
  { question: 'What payment methods are accepted?', answer: 'Paid supplier plans are processed securely through Paystack in South African rand. Available payment methods are shown during checkout.', sortOrder: 3, isActive: true },
  { question: 'Is there a free trial?', answer: 'Paid plans do not currently include a free trial. The recurring charge starts when you complete Paystack checkout and renews each billing period until you cancel.', sortOrder: 4, isActive: true },
  { question: 'Can I pay annually?', answer: 'Yes â€” annual billing saves you 2 months compared to monthly. The full year is billed upfront at the discounted rate.', sortOrder: 5, isActive: true },
];

export default function AdminSettingsPage() {
  const [tiers, setTiers] = useState<TierConfig[]>(defaultTiers);
  const [onboardingFee, setOnboardingFee] = useState('150.00');
  const [settingsLoading, setSettingsLoading] = useState(true);
  // Platform rules (fees, payouts, operations) â€” saved via /admin/settings/platform
  const [rules, setRules] = useState<PlatformRules | null>(null);
  const [savedRules, setSavedRules] = useState<PlatformRules | null>(null);
  const [rulesSaving, setRulesSaving] = useState(false);
  const maintenanceMode = savedRules?.maintenanceMode ?? false;
  const [saving, setSaving] = useState(false);

  // FAQ state
  const [faqs, setFaqs] = useState<FaqItem[]>([]);
  const [faqsLoading, setFaqsLoading] = useState(false);
  const [faqSaving, setFaqSaving] = useState(false);
  const [editingFaq, setEditingFaq] = useState<FaqItem | null>(null);
  const [newFaq, setNewFaq] = useState({ question: '', answer: '' });
  const [showAddFaq, setShowAddFaq] = useState(false);
  const [faqsFromApi, setFaqsFromApi] = useState(true);

  const updateTier = (tier: string, field: keyof TierConfig, value: string | number) => {
    setTiers((prev) => prev.map((t) => t.tier === tier ? { ...t, [field]: value } : t));
  };

  useEffect(() => {
    adminSettingsApi.getPlatform()
      .then((r) => { setRules(r); setSavedRules(r); })
      .catch(() => undefined);
    // Real plans (read-only here: prices are charged through Paystack plans)
    subscriptionApi.getPlans()
      .then((res) => {
        const plans = (res?.data ?? res ?? []) as any[];
        if (!Array.isArray(plans) || plans.length === 0) return;
        setTiers(defaultTiers.map((t) => {
          const p = plans.find((x) => x.tier === t.tier);
          return p ? { ...t, monthlyFee: Number(p.monthlyPrice), commissionRate: Number(p.commissionRate), maxListings: Number(p.maxListings) } : t;
        }));
      })
      .catch(() => undefined);
  }, []);

  const saveRules = async (next: PlatformRules) => {
    setRulesSaving(true);
    try {
      const saved = await adminSettingsApi.updatePlatform(next);
      setRules(saved);
      setSavedRules(saved);
      toast.success('Settings saved');
    } catch {
      // error toast handled by api interceptor
    } finally {
      setRulesSaving(false);
    }
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
      const res = await adminFaqApi.list();
      const items = res?.data ?? res ?? [];
      // The FAQ service answered, so show exactly what it holds (even an empty list)
      setFaqsFromApi(true);
      setFaqs(Array.isArray(items) ? items : []);
    } catch {
      // Service unreachable: show the built-in FAQs suppliers fall back to, read-only
      setFaqsFromApi(false);
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
      const res = await adminFaqApi.create({
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
      toast.error(FAQ_UNAVAILABLE, { id: 'faq-unavailable' });
    } finally {
      setFaqSaving(false);
    }
  };

  const handleUpdateFaq = async (faq: FaqItem) => {
    setFaqSaving(true);
    try {
      await adminFaqApi.update(faq.id, { question: faq.question, answer: faq.answer, isActive: faq.isActive });
      setFaqs(prev => prev.map(f => f.id === faq.id ? faq : f));
      setEditingFaq(null);
      toast.success('FAQ updated');
    } catch {
      toast.error(FAQ_UNAVAILABLE, { id: 'faq-unavailable' });
    } finally {
      setFaqSaving(false);
    }
  };

  const handleDeleteFaq = async (id: string) => {
    if (!confirm('Delete this FAQ? Suppliers will no longer see it.')) return;
    try {
      await adminFaqApi.delete(id);
      setFaqs(prev => prev.filter(f => f.id !== id));
      toast.success('FAQ deleted');
    } catch {
      toast.error(FAQ_UNAVAILABLE, { id: 'faq-unavailable' });
    }
  };

  const handleToggleFaq = async (faq: FaqItem) => {
    const updated = { ...faq, isActive: !faq.isActive };
    try {
      await adminFaqApi.update(faq.id, { isActive: updated.isActive });
      setFaqs(prev => prev.map(f => f.id === faq.id ? updated : f));
      toast.success(updated.isActive ? 'FAQ enabled' : 'FAQ hidden from suppliers');
    } catch {
      toast.error(FAQ_UNAVAILABLE, { id: 'faq-unavailable' });
    }
  };

  const handleSaveFaqs = async () => {
    setFaqSaving(true);
    try {
      await adminFaqApi.reorder(faqs.map(f => f.id));
      toast.success('FAQ order saved');
    } catch {
      toast.error(FAQ_UNAVAILABLE, { id: 'faq-unavailable' });
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
    <div className="p-4 sm:p-6 space-y-6 animate-in max-w-5xl">
      <div>
        <h1 className="page-title">Platform Settings</h1>
        <p className="page-subtitle">Configure commission rates, fees, and platform behaviour</p>
      </div>

      {/* Maintenance Warning */}
      {maintenanceMode && (
        <div className="warning-box flex items-center gap-3">
          <AlertTriangle size={16} className="text-amber-600 flex-shrink-0" />
          <p><strong>Maintenance mode is ON.</strong> Shops and suppliers are currently blocked. Turn it off under Operations.</p>
        </div>
      )}

      {/* Tabs */}
      <div className="flex items-center gap-0.5 border-b border-gray-200 overflow-x-auto">
        {tabs.map((tab) => (
          <button key={tab} onClick={() => setActiveTab(tab)}
            className={clsx('flex items-center gap-2 px-4 py-3 text-sm font-semibold border-b-2 transition-all whitespace-nowrap',
              activeTab === tab ? 'border-brand-blue text-brand-blue' : 'border-transparent text-gray-500 hover:text-gray-700'
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
          <div className="info-box">
            These are the live plans. Prices are charged through the matching Paystack plans, so change a price in Paystack first,
            then update the plan in the database. Editing prices here would make renewals fail.
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
                      disabled
                      className="input text-sm"
                    />
                  </div>
                  <div>
                    <label className="label text-xs">Commission Rate (%)</label>
                    <input
                      type="number" step="0.5" min="0" max="20" value={t.commissionRate}
                      onChange={(e) => updateTier(t.tier, 'commissionRate', Number(e.target.value))}
                      disabled
                      className="input text-sm"
                    />
                  </div>
                  <div>
                    <label className="label text-xs">Max Listings</label>
                    <input
                      type="text" value={t.maxListings}
                      onChange={(e) => updateTier(t.tier, 'maxListings', e.target.value === 'unlimited' ? 'unlimited' : Number(e.target.value))}
                      disabled
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
        <div className="space-y-5 animate-in">
          <div className="card p-6 space-y-5">
            <div>
              <h2 className="section-title">Shop onboarding fee</h2>
              <p className="text-xs text-ink-3 mt-1">Once-off amount charged to new shop owners. R0 waives the fee.</p>
            </div>
            <div className="flex flex-col sm:flex-row sm:items-end gap-3 max-w-md">
              <div className="flex-1">
                <label className="label">Amount (R)</label>
                <input type="number" min="0" max="100000" step="0.01" value={onboardingFee}
                  disabled={settingsLoading} onChange={(e) => setOnboardingFee(e.target.value)} className="input" />
              </div>
              <button onClick={handleSave} disabled={saving || settingsLoading} className="btn-primary">
                {saving ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />} Save
              </button>
            </div>
          </div>

          <div className="card p-6 space-y-5">
            <div>
              <h2 className="section-title">Order fees & payouts</h2>
              <p className="text-xs text-ink-3 mt-1">Applied to new orders as soon as you save. Existing orders keep the fees they were placed with.</p>
            </div>
            {!rules ? (
              <div className="flex items-center gap-2 text-sm text-ink-3"><Loader2 size={16} className="animate-spin" /> Loading settings...</div>
            ) : (
              <>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                  {([
                    { key: 'standardDeliveryFee', label: 'Standard delivery fee (R)', hint: 'Per supplier on each order. Pickup is always free.', step: '1', max: 5000 },
                    { key: 'expressDeliveryFee', label: 'Express delivery fee (R)', hint: 'Per supplier, same-day delivery.', step: '1', max: 5000 },
                    { key: 'groupBuyBaseDeliveryFee', label: 'Group-buy base delivery fee (R)', hint: 'Group-buy delivery = base fee + distance × rate.', step: '1', max: 5000 },
                    { key: 'groupBuyPerKmRate', label: 'Group-buy rate per km (R)', hint: 'Straight-line distance from supplier to shop.', step: '0.5', max: 500 },
                    { key: 'shopServiceFeePercent', label: 'Shop service fee (%)', hint: 'Extra % of the product subtotal, charged to the shop on top of the supplier\'s tier commission. 0 = off.', step: '0.1', max: 20 },
                    { key: 'payoutDelayDays', label: 'Supplier payout delay (days)', hint: 'Days after delivery before the supplier\'s money becomes available to pay out.', step: '1', max: 60 },
                    { key: 'supplierReachKm', label: 'Supplier reach (km)', hint: 'Shops only see and order from suppliers in their province and within this straight-line distance.', step: '1', max: 2000 },
                  ] as const).map(({ key, label, hint, step, max }) => (
                    <div key={key}>
                      <label className="label" htmlFor={key}>{label}</label>
                      <input id={key} type="number" min="0" max={max} step={step} className="input"
                        value={rules[key]}
                        onChange={(e) => setRules({ ...rules, [key]: e.target.value === '' ? 0 : Number(e.target.value) })} />
                      <p className="text-xs text-ink-3 mt-1.5">{hint}</p>
                    </div>
                  ))}
                </div>
                <div className="info-box">
                  <strong>How an order is charged:</strong> products + delivery (R{rules.standardDeliveryFee} standard / R{rules.expressDeliveryFee} express)
                  + supplier tier commission{rules.shopServiceFeePercent > 0 ? ` + ${rules.shopServiceFeePercent}% service fee` : ''}.
                  The supplier is paid the products + delivery, {rules.payoutDelayDays} day{rules.payoutDelayDays === 1 ? '' : 's'} after delivery.
                </div>
                <div className="flex justify-end">
                  <button onClick={() => saveRules(rules)} disabled={rulesSaving} className="btn-primary">
                    {rulesSaving ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />} Save fees & payouts
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      )}

      {/* Operations */}
      {activeTab === 'Operations' && (
        <div className="card p-6 animate-in space-y-5">
          <div>
            <h2 className="section-title">Platform operations</h2>
            <p className="text-xs text-ink-3 mt-1">Switches take effect within about 15 seconds of saving.</p>
          </div>
          {!rules ? (
            <div className="flex items-center gap-2 text-sm text-ink-3"><Loader2 size={16} className="animate-spin" /> Loading settings...</div>
          ) : (
            <>
              <div className="space-y-4">
                {([
                  {
                    key: 'maintenanceMode', danger: true, label: 'Maintenance mode',
                    description: 'Shops and suppliers see the message below instead of using the platform. Admins, sign-in and Paystack payment notifications keep working.',
                  },
                  {
                    key: 'requireSupplierApproval', danger: false, label: 'Require manual supplier approval',
                    description: 'New suppliers stay hidden from shops until an admin verifies them. When off, they go live immediately as unverified suppliers.',
                  },
                  {
                    key: 'cashOnDeliveryEnabled', danger: false, label: 'Allow cash on delivery',
                    description: 'Shops can choose to pay in cash when the order arrives (app only). SpazaSure collects the cash on delivery and pays the supplier out as usual.',
                  },
                  {
                    key: 'autoApproveVerifiedSupplierProducts', danger: false, label: 'Auto-approve products from verified suppliers',
                    description: 'New products from verified suppliers go live without admin review. Unverified suppliers\' products always need review.',
                  },
                ] as const).map(({ key, danger, label, description }) => {
                  const value = rules[key];
                  return (
                    <div key={key} className={clsx('flex items-center justify-between gap-4 p-4 rounded-2xl border-2 transition-all',
                      danger && value ? 'bg-red-50 border-red-200' : 'bg-surface border-brand-blue-light')}>
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          {danger && <Shield size={14} className="text-red-500 flex-shrink-0" />}
                          <p className="text-sm font-bold text-gray-900">{label}</p>
                        </div>
                        <p className="text-xs text-ink-2 mt-0.5">{description}</p>
                      </div>
                      <button
                        role="switch" aria-checked={value} aria-label={label}
                        onClick={() => setRules({ ...rules, [key]: !value })}
                        className={clsx('transition-colors flex-shrink-0', value ? (danger ? 'text-red-500' : 'text-emerald-600') : 'text-gray-300')}
                      >
                        {value ? <ToggleRight size={34} /> : <ToggleLeft size={34} />}
                      </button>
                    </div>
                  );
                })}
              </div>
              <div className="max-w-xs">
                <label className="label" htmlFor="codMax">Cash on delivery limit (R)</label>
                <input id="codMax" type="number" min="0" max="1000000" step="100" className="input"
                  value={rules.cashOnDeliveryMaxOrderTotal} disabled={!rules.cashOnDeliveryEnabled}
                  onChange={(e) => setRules({ ...rules, cashOnDeliveryMaxOrderTotal: e.target.value === '' ? 0 : Number(e.target.value) })} />
                <p className="text-xs text-ink-3 mt-1.5">Largest order total a shop can pay in cash, to limit cash-handling risk. 0 = no limit.</p>
              </div>
              <div className="rounded-2xl border-2 border-brand-blue-light bg-surface p-4 space-y-4">
                <div>
                  <p className="text-sm font-bold text-gray-900">Direct deposit account</p>
                  <p className="text-xs text-ink-2 mt-0.5">
                    Shops top up their wallet by depositing into this account with their own reference.
                    Confirm each deposit under Wallet Deposits once it reflects in the bank.
                  </p>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {([
                    { key: 'depositBankName', label: 'Bank', maxLength: 80, digits: false },
                    { key: 'depositAccountHolder', label: 'Account holder', maxLength: 120, digits: false },
                    { key: 'depositAccountNumber', label: 'Account number', maxLength: 16, digits: true },
                    { key: 'depositBranchCode', label: 'Branch code', maxLength: 8, digits: true },
                  ] as const).map(({ key, label, maxLength, digits }) => (
                    <div key={key}>
                      <label className="label" htmlFor={key}>{label}</label>
                      <input id={key} className={clsx('input', digits && 'font-mono')} maxLength={maxLength}
                        inputMode={digits ? 'numeric' : undefined} value={rules[key]}
                        onChange={(e) => setRules({ ...rules, [key]: digits ? e.target.value.replace(/\D/g, '') : e.target.value })} />
                    </div>
                  ))}
                </div>
              </div>
              <div>
                <label className="label" htmlFor="maintenanceMessage">Maintenance message</label>
                <textarea id="maintenanceMessage" rows={2} maxLength={300} className="input resize-none"
                  value={rules.maintenanceMessage}
                  onChange={(e) => setRules({ ...rules, maintenanceMessage: e.target.value })} />
                <p className="text-xs text-ink-3 mt-1.5">Shown to shops and suppliers while maintenance mode is on.</p>
              </div>
              <div className="flex justify-end">
                <button
                  onClick={() => {
                    if (rules.maintenanceMode && !savedRules?.maintenanceMode
                      && !confirm('Turn on maintenance mode? Shops and suppliers will be blocked until you turn it off.')) return;
                    saveRules(rules);
                  }}
                  disabled={rulesSaving}
                  className="btn-primary"
                >
                  {rulesSaving ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />} Save operations
                </button>
              </div>
            </>
          )}
        </div>
      )}

      {/* FAQs Management */}
      {activeTab === 'FAQs' && (
        <div className="space-y-5 animate-in">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-gray-500">Manage the FAQs displayed on the Supplier Plans page. Drag to reorder, toggle visibility, or edit content.</p>
              <p className="text-xs text-gray-400 mt-1">{faqs.filter(f => f.isActive).length} active Â· {faqs.filter(f => !f.isActive).length} hidden</p>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={handleSaveFaqs}
                disabled={faqSaving}
                className="bg-brand-blue hover:bg-brand-blue-dark text-white px-4 py-2 rounded-full font-semibold text-xs flex items-center gap-2 transition-all disabled:opacity-50 shadow-sm"
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

          {!faqsLoading && !faqsFromApi && (
            <NotConnectedNote>
              The FAQ service isn't available, so these are the built-in FAQs suppliers currently see. Edits can't be saved until the FAQ API is deployed.
            </NotConnectedNote>
          )}

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
                Reorder items using the arrows â€” changes are reflected immediately on the subscription page.
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
