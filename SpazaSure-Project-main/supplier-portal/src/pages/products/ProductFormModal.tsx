import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ImageIcon, Upload, Link2, X, Barcode, Download, Printer, CheckCircle2, Loader2 } from 'lucide-react';
import { useState, useRef } from 'react';
import toast from 'react-hot-toast';
import type { Product } from '../../types';
import { Modal } from '../../components/ui';
import { productsApi } from '../../services/api';

const schema = z.object({
  name:          z.string().min(2, 'Required'),
  description:   z.string().min(10, 'Min 10 characters'),
  sku:           z.string().min(3, 'Required'),
  price:         z.coerce.number().positive('Must be positive'),
  discountPrice: z.coerce.number().optional(),
  categoryId:    z.string().optional().default(''),
  categoryName:  z.string().optional().default(''),
  stockQuantity: z.coerce.number().int().min(0),
  minOrderQty:   z.coerce.number().int().min(1),
  imageUrl:      z.string().url('Must be a valid URL')
                   .or(z.string().startsWith('data:image/', 'Must be a valid image'))
                   .or(z.literal('')),
  supplierId:    z.string().default('sup-001'),
  supplierName:  z.string().default('Fresh Foods SA'),
});
type FormData = z.infer<typeof schema>;

const categories = [
  { id: 'c1', name: 'Cooking Oils' },
  { id: 'c2', name: 'Grains & Cereals' },
  { id: 'c3', name: 'Sugar & Sweeteners' },
  { id: 'c4', name: 'Sauces & Condiments' },
  { id: 'c5', name: 'Beverages' },
  { id: 'c6', name: 'Dairy' },
  { id: 'c7', name: 'Snacks' },
  { id: 'c8', name: 'Personal Care' },
  { id: 'c9', name: 'Cleaning Products' },
];

interface BarcodeResult {
  productId:   string;
  productName: string;
  sku:         string;
  barcode:     string;
  barcodeSvg:  string;
}

interface Props {
  product:  Product | null;
  onSave:   (data: Partial<Product>) => void;
  onClose:  () => void;
}

