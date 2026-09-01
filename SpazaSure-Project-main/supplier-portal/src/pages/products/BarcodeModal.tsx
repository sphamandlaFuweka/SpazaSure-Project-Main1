import { useEffect, useState } from 'react';
import { X, Printer, Download, Loader2, Barcode, QrCode, Package, ShieldCheck, AlertTriangle } from 'lucide-react';
import type { Product, ProductQrCodeData } from '../../types';
import { productsApi } from '../../services/api';

interface Props {
  product: Product;
  onClose: () => void;
}

const escapeHtml = (value: unknown) => String(value ?? '')
  .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;').replace(/'/g, '&#039;');

function downloadSvg(svg: string, filename: string) {
  const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

export default function BarcodeModal({ product, onClose }: Props) {
  const [data, setData] = useState<ProductQrCodeData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let active = true;
    productsApi.getQrCode(product.id)
      .then((value) => { if (active) setData(value); })
      .catch(() => { if (active) setError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [product.id]);

  function handlePrint() {
    if (!data) return;
    const win = window.open('', '_blank', 'width=760,height=720');
    if (!win) return;
    const verified = data.isVerified ? 'Verified product' : 'Not fully verified';
    win.document.write(`<!doctype html><html><head><title>QR - ${escapeHtml(data.productName)}</title>
      <style>body{font-family:Arial,sans-serif;padding:28px;color:#111;max-width:680px;margin:auto}h2{margin:0}.meta{color:#666;font-size:12px}.codes{display:grid;grid-template-columns:1fr 1fr;gap:24px;margin-top:24px}.code{text-align:center;border:1px solid #ddd;border-radius:12px;padding:18px}.code svg{max-width:100%;height:auto}.token{overflow-wrap:anywhere;font:11px monospace}.badge{display:inline-block;margin-top:8px;padding:5px 10px;border-radius:20px;background:${data.isVerified ? '#d1fae5' : '#fef3c7'};color:${data.isVerified ? '#065f46' : '#92400e'};font-weight:bold;font-size:12px}@media print{button{display:none}}</style>
      </head><body><h2>${escapeHtml(data.productName)}</h2><p class="meta">SKU: ${escapeHtml(data.sku)} · Supplier: ${escapeHtml(data.supplierName)}</p><span class="badge">${escapeHtml(verified)}</span>
      <div class="codes"><div class="code"><h3>Traceable QR</h3>${data.qrSvg}<p class="token">${escapeHtml(data.qrToken)}</p></div><div class="code"><h3>EAN-13 Barcode</h3>${data.barcodeSvg}<p class="token">${escapeHtml(data.barcode)}</p></div></div>
      <script>window.onload=()=>{window.print();window.close();}<\/script></body></html>`);
    win.document.close();
  }

  return <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
    <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onClose} />
    <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-2xl max-h-[90vh] flex flex-col overflow-hidden">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 bg-gray-50">
        <div className="flex items-center gap-2"><QrCode size={16} className="text-indigo-600"/><span className="font-bold text-sm">Product QR & Barcode</span></div>
        <button onClick={onClose} className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-gray-200"><X size={16}/></button>
      </div>
      <div className="p-5 overflow-y-auto">
        {loading && <div className="py-16 text-center"><Loader2 className="animate-spin mx-auto text-indigo-600"/><p className="mt-3 text-sm text-gray-500">Loading persisted product codes...</p></div>}
        {error && <div className="py-16 text-center text-red-600"><AlertTriangle className="mx-auto"/><p className="mt-3 text-sm font-semibold">Unable to load the registered QR code.</p><p className="text-xs mt-1">No local or unregistered code was generated.</p></div>}
        {data && <div className="space-y-4">
          <div className="flex items-center gap-4 p-4 bg-gray-50 rounded-2xl border">
            <div className="w-14 h-14 rounded-xl bg-white border flex items-center justify-center">{product.imageUrl ? <img src={product.imageUrl} alt={product.name} className="w-full h-full object-cover rounded-xl"/> : <Package className="text-gray-300"/>}</div>
            <div className="flex-1"><p className="font-bold">{data.productName}</p><p className="text-xs text-gray-500 font-mono">SKU: {data.sku}</p><p className="text-xs text-gray-500">Supplier: {data.supplierName}</p></div>
            <span className={`inline-flex items-center gap-1 px-3 py-1.5 rounded-full text-xs font-bold ${data.isVerified ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'}`}><ShieldCheck size={13}/>{data.isVerified ? 'Verified' : 'Not fully verified'}</span>
          </div>
          {data.isRecalled && <div className="p-3 rounded-xl bg-red-50 border border-red-200 text-red-700 text-sm font-bold">This QR code has been recalled.</div>}
          <div className="grid md:grid-cols-2 gap-4">
            <div className="border-2 border-gray-100 rounded-2xl p-4 text-center"><p className="font-bold text-sm mb-3">Traceable QR Code</p><div className="flex justify-center [&_svg]:max-w-full [&_svg]:h-auto" dangerouslySetInnerHTML={{ __html: data.qrSvg }}/><p className="text-[10px] font-mono break-all mt-2">{data.qrToken}</p><button onClick={() => downloadSvg(data.qrSvg, `qr-${data.sku}.svg`)} className="mt-3 btn-secondary text-xs inline-flex items-center gap-1"><Download size={13}/>QR SVG</button></div>
            <div className="border-2 border-gray-100 rounded-2xl p-4 text-center"><p className="font-bold text-sm mb-3">EAN-13 Barcode</p><div className="flex justify-center [&_svg]:max-w-full [&_svg]:h-auto" dangerouslySetInnerHTML={{ __html: data.barcodeSvg }}/><p className="text-xs font-mono tracking-widest mt-2">{data.barcode}</p><button onClick={() => downloadSvg(data.barcodeSvg, `barcode-${data.sku}.svg`)} className="mt-3 btn-secondary text-xs inline-flex items-center gap-1"><Download size={13}/>Barcode SVG</button></div>
          </div>
          <div className="flex gap-2"><button onClick={handlePrint} className="btn-primary flex-1 flex items-center justify-center gap-2"><Printer size={14}/>Print Both</button><button onClick={onClose} className="btn-secondary flex-1">Close</button></div>
        </div>}
      </div>
    </div>
  </div>;
}
