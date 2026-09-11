import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  AlertTriangle,
  CheckCircle,
  ChevronDown,
  ChevronUp,
  Clock,
  MapPin,
  Package,
  Share2,
  ShoppingBag,
  TrendingUp,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { groupBuyApi } from '../../services/api';
import type { GroupBuy, GroupBuyFilter, GroupBuyProduct } from '../../types';

const filters: { value: GroupBuyFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'active', label: 'Active' },
  { value: 'completed', label: 'Completed' },
];

const money = (value: number) =>
  new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' }).format(Number(value) || 0);

const discountedPrice = (originalPrice: number, discountPct: number) =>
  Math.round((originalPrice * (1 - discountPct / 100) + Number.EPSILON) * 100) / 100;

const statusClass = (status: string) => {
  const normalized = status.toLowerCase();
  if (normalized === 'qualified' || normalized === 'completed') return 'bg-emerald-100 text-emerald-700';
  if (normalized === 'expired' || normalized === 'cancelled') return 'bg-red-100 text-red-700';
  return 'bg-amber-100 text-amber-700';
};

const canApprove = (group: GroupBuy) =>
  group.products.some((product) => product.status.toLowerCase() === 'qualified');

const supplierOfferRevenue = (group: GroupBuy, discounts: Record<string, string>) => {
  const qualifiedProducts = group.products.filter((product) => product.status.toLowerCase() === 'qualified');
  if (qualifiedProducts.length === 0) return null;

  return qualifiedProducts.reduce<number | null>((total, product) => {
    if (total == null) return null;
    const discountPct = Number(discounts[product.id]);
    if (!Number.isInteger(discountPct) || discountPct < 1 || discountPct > 99) return null;
    return total + discountedPrice(product.originalPrice, discountPct) * product.currentQty;
  }, 0);
};

