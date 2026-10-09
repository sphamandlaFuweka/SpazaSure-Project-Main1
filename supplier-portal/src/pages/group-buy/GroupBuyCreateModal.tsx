import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { Loader2, Plus, Trash2 } from 'lucide-react';
import { Modal } from '../../components/ui';
import {
  adminGroupBuyApi, adminSuppliersApi, groupBuyApi, productsApi,
  type SupplierProductOption,
} from '../../services/api';

interface Props {
  mode: 'supplier' | 'admin';
  onClose: () => void;
  onCreated: () => void;
}

interface Line { productId: string; targetQty: number; discountPct: number }

/** One form for both roles. Admins also choose the supplier, and their campaigns go live immediately. */
export default function GroupBuyCreateModal({ mode, onClose, onCreated }: Props) {
  const [suppliers, setSuppliers] = useState<{ id: string; companyName: string }[]>([]);
  const [supplierId, setSupplierId] = useState('');
  const [products, setProducts] = useState<SupplierProductOption[]>([]);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [days, setDays] = useState(7);
  const [lines, setLines] = useState<Line[]>([{ productId: '', targetQty: 50, discountPct: 10 }]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (mode !== 'admin') return;
    adminSuppliersApi.list({ pageSize: 200, status: 'verified' })
      .then((r) => setSuppliers((r?.data?.items ?? r?.items ?? []) as { id: string; companyName: string }[]))
      .catch(() => toast.error('Could not load suppliers'));
  }, [mode]);

  useEffect(() => {
    setLines([{ productId: '', targetQty: 50, discountPct: 10 }]);
    if (mode === 'supplier') {
      productsApi.list({ pageSize: 200, status: 'active' })
        .then((r) => setProducts(r.data.map((p: { id: string; name: string; price: number; stockQuantity: number; minOrderQty?: number }) => ({
          id: p.id, name: p.name, price: p.price, stockQty: p.stockQuantity, minOrderQty: p.minOrderQty ?? 1,
        }))))
        .catch(() => toast.error('Could not load your products'));
    } else if (supplierId) {
      adminGroupBuyApi.supplierProducts(supplierId).then(setProducts).catch(() => toast.error('Could not load products'));
    } else {
      setProducts([]);
    }
  }, [mode, supplierId]);

  const update = (i: number, patch: Partial<Line>) =>
    setLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  const submit = async () => {
    if (mode === 'admin' && !supplierId) { toast.error('Choose a supplier'); return; }
    if (title.trim().length < 3) { toast.error('Give the group buy a title'); return; }
    if (lines.some((l) => !l.productId)) { toast.error('Choose a product on every line'); return; }
    setBusy(true);
    try {
      const body = {
        supplierId: mode === 'admin' ? supplierId : undefined,
        title: title.trim(),
        description: description.trim() || undefined,
        durationDays: days,
        products: lines,
      };
      const res = mode === 'admin' ? await adminGroupBuyApi.create(body) : await groupBuyApi.create(body);
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
          {mode === 'admin'
            ? 'Admin campaigns go live straight away. Shops can only join or share them.'
            : 'An admin reviews your campaign before shops can see it.'}
        </p>

        {mode === 'admin' && (
          <div>
            <label className="label">Supplier</label>
            <select className="input" value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
              <option value="">Select a verified supplier</option>
              {suppliers.map((s) => <option key={s.id} value={s.id}>{s.companyName}</option>)}
            </select>
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

        <div className="space-y-2">
          <p className="label">Products</p>
          {lines.map((line, i) => {
            const chosen = products.find((p) => p.id === line.productId);
            const price = chosen ? chosen.price * (1 - line.discountPct / 100) : null;
            return (
              <div key={i} className="grid grid-cols-12 gap-2 items-end">
                <div className="col-span-5">
                  <select className="input" value={line.productId} onChange={(e) => update(i, { productId: e.target.value })}>
                    <option value="">Select product</option>
                    {products.map((p) => (
                      <option key={p.id} value={p.id} disabled={lines.some((l, idx) => idx !== i && l.productId === p.id)}>
                        {p.name} (R{p.price})
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
            {mode === 'admin' ? 'Create and go live' : 'Submit for approval'}
          </button>
        </div>
      </div>
    </Modal>
  );
}
