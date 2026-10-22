import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Loader2 } from 'lucide-react';
import { Modal } from '../../components/ui';
import { branchStockApi, type ProductBranchStock } from '../../services/api';
import type { Product } from '../../types';

/**
 * How much of a product sits at the head office and at each branch. Orders ship from the shop's
 * nearest branch that has the stock, and shops are told when their nearest branch has run out.
 */
export default function BranchStockModal({ product, onClose, onSaved }: { product: Product; onClose: () => void; onSaved: () => void }) {
  const [data, setData] = useState<ProductBranchStock | null>(null);
  const [track, setTrack] = useState(false);
  const [headOffice, setHeadOffice] = useState(0);
  const [qty, setQty] = useState<Record<string, number>>({});
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    branchStockApi.get(product.id)
      .then((d) => {
        setData(d);
        setTrack(d.trackPerBranch);
        // Untracked stock all counts as head office stock until it is spread over the branches
        setHeadOffice(d.trackPerBranch ? d.headOfficeQty : d.totalStock);
        setQty(Object.fromEntries(d.branches.map((b) => [b.supplierBranchId, b.stockQty])));
      })
      .catch(() => { toast.error('Could not load stock per branch'); onClose(); });
  }, [product.id]);

  const total = headOffice + Object.values(qty).reduce((a, b) => a + (b || 0), 0);

  const save = async () => {
    if (!data) return;
    setBusy(true);
    try {
      await branchStockApi.set(product.id, {
        trackPerBranch: track,
        headOfficeQty: headOffice,
        branches: data.branches.map((b) => ({ supplierBranchId: b.supplierBranchId, stockQty: qty[b.supplierBranchId] ?? 0 })),
      });
      toast.success(track ? 'Stock per branch saved' : 'Stock is shared by all your locations');
      onSaved();
      onClose();
    } catch {
      // The API interceptor shows the backend message.
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={`Stock per branch: ${product.name}`} onClose={onClose}>
      <div className="p-6 space-y-4">
        {!data ? (
          <div className="flex justify-center py-8"><Loader2 className="animate-spin text-gray-400" /></div>
        ) : data.branches.length === 0 ? (
          <p className="text-sm text-gray-600">
            You haven't added any branches. Add your warehouses on your <Link className="text-primary font-semibold" to="/profile">profile's Branches tab</Link> first.
          </p>
        ) : (
          <>
            <label className="flex items-start gap-3 rounded-xl border border-gray-200 p-3 text-sm cursor-pointer">
              <input type="checkbox" className="mt-1" checked={track} onChange={(e) => setTrack(e.target.checked)} />
              <span>
                <b>Track this product per branch</b>
                <span className="block text-xs text-gray-500">
                  When on, each order ships from the shop's nearest branch that has enough, and shops see when their nearest branch has run out.
                  When off, any branch can ship your full stock of {data.totalStock}.
                </span>
              </span>
            </label>

            {track && (
              <div className="space-y-2">
                <Row label="Head office" value={headOffice} onChange={setHeadOffice} />
                {data.branches.map((b) => (
                  <Row key={b.supplierBranchId} label={`${b.name} (${b.city})${b.isActive ? '' : ' - paused'}`}
                    value={qty[b.supplierBranchId] ?? 0}
                    onChange={(v) => setQty((q) => ({ ...q, [b.supplierBranchId]: v }))} />
                ))}
                <p className="text-sm font-semibold text-gray-800 text-right">Total stock: {total}</p>
              </div>
            )}

            <div className="flex justify-end gap-2 pt-2">
              <button className="btn-secondary" onClick={onClose}>Cancel</button>
              <button className="btn-primary inline-flex items-center gap-2" disabled={busy} onClick={save}>
                {busy && <Loader2 size={14} className="animate-spin" />} Save
              </button>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}

function Row({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <div className="flex items-center gap-3">
      <span className="flex-1 text-sm text-gray-700">{label}</span>
      <input type="number" min={0} className="input w-28 text-right" value={value}
        onChange={(e) => onChange(Math.max(0, Math.floor(Number(e.target.value) || 0)))} />
    </div>
  );
}
