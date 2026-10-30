import { useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { ImagePlus, Loader2, MapPin, Plus, Trash2, X } from 'lucide-react';
import { Modal } from '../../components/ui';
import {
  adminGroupBuyApi, adminProductsApi, adminSuppliersApi, resolveUploadUrl,
  type SupplierBranchOptions, type SupplierProductOption,
} from '../../services/api';
import type { GroupBuy, Product } from '../../types';

interface Props {
  onClose: () => void;
  onCreated: () => void;
  /** Editing an existing deal (any stage, live included) instead of drafting a new offer. */
  editing?: GroupBuy;
}

interface Line { productId: string; targetQty: number; discountPct: number }

const blankLine: Line = { productId: '', targetQty: 50, discountPct: 0 };

/** "2026-11-12T14:30" for a date-time input, in local time. */
const toLocalInput = (iso: string) => {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

/**
 * Admins draft group buys as offers to the supplier, who sets its discount and accepts or
 * declines; the admin then publishes. The same form edits a deal at any stage.
 */
export default function GroupBuyCreateModal({ onClose, onCreated, editing }: Props) {
  const live = editing?.status === 'active';
  const [suppliers, setSuppliers] = useState<{ id: string; companyName: string }[]>([]);
  const [supplierId, setSupplierId] = useState(editing?.supplierId ?? '');
  const [products, setProducts] = useState<SupplierProductOption[]>([]);
  const [places, setPlaces] = useState<SupplierBranchOptions | null>(null);
  /** Where the deal is open: any of the supplier's branches and/or its head office */
  const [branchIds, setBranchIds] = useState<string[]>(
    editing?.area?.places?.map((p) => p.supplierBranchId).filter((id): id is string => !!id) ?? []);
  const [headOffice, setHeadOffice] = useState(
    !!editing?.area?.places?.some((p) => !p.supplierBranchId) || (!!editing && !editing.area?.places?.length));
  const [radiusKm, setRadiusKm] = useState(editing?.area?.radiusKm ?? 40);
  const [title, setTitle] = useState(editing?.title ?? '');
  const [description, setDescription] = useState(editing?.description ?? '');
  const [days, setDays] = useState(editing?.durationDays || 7);
  const [endsAt, setEndsAt] = useState(editing ? toLocalInput(editing.expiresAt) : '');
  const [lines, setLines] = useState<Line[]>(
    editing?.products.map((p) => ({ productId: p.productId, targetQty: p.targetQty, discountPct: p.discountPct })) ?? [blankLine]);
  const [busy, setBusy] = useState(false);
  const [bannerFile, setBannerFile] = useState<File | null>(null);
  const [bannerPreview, setBannerPreview] = useState<string | null>(null);
  /** The deal's current poster when editing; cleared to remove it */
  const [currentBanner, setCurrentBanner] = useState(editing?.bannerImageUrl ?? null);
  /** When editing, the first supplier load keeps the deal's own places and products */
  const keepEditingValues = useRef(!!editing);

  useEffect(() => () => { if (bannerPreview) URL.revokeObjectURL(bannerPreview); }, [bannerPreview]);

  const pickBanner = (file: File | undefined) => {
    if (!file) return;
    if (!/\.(jpe?g|png|webp)$/i.test(file.name)) { toast.error('Use a JPG, PNG or WEBP image'); return; }
    if (file.size > 5 * 1024 * 1024) { toast.error('Image must be under 5MB'); return; }
    setBannerFile(file);
    setBannerPreview(URL.createObjectURL(file));
  };

  const clearBanner = () => { setBannerFile(null); setBannerPreview(null); setCurrentBanner(null); };
  useEffect(() => {
    adminSuppliersApi.list({ pageSize: 200, status: 'verified' })
      .then((r) => setSuppliers((r?.data?.items ?? r?.items ?? []) as { id: string; companyName: string }[]))
      .catch(() => toast.error('Could not load suppliers'));
  }, []);

  useEffect(() => {
    const keep = keepEditingValues.current;
    keepEditingValues.current = false;
    if (!keep) {
      setLines([blankLine]);
      setBranchIds([]);
      setHeadOffice(false);
    }
    setPlaces(null);
    if (supplierId) {
      adminGroupBuyApi.supplierBranches(supplierId)
        .then((p) => {
          setPlaces(p);
          if (keep) return;
          setRadiusKm(Math.min(40, p.defaultRadiusKm));
          // Start with the head office when it has an address, else the first warehouse
          const headOfficeKnown = !!p.headOffice.city || p.headOffice.latitude != null;
          setHeadOffice(headOfficeKnown);
          if (!headOfficeKnown && p.branches.length > 0) setBranchIds([p.branches[0].id]);
        })
        .catch(() => toast.error("Could not load the supplier's branches"));
      const supplierName = suppliers.find((s) => s.id === supplierId)?.companyName ?? editing?.supplierName;
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
  }, [supplierId]); // eslint-disable-line react-hooks/exhaustive-deps

  const isEligible = (p: SupplierProductOption) => p.isApproved !== false && p.isAvailable !== false;
  const eligibility = (p: SupplierProductOption) =>
    p.isApproved === false ? ' - awaiting approval' : p.isAvailable === false ? ' - unavailable' : '';
  /** Products already in the deal stay selectable even if no longer approved/available */
  const inDeal = (id: string) => !!editing?.products.some((p) => p.productId === id);

  const update = (i: number, patch: Partial<Line>) =>
    setLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  const headOfficeMissing = !!places && !places.headOffice.city && places.headOffice.latitude == null;
  const toggleBranch = (id: string) =>
    setBranchIds((ids) => (ids.includes(id) ? ids.filter((b) => b !== id) : [...ids, id]));
  // e.g. "Johannesburg, Durban or Pietermaritzburg"
  const chosenPlaces = [
    ...(headOffice ? [places?.headOffice.city || 'the head office'] : []),
    ...(places?.branches.filter((b) => branchIds.includes(b.id)).map((b) => b.city || b.name) ?? []),
  ].filter((name, i, all) => all.indexOf(name) === i);
  const areaPlace = chosenPlaces.length <= 1
    ? chosenPlaces[0] ?? ''
    : `${chosenPlaces.slice(0, -1).join(', ')} or ${chosenPlaces[chosenPlaces.length - 1]}`;

  const submit = async () => {
    if (!supplierId) { toast.error('Choose a supplier'); return; }
    if (!headOffice && branchIds.length === 0) { toast.error('Choose at least one branch (or the head office) for this deal'); return; }
    if (!(radiusKm >= 1 && radiusKm <= (places?.maxRadiusKm ?? 300))) { toast.error(`The area radius must be between 1 and ${places?.maxRadiusKm ?? 300} km`); return; }
    if (title.trim().length < 3) { toast.error('Give the group buy a title'); return; }
    if (lines.some((l) => !l.productId)) { toast.error('Choose a product on every line'); return; }
    if (live && lines.some((l) => !(l.discountPct >= 1 && l.discountPct <= 99))) { toast.error('A live deal needs a discount of 1 to 99% on every product'); return; }
    if (live && !(new Date(endsAt).getTime() > Date.now())) { toast.error('The end date must be in the future'); return; }
    setBusy(true);
    try {
      let bannerImageUrl = currentBanner ?? undefined;
      if (bannerFile) bannerImageUrl = await adminGroupBuyApi.uploadBanner(bannerFile);
      if (editing) {
        const res = await adminGroupBuyApi.update(editing.id, {
          title: title.trim(),
          description: description.trim() || undefined,
          bannerImageUrl,
          durationDays: live ? undefined : days,
          expiresAt: live ? new Date(endsAt).toISOString() : undefined,
          areaRadiusKm: radiusKm,
          supplierBranchIds: branchIds,
          includeHeadOffice: headOffice,
          products: lines,
        });
        toast.success(res?.message ?? 'Deal saved');
      } else {
        const res = await adminGroupBuyApi.create({
          supplierId,
          title: title.trim(),
          description: description.trim() || undefined,
          bannerImageUrl,
          durationDays: days,
          products: lines,
          supplierBranchIds: branchIds,
          includeHeadOffice: headOffice,
          areaRadiusKm: radiusKm,
        });
        toast.success(res?.message ?? 'Offer sent to the supplier');
      }
      onCreated();
      onClose();
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setBusy(false);
    }
  };

  const shownBanner = bannerPreview ?? (currentBanner ? resolveUploadUrl(currentBanner) : null);

  return (
    <Modal title={editing ? `Edit: ${editing.title}` : 'New group buy offer'} onClose={onClose} size="lg">
      <div className="p-6 space-y-4">
        <p className="text-sm text-gray-500">
          {editing
            ? live
              ? 'This deal is live. Shops that already paid keep their price; places and products shops joined through can\'t be removed. The supplier is told about changes to products, targets or discounts.'
              : 'Changes are saved to the deal. Send it to the supplier again for their answer, or publish it.'
            : 'The deal is sent to the supplier as an offer: they set the discount they give and accept or decline it. Shops only see it once you publish it.'}
        </p>

        <div>
          <label className="label">Supplier</label>
          <select className="input" value={supplierId} onChange={(e) => setSupplierId(e.target.value)} disabled={!!editing}>
            <option value="">Select a verified supplier</option>
            {editing && !suppliers.some((s) => s.id === editing.supplierId) && (
              <option value={editing.supplierId}>{editing.supplierName}</option>
            )}
            {suppliers.map((s) => <option key={s.id} value={s.id}>{s.companyName}</option>)}
          </select>
        </div>

        {supplierId && (
          <div className="rounded-xl border border-gray-200 bg-gray-50 p-4 space-y-3">
            <div className="grid grid-cols-3 gap-4">
              <div className="col-span-2">
                <label className="label">Available at</label>
                {/* Tick every branch the deal runs from; each shop is served by its nearest one */}
                <div className="rounded-xl border border-gray-200 bg-white divide-y divide-gray-100 max-h-44 overflow-y-auto">
                  <label className={`flex items-center gap-2.5 px-3 py-2 text-sm ${headOfficeMissing ? 'text-gray-400' : 'cursor-pointer'}`}>
                    <input type="checkbox" checked={headOffice} disabled={!places || headOfficeMissing}
                      onChange={(e) => setHeadOffice(e.target.checked)} />
                    <span className="font-semibold">Head office</span>
                    <span className="text-gray-500 truncate">
                      {places?.headOffice.city ? places.headOffice.city : headOfficeMissing ? 'no address yet' : ''}
                    </span>
                  </label>
                  {places?.branches.map((b) => (
                    <label key={b.id} className="flex items-center gap-2.5 px-3 py-2 text-sm cursor-pointer">
                      <input type="checkbox" checked={branchIds.includes(b.id)} onChange={() => toggleBranch(b.id)} />
                      <span className="font-semibold truncate">{b.name}</span>
                      <span className="text-gray-500 truncate">{b.city}</span>
                    </label>
                  ))}
                </div>
              </div>
              <div>
                <label className="label">Area radius (km)</label>
                <input className="input" type="number" min={1} max={places?.maxRadiusKm ?? 300} value={radiusKm} onChange={(e) => setRadiusKm(Number(e.target.value))} />
                <p className="text-[11px] text-gray-400 mt-1">Around each chosen place</p>
              </div>
            </div>
            <p className="text-xs text-gray-600 flex items-start gap-1.5">
              <MapPin size={13} className="mt-0.5 shrink-0 text-primary" />
              {areaPlace
                ? <>Only shops within <b>{radiusKm || 0} km</b> of <b>{areaPlace}</b> will see and be able to join this group buy. Each shop is served, and delivered to, from its nearest one.</>
                : 'Tick where the stock ships from. Only shops around those places can join.'}
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
          {live ? (
            <div>
              <label className="label">Ends on</label>
              <input className="input" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
            </div>
          ) : (
            <div>
              <label className="label">Runs for (days)</label>
              <input className="input" type="number" min={1} max={60} value={days} onChange={(e) => setDays(Number(e.target.value))} />
              <p className="text-[11px] text-gray-400 mt-1">Counted from when it's published</p>
            </div>
          )}
        </div>
        <div>
          <label className="label">Description (optional)</label>
          <textarea className="input" rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>

        <div>
          <label className="label">Banner image (optional)</label>
          {shownBanner ? (
            <div className="relative rounded-xl overflow-hidden border border-gray-200">
              <img src={shownBanner} alt="Banner preview" className="w-full h-40 object-cover" />
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
          <p className="text-[11px] text-gray-400 mt-1">
            Shown on the group buy banner in the shop app; the supplier never sees it. A wide image works best.
            {!editing && ' You can add it later with Edit deal, once the supplier has set its discount.'}
          </p>
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
            const price = chosen && line.discountPct > 0 ? chosen.price * (1 - line.discountPct / 100) : null;
            return (
              <div key={i} className="grid grid-cols-12 gap-2 items-end">
                <div className="col-span-5">
                  <select className="input" value={line.productId} onChange={(e) => update(i, { productId: e.target.value })}>
                    <option value="">Select product</option>
                    {products.map((p) => (
                      <option key={p.id} value={p.id}
                        disabled={(!isEligible(p) && !inDeal(p.id)) || lines.some((l, idx) => idx !== i && l.productId === p.id)}>
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
                  <input className="input" type="number" min={0} max={99} value={line.discountPct || ''} placeholder={live ? '' : 'Supplier sets'}
                    onChange={(e) => update(i, { discountPct: Number(e.target.value) || 0 })} title="Discount percent" />
                  <p className="text-[10px] text-gray-400 mt-0.5">
                    Discount %{price != null ? ` (R${price.toFixed(2)} each)` : live ? '' : ' (optional)'}
                  </p>
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
          <button type="button" className="text-sm font-semibold text-primary flex items-center gap-1" onClick={() => setLines((ls) => [...ls, blankLine])}>
            <Plus size={14} /> Add another product
          </button>
          {!live && (
            <p className="text-[11px] text-gray-400">
              Leave the discount empty for the supplier to set it, or suggest one. The prices here are the supplier's own; shops see them with SpazaSure's fee included.
            </p>
          )}
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn-primary flex items-center gap-2" disabled={busy} onClick={submit}>
            {busy && <Loader2 size={14} className="animate-spin" />}
            {editing ? 'Save changes' : 'Send offer to supplier'}
          </button>
        </div>
      </div>
    </Modal>
  );
}
