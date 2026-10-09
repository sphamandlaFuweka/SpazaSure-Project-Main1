import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { Download, ShieldCheck, TriangleAlert, Loader2, Undo2 } from 'lucide-react';
import { Modal } from '../../components/ui';
import { productCodesApi, type CodeBatch, type IssuedCode } from '../../services/api';
import type { Product } from '../../types';

interface Props {
  product: Product;
  onClose: () => void;
}

function downloadCsv(product: Product, batch: string, expiry: string, codes: IssuedCode[]) {
  const rows = [
    ['product', 'batch', 'expiry', 'code', 'scratch_pin'],
    ...codes.map((c) => [product.name, batch, expiry, c.code, c.pin]),
  ];
  const csv = rows.map((r) => r.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\n');
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = `${product.name.replace(/\W+/g, '_')}_${batch}_codes.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

export default function ProductCodesModal({ product, onClose }: Props) {
  const [batchNumber, setBatchNumber] = useState('');
  const [expiry, setExpiry] = useState('');
  const [quantity, setQuantity] = useState(100);
  const [busy, setBusy] = useState(false);
  const [batches, setBatches] = useState<CodeBatch[]>([]);
  const [issued, setIssued] = useState<{ batch: string; expiry: string; codes: IssuedCode[] } | null>(null);

  const loadBatches = () => productCodesApi.batches(product.id).then(setBatches).catch(() => setBatches([]));
  useEffect(() => { loadBatches(); }, [product.id]);

  const generate = async () => {
    if (batchNumber.trim().length < 2) { toast.error('Enter a batch number'); return; }
    setBusy(true);
    try {
      const res = await productCodesApi.generate(product.id, { batchNumber: batchNumber.trim(), expiryDate: expiry || null, quantity });
      setIssued({ batch: res.batchNumber, expiry, codes: res.codes });
      downloadCsv(product, res.batchNumber, expiry, res.codes);
      toast.success(`${res.codes.length} codes generated`);
      loadBatches();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg || 'Could not generate codes');
    } finally {
      setBusy(false);
    }
  };

  const recall = async (batch: string) => {
    if (!window.confirm(`Recall batch ${batch}? Customers who scan it will be warned.`)) return;
    try {
      await productCodesApi.recall(product.id, batch);
      toast.success('Batch recalled');
      loadBatches();
    } catch {
      toast.error('Could not recall batch');
    }
  };

  return (
    <Modal title={`Authenticity codes: ${product.name}`} onClose={onClose} size="lg">
      <div className="p-6 space-y-6">
        <div className="flex items-start gap-3 rounded-xl bg-primary-50 border border-primary-100 p-4">
          <ShieldCheck size={18} className="text-primary mt-0.5 flex-shrink-0" />
          <p className="text-sm text-primary-800">
            Each unit gets an open code (print it as a barcode or QR) and a secret PIN (print it under a scratch panel).
            Customers scan the code, scratch the panel and enter the PIN. A PIN works once, so reused or copied packs are caught.
          </p>
        </div>

        <div className="grid grid-cols-3 gap-4">
          <div>
            <label className="label">Batch number</label>
            <input className="input" value={batchNumber} onChange={(e) => setBatchNumber(e.target.value)} placeholder="e.g. B2026-10A" />
          </div>
          <div>
            <label className="label">Expiry date</label>
            <input className="input" type="date" value={expiry} onChange={(e) => setExpiry(e.target.value)} />
          </div>
          <div>
            <label className="label">Quantity (max 5000)</label>
            <input className="input" type="number" min={1} max={5000} value={quantity} onChange={(e) => setQuantity(Number(e.target.value))} />
          </div>
        </div>
        <button onClick={generate} disabled={busy} className="btn-primary flex items-center gap-2">
          {busy ? <Loader2 size={15} className="animate-spin" /> : <Download size={15} />} Generate and download CSV
        </button>

        {issued && (
          <div className="flex items-start gap-3 rounded-xl bg-accent-50 border border-accent-400/50 p-4">
            <TriangleAlert size={18} className="text-accent-700 mt-0.5 flex-shrink-0" />
            <div className="text-sm text-gray-800">
              <p className="font-bold">Keep the file safe.</p>
              <p>The PINs are shown only once and cannot be recovered later.</p>
              <button className="mt-2 text-primary font-semibold hover:underline" onClick={() => downloadCsv(product, issued.batch, issued.expiry, issued.codes)}>
                Download again
              </button>
            </div>
          </div>
        )}

        <div>
          <h3 className="font-bold text-gray-900 mb-2">Batches</h3>
          {batches.length === 0 ? (
            <p className="text-sm text-gray-400">No codes generated yet.</p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-gray-400 uppercase">
                  <th className="py-2">Batch</th><th>Expiry</th><th className="text-right">Codes</th><th className="text-right">Verified</th><th className="text-right">Flagged</th><th />
                </tr>
              </thead>
              <tbody>
                {batches.map((b) => (
                  <tr key={b.batchNumber} className="border-t border-gray-100">
                    <td className="py-2 font-semibold">{b.batchNumber}</td>
                    <td>{b.expiryDate ?? '-'}</td>
                    <td className="text-right tabular-nums">{b.total}</td>
                    <td className="text-right tabular-nums">{b.verified}</td>
                    <td className="text-right tabular-nums">{b.compromised}</td>
                    <td className="text-right">
                      {b.recalled > 0 ? (
                        <span className="text-xs font-bold text-red-600">Recalled</span>
                      ) : (
                        <button onClick={() => recall(b.batchNumber)} className="inline-flex items-center gap-1 text-xs font-semibold text-red-600 hover:underline">
                          <Undo2 size={12} /> Recall
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </Modal>
  );
}