export default function GroupBuyPage() {
  const [groups, setGroups] = useState<GroupBuy[]>([]);
  const [filter, setFilter] = useState<GroupBuyFilter>('all');
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [approvingId, setApprovingId] = useState<string | null>(null);
  const [discountInputs, setDiscountInputs] = useState<Record<string, string>>({});

  const loadGroups = useCallback(async () => {
    setLoading(true);
    try {
      setGroups(await groupBuyApi.list(filter));
      // Approval discounts are always a fresh supplier decision; never reuse API or prior-entry values.
      setDiscountInputs({});
    } catch {
      // The shared API interceptor displays the backend message.
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => { loadGroups(); }, [loadGroups]);

  const approve = async (group: GroupBuy) => {
    const qualifiedProducts = group.products.filter((product) => product.status.toLowerCase() === 'qualified');
    if (qualifiedProducts.length === 0) return;

    const products = qualifiedProducts.map((product) => ({
      groupBuyProductId: product.id,
      discountPct: Number(discountInputs[product.id]),
    }));
    const invalidProducts = qualifiedProducts.filter((product, index) => {
      const value = discountInputs[product.id];
      const discountPct = products[index].discountPct;
      return value == null || value.trim() === '' || !Number.isInteger(discountPct) || discountPct < 1 || discountPct > 99;
    });
    if (invalidProducts.length > 0) {
      toast.error(`Enter a whole-number supplier discount from 1% to 99% for every qualified product before approval. Missing or invalid: ${invalidProducts.map((product) => product.productName).join(', ')}.`);
      return;
    }

    const confirmed = window.confirm(
      'Approve this group buy with your entered discounts? You, the supplier, own and fund these discounts. Approval creates one individual multi-line order per shop, with delivery fees calculated from your dispatch location.'
    );
    if (!confirmed) return;

    setApprovingId(group.id);
    try {
      const result = await groupBuyApi.approve(group.id, { products });
      toast.success(result?.message ?? result?.Message ?? 'Group buy approved and shop orders created.');
      await loadGroups();
    } catch {
      // The shared API interceptor displays the backend message.
    } finally {
      setApprovingId(null);
    }
  };

  const stats = useMemo(() => ({
    total: groups.length,
    active: groups.filter((group) => group.status.toLowerCase() === 'active').length,
    qualifiedProducts: groups.reduce(
      (total, group) => total + group.products.filter((product) => product.status.toLowerCase() === 'qualified').length,
      0
    ),
    shops: groups.reduce((total, group) => total + group.participantCount, 0),
  }), [groups]);

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto animate-in">
      <div>
        <h1 className="page-title">Group Buy Contracts</h1>
        <p className="page-subtitle">Review pooled, multi-product orders and approve qualified product lines.</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-3">
        <Stat icon={<ShoppingBag size={19} />} label="Contracts" value={stats.total} tone="blue" />
        <Stat icon={<Clock size={19} />} label="Active" value={stats.active} tone="amber" />
        <Stat icon={<CheckCircle size={19} />} label="Qualified products" value={stats.qualifiedProducts} tone="green" />
        <Stat icon={<Users size={19} />} label="Participating shops" value={stats.shops} tone="violet" />
      </div>

      <div className="flex items-center gap-1 border-b border-gray-200 overflow-x-auto">
        {filters.map(({ value, label }) => (
          <button
            key={value}
            onClick={() => setFilter(value)}
            className={`px-4 py-3 text-sm font-semibold border-b-2 transition-colors ${
              filter === value ? 'border-primary text-primary' : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="flex justify-center py-16">
          <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin" />
        </div>
      ) : groups.length === 0 ? (
        <div className="card py-16 text-center">
          <Users size={44} className="mx-auto text-gray-300 mb-3" />
          <p className="font-semibold text-gray-700">No {filter === 'all' ? '' : filter} group buys</p>
          <p className="text-sm text-gray-400 mt-1">Shop-created contracts for your products will appear here.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {groups.map((group) => (
            <GroupCard
              key={group.id}
              group={group}
              expanded={expandedId === group.id}
              approving={approvingId === group.id}
              discounts={discountInputs}
              onDiscountChange={(productId, value) => setDiscountInputs((current) => ({ ...current, [productId]: value }))}
              onToggle={() => setExpandedId((current) => current === group.id ? null : group.id)}
              onApprove={() => approve(group)}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function Stat({ icon, label, value, tone }: { icon: ReactNode; label: string; value: number; tone: string }) {
  const tones: Record<string, string> = {
    blue: 'bg-blue-50 text-blue-600',
    amber: 'bg-amber-50 text-amber-600',
    green: 'bg-emerald-50 text-emerald-600',
    violet: 'bg-violet-50 text-violet-600',
  };
  return (
    <div className="card p-4 flex items-center gap-3">
      <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${tones[tone]}`}>{icon}</div>
      <div><p className="text-xl font-black text-gray-900">{value}</p><p className="text-xs text-gray-500">{label}</p></div>
    </div>
  );
}

function GroupCard({
  group,
  expanded,
  approving,
  discounts,
  onDiscountChange,
  onToggle,
  onApprove,
}: {
  group: GroupBuy;
  expanded: boolean;
  approving: boolean;
  discounts: Record<string, string>;
  onDiscountChange: (productId: string, value: string) => void;
  onToggle: () => void;
  onApprove: () => void;
}) {
  const qualified = canApprove(group);
  const offerRevenue = supplierOfferRevenue(group, discounts);
  const daysLeft = Math.max(0, Math.ceil((new Date(group.expiresAt).getTime() - Date.now()) / 86_400_000));

  return (
    <article className="card overflow-hidden">
      <div className="p-5 border-b border-gray-100 bg-gradient-to-r from-gray-50 to-white">
        <div className="flex flex-col lg:flex-row lg:items-start justify-between gap-4">
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h2 className="text-lg font-black text-gray-900">{group.title}</h2>
              <span className={`text-[11px] font-bold px-2.5 py-1 rounded-full capitalize ${statusClass(group.status)}`}>
                {group.status}
              </span>
              {qualified && group.status.toLowerCase() !== 'completed' && (
                <span className="text-[11px] font-bold px-2.5 py-1 rounded-full bg-emerald-100 text-emerald-700">
                  Ready for approval
                </span>
              )}
            </div>
            {group.description && <p className="text-sm text-gray-500 mt-1 max-w-3xl">{group.description}</p>}
            <div className="flex flex-wrap gap-x-5 gap-y-1 mt-3 text-xs text-gray-500">
              <span>Started by <strong className="text-gray-700">{group.createdByShopName}</strong></span>
              <span>{group.participantCount} shop{group.participantCount === 1 ? '' : 's'}</span>
              <span>{group.products.length} product{group.products.length === 1 ? '' : 's'}</span>
              <span>{daysLeft > 0 ? `${daysLeft} days remaining` : 'Closed'}</span>
            </div>
          </div>
          <div className="text-left lg:text-right flex-shrink-0">
            <p className="text-xs text-gray-400">Supplier offer revenue</p>
            <p className="text-2xl font-black text-emerald-700">{offerRevenue == null ? '—' : money(offerRevenue)}</p>
            <p className="text-[11px] text-gray-400">{offerRevenue == null ? 'Enter all qualified-product discounts' : 'Based on your fresh discount entries'}</p>
          </div>
        </div>
      </div>

      <div className="p-5 space-y-3">
        {group.products.map((product) => (
          <ProductRow
            key={product.id}
            product={product}
            discountValue={discounts[product.id] ?? ''}
            onDiscountChange={(value) => onDiscountChange(product.id, value)}
          />
        ))}

        {!qualified && group.status.toLowerCase() === 'active' && (
          <div className="flex items-start gap-3 bg-blue-50 border border-blue-100 rounded-xl p-4">
            <Share2 size={17} className="text-blue-600 mt-0.5 flex-shrink-0" />
            <div>
              <p className="text-sm font-bold text-blue-900">Help this group buy reach a product target</p>
              <p className="text-xs text-blue-700 mt-0.5">Prompt participating shops to share the deal with nearby shops. Approval unlocks as soon as any product is qualified.</p>
            </div>
          </div>
        )}

        <div className="flex flex-col sm:flex-row gap-3 sm:items-center justify-between pt-2">
          <button onClick={onToggle} className="btn-secondary flex items-center justify-center gap-2">
            <Users size={15} /> {expanded ? 'Hide participants' : 'View participant quantities'}
            {expanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
          </button>
          <div className="flex flex-col sm:items-end gap-1">
            <button
              onClick={onApprove}
              disabled={!qualified || approving || group.status.toLowerCase() === 'completed'}
              className="btn-primary flex items-center justify-center gap-2 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <CheckCircle size={15} /> {approving ? 'Creating orders...' : 'Approve & create shop orders'}
            </button>
            <p className="text-[11px] text-gray-400 sm:text-right">
              You, the supplier, own and fund every entered discount. Qualified products require a fresh 1–99% whole-number discount.
            </p>
          </div>
        </div>
      </div>

      {expanded && <Participants group={group} discounts={discounts} />}
    </article>
  );
}

function ProductRow({
  product,
  discountValue,
  onDiscountChange,
}: {
  product: GroupBuyProduct;
  discountValue: string;
  onDiscountChange: (value: string) => void;
}) {
  const progress = Math.max(0, Math.min(100, Number(product.progress) || (product.targetQty > 0 ? product.currentQty / product.targetQty * 100 : 0)));
  const normalizedStatus = product.status.toLowerCase();
  const qualified = normalizedStatus === 'qualified';
  const approved = normalizedStatus === 'approved' || normalizedStatus === 'completed';
  const enteredDiscount = discountValue.trim() === '' ? null : Number(discountValue);
  const validEnteredDiscount = enteredDiscount != null && Number.isInteger(enteredDiscount) && enteredDiscount >= 1 && enteredDiscount <= 99;
  const effectiveDiscount = qualified && validEnteredDiscount
    ? enteredDiscount
    : approved && Number.isInteger(product.discountPct) && product.discountPct >= 1 && product.discountPct <= 99
      ? product.discountPct
      : null;
  const supplierOffer = effectiveDiscount == null ? product.originalPrice : discountedPrice(product.originalPrice, effectiveDiscount);
  const discountAmount = effectiveDiscount == null ? 0 : product.originalPrice - supplierOffer;

  return (
    <div className={`rounded-xl border p-4 ${qualified ? 'border-emerald-200 bg-emerald-50/40' : 'border-gray-100 bg-gray-50/60'}`}>
      <div className="flex flex-col md:flex-row md:items-center gap-4">
        <div className="w-10 h-10 rounded-xl bg-white border border-gray-100 flex items-center justify-center flex-shrink-0">
          <Package size={18} className={qualified ? 'text-emerald-600' : 'text-primary'} />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <p className="font-bold text-gray-900">{product.productName}</p>
            <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full capitalize ${statusClass(product.status)}`}>{product.status}</span>
          </div>
          <div className="mt-2">
            <div className="flex justify-between text-xs mb-1.5">
              <span className="text-gray-600"><strong>{product.currentQty}</strong> / {product.targetQty} units</span>
              <span className="font-bold text-gray-700">{progress.toFixed(0)}%</span>
            </div>
            <div className="h-2.5 bg-gray-200 rounded-full overflow-hidden">
              <div className={`h-full rounded-full ${qualified ? 'bg-emerald-500' : 'bg-primary'}`} style={{ width: `${progress}%` }} />
            </div>
          </div>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 md:min-w-[460px]">
          <Metric label="Original price" value={money(product.originalPrice)} />
          {qualified ? (
            <div>
              <label htmlFor={`discount-${product.id}`} className="text-[10px] uppercase tracking-wide text-gray-500 font-bold">
                Your supplier discount *
              </label>
              <div className="relative mt-1">
                <input
                  id={`discount-${product.id}`}
                  type="number"
                  min="1"
                  max="99"
                  step="1"
                  inputMode="numeric"
                  required
                  value={discountValue}
                  onChange={(event) => onDiscountChange(event.target.value)}
                  placeholder="1–99"
                  aria-label={`Supplier discount percentage for ${product.productName}`}
                  className="input h-9 pr-7 text-sm font-bold"
                />
                <span className="absolute right-3 top-2 text-xs font-bold text-gray-400">%</span>
              </div>
            </div>
          ) : (
            <Metric label="Supplier discount" value="Not editable" />
          )}
          <Metric label="Discount per unit" value={effectiveDiscount != null ? money(discountAmount) : '—'} />
          <Metric label="Your supplier offer" value={effectiveDiscount != null ? money(supplierOffer) : '—'} />
          <Metric label="Shops" value={`${product.participantCount}`} />
          <Metric label="Offer revenue" value={effectiveDiscount != null ? money(supplierOffer * product.currentQty) : '—'} />
        </div>
      </div>
    </div>
  );
}

function Metric({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <div>
      <p className="text-[10px] uppercase tracking-wide text-gray-400 font-bold">{label}</p>
      <p className="text-sm font-black text-gray-900 mt-0.5">{value}</p>
      {sub && <p className="text-[10px] text-gray-400 line-through">{sub}</p>}
    </div>
  );
}

function Participants({ group, discounts }: { group: GroupBuy; discounts: Record<string, string> }) {
  const offerRevenue = supplierOfferRevenue(group, discounts);

  return (
    <div className="border-t border-gray-100 bg-gray-50/70 p-5">
      <div className="flex items-center gap-2 mb-3">
        <Users size={16} className="text-gray-500" />
        <h3 className="text-sm font-bold text-gray-800">Participant product quantities</h3>
      </div>
      {group.participants.length === 0 ? (
        <p className="text-sm text-gray-400 py-3">No participants have joined yet.</p>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
          {group.participants.map((participant) => (
            <div key={participant.id} className="bg-white border border-gray-100 rounded-xl p-4">
              <div className="flex items-center justify-between gap-3 mb-3">
                <div className="flex items-center gap-2 min-w-0">
                  <div className="w-8 h-8 rounded-full bg-primary/10 text-primary flex items-center justify-center text-xs font-black flex-shrink-0">
                    {participant.shopName?.charAt(0) || '?'}
                  </div>
                  <p className="text-sm font-bold text-gray-900 truncate">{participant.shopName}</p>
                </div>
                <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full capitalize ${statusClass(participant.status)}`}>
                  {participant.status}
                </span>
              </div>
              <div className="space-y-2">
                {participant.items.length === 0 ? (
                  <p className="text-xs text-gray-400">No product quantities recorded.</p>
                ) : participant.items.map((item) => (
                  <div key={`${participant.id}-${item.groupBuyProductId}`} className="flex items-center justify-between gap-3 text-xs bg-gray-50 rounded-lg px-3 py-2">
                    <div className="min-w-0">
                      <p className="font-semibold text-gray-700 truncate">{item.productName}</p>
                      <p className="text-[10px] text-gray-400 capitalize">{item.status}</p>
                    </div>
                    <span className="font-black text-gray-900 whitespace-nowrap">{item.quantity} units</span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
      <div className="flex items-start gap-2 mt-4 text-xs text-gray-500">
        <MapPin size={14} className="mt-0.5 flex-shrink-0" />
        <span>On approval, each shop receives one order containing all of its qualified product lines; delivery is priced from your dispatch coordinates by distance.</span>
      </div>
      {!canApprove(group) && (
        <div className="flex items-start gap-2 mt-3 text-xs text-amber-700">
          <AlertTriangle size={14} className="mt-0.5 flex-shrink-0" />
          <span>No product is qualified yet. Approval remains unavailable until at least one product reaches qualified status.</span>
        </div>
      )}
      <div className="flex items-center gap-2 mt-3 text-sm font-bold text-emerald-700">
        <TrendingUp size={15} /> Supplier offer revenue: {offerRevenue == null ? 'Enter every qualified-product discount' : money(offerRevenue)}
      </div>
    </div>
  );
}
