import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Zap, Check, X, Star, Shield, BarChart2, Package,
  Headphones, Code2, Palette, ArrowRight, Crown,
  ChevronDown, ChevronUp, Receipt, AlertTriangle,
  CheckCircle, Clock, Loader2, ExternalLink, Sparkles,
  TrendingUp, Trophy,
} from 'lucide-react';
import toast from 'react-hot-toast';
import clsx from 'clsx';
import { subscriptionApi, paymentApi, faqApi } from '../../services/api';
import { useAuthStore } from '../../store/authStore';
import type { SubscriptionPlan, CurrentSubscription } from '../../types';
import { Modal, Spinner } from '../../components/ui';
import { format } from 'date-fns';

// ─── Tier theme config ────────────────────────────────────────────────────────
const TIER_THEME = {
  basic: {
    gradient: 'from-slate-700 via-slate-600 to-slate-500',
    lightGradient: 'from-slate-50 to-gray-50',
    border: 'border-slate-200',
    ring: 'ring-slate-300',
    badge: 'bg-slate-100 text-slate-700 border-slate-200',
    icon: '⚡',
    iconBg: 'bg-slate-100',
    glow: '',
    popular: false,
    accentColor: 'slate',
  },
  bronze: {
    gradient: 'from-orange-600 via-amber-500 to-orange-400',
    lightGradient: 'from-orange-50 to-amber-50',
    border: 'border-orange-200',
    ring: 'ring-orange-300',
    badge: 'bg-orange-50 text-orange-700 border-orange-200',
    icon: '🥉',
    iconBg: 'bg-orange-100',
    glow: 'shadow-[0_8px_40px_rgba(234,88,12,0.12)]',
    popular: false,
    accentColor: 'orange',
  },
  silver: {
    gradient: 'from-indigo-600 via-purple-500 to-indigo-400',
    lightGradient: 'from-indigo-50 to-purple-50',
    border: 'border-indigo-200',
    ring: 'ring-indigo-300',
    badge: 'bg-indigo-50 text-indigo-700 border-indigo-200',
    icon: '🥈',
    iconBg: 'bg-indigo-100',
    glow: 'shadow-[0_8px_40px_rgba(99,102,241,0.15)]',
    popular: true,
    accentColor: 'indigo',
  },
  gold: {
    gradient: 'from-amber-500 via-yellow-400 to-amber-300',
    lightGradient: 'from-amber-50 to-yellow-50',
    border: 'border-amber-300',
    ring: 'ring-amber-400',
    badge: 'bg-amber-50 text-amber-700 border-amber-200',
    icon: '🥇',
    iconBg: 'bg-amber-100',
    glow: 'shadow-[0_8px_40px_rgba(245,158,11,0.20)]',
    popular: false,
    accentColor: 'amber',
  },
} as const;

// ─── Feature rows for comparison ─────────────────────────────────────────────
const FEATURE_ROWS = [
  { label: 'Commission Rate', key: 'commissionRate', type: 'rate' },
  { label: 'Product Listings', key: 'maxListings', type: 'number' },
  { label: 'Orders / Month', key: 'maxOrders', type: 'number' },
  { label: 'Analytics Dashboard', key: 'hasAnalytics', type: 'bool' },
  { label: 'Bulk Pricing', key: 'hasBulkPricing', type: 'bool' },
  { label: 'Priority Support', key: 'hasPrioritySupport', type: 'bool' },
  { label: 'API Access', key: 'hasApiAccess', type: 'bool' },
  { label: 'Custom Branding', key: 'hasCustomBranding', type: 'bool' },
];

// Default FAQs (fallback if API is unavailable)
const DEFAULT_FAQS = [
  {
    q: 'Can I downgrade my plan?',
    a: 'Yes. You can cancel at any time. Your current tier stays active until the end of your billing period, then reverts to Basic (free).',
  },
  {
    q: 'How does the commission rate work?',
    a: 'SpazaSure takes a percentage of each order total. Higher tiers have lower rates — Gold saves the most for high-volume suppliers.',
  },
  {
    q: 'What payment methods are accepted?',
    a: 'We accept EFT (bank transfer) and will be adding PayFast soon. After payment, email your proof of payment to billing@spazasure.co.za with your subscription ID as the reference.',
  },
  {
    q: 'Is there a free trial?',
    a: 'All paid plans include a 14-day free trial. No credit card required. Your tier upgrades immediately and billing only starts after the trial.',
  },
  {
    q: 'Can I pay annually?',
    a: 'Yes — annual billing saves you 2 months compared to monthly. The full year is billed upfront at the discounted rate.',
  },
];

