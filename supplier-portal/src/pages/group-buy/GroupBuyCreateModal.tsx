import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { ImagePlus, Loader2, MapPin, Plus, Trash2, X } from 'lucide-react';
import { Modal } from '../../components/ui';
import {
  adminGroupBuyApi, adminProductsApi, adminSuppliersApi,
  type SupplierBranchOptions, type SupplierProductOption,
} from '../../services/api';
import type { Product } from '../../types';

interface Props {
  onClose: () => void;
  onCreated: () => void;
}

interface Line { productId: string; targetQty: number; discountPct: number }

/** Only admins create group buys. They choose the supplier, and the campaign goes live immediately. */
export default function GroupBuyCreateModal({ onClose, onCreated }: Props) {
  const [suppliers, setSuppliers] = useState<{ id: string; companyName: string }[]>([]);
  const [supplierId, setSupplierId] = useState('');
  const [products, setProducts] = useState<SupplierProductOption[]>([]);
  const [places, setPlaces] = useState<SupplierBranchOptions | null>(null);
  /** '' is the head office */
  const [branchId, setBranchId] = useState('');
  const [radiusKm, setRadiusKm] = useState(40);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [days, setDays] = useState(7);
  const [lines, setLines] = useState<Line[]>([{ productId: '', targetQty: 50, discountPct: 10 }]);
  const [busy, setBusy] = useState(false);
  const [bannerFile, setBannerFile] = useState<File | null>(null);
  const [bannerPreview, setBannerPreview] = useState<string | null>(null);

  useEffect(() => () => { if (bannerPreview) URL.revokeObjectURL(bannerPreview); }, [bannerPreview]);

  const pickBanner = (file: File | undefined) => {
    if (!file) return;
    if (!/\.(jpe?g|png|webp)$/i.test(file.name)) { toast.error('Use a JPG, PNG or WEBP image'); return; }
    if (file.size > 5 * 1024 * 1024) { toast.error('Image must be under 5MB'); return; }
    setBannerFile(file);
    setBannerPreview(URL.createObjectURL(file));
  };

  const clearBanner = () => { setBannerFile(null); setBannerPreview(null); };
  useEffect(() => {
    adminSuppliersApi.list({ pageSize: 200, status: 'verified' })
      .then((r) => setSuppliers((r?.data?.items ?? r?.items ?? []) as { id: string; companyName: string }[]))
      .catch(() => toast.error('Could not load suppliers'));
  }, []);

  useEffect(() => {
    setLines([{ productId: '', targetQty: 50, discountPct: 10 }]);
    setPlaces(null);
    setBranchId('');
    if (supplierId) {
      adminGroupBuyApi.supplierBranches(supplierId)
        .then((p) => {
          setPlaces(p);
          setRadiusKm(Math.min(40, p.defaultRadiusKm));
          // A supplier with warehouses usually runs each campaign from one of them
          if (p.branches.length > 0 && !p.headOffice.city && p.headOffice.latitude == null) setBranchId(p.branches[0].id);
        })
        .catch(() => toast.error("Could not load the supplier's branches"));
      const supplierName = suppliers.find((s) => s.id === supplierId)?.companyName;
      // Fallback to the admin products list (the same data as the Products page) when the dedicated endpoint returns nothing.
      const fromProductsPage = () => adminProductsApi.list({ pageSize: 500, search: supplierName })
        .then((r) => r.data
          .filter((p: Product) => p.supplierId === supplierId)
          .map((p: Product) => ({
            id: p.id, name: p.name, price: p.price, stockQty: p.stockQuantity, minOrderQty: p.minOrderQty ?? 1,
            isApproved: p.status === 'active', isAvailable: p.isAvailable,
          })));
      adminGroupBuyApi.supplierProducts(supplierId)
        .then((list) => (list.length > 0 ? list : fromProductsPage()))
        .catch(() => fromProductsPage())
        .then(setProducts)
        .catch(() => toast.error('Could not load products'));
    } else {
      setProducts([]);
    }
  }, [supplierId]);

  const isEligible = (p: SupplierProductOption) => p.isApproved !== false && p.isAvailable !== false;
  const eligibility = (p: SupplierProductOption) =>
    p.isApproved === false ? ' - awaiting approval' : p.isAvailable === false ? ' - unavailable' : '';

  const update = (i: number, patch: Partial<Line>) =>
    setLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  const chosenBranch = places?.branches.find((b) => b.id === branchId);
  const areaPlace = chosenBranch
    ? `${chosenBranch.city}, ${chosenBranch.province}`
    : [places?.headOffice.city, places?.headOffice.province].filter(Boolean).join(', ');
  const headOfficeMissing = !!places && !places.headOffice.city && places.headOffice.latitude == null;

  const submit = async () => {
    if (!supplierId) { toast.error('Choose a supplier'); return; }
    if (!branchId && headOfficeMissing) { toast.error('Choose the branch this group buy runs from'); return; }
    if (!(radiusKm >= 1 && radiusKm <= (places?.maxRadiusKm ?? 300))) { toast.error(`The area radius must be between 1 and ${places?.maxRadiusKm ?? 300} km`); return; }
    if (title.trim().length < 3) { toast.error('Give the group buy a title'); return; }
    if (lines.some((l) => !l.productId)) { toast.error('Choose a product on every line'); return; }
    setBusy(true);
    try {
      let bannerImageUrl: string | undefined;
      if (bannerFile) {
        bannerImageUrl = await adminGroupBuyApi.uploadBanner(bannerFile);
      }
      const body = {
        supplierId,
        title: title.trim(),
        description: description.trim() || undefined,
        bannerImageUrl,
        durationDays: days,
        products: lines,
        supplierBranchId: branchId || undefined,
        areaRadiusKm: radiusKm,
      };
      const res = await adminGroupBuyApi.create(body);
      toast.success(res?.message ?? 'Group buy created');
      onCreated();
      onClose();
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title="Create group buy" onClose={onClose} size="lg">
      <div className="p-6 space-y-4">
        <p className="text-sm text-gray-500">
          The campaign goes live straight away. Shops join it, and the supplier accepts and dispatches the orders once a product reaches its target.
        </p>

        <div>
          <label className="label">Supplier</label>
          <select className="input" value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
            <option value="">Select a verified supplier</option>
            {suppliers.map((s) => <option key={s.id} value={s.id}>{s.companyName}</option>)}
          </select>
        </div>

        {supplierId && (
          <div className="rounded-xl border border-gray-200 bg-gray-50 p-4 space-y-3">
            <div className="grid grid-cols-3 gap-4">
              <div className="col-span-2">
                <label className="label">Supplied from</label>
                <select className="input" value={branchId} onChange={(e) => setBranchId(e.target.value)} disabled={!places}>
                  <option value="" disabled={headOfficeMissing}>
                    Head office{places?.headOffice.city ? ` (${places.headOffice.city})` : headOfficeMissing ? ' (no address yet)' : ''}
                  </option>
                  {places?.branches.map((b) => <option key={b.id} value={b.id}>{b.name} ({b.city})</option>)}
                </select>
              </div>
              <div>
                <label className="label">Area radius (km)</label>
                <input className="input" type="number" min={1} max={places?.maxRadiusKm ?? 300} value={radiusKm} onChange={(e) => setRadiusKm(Number(e.target.value))} />
              </div>
            </div>
            <p className="text-xs text-gray-600 flex items-start gap-1.5">
              <MapPin size={13} className="mt-0.5 shrink-0 text-primary" />
              {areaPlace
                ? <>Only shops within <b>{radiusKm || 0} km</b> of <b>{areaPlace}</b> will see and be able to join this group buy.</>
                : 'Choose where the stock ships from. Only shops around it can join.'}
            </p>
            {places && places.branches.length === 0 && (
              <p className="text-[11px] text-gray-400">This supplier hasn't added any branches, so the campaign runs from their head office.</p>
            )}
          </div>
        )}

        <div className="grid grid-cols-3 gap-4">
          <div className="col-span-2">
            <label className="label">Title</label>
            <input className="input" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Maize meal bulk deal" />
          </div>
          <div>
            <label className="label">Runs for (days)</label>
            <input className="input" type="number" min={1} max={60} value={days} onChange={(e) => setDays(Number(e.target.value))} />
          </div>
        </div>
        <div>
          <label className="label">Description (optional)</label>
          <textarea className="input" rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>

        <div>
          <label className="label">Banner image (optional)</label>
          {bannerPreview ? (
            <div className="relative rounded-xl overflow-hidden border border-gray-200">
              <img src={bannerPreview} alt="Banner preview" className="w-full h-40 object-cover" />
              <button type="button" className="absolute top-2 right-2 bg-white/90 rounded-full p-1.5 shadow hover:text-red-500" onClick={clearBanner} aria-label="Remove banner">
                <X size={14} />
              </button>
            </div>
          ) : (
            <label className="flex flex-col items-center justify-center gap-1 h-28 rounded-xl border-2 border-dashed border-gray-300 text-sm text-gray-500 cursor-pointer hover:border-primary hover:text-primary">
              <ImagePlus size={20} />
              Upload a poster (JPG, PNG or WEBP, max 5MB)
              <input type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(e) => { pickBanner(e.target.files?.[0]); e.target.value = ''; }} />
            </label>
          )}
          <p className="text-[11px] text-gray-400 mt-1">Shown on the group buy banner in the shop app. A wide image works best.</p>
        </div>

        <div className="space-y-2">
          <p className="label">Products</p>
          {!supplierId && <p className="text-xs text-gray-500">Choose a supplier to see their products.</p>}
          {supplierId && !products.some(isEligible) && (
            <p className="text-xs text-amber-600">
              {products.length === 0
                ? 'No products found for this supplier.'
                : 'None of these products can be used yet. Only approved, available products can join a group buy.'}
            </p>
          )}
          {lines.map((line, i) => {
            const chosen = products.find((p) => p.id === line.productId);
            const price = chosen ? chosen.price * (1 - line.discountPct / 100) : null;
            return (
              <div key={i} className="grid grid-cols-12 gap-2 items-end">
                <div className="col-span-5">
                  <select className="input" value={line.productId} onChange={(e) => update(i, { productId: e.target.value })}>
                    <option value="">Select product</option>
                    {products.map((p) => (
                      <option key={p.id} value={p.id} disabled={!isEligible(p) || lines.some((l, idx) => idx !== i && l.productId === p.id)}>
                        {p.name} (R{p.price}){eligibility(p)}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="col-span-3">
                  <input className="input" type="number" min={2} value={line.targetQty} onChange={(e) => update(i, { targetQty: Number(e.target.value) })} title="Target quantity" />
                  <p className="text-[10px] text-gray-400 mt-0.5">Target qty{chosen ? ` (min ${Math.max(chosen.minOrderQty, 2)})` : ''}</p>
                </div>
                <div className="col-span-3">
                  <input className="input" type="number" min={1} max={99} value={line.discountPct} onChange={(e) => update(i, { discountPct: Number(e.target.value) })} title="Discount percent" />
                  <p className="text-[10px] text-gray-400 mt-0.5">Discount %{price != null ? ` (R${price.toFixed(2)} each)` : ''}</p>
                </div>
                <div className="col-span-1 pb-5">
                  {lines.length > 1 && (
                    <button type="button" className="btn-icon hover:text-red-500" onClick={() => setLines((ls) => ls.filter((_, idx) => idx !== i))} aria-label="Remove product">
                      <Trash2 size={14} />
                    </button>
                  )}
                </div>
              </div>
            );
          })}
          <button type="button" className="text-sm font-semibold text-primary flex items-center gap-1" onClick={() => setLines((ls) => [...ls, { productId: '', targetQty: 50, discountPct: 10 }])}>
            <Plus size={14} /> Add another product
          </button>
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn-primary flex items-center gap-2" disabled={busy} onClick={submit}>
            {busy && <Loader2 size={14} className="animate-spin" />}
            Create and go live
          </button>
        </div>
      </div>
    </Modal>
  );
}