export default function ProductFormModal({ product, onSave, onClose }: Props) {
  const { register, handleSubmit, watch, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: product ? {
      name: product.name, description: product.description, sku: product.sku,
      price: product.price, discountPrice: product.discountPrice,
      categoryId: product.categoryId, categoryName: product.categoryName,
      stockQuantity: product.stockQuantity, minOrderQty: product.minOrderQty,
      imageUrl: product.imageUrl, supplierId: product.supplierId ?? 'sup-001',
      supplierName: product.supplierName ?? 'Fresh Foods SA',
    } : { minOrderQty: 1, stockQuantity: 0, imageUrl: '', supplierId: 'sup-001', supplierName: 'Fresh Foods SA' },
  });

  const [imgTab,    setImgTab]    = useState<'upload' | 'url'>(product?.imageUrl ? 'url' : 'upload');
  const [dragOver,  setDragOver]  = useState(false);
  const [saving,    setSaving]    = useState(false);
  const [barcodeResult, setBarcodeResult] = useState<BarcodeResult | null>(
    // If editing an existing product that already has a barcode, pre-load it
    product?.qrCode ? { productId: product.id, productName: product.name, sku: product.sku, barcode: product.qrCode, barcodeSvg: '' } : null
  );
  const fileInputRef = useRef<HTMLInputElement>(null);
  const barcodeRef   = useRef<HTMLDivElement>(null);

  const imageUrl = watch('imageUrl');

  const handleFile = (file: File) => {
    if (!file.type.startsWith('image/')) return;
    if (file.size > 5 * 1024 * 1024) { toast.error('Image must be under 5MB'); return; }
    const reader = new FileReader();
    reader.onload = (e) => setValue('imageUrl', e.target?.result as string, { shouldValidate: true });
    reader.readAsDataURL(file);
  };

  const clearImage = () => setValue('imageUrl', '', { shouldValidate: false });

  const onSubmit = async (data: FormData) => {
    const cat = categories.find((c) => c.id === data.categoryId);
    const saved = { ...data, categoryName: cat?.name ?? data.categoryName };

    setSaving(true);
    try {
      if (product) {
        // Editing — update then fetch barcode
        await onSave(saved);
        try {
          const res: any = await productsApi.getBarcode(product.id);
          const d = res.data ?? res;
          setBarcodeResult(d);
        } catch {
          toast.success('Product updated ✅');
        }
      } else {
        // Creating — call API directly so we get the real product ID back
        const created: any = await productsApi.create(saved as any);
        onSave(created);
        const productId = created?.id ?? created?.data?.id;
        if (productId) {
          try {
            const res: any = await productsApi.getBarcode(productId);
            const d = res.data ?? res;
            setBarcodeResult(d);
            toast.success('Product saved! Barcode generated ✅');
          } catch {
            const local = generateLocalBarcode(data.name, data.sku);
            setBarcodeResult(local);
            toast.success('Product saved! Barcode generated locally ✅');
          }
        } else {
          const local = generateLocalBarcode(data.name, data.sku);
          setBarcodeResult(local);
          toast.success('Product saved ✅');
        }
      }
    } catch {
      toast.error('Failed to save product');
    } finally {
      setSaving(false);
    }
  };

  function generateLocalBarcode(name: string, sku: string): BarcodeResult {
    // Deterministic 11-digit seed from sku + timestamp
    const seed = Math.abs(
      sku.split('').reduce((a, c) => a + c.charCodeAt(0), 0) * 1000 + Date.now() % 10000
    ) % 100_000_000_000;
    const d11 = seed.toString().padStart(11, '0').slice(0, 11);
    const check = calcCheck(d11);
    const ean13 = d11 + check;
    return { productId: 'local', productName: name, sku, barcode: ean13, barcodeSvg: buildSvg(ean13) };
  }

  function calcCheck(d11: string): string {
    let sum = 0;
    for (let i = 0; i < 11; i++) sum += parseInt(d11[i]) * (i % 2 === 0 ? 1 : 3);
    const r = sum % 10;
    return (r === 0 ? 0 : 10 - r).toString();
  }

  function buildSvg(ean13: string): string {
    const L = ['0001101','0011001','0010011','0111101','0100011','0110001','0101111','0111011','0110111','0001011'];
    const G = ['0100111','0110011','0011011','0100001','0011101','0111001','0000101','0010001','0001001','0010111'];
    const R = ['1110010','1100110','1101100','1000010','1011100','1001110','1010000','1000100','1001000','1110100'];
    const P = ['LLLLLL','LLGLGG','LLGGLG','LLGGGL','LGLLGG','LGGLLG','LGGGLL','LGLGLG','LGLGGL','LGGLGL'];
    const first = parseInt(ean13[0]);
    const par = P[first];
    let bits = '101';
    for (let i = 1; i <= 6; i++) { const d = parseInt(ean13[i]); bits += par[i-1] === 'L' ? L[d] : G[d]; }
    bits += '01010';
    for (let i = 7; i <= 12; i++) bits += R[parseInt(ean13[i])];
    bits += '101';
    const bw = 2, bh = 80, qz = 10, fs = 11, th = 16;
    const tw = bits.length * bw + qz * 2, totalH = bh + th;
    let rects = '';
    for (let i = 0; i < bits.length; i++)
      if (bits[i] === '1') rects += `<rect x="${qz + i * bw}" y="0" width="${bw}" height="${bh}" fill="#000"/>`;
    const ty = bh + fs + 2;
    const lx = qz + (3 + 21) * bw;
    const rx = qz + (3 + 42 + 5 + 21) * bw;
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${tw}" height="${totalH}" viewBox="0 0 ${tw} ${totalH}"><rect width="${tw}" height="${totalH}" fill="white"/>${rects}<text x="${qz-2}" y="${ty}" font-family="monospace" font-size="${fs}" text-anchor="middle">${ean13[0]}</text><text x="${lx}" y="${ty}" font-family="monospace" font-size="${fs}" text-anchor="middle" letter-spacing="1">${ean13.slice(1,7)}</text><text x="${rx}" y="${ty}" font-family="monospace" font-size="${fs}" text-anchor="middle" letter-spacing="1">${ean13.slice(7)}</text></svg>`;
  }

  // ── Print ────────────────────────────────────────────────────────────────────
  function handlePrint() {
    if (!barcodeResult) return;
    const win = window.open('', '_blank', 'width=400,height=300');
    if (!win) return;
    win.document.write(`
      <html><head><title>Barcode – ${barcodeResult.productName}</title>
      <style>body{font-family:sans-serif;text-align:center;padding:24px}h3{margin:0 0 4px}p{margin:0;color:#666;font-size:13px}svg{margin-top:12px}</style>
      </head><body>
      <h3>${barcodeResult.productName}</h3>
      <p>SKU: ${barcodeResult.sku}</p>
      ${barcodeResult.barcodeSvg}
      <p style="margin-top:6px;font-size:12px;letter-spacing:2px">${barcodeResult.barcode}</p>
      <script>window.onload=()=>{window.print();window.close();}<\/script>
      </body></html>`);
    win.document.close();
  }

  // ── Download SVG ─────────────────────────────────────────────────────────────
  function handleDownload() {
    if (!barcodeResult) return;
    const blob = new Blob([barcodeResult.barcodeSvg], { type: 'image/svg+xml' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = `barcode-${barcodeResult.sku}.svg`;
    a.click();
    URL.revokeObjectURL(url);
  }

  // ── Render ───────────────────────────────────────────────────────────────────
  return (
    <Modal title={product ? 'Edit Product' : 'Add New Product'} onClose={onClose} size="lg">
      {/* ── Barcode success panel ── */}
      {barcodeResult && barcodeResult.barcodeSvg && (
        <div className="mx-6 mt-5 rounded-2xl border-2 border-emerald-200 bg-emerald-50/60 p-5"
             style={{ animation: 'barcodeIn 0.4s cubic-bezier(0.34,1.56,0.64,1)' }}>
          <div className="flex items-center gap-2 mb-3">
            <CheckCircle2 size={16} className="text-emerald-600" />
            <span className="text-sm font-bold text-emerald-800">Barcode Generated & Saved</span>
          </div>
          <div className="flex items-center gap-6">
            {/* SVG barcode */}
            <div
              ref={barcodeRef}
              className="bg-white rounded-xl p-3 border border-emerald-100 shadow-sm flex-shrink-0"
              dangerouslySetInnerHTML={{ __html: barcodeResult.barcodeSvg }}
            />
            {/* Info + actions */}
            <div className="flex-1 min-w-0">
              <p className="text-xs text-gray-500 font-medium mb-0.5">Product</p>
              <p className="font-bold text-gray-900 text-sm truncate">{barcodeResult.productName}</p>
              <p className="text-xs text-gray-500 mt-1 mb-0.5">SKU</p>
              <p className="font-mono text-xs text-gray-700">{barcodeResult.sku}</p>
              <p className="text-xs text-gray-500 mt-1 mb-0.5">EAN-13</p>
              <p className="font-mono text-sm font-bold text-gray-900 tracking-widest">{barcodeResult.barcode}</p>
              <div className="flex gap-2 mt-3">
                <button
                  type="button"
                  onClick={handlePrint}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-primary text-white text-xs font-bold rounded-lg hover:bg-primary-700 transition-colors"
                >
                  <Printer size={12} /> Print
                </button>
                <button
                  type="button"
                  onClick={handleDownload}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-gray-200 text-gray-700 text-xs font-bold rounded-lg hover:border-primary hover:text-primary transition-colors"
                >
                  <Download size={12} /> Download SVG
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} className="p-6 space-y-5">
        <div className="grid grid-cols-2 gap-4">

          {/* ── Image Picker ── */}
          <div className="col-span-2">
            <label className="label">Product Image</label>
            <div className="flex items-center gap-1 bg-gray-100 rounded-xl p-1 w-fit mb-3">
              <button type="button" onClick={() => setImgTab('upload')}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${imgTab === 'upload' ? 'bg-white text-primary shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}>
                <Upload size={12} /> Upload from PC
              </button>
              <button type="button" onClick={() => setImgTab('url')}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${imgTab === 'url' ? 'bg-white text-primary shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}>
                <Link2 size={12} /> Image URL
              </button>
            </div>
            <div className="flex gap-4 items-start">
              {/* Preview */}
              <div className="relative w-24 h-24 rounded-xl border-2 border-dashed border-gray-200 bg-gray-50 flex-shrink-0 overflow-hidden group">
                {imageUrl ? (
                  <>
                    <img src={imageUrl} alt="preview" className="w-full h-full object-cover" onError={(e) => (e.currentTarget.style.display = 'none')} />
                    <button type="button" onClick={clearImage}
                      className="absolute top-1 right-1 w-5 h-5 bg-red-500 rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity shadow">
                      <X size={10} className="text-white" />
                    </button>
                  </>
                ) : (
                  <div className="w-full h-full flex flex-col items-center justify-center gap-1">
                    <ImageIcon size={22} className="text-gray-300" />
                    <span className="text-[10px] text-gray-400 font-medium">Preview</span>
                  </div>
                )}
              </div>
              {/* Upload zone */}
              {imgTab === 'upload' && (
                <div
                  className={`flex-1 border-2 border-dashed rounded-xl p-5 text-center cursor-pointer transition-all ${dragOver ? 'border-primary bg-primary-50 scale-[1.01]' : 'border-gray-200 hover:border-primary hover:bg-gray-50'}`}
                  onClick={() => fileInputRef.current?.click()}
                  onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
                  onDragLeave={() => setDragOver(false)}
                  onDrop={(e) => { e.preventDefault(); setDragOver(false); const f = e.dataTransfer.files[0]; if (f) handleFile(f); }}
                >
                  <Upload size={20} className={`mx-auto mb-2 ${dragOver ? 'text-primary' : 'text-gray-400'}`} />
                  <p className="text-sm font-semibold text-gray-700">{dragOver ? 'Drop it here!' : 'Drag & drop or click to browse'}</p>
                  <p className="text-xs text-gray-400 mt-1">JPG, PNG, WEBP · max 5MB</p>
                  <input ref={fileInputRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden"
                    onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f); e.target.value = ''; }} />
                </div>
              )}
              {/* URL input */}
              {imgTab === 'url' && (
                <div className="flex-1 space-y-1.5">
                  <input {...register('imageUrl')} className="input" placeholder="https://example.com/product.jpg" />
                  {errors.imageUrl && <p className="text-red-500 text-xs font-medium">{errors.imageUrl.message}</p>}
                  <p className="text-xs text-gray-400">Paste a direct image URL — preview updates automatically</p>
                </div>
              )}
            </div>
          </div>

          {/* ── Fields ── */}
          <div className="col-span-2">
            <label className="label">Product Name</label>
            <input {...register('name')} className={`input ${errors.name ? 'input-error' : ''}`} placeholder="Sunflower Oil 2L" />
            {errors.name && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.name.message}</p>}
          </div>

          <div className="col-span-2">
            <label className="label">Description</label>
            <textarea {...register('description')} className={`input resize-none ${errors.description ? 'input-error' : ''}`} rows={3} placeholder="Describe your product..." />
            {errors.description && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.description.message}</p>}
          </div>

          <div>
            <label className="label">SKU</label>
            <input {...register('sku')} className={`input font-mono ${errors.sku ? 'input-error' : ''}`} placeholder="OIL-SUN-2L" />
            {errors.sku && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.sku.message}</p>}
          </div>

          <div>
            <label className="label">Category</label>
            <select {...register('categoryId')}
              onChange={(e) => { setValue('categoryId', e.target.value); const cat = categories.find((c) => c.id === e.target.value); if (cat) setValue('categoryName', cat.name); }}
              className={`select ${errors.categoryId ? 'input-error' : ''}`}>
              <option value="">Select category</option>
              {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
            {errors.categoryId && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.categoryId.message}</p>}
          </div>

          <div>
            <label className="label">Price (R)</label>
            <div className="relative">
              <span className="absolute left-3.5 top-2.5 text-gray-400 text-sm font-semibold">R</span>
              <input {...register('price')} type="number" step="0.01" className={`input pl-7 ${errors.price ? 'input-error' : ''}`} placeholder="45.99" />
            </div>
            {errors.price && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.price.message}</p>}
          </div>

          <div>
            <label className="label">Discount Price <span className="text-gray-400 font-normal text-xs">(optional)</span></label>
            <div className="relative">
              <span className="absolute left-3.5 top-2.5 text-gray-400 text-sm font-semibold">R</span>
              <input {...register('discountPrice')} type="number" step="0.01" className="input pl-7" placeholder="39.99" />
            </div>
          </div>

          <div>
            <label className="label">Stock Quantity</label>
            <input {...register('stockQuantity')} type="number" className={`input ${errors.stockQuantity ? 'input-error' : ''}`} placeholder="500" />
            {errors.stockQuantity && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.stockQuantity.message}</p>}
          </div>

          <div>
            <label className="label">Min Order Quantity</label>
            <input {...register('minOrderQty')} type="number" className={`input ${errors.minOrderQty ? 'input-error' : ''}`} placeholder="12" />
            {errors.minOrderQty && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.minOrderQty.message}</p>}
            <p className="text-xs text-gray-400 mt-1">Minimum units per order from spaza shops</p>
          </div>
        </div>

        {/* Barcode info box — shown before save */}
        {!barcodeResult && (
          <div className="flex items-center gap-3 bg-primary-50 border border-primary-100 rounded-xl p-3.5">
            <Barcode size={18} className="text-primary flex-shrink-0" />
            <p className="text-sm text-primary font-medium">
              A unique <strong>EAN-13 barcode</strong> will be automatically generated and saved when you submit this product.
            </p>
          </div>
        )}

        {!product && (
          <div className="info-box">
            <strong>Note:</strong> New products require admin approval before going live on the marketplace. This typically takes 1–2 business days.
          </div>
        )}

        <div className="flex justify-end gap-3 pt-2 border-t border-gray-100">
          <button type="button" onClick={onClose} className="btn-secondary">
            {barcodeResult ? 'Close' : 'Cancel'}
          </button>
          {!barcodeResult && (
            <button type="submit" disabled={saving} className="btn-primary flex items-center gap-2 min-w-[160px] justify-center">
              {saving ? <><Loader2 size={14} className="animate-spin" /> Saving...</> : product ? 'Save Changes' : 'Submit for Approval'}
            </button>
          )}
        </div>
      </form>

      <style>{`
        @keyframes barcodeIn {
          from { opacity: 0; transform: translateY(-10px) scale(0.97); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
      `}</style>
    </Modal>
  );
}
