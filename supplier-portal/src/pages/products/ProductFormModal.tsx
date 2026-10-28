import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ImageIcon, Upload, Link2, X, Barcode, Download, Printer, CheckCircle2, Loader2 } from 'lucide-react';
import { useState, useRef, useEffect } from 'react';
import toast from 'react-hot-toast';
import type { Product, ProductInfo } from '../../types';
import { emptyProductInfo } from '../../types';
import { Modal } from '../../components/ui';
import { productsApi } from '../../services/api';
import { barcodeDigits, barcodeProblem } from '../../utils/barcode';
import { AuthenticitySection, DetailsSection, NutritionSection } from './ProductInfoFields';

type Tab = 'basics' | 'details' | 'nutrition' | 'authenticity';
const TABS: { key: Tab; label: string }[] = [
  { key: 'basics', label: 'Basics' },
  { key: 'details', label: 'Details & specs' },
  { key: 'nutrition', label: 'Ingredients & nutrition' },
  { key: 'authenticity', label: 'Spot a fake' },
];

/** Drops rows the supplier started but left empty, so they don't block saving. */
const cleanInfo = (info: ProductInfo): ProductInfo => ({
  ...info,
  nutrition: info.nutrition.filter((n) => n.nutrient.trim() && (n.per100.trim() || n.perServing.trim())),
  specifications: info.specifications.filter((s) => s.label.trim() || s.value.trim()),
  authenticityTips: info.authenticityTips.filter((t) => t.trim()),
});

const schema = z.object({
  name:          z.string().min(2, 'Required'),
  description:   z.string().min(10, 'Min 10 characters'),
  // Optional: the barcode identifies the product; the SKU is the supplier's own stock code
  sku:           z.string().optional().default(''),
  // Checked in the form, and only when changed: an existing product may hold an older code
  barcode:       z.string().optional().default(''),
  price:         z.coerce.number().positive('Must be positive'),
  categoryId:    z.string().optional().default(''),
  categoryName:  z.string().optional().default(''),
  stockQuantity: z.coerce.number().int().min(0),
  minOrderQty:   z.coerce.number().int().min(1),
  imageUrl:      z.string().url('Must be a valid URL')
                   .or(z.string().startsWith('data:image/', 'Must be a valid image'))
                   .or(z.literal('')),
});
type FormData = z.infer<typeof schema>;

interface BarcodeResult {
  productId:   string;
  productName: string;
  sku:         string;
  barcode:     string;
  barcodeSvg:  string;
  /** SpazaSure made it because the pack has no barcode of its own. */
  generated?:  boolean;
}

interface Props {
  product:  Product | null;
  onSave:   (data: Partial<Product>) => void;
  onClose:  () => void;
}