// ─── Component ────────────────────────────────────────────────────────────────
export default function SubscriptionPage() {
  const { user, setUser } = useAuthStore();
  const navigate = useNavigate();

  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [current, setCurrent] = useState<CurrentSubscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [billingCycle, setBillingCycle] = useState<'monthly' | 'annual'>('monthly');
  const [selectedPlan, setSelectedPlan] = useState<SubscriptionPlan | null>(null);
  const [subscribing, setSubscribing] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [showCancel, setShowCancel] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [openFaq, setOpenFaq] = useState<number | null>(null);
  const [showHistory, setShowHistory] = useState(false);
  const [history, setHistory] = useState<any[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [paymentStatus, setPaymentStatus] = useState<'success' | 'cancelled' | null>(null);
  const [faqs, setFaqs] = useState<{ q: string; a: string }[]>(DEFAULT_FAQS);

  // Handle PayFast return URL query params
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const payment = params.get('payment');
    if (payment === 'success') {
      setPaymentStatus('success');
      toast.success('Payment received! Your subscription is being activated.', { duration: 6000 });
      window.history.replaceState({}, '', '/subscription');
    } else if (payment === 'cancelled') {
      setPaymentStatus('cancelled');
      toast.error('Payment was cancelled. You can try again anytime.', { duration: 5000 });
      window.history.replaceState({}, '', '/subscription');
    }
  }, []);

  useEffect(() => {
    const load = async () => {
      try {
        const [plansRes, currentRes] = await Promise.allSettled([
          subscriptionApi.getPlans(),
          subscriptionApi.getCurrent(),
        ]);
        if (plansRes.status === 'fulfilled') {
          const raw = plansRes.value?.data ?? plansRes.value ?? [];
          setPlans(raw);
        }
        if (currentRes.status === 'fulfilled') {
          const d = currentRes.value?.data ?? currentRes.value;
          setCurrent(d);
        }
      } finally {
        setLoading(false);
      }
    };
    load();

    // Load FAQs from API (falls back to defaults on error)
    const loadFaqs = async () => {
      try {
        const res = await faqApi.list();
        const items = res?.data ?? res ?? [];
        if (items.length > 0) {
          setFaqs(items.map((f: any) => ({ q: f.question, a: f.answer })));
        }
      } catch {
        // Keep default FAQs
      }
    };
    loadFaqs();
  }, []);

  const handleSelectPlan = (plan: SubscriptionPlan) => {
    if (plan.tier === current?.currentTier) return;
    setSelectedPlan(plan);
    setShowConfirm(true);
  };

  const handleSubscribe = async () => {
    if (!selectedPlan) return;
    setSubscribing(true);
    try {
      const res = await subscriptionApi.subscribe({
        tier: selectedPlan.tier,
        billingCycle,
        paymentMethod: selectedPlan.monthlyPrice === 0 ? 'free' : 'payfast',
        confirmPayment: selectedPlan.monthlyPrice === 0,
      });
      const d = res?.data ?? res;

      if (selectedPlan.monthlyPrice === 0) {
        if (user && d?.tier) {
          setUser({ ...user, tier: d.tier });
        }
        setShowConfirm(false);
        toast.success(`Switched to ${selectedPlan.name} successfully.`);
        const refreshed = await subscriptionApi.getCurrent();
        setCurrent(refreshed?.data ?? refreshed);
      } else {
        const payRes = await paymentApi.initiate(d.subscriptionId);
        const payData = payRes?.data ?? payRes;

        if (payData?.payFastUrl && payData?.paymentData) {
          const form = document.createElement('form');
          form.method = 'POST';
          form.action = payData.payFastUrl;
          form.target = '_self';

          Object.entries(payData.paymentData as Record<string, string>).forEach(([key, value]) => {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = key;
            input.value = value;
            form.appendChild(input);
          });

          document.body.appendChild(form);
          form.submit();
        } else {
          toast.error('Failed to initiate payment. Please try again.');
          setShowConfirm(false);
        }
      }
    } catch {
      toast.error('Failed to process subscription. Please try again.');
    } finally {
      setSubscribing(false);
    }
  };

  const handleCancel = async () => {
    setCancelling(true);
    try {
      await subscriptionApi.cancel({ reason: 'User requested cancellation' });
      toast.success('Subscription cancelled. You will revert to Basic at the end of your billing period.');
      setShowCancel(false);
      if (user) setUser({ ...user, tier: 'basic' });
      const refreshed = await subscriptionApi.getCurrent();
      setCurrent(refreshed?.data ?? refreshed);
    } catch {
      toast.error('Failed to cancel subscription. Please try again.');
    } finally {
      setCancelling(false);
    }
  };

  const loadHistory = async () => {
    setHistoryLoading(true);
    try {
      const res = await subscriptionApi.getHistory();
      setHistory(res?.data ?? res ?? []);
      setShowHistory(true);
    } finally {
      setHistoryLoading(false);
    }
  };

  const annualSavings = (plan: SubscriptionPlan) => {
    if (!plan.monthlyPrice) return 0;
    const annualIfMonthly = plan.monthlyPrice * 12;
    return Math.round(annualIfMonthly - plan.annualPrice);
  };

  const displayPrice = (plan: SubscriptionPlan) =>
    billingCycle === 'annual' && plan.annualPrice > 0
      ? Math.round(plan.annualPrice / 12)
      : plan.monthlyPrice;

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="text-center space-y-4">
          <div className="relative">
            <div className="w-16 h-16 rounded-full bg-gradient-to-br from-primary-100 to-primary-50 flex items-center justify-center mx-auto">
              <Spinner size="lg" />
            </div>
            <div className="absolute inset-0 w-16 h-16 rounded-full bg-primary/10 animate-ping mx-auto" />
          </div>
          <p className="text-sm text-gray-400 font-medium animate-pulse">Loading plans…</p>
        </div>
      </div>
    );
  }

  const currentTier = current?.currentTier ?? user?.tier ?? 'basic';
  const currentPlan = plans.find(p => p.tier === currentTier);
  const hasActivePaidSub = current?.subscription?.status === 'active' && currentTier !== 'basic';

  return (
    <div className="p-4 md:p-8 space-y-10 max-w-7xl mx-auto">

      {/* ── Hero Header ───────────────────────────────────────────────────── */}
      <div className="relative rounded-[2rem] overflow-hidden shadow-card-xl">
        {/* Background layers */}
        <div className="absolute inset-0 bg-gradient-to-br from-primary-800 via-primary-700 to-primary-500" />
        <div className="absolute inset-0 bg-gradient-mesh opacity-40" />
        <div className="absolute top-0 right-0 w-[500px] h-[500px] bg-gradient-to-bl from-accent/20 to-transparent rounded-full -translate-y-1/2 translate-x-1/4" />
        <div className="absolute bottom-0 left-0 w-80 h-80 bg-primary-400/20 rounded-full translate-y-1/2 -translate-x-1/4" />
        <div className="absolute top-1/2 right-1/3 w-32 h-32 bg-white/5 rounded-full animate-pulse-soft" />

        {/* Floating decorative dots */}
        <div className="absolute top-8 right-20 w-2 h-2 rounded-full bg-accent/40 animate-bounce-sm" />
        <div className="absolute top-20 right-40 w-1.5 h-1.5 rounded-full bg-white/30 animate-bounce-sm" style={{ animationDelay: '1s' }} />
        <div className="absolute bottom-12 right-60 w-2.5 h-2.5 rounded-full bg-primary-300/30 animate-bounce-sm" style={{ animationDelay: '2s' }} />

        <div className="relative z-10 p-8 md:p-10">
          <div className="flex flex-col lg:flex-row items-start justify-between gap-8">
            <div className="space-y-5 animate-fade-in">
              {/* Badge */}
              <div className="inline-flex items-center gap-2 bg-white/10 backdrop-blur-sm border border-white/20 rounded-full px-4 py-1.5 shadow-sm">
                <Sparkles size={12} className="text-accent" />
                <span className="text-[11px] font-bold text-white/90 tracking-widest uppercase">Supplier Plans</span>
              </div>

              {/* Headline */}
              <div>
                <h1 className="text-3xl md:text-4xl font-black text-white leading-[1.15] tracking-tight">
                  Grow faster.
                  <br />
                  <span className="bg-gradient-to-r from-accent-400 to-yellow-300 bg-clip-text text-transparent">
                    Pay less commission.
                  </span>
                </h1>
                <p className="text-green-200/80 text-sm md:text-base max-w-lg leading-relaxed mt-3">
                  Upgrade your plan to unlock lower commission rates, more product listings, and powerful tools to scale your spaza shop network.
                </p>
              </div>

              {/* Current plan chips */}
              <div className="flex flex-wrap items-center gap-3">
                <div className="group flex items-center gap-3 bg-white/10 backdrop-blur-sm border border-white/15 rounded-2xl px-5 py-3 hover:bg-white/15 transition-all duration-300">
                  <span className="text-xl">{TIER_THEME[currentTier as keyof typeof TIER_THEME]?.icon ?? '⚡'}</span>
                  <div>
                    <p className="text-[9px] text-green-300/80 font-bold uppercase tracking-[0.15em]">Current Plan</p>
                    <p className="text-base font-black text-white capitalize">{currentTier}</p>
                  </div>
                </div>
                <div className="group flex items-center gap-3 bg-white/10 backdrop-blur-sm border border-white/15 rounded-2xl px-5 py-3 hover:bg-white/15 transition-all duration-300">
                  <div className="w-8 h-8 rounded-xl bg-white/10 flex items-center justify-center">
                    <TrendingUp size={14} className="text-green-300" />
                  </div>
                  <div>
                    <p className="text-[9px] text-green-300/80 font-bold uppercase tracking-[0.15em]">Commission</p>
                    <p className="text-base font-black text-white">{current?.commissionRate ?? 5}%</p>
                  </div>
                </div>
                {current?.subscription?.nextBillingDate && (
                  <div className="group flex items-center gap-3 bg-white/10 backdrop-blur-sm border border-white/15 rounded-2xl px-5 py-3 hover:bg-white/15 transition-all duration-300">
                    <div className="w-8 h-8 rounded-xl bg-white/10 flex items-center justify-center">
                      <Clock size={14} className="text-green-300" />
                    </div>
                    <div>
                      <p className="text-[9px] text-green-300/80 font-bold uppercase tracking-[0.15em]">Next Bill</p>
                      <p className="text-base font-black text-white">
                        {format(new Date(current.subscription.nextBillingDate), 'dd MMM')}
                      </p>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* Right side actions */}
            <div className="hidden lg:flex flex-col items-end gap-3 flex-shrink-0 animate-fade-in" style={{ animationDelay: '0.2s' }}>
              <button
                onClick={loadHistory}
                disabled={historyLoading}
                className="group flex items-center gap-2.5 bg-white/10 backdrop-blur-sm hover:bg-white/20 border border-white/20 text-white text-sm font-semibold px-5 py-3 rounded-2xl transition-all duration-300 hover:shadow-lg hover:shadow-white/5"
              >
                {historyLoading ? <Loader2 size={15} className="animate-spin" /> : <Receipt size={15} className="group-hover:scale-110 transition-transform" />}
                Billing History
              </button>
              {hasActivePaidSub && (
                <button
                  onClick={() => setShowCancel(true)}
                  className="group flex items-center gap-2.5 bg-white/5 hover:bg-red-500/20 border border-white/10 hover:border-red-400/40 text-white/60 hover:text-red-300 text-sm font-semibold px-5 py-3 rounded-2xl transition-all duration-300"
                >
                  <X size={15} className="group-hover:rotate-90 transition-transform duration-300" /> Cancel Plan
                </button>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ── Payment Status Banner ─────────────────────────────────────── */}
      {paymentStatus === 'success' && (
        <div className="bg-gradient-to-r from-emerald-50 to-green-50 border border-emerald-200 rounded-2xl p-5 flex items-center gap-4 animate-slide-up shadow-sm">
          <div className="w-10 h-10 rounded-xl bg-emerald-100 flex items-center justify-center flex-shrink-0">
            <CheckCircle size={20} className="text-emerald-600" />
          </div>
          <div>
            <p className="font-bold text-emerald-900">Payment successful!</p>
            <p className="text-xs text-emerald-700 mt-0.5">Your subscription is being activated. This usually takes a few seconds. Refresh if your tier doesn't update.</p>
          </div>
        </div>
      )}
      {paymentStatus === 'cancelled' && (
        <div className="bg-gradient-to-r from-amber-50 to-orange-50 border border-amber-200 rounded-2xl p-5 flex items-center gap-4 animate-slide-up shadow-sm">
          <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center flex-shrink-0">
            <AlertTriangle size={20} className="text-amber-600" />
          </div>
          <div>
            <p className="font-bold text-amber-900">Payment cancelled</p>
            <p className="text-xs text-amber-700 mt-0.5">No charge was made. You can select a plan below to try again.</p>
          </div>
        </div>
      )}

      {/* ── Billing Toggle ────────────────────────────────────────────────── */}
      <div className="flex flex-col items-center gap-4 animate-fade-in" style={{ animationDelay: '0.1s' }}>
        <div className="relative flex items-center gap-1 bg-white border border-gray-200/80 rounded-2xl p-1.5 shadow-card">
          {/* Animated background pill */}
          <div
            className={clsx(
              'absolute top-1.5 bottom-1.5 rounded-xl bg-gradient-to-r from-primary-700 to-primary-500 shadow-md transition-all duration-300 ease-out',
              billingCycle === 'monthly' ? 'left-1.5 w-[calc(50%-6px)]' : 'left-[calc(50%+2px)] w-[calc(50%-6px)]'
            )}
          />
          {(['monthly', 'annual'] as const).map((cycle) => (
            <button
              key={cycle}
              onClick={() => setBillingCycle(cycle)}
              className={clsx(
                'relative z-10 flex items-center justify-center gap-2 px-7 py-2.5 rounded-xl text-sm font-bold transition-all duration-300',
                billingCycle === cycle
                  ? 'text-white'
                  : 'text-gray-400 hover:text-gray-600'
              )}
            >
              {cycle === 'monthly' ? 'Monthly' : 'Annual'}
              {cycle === 'annual' && (
                <span className={clsx(
                  'text-[9px] font-black px-2 py-0.5 rounded-full transition-all duration-300',
                  billingCycle === 'annual' ? 'bg-accent text-white' : 'bg-accent/10 text-accent'
                )}>
                  SAVE 17%
                </span>
              )}
            </button>
          ))}
        </div>
        {billingCycle === 'annual' && (
          <p className="text-xs text-gray-400 font-medium animate-fade-in flex items-center gap-1.5">
            <Sparkles size={12} className="text-accent" />
            Annual billing saves you 2 months — billed once per year
          </p>
        )}
      </div>

      {/* ── Plan Cards ────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
        {plans.map((plan, idx) => {
          const theme = TIER_THEME[plan.tier as keyof typeof TIER_THEME] ?? TIER_THEME.basic;
          const isCurrent = plan.tier === currentTier;
          const price = displayPrice(plan);
          const savings = annualSavings(plan);
          const isUpgrade = plans.findIndex(p => p.tier === plan.tier) > plans.findIndex(p => p.tier === currentTier);
          const isDowngrade = plans.findIndex(p => p.tier === plan.tier) < plans.findIndex(p => p.tier === currentTier);

          return (
            <div
              key={plan.tier}
              className={clsx(
                'group relative rounded-[1.5rem] border-2 bg-white transition-all duration-500 flex flex-col overflow-hidden animate-fade-in',
                isCurrent
                  ? `${theme.border} ring-2 ${theme.ring} ring-offset-2 ${theme.glow} scale-[1.02]`
                  : `border-gray-100 hover:border-gray-200 hover:shadow-card-xl hover:-translate-y-2 ${theme.glow}`,
                theme.popular && !isCurrent && 'ring-2 ring-indigo-200 ring-offset-2 hover:ring-indigo-300'
              )}
              style={{ animationDelay: `${idx * 0.1}s` }}
            >
              {/* Popular badge */}
              {theme.popular && !isCurrent && (
                <div className="absolute -top-px left-1/2 -translate-x-1/2 z-10">
                  <div className="relative">
                    <span className="bg-gradient-to-r from-indigo-600 to-purple-600 text-white text-[10px] font-black px-4 py-1.5 rounded-b-xl shadow-lg flex items-center gap-1.5">
                      <Star size={9} fill="white" /> MOST POPULAR
                    </span>
                  </div>
                </div>
              )}

              {/* Current plan badge */}
              {isCurrent && (
                <div className="absolute top-4 right-4 z-10">
                  <span className={clsx(
                    'text-[9px] font-black px-3 py-1.5 rounded-full border backdrop-blur-sm shadow-sm',
                    theme.badge
                  )}>
                    ✓ CURRENT
                  </span>
                </div>
              )}

              {/* Card Header with gradient */}
              <div className={clsx('relative p-6 pb-5 bg-gradient-to-br text-white overflow-hidden', theme.gradient)}>
                {/* Decorative circles in header */}
                <div className="absolute -top-6 -right-6 w-24 h-24 bg-white/10 rounded-full" />
                <div className="absolute -bottom-4 -left-4 w-16 h-16 bg-white/5 rounded-full" />

                <div className="relative">
                  <div className="flex items-start justify-between mb-4">
                    <div>
                      <span className="text-3xl drop-shadow-sm">{theme.icon}</span>
                      <h3 className="text-xl font-black mt-2 tracking-tight">{plan.name}</h3>
                    </div>
                    <div className="bg-white/15 backdrop-blur-sm border border-white/20 text-[10px] font-black px-3 py-1.5 rounded-full text-white/90">
                      {plan.commissionRate}% commission
                    </div>
                  </div>

                  {/* Price display */}
                  <div className="flex items-end gap-1 mt-2">
                    {plan.monthlyPrice === 0 ? (
                      <span className="text-4xl font-black tracking-tight">Free</span>
                    ) : (
                      <>
                        <span className="text-lg font-bold opacity-70 mb-1">R</span>
                        <span className="text-4xl font-black tabular-nums tracking-tight">{price.toLocaleString()}</span>
                        <span className="text-sm font-medium opacity-60 mb-1">/mo</span>
                      </>
                    )}
                  </div>

                  {billingCycle === 'annual' && savings > 0 && (
                    <div className="mt-2 inline-flex items-center gap-1 bg-white/15 border border-white/20 rounded-full px-2.5 py-1 text-[10px] font-bold text-white/90">
                      <Sparkles size={9} /> Save R{savings.toLocaleString()}/year
                    </div>
                  )}
                  {billingCycle === 'annual' && plan.annualPrice > 0 && (
                    <p className="mt-1 text-[10px] text-white/50">
                      Billed R{plan.annualPrice.toLocaleString()} annually
                    </p>
                  )}
                </div>
              </div>

              {/* Features section */}
              <div className="p-6 flex-1 space-y-4">
                <p className="text-xs text-gray-500 leading-relaxed">{plan.description}</p>

                {/* Key metrics - big numbers */}
                <div className="grid grid-cols-2 gap-3">
                  <div className={clsx('rounded-2xl p-3.5 text-center border transition-colors', `bg-gradient-to-br ${theme.lightGradient} border-gray-100`)}>
                    <p className="text-2xl font-black text-gray-900">{plan.maxListings === 999 ? '∞' : plan.maxListings}</p>
                    <p className="text-[10px] text-gray-400 font-bold uppercase tracking-wider mt-0.5">Products</p>
                  </div>
                  <div className={clsx('rounded-2xl p-3.5 text-center border transition-colors', `bg-gradient-to-br ${theme.lightGradient} border-gray-100`)}>
                    <p className="text-2xl font-black text-gray-900">{plan.maxOrders === 9999 ? '∞' : plan.maxOrders.toLocaleString()}</p>
                    <p className="text-[10px] text-gray-400 font-bold uppercase tracking-wider mt-0.5">Orders/mo</p>
                  </div>
                </div>

                {/* Feature checklist */}
                <div className="space-y-2.5 pt-1">
                  {[
                    { key: 'hasAnalytics', label: 'Analytics Dashboard', value: plan.hasAnalytics },
                    { key: 'hasBulkPricing', label: 'Bulk Pricing', value: plan.hasBulkPricing },
                    { key: 'hasPrioritySupport', label: 'Priority Support', value: plan.hasPrioritySupport },
                    { key: 'hasApiAccess', label: 'API Access', value: plan.hasApiAccess },
                    { key: 'hasCustomBranding', label: 'Custom Branding', value: plan.hasCustomBranding },
                  ].map(({ key, label, value }) => (
                    <div key={key} className="flex items-center gap-2.5">
                      {value ? (
                        <div className="w-5 h-5 rounded-full bg-primary-50 flex items-center justify-center flex-shrink-0">
                          <Check size={11} className="text-primary" strokeWidth={3} />
                        </div>
                      ) : (
                        <div className="w-5 h-5 rounded-full bg-gray-50 flex items-center justify-center flex-shrink-0">
                          <X size={11} className="text-gray-300" strokeWidth={3} />
                        </div>
                      )}
                      <span className={clsx('text-[13px]', value ? 'text-gray-700 font-medium' : 'text-gray-300')}>
                        {label}
                      </span>
                    </div>
                  ))}
                </div>
              </div>

              {/* CTA Button */}
              <div className="p-5 pt-0">
                {isCurrent ? (
                  <button disabled className="w-full py-3 rounded-xl text-sm font-bold bg-gray-50 text-gray-400 cursor-not-allowed border border-gray-100">
                    Current Plan
                  </button>
                ) : isDowngrade ? (
                  <button
                    onClick={() => handleSelectPlan(plan)}
                    className="w-full py-3 rounded-xl text-sm font-bold border-2 border-gray-200 text-gray-500 hover:border-gray-300 hover:text-gray-700 hover:bg-gray-50 transition-all duration-300"
                  >
                    Downgrade
                  </button>
                ) : (
                  <button
                    onClick={() => handleSelectPlan(plan)}
                    className={clsx(
                      'w-full py-3 rounded-xl text-sm font-bold text-white transition-all duration-300 flex items-center justify-center gap-2',
                      'shadow-md hover:shadow-xl hover:-translate-y-0.5 active:scale-[0.97]',
                      `bg-gradient-to-r ${theme.gradient}`
                    )}
                  >
                    {plan.monthlyPrice === 0 ? 'Get Started Free' : `Upgrade to ${plan.name}`}
                    <ArrowRight size={14} className="group-hover:translate-x-0.5 transition-transform" />
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* ── Commission Savings Calculator ─────────────────────────────────── */}
      <CommissionCalculator currentTier={currentTier} plans={plans} />

      {/* ── Full Comparison Table ─────────────────────────────────────────── */}
      <div className="bg-white rounded-[1.5rem] border border-gray-100/80 shadow-card overflow-hidden">
        <div className="px-7 py-5 border-b border-gray-100 bg-gradient-to-r from-gray-50/80 to-white">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-primary-50 flex items-center justify-center">
              <BarChart2 size={18} className="text-primary" />
            </div>
            <div>
              <h2 className="font-black text-gray-900 text-lg tracking-tight">Full Plan Comparison</h2>
              <p className="text-sm text-gray-400 mt-0.5">Everything you get at each tier</p>
            </div>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100">
                <th className="table-header w-48">Feature</th>
                {plans.map(p => (
                  <th key={p.tier} className={clsx('table-header text-center', p.tier === currentTier && 'text-primary')}>
                    <span className="inline-flex items-center gap-1.5">
                      <span className="text-base">{TIER_THEME[p.tier as keyof typeof TIER_THEME]?.icon}</span>
                      <span>{p.name}</span>
                      {p.tier === currentTier && <span className="w-1.5 h-1.5 rounded-full bg-primary animate-pulse" />}
                    </span>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {FEATURE_ROWS.map(({ label, key, type }) => (
                <tr key={key} className="table-row">
                  <td className="table-cell text-gray-600 font-semibold">{label}</td>
                  {plans.map(p => {
                    const val = (p as any)[key];
                    return (
                      <td key={p.tier} className={clsx('table-cell text-center', p.tier === currentTier && 'bg-primary-50/20')}>
                        {type === 'bool' ? (
                          val
                            ? <div className="w-6 h-6 rounded-full bg-primary-50 flex items-center justify-center mx-auto"><Check size={13} className="text-primary" strokeWidth={3} /></div>
                            : <div className="w-6 h-6 rounded-full bg-gray-50 flex items-center justify-center mx-auto"><X size={13} className="text-gray-300" /></div>
                        ) : type === 'rate' ? (
                          <span className="font-black text-gray-900 text-base">{val}%</span>
                        ) : (
                          <span className="font-bold text-gray-900">
                            {val === 999 || val === 9999 ? '∞' : val?.toLocaleString()}
                          </span>
                        )}
                      </td>
                    );
                  })}
                </tr>
              ))}
              <tr className="table-row bg-gradient-to-r from-gray-50/50 to-transparent">
                <td className="table-cell text-gray-600 font-semibold">Monthly Price</td>
                {plans.map(p => (
                  <td key={p.tier} className={clsx('table-cell text-center font-black text-gray-900', p.tier === currentTier && 'text-primary')}>
                    {p.monthlyPrice === 0 ? (
                      <span className="inline-flex items-center gap-1 bg-primary-50 text-primary px-3 py-1 rounded-full text-xs font-black">Free</span>
                    ) : (
                      `R${p.monthlyPrice.toLocaleString()}/mo`
                    )}
                  </td>
                ))}
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* ── FAQ ───────────────────────────────────────────────────────────── */}
      <div className="space-y-4">
        <div className="flex items-center gap-3 mb-2">
          <div className="w-10 h-10 rounded-xl bg-amber-50 flex items-center justify-center">
            <span className="text-lg">💡</span>
          </div>
          <div>
            <h2 className="font-black text-gray-900 text-lg tracking-tight">Frequently Asked Questions</h2>
            <p className="text-sm text-gray-400">Quick answers to common questions</p>
          </div>
        </div>
        <div className="grid gap-3">
          {faqs.map(({ q, a }, i) => (
            <div
              key={i}
              className={clsx(
                'bg-white rounded-2xl border transition-all duration-300 overflow-hidden',
                openFaq === i ? 'border-primary-200 shadow-card-hover' : 'border-gray-100 hover:border-gray-200 shadow-card'
              )}
            >
              <button
                onClick={() => setOpenFaq(openFaq === i ? null : i)}
                className="w-full flex items-center justify-between px-6 py-4.5 text-left group"
              >
                <span className="font-semibold text-gray-900 text-sm group-hover:text-primary transition-colors">{q}</span>
                <div className={clsx(
                  'w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0 transition-all duration-300',
                  openFaq === i ? 'bg-primary-100 rotate-180' : 'bg-gray-100 group-hover:bg-gray-200'
                )}>
                  <ChevronDown size={14} className={clsx(openFaq === i ? 'text-primary' : 'text-gray-400')} />
                </div>
              </button>
              <div className={clsx(
                'overflow-hidden transition-all duration-300',
                openFaq === i ? 'max-h-40 opacity-100' : 'max-h-0 opacity-0'
              )}>
                <div className="px-6 pb-5 text-sm text-gray-500 leading-relaxed border-t border-gray-50 pt-3">
                  {a}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* ── Support Banner ────────────────────────────────────────────────── */}
      <div className="relative bg-gradient-to-br from-primary-50 via-emerald-50 to-green-50 border border-primary-100/60 rounded-[1.5rem] p-7 overflow-hidden">
        <div className="absolute top-0 right-0 w-40 h-40 bg-primary-100/30 rounded-full -translate-y-1/2 translate-x-1/2" />
        <div className="absolute bottom-0 left-20 w-24 h-24 bg-emerald-100/40 rounded-full translate-y-1/2" />

        <div className="relative flex flex-col sm:flex-row items-center justify-between gap-5">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-white rounded-2xl border border-primary-100 flex items-center justify-center flex-shrink-0 shadow-sm">
              <Shield size={22} className="text-primary" />
            </div>
            <div>
              <p className="font-bold text-gray-900 text-base">Need help choosing a plan?</p>
              <p className="text-sm text-gray-500 mt-0.5">Our team is ready to help you pick the right tier for your business.</p>
            </div>
          </div>
          <a
            href="mailto:support@spazasure.co.za"
            className="flex items-center gap-2 bg-primary text-white text-sm font-bold px-6 py-3 rounded-xl hover:bg-primary-700 transition-all duration-300 hover:shadow-lg hover:shadow-primary/20 flex-shrink-0 active:scale-[0.97]"
          >
            Contact Support <ExternalLink size={13} />
          </a>
        </div>
      </div>

      {/* ── Confirm Subscribe Modal ───────────────────────────────────────── */}
      {showConfirm && selectedPlan && (
        <Modal title={`Subscribe to ${selectedPlan.name}`} onClose={() => !subscribing && setShowConfirm(false)} size="md">
          <div className="p-6 space-y-5">
            {/* Plan summary */}
            <div className={clsx(
              'relative rounded-2xl p-6 bg-gradient-to-br text-white overflow-hidden',
              TIER_THEME[selectedPlan.tier as keyof typeof TIER_THEME]?.gradient ?? 'from-gray-600 to-gray-500'
            )}>
              <div className="absolute -top-8 -right-8 w-32 h-32 bg-white/10 rounded-full" />
              <div className="absolute -bottom-6 -left-6 w-20 h-20 bg-white/5 rounded-full" />
              <div className="relative flex items-center justify-between">
                <div>
                  <p className="text-white/60 text-xs font-semibold uppercase tracking-widest">Upgrading to</p>
                  <h3 className="text-2xl font-black mt-0.5">{selectedPlan.name}</h3>
                  <p className="text-white/80 text-sm mt-1">{selectedPlan.commissionRate}% commission rate</p>
                </div>
                <div className="text-right">
                  <p className="text-3xl font-black">
                    {selectedPlan.monthlyPrice === 0 ? 'Free' : `R${displayPrice(selectedPlan).toLocaleString()}`}
                  </p>
                  {selectedPlan.monthlyPrice > 0 && (
                    <p className="text-white/60 text-xs mt-0.5">{billingCycle === 'annual' ? 'per month, billed annually' : 'per month'}</p>
                  )}
                </div>
              </div>
            </div>

            {/* Payment instructions for paid plans */}
            {selectedPlan.monthlyPrice > 0 && (
              <div className="bg-blue-50 border border-blue-100 rounded-2xl p-5">
                <div className="flex items-start gap-3">
                  <div className="w-8 h-8 rounded-xl bg-blue-100 flex items-center justify-center flex-shrink-0 mt-0.5">
                    <Shield size={14} className="text-blue-600" />
                  </div>
                  <div>
                    <p className="font-bold text-blue-900 text-sm">Secure payment via PayFast</p>
                    <p className="mt-1.5 text-xs text-blue-700 leading-relaxed">
                      You'll be redirected to <strong>PayFast</strong> — South Africa's trusted payment gateway — to complete your payment of{' '}
                      <strong>R{billingCycle === 'annual' ? selectedPlan.annualPrice.toLocaleString() : selectedPlan.monthlyPrice.toLocaleString()}</strong>.
                    </p>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {['💳 Credit/Debit Card', '🏦 EFT/Bank Transfer', '📱 SnapScan', '🔄 Mobicred'].map(method => (
                        <span key={method} className="text-[10px] font-semibold bg-white/80 border border-blue-200 px-2.5 py-1 rounded-full text-blue-700">
                          {method}
                        </span>
                      ))}
                    </div>
                    <p className="mt-3 text-[10px] text-blue-500 flex items-center gap-3 flex-wrap">
                      <span className="flex items-center gap-1"><Check size={9} strokeWidth={3} /> 256-bit SSL</span>
                      <span className="flex items-center gap-1"><Check size={9} strokeWidth={3} /> PCI DSS Level 1</span>
                      <span className="flex items-center gap-1"><Check size={9} strokeWidth={3} /> Recurring billing</span>
                    </p>
                  </div>
                </div>
              </div>
            )}

            {selectedPlan.monthlyPrice === 0 && (
              <div className="bg-emerald-50 border border-emerald-100 rounded-2xl p-4 flex items-center gap-3">
                <CheckCircle size={16} className="text-emerald-600 flex-shrink-0" />
                <p className="text-sm text-emerald-800">Basic is free — your plan will change immediately with no payment required.</p>
              </div>
            )}

            {/* What you get */}
            <div>
              <p className="text-xs font-bold text-gray-400 uppercase tracking-widest mb-3">What you'll get</p>
              <div className="grid grid-cols-2 gap-2.5">
                {[
                  `${selectedPlan.maxListings === 999 ? 'Unlimited' : selectedPlan.maxListings} product listings`,
                  `${selectedPlan.maxOrders === 9999 ? 'Unlimited' : selectedPlan.maxOrders.toLocaleString()} orders/month`,
                  selectedPlan.hasAnalytics && 'Analytics dashboard',
                  selectedPlan.hasBulkPricing && 'Bulk pricing tools',
                  selectedPlan.hasPrioritySupport && 'Priority support',
                  selectedPlan.hasApiAccess && 'API access',
                ].filter(Boolean).map((item, i) => (
                  <div key={i} className="flex items-center gap-2 text-xs text-gray-700 bg-gray-50 rounded-lg px-3 py-2">
                    <Check size={12} className="text-primary flex-shrink-0" strokeWidth={3} />
                    {item}
                  </div>
                ))}
              </div>
            </div>

            <div className="flex gap-3 pt-2">
              <button
                onClick={() => setShowConfirm(false)}
                disabled={subscribing}
                className="btn-secondary flex-1"
              >
                Back
              </button>
              <button
                onClick={handleSubscribe}
                disabled={subscribing}
                className={clsx(
                  'flex-1 flex items-center justify-center gap-2 py-3 rounded-xl text-sm font-bold text-white transition-all duration-300',
                  `bg-gradient-to-r ${TIER_THEME[selectedPlan.tier as keyof typeof TIER_THEME]?.gradient ?? 'from-primary-700 to-primary-500'}`,
                  'hover:shadow-lg active:scale-[0.97]'
                )}
              >
                {subscribing ? <><Loader2 size={14} className="animate-spin" /> Processing…</> : <>Pay with PayFast <ArrowRight size={14} /></>}
              </button>
            </div>
          </div>
        </Modal>
      )}

      {/* ── Cancel Modal ─────────────────────────────────────────────────── */}
      {showCancel && (
        <Modal title="Cancel Subscription" onClose={() => !cancelling && setShowCancel(false)} size="sm">
          <div className="p-6 space-y-5">
            <div className="bg-red-50 border border-red-100 rounded-2xl p-5">
              <div className="flex items-start gap-3">
                <div className="w-8 h-8 rounded-xl bg-red-100 flex items-center justify-center flex-shrink-0">
                  <AlertTriangle size={14} className="text-red-600" />
                </div>
                <div>
                  <p className="font-bold text-red-900 text-sm">Are you sure?</p>
                  <p className="text-xs text-red-700 mt-1.5 leading-relaxed">
                    Your {currentTier} plan stays active until the end of your billing period, then reverts to Basic (5% commission, 10 products).
                  </p>
                </div>
              </div>
            </div>
            <div className="flex gap-3">
              <button onClick={() => setShowCancel(false)} disabled={cancelling} className="btn-secondary flex-1">
                Keep Plan
              </button>
              <button
                onClick={handleCancel}
                disabled={cancelling}
                className="btn-danger flex-1 flex items-center justify-center gap-2"
              >
                {cancelling ? <><Loader2 size={14} className="animate-spin" /> Cancelling…</> : 'Yes, Cancel'}
              </button>
            </div>
          </div>
        </Modal>
      )}

      {/* ── Billing History Modal ─────────────────────────────────────────── */}
      {showHistory && (
        <Modal title="Billing History" onClose={() => setShowHistory(false)} size="lg">
          <div className="p-6">
            {history.length === 0 ? (
              <div className="text-center py-12">
                <div className="w-14 h-14 rounded-2xl bg-gray-50 flex items-center justify-center mx-auto mb-3">
                  <Receipt size={24} className="text-gray-300" />
                </div>
                <p className="text-sm font-medium text-gray-400">No billing history yet.</p>
                <p className="text-xs text-gray-300 mt-1">Transactions will appear here once you subscribe.</p>
              </div>
            ) : (
              <div className="space-y-3">
                {history.map((item: any) => (
                  <div key={item.id} className="flex items-center justify-between p-4 rounded-2xl border border-gray-100 hover:bg-gray-50 hover:border-gray-200 transition-all duration-200">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-primary-50 flex items-center justify-center text-primary">
                        <Receipt size={16} />
                      </div>
                      <div>
                        <p className="font-semibold text-gray-900 text-sm capitalize">
                          {item.plan?.name ?? item.plan?.tier} — {item.billingCycle}
                        </p>
                        <p className="text-xs text-gray-400 mt-0.5">
                          {format(new Date(item.startDate), 'dd MMM yyyy')}
                          {item.endDate && ` → ${format(new Date(item.endDate), 'dd MMM yyyy')}`}
                        </p>
                        {item.paymentReference && (
                          <p className="text-xs text-gray-300 mt-0.5">Ref: {item.paymentReference}</p>
                        )}
                      </div>
                    </div>
                    <div className="text-right">
                      <p className="font-black text-gray-900">
                        {item.amountPaid === 0 ? 'Free' : `R${Number(item.amountPaid).toLocaleString()}`}
                      </p>
                      <span className={clsx(
                        'text-[10px] font-bold px-2.5 py-0.5 rounded-full',
                        item.status === 'active' ? 'bg-emerald-50 text-emerald-700' :
                        item.status === 'cancelled' ? 'bg-red-50 text-red-700' :
                        item.status === 'pending' ? 'bg-amber-50 text-amber-700' :
                        'bg-gray-100 text-gray-600'
                      )}>
                        {item.status}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </Modal>
      )}
    </div>
  );
}

// ─── Commission Savings Calculator ───────────────────────────────────────────
function CommissionCalculator({ currentTier, plans }: { currentTier: string; plans: SubscriptionPlan[] }) {
  const [monthlyRevenue, setMonthlyRevenue] = useState(50000);

  const currentPlan = plans.find(p => p.tier === currentTier);
  const currentCommission = currentPlan?.commissionRate ?? 5;

  return (
    <div className="bg-white rounded-[1.5rem] border border-gray-100/80 shadow-card p-7 space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          <div className="w-11 h-11 rounded-xl bg-gradient-to-br from-primary-100 to-emerald-50 flex items-center justify-center">
            <Zap size={20} className="text-primary" />
          </div>
          <div>
            <h2 className="font-black text-gray-900 text-lg tracking-tight">Commission Savings Calculator</h2>
            <p className="text-sm text-gray-400 mt-0.5">See how much you'd save by upgrading</p>
          </div>
        </div>
      </div>

      {/* Slider */}
      <div className="space-y-3">
        <label className="label flex items-center gap-2">
          Your Monthly Revenue (R)
          <span className="text-primary font-black text-base ml-auto">R{monthlyRevenue.toLocaleString()}</span>
        </label>
        <div className="flex items-center gap-4">
          <input
            type="range"
            min={5000}
            max={500000}
            step={5000}
            value={monthlyRevenue}
            onChange={e => setMonthlyRevenue(Number(e.target.value))}
            className="flex-1 h-2.5 bg-gray-100 rounded-full appearance-none cursor-pointer accent-primary [&::-webkit-slider-thumb]:w-5 [&::-webkit-slider-thumb]:h-5 [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-primary [&::-webkit-slider-thumb]:shadow-md [&::-webkit-slider-thumb]:border-2 [&::-webkit-slider-thumb]:border-white"
          />
        </div>
        <div className="flex justify-between text-[11px] text-gray-300 font-medium">
          <span>R5,000</span><span>R500,000</span>
        </div>
      </div>

      {/* Plan comparison cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {plans.map(plan => {
          const commission = (monthlyRevenue * plan.commissionRate) / 100;
          const currentCommissionAmt = (monthlyRevenue * currentCommission) / 100;
          const saving = currentCommissionAmt - commission;
          const isCurrent = plan.tier === currentTier;
          const theme = TIER_THEME[plan.tier as keyof typeof TIER_THEME] ?? TIER_THEME.basic;

          return (
            <div
              key={plan.tier}
              className={clsx(
                'relative rounded-2xl p-5 text-center border-2 transition-all duration-300 overflow-hidden',
                isCurrent
                  ? `border-transparent bg-gradient-to-br ${theme.gradient} text-white shadow-lg`
                  : 'border-gray-100 bg-white hover:border-gray-200 hover:shadow-md'
              )}
            >
              {/* Decorative */}
              {isCurrent && (
                <>
                  <div className="absolute -top-4 -right-4 w-16 h-16 bg-white/10 rounded-full" />
                  <div className="absolute -bottom-3 -left-3 w-12 h-12 bg-white/5 rounded-full" />
                </>
              )}

              <div className="relative">
                <p className={clsx('text-xs font-bold uppercase tracking-wider mb-3', isCurrent ? 'text-white/70' : 'text-gray-400')}>
                  {theme.icon} {plan.name}
                </p>
                <p className={clsx('text-2xl font-black tabular-nums', isCurrent ? 'text-white' : 'text-gray-900')}>
                  R{commission.toLocaleString(undefined, { maximumFractionDigits: 0 })}
                </p>
                <p className={clsx('text-[10px] mt-1 font-medium', isCurrent ? 'text-white/50' : 'text-gray-400')}>
                  commission/mo
                </p>
                {!isCurrent && saving > 0 && (
                  <div className="mt-3 inline-flex items-center gap-1 bg-primary-50 text-primary text-xs font-black px-2.5 py-1 rounded-full">
                    <TrendingUp size={10} /> Save R{saving.toLocaleString(undefined, { maximumFractionDigits: 0 })}/mo
                  </div>
                )}
                {!isCurrent && saving < 0 && (
                  <p className="text-xs text-gray-300 mt-3">
                    +R{Math.abs(saving).toLocaleString(undefined, { maximumFractionDigits: 0 })}/mo
                  </p>
                )}
                {isCurrent && (
                  <div className="mt-3 inline-flex items-center gap-1 bg-white/15 text-white/80 text-[10px] font-bold px-2.5 py-1 rounded-full">
                    Current
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* Annual savings highlight */}
      {(() => {
        const goldPlan = plans.find(p => p.tier === 'gold');
        if (!goldPlan || currentTier === 'gold') return null;
        const annualSaving = ((monthlyRevenue * (currentCommission - goldPlan.commissionRate)) / 100) * 12;
        if (annualSaving <= 0) return null;
        return (
          <div className="bg-gradient-to-r from-amber-50 via-yellow-50 to-orange-50 border border-amber-200/60 rounded-2xl p-5 flex items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center flex-shrink-0">
                <Trophy size={18} className="text-amber-600" />
              </div>
              <p className="text-sm font-semibold text-amber-900">
                Upgrade to Gold and save{' '}
                <strong className="text-amber-700">R{annualSaving.toLocaleString(undefined, { maximumFractionDigits: 0 })}</strong> in commission annually
              </p>
            </div>
            <span className="text-amber-600 text-xs font-black bg-amber-100 px-3 py-1.5 rounded-full flex-shrink-0">vs {currentTier}</span>
          </div>
        );
      })()}
    </div>
  );
}