export default function ProductFormModal({ product, onSave, onClose }: Props) {
  const { register, handleSubmit, watch, setValue, setError, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: product ? {
      name: product.name, description: product.description, sku: product.sku,
      barcode: product.barcode ?? '',
      price: product.price,
      categoryId: product.categoryId, categoryName: product.categoryName,
      stockQuantity: product.stockQuantity, minOrderQty: product.minOrderQty,
      imageUrl: product.imageUrl,
    } : { minOrderQty: 1, stockQuantity: 0, imageUrl: '', barcode: '' },
  });

  // Whether the product's current barcode was made by SpazaSure rather than read off the pack.
  // Those don't match a scan of the real pack, so the supplier is asked for the printed one.
  const [barcodeWasGenerated, setBarcodeWasGenerated] = useState(false);
  useEffect(() => {
    if (!product?.barcode) return;
    let cancelled = false;
    productsApi.getBarcode(product.id)
      .then((res: any) => { if (!cancelled) setBarcodeWasGenerated((res?.data ?? res)?.generated === true); })
      .catch(() => { /* only a hint; the form works without it */ });
    return () => { cancelled = true; };
  }, [product]);

  const [tab, setTab] = useState<Tab>('basics');
  const [isFoodItem, setIsFoodItem] = useState(product?.isFoodItem ?? false);
  const [info, setInfo] = useState<ProductInfo>(emptyProductInfo());
  // New products start empty. Existing ones load their information first; until then (or if
  // loading fails) the form doesn't send it, so the stored information can't be wiped.
  const [infoReady, setInfoReady] = useState(!product);
  useEffect(() => {
    if (!product) return;
    let cancelled = false;
    productsApi.getInfo(product.id)
      .then((loaded) => { if (!cancelled) { setInfo(loaded); setInfoReady(true); } })
      .catch(() => { if (!cancelled) toast.error('Could not load the product information. Basics can still be saved.'); });
    return () => { cancelled = true; };
  }, [product]);

  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);
  useEffect(() => {
    let cancelled = false;
    productsApi.categories()
      .then((list) => { if (!cancelled) setCategories(list); })
      .catch(() => { /* category is optional; the dropdown just stays empty */ });
    return () => { cancelled = true; };
  }, []);

  const [imgTab,    setImgTab]    = useState<'upload' | 'url'>(product?.imageUrl ? 'url' : 'upload');
  const [dragOver,  setDragOver]  = useState(false);
  const [saving,    setSaving]    = useState(false);
  const [barcodeResult, setBarcodeResult] = useState<BarcodeResult | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const barcodeRef   = useRef<HTMLDivElement>(null);

  const imageUrl = watch('imageUrl');
  const barcodeValue = watch('barcode') ?? '';
  const barcodeEntered = barcodeDigits(barcodeValue).length > 0;
  const barcodeChanged = barcodeValue.trim() !== (product?.barcode ?? '');
  const barcodeError = barcodeChanged ? barcodeProblem(barcodeValue) : null;

  const handleFile = (file: File) => {
    if (!file.type.startsWith('image/')) return;
    if (file.size > 5 * 1024 * 1024) { toast.error('Image must be under 5MB'); return; }
    const reader = new FileReader();
    reader.onload = (e) => setValue('imageUrl', e.target?.result as string, { shouldValidate: true });
    reader.readAsDataURL(file);
  };

  const clearImage = () => setValue('imageUrl', '', { shouldValidate: false });

  const onSubmit = async (data: FormData) => {
    if (!data.categoryId) {
      setError('categoryId', { type: 'required', message: 'Choose a category so shops can find this product' });
      setTab('basics');
      return;
    }
    if (barcodeError) {
      setTab('basics');
      toast.error(barcodeError);
      return;
    }
    if (!product && !data.imageUrl) {
      setError('imageUrl', { type: 'required', message: 'Product image is required' });
      toast.error('Add a product image before submitting');
      return;
    }

    const cat = categories.find((c) => c.id === data.categoryId);
    const saved = {
      ...data,
      categoryName: cat?.name ?? data.categoryName,
      isFoodItem,
      info: infoReady ? cleanInfo(info) : undefined,
    };

    setSaving(true);
    try {
      if (product) {
        // Editing — the page saves, shows the toast and closes the modal
        await onSave(saved);
      } else {
        // Creating — call API directly so we get the real product ID back
        const created: any = await productsApi.create(saved as any);
        onSave(created);
        const productId = created?.id ?? created?.data?.id;
        try {
          if (!productId) throw new Error('No product id returned');
          const res: any = await productsApi.getBarcode(productId);
          const result: BarcodeResult = res.data ?? res;
          setBarcodeResult(result);
          toast.success(result.generated
            ? 'Product submitted for approval. SpazaSure barcode created.'
            : 'Product submitted for approval.');
        } catch {
          // The product is saved; its barcode can be opened later from the product list
          toast.success('Product submitted for approval');
          onClose();
        }
      }
    } catch (err: any) {
      const status = err?.response?.status;
      const message = err?.response?.data?.message
        ?? err?.response?.data?.error
        ?? (status ? `Product save failed (${status})` : 'Could not reach the API while saving the product');
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

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
            <span className="text-sm font-bold text-emerald-800">
              {barcodeResult.generated ? 'SpazaSure barcode created. Print it and stick it on each pack' : 'Barcode saved'}
            </span>
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

      <form onSubmit={handleSubmit(onSubmit, () => setTab('basics'))} className="p-6 space-y-5">
        {!barcodeResult && (
          <div className="flex gap-1 bg-gray-100 rounded-xl p-1 overflow-x-auto" role="tablist" aria-label="Product sections">
            {TABS.map(({ key, label }) => (
              <button key={key} type="button" role="tab" aria-selected={tab === key} onClick={() => setTab(key)}
                className={`flex-1 whitespace-nowrap px-3 py-2 rounded-lg text-xs font-bold transition-all ${tab === key ? 'bg-white text-primary shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}>
                {label}
              </button>
            ))}
          </div>
        )}

        {tab !== 'basics' && !infoReady && (
          <div className="flex items-center gap-2 text-sm text-gray-500"><Loader2 size={14} className="animate-spin" /> Loading product information...</div>
        )}
        {tab === 'details' && infoReady && <DetailsSection info={info} onChange={setInfo} />}
        {tab === 'nutrition' && infoReady && <NutritionSection info={info} onChange={setInfo} isFoodItem={isFoodItem} />}
        {tab === 'authenticity' && infoReady && <AuthenticitySection info={info} onChange={setInfo} />}

        <div className={`grid grid-cols-2 gap-4 ${tab === 'basics' ? '' : 'hidden'}`}>

          {/* ── Image Picker ── */}
          <div className="col-span-2">
            <label className="label">Product Image {!product && <span className="text-red-500">*</span>}</label>
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
                  <input {...register('imageUrl')} className={`input ${errors.imageUrl ? 'input-error' : ''}`} placeholder="https://example.com/product.jpg" />
                  {errors.imageUrl && <p className="text-red-500 text-xs font-medium">{errors.imageUrl.message}</p>}
                  <p className="text-xs text-gray-400">Paste a direct image URL — preview updates automatically</p>
                </div>
              )}
            </div>
            {!product && !imageUrl && !errors.imageUrl && <p className="text-xs text-gray-400 mt-2">Add a clear product photo by upload or image URL.</p>}
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
            <label className="label">SKU <span className="text-gray-400 font-normal">(optional, your own stock code)</span></label>
            <input {...register('sku')} className={`input font-mono ${errors.sku ? 'input-error' : ''}`} placeholder="OIL-SUN-2L" />
            {errors.sku && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.sku.message}</p>}
          </div>

          <div className="col-span-2">
            <label className="label">Barcode on the pack <span className="text-gray-400 font-normal">(if it has one)</span></label>
            <input {...register('barcode')} inputMode="numeric" autoComplete="off"
              className={`input font-mono tracking-wider ${barcodeError ? 'input-error' : ''}`}
              placeholder="e.g. 6001234567890" />
            {barcodeError ? (
              <p className="text-red-500 text-xs mt-1.5 font-medium">{barcodeError}</p>
            ) : barcodeEntered && (barcodeChanged ? true : !barcodeWasGenerated) ? (
              <p className="text-emerald-600 text-xs mt-1.5 font-medium flex items-center gap-1">
                <CheckCircle2 size={12} /> Valid barcode. Shops and customers who scan the pack will find this product.
              </p>
            ) : (
              <p className="text-xs text-gray-400 mt-1.5">
                Type the numbers printed under the bars, exactly as on the pack. No barcode on the pack? Leave this empty and SpazaSure will create one.
              </p>
            )}
            {product && barcodeWasGenerated && !barcodeChanged && (
              <p className="text-xs mt-2 font-medium text-amber-700 bg-amber-50 border border-amber-200 rounded-lg p-2.5">
                This barcode was created by SpazaSure, so scanning the real pack won't find this product.
                If the pack has its own barcode, type it here instead.
              </p>
            )}
          </div>

          <div>
            <label className="label">Category <span className="text-red-500">*</span></label>
            <select {...register('categoryId')}
              onChange={(e) => { setValue('categoryId', e.target.value); const cat = categories.find((c) => c.id === e.target.value); if (cat) setValue('categoryName', cat.name); }}
              className={`select ${errors.categoryId ? 'input-error' : ''}`}>
              <option value="">Select category</option>
              {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              {/* Keep a category an admin has since hidden, so editing doesn't silently clear it. */}
              {product?.categoryId && !categories.some((c) => c.id === product.categoryId) && (
                <option value={product.categoryId}>{product.categoryName || 'Current category'} (no longer listed)</option>
              )}
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

          <label className="col-span-2 flex items-start gap-3 rounded-xl border border-gray-200 p-3 cursor-pointer">
            <input type="checkbox" className="mt-0.5" checked={isFoodItem} onChange={(e) => setIsFoodItem(e.target.checked)} />
            <span>
              <span className="block text-sm font-semibold text-gray-800">This is a food or drink product</span>
              <span className="block text-xs text-gray-400">Customers get allergy warnings and nutrition details for food and drink.</span>
            </span>
          </label>
        </div>

        {/* Barcode info box: only a new product with no barcode gets one made */}
        {!barcodeResult && !product && !barcodeEntered && (
          <div className="flex items-center gap-3 bg-primary-50 border border-primary-100 rounded-xl p-3.5">
            <Barcode size={18} className="text-primary flex-shrink-0" />
            <p className="text-sm text-primary font-medium">
              No barcode entered, so SpazaSure will create one for you to print and stick on the packs.
              If the pack already has a barcode, type it in the <strong>Basics</strong> tab instead.
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
