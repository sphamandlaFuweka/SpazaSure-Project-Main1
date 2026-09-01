import { useState, useRef, useCallback } from 'react';
import { Upload, FileSpreadsheet, X, CheckCircle2, AlertCircle, Download, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { Modal } from '../../components/ui';
import { productsApi } from '../../services/api';
import type { ProductFormData } from '../../types';
import clsx from 'clsx';

interface ParsedRow {
  name: string;
  description: string;
  sku: string;
  price: number;
  discountPrice?: number;
  categoryId: string;
  stockQuantity: number;
  minOrderQty: number;
  imageUrl: string;
  error?: string;
}

interface UploadResult {
  row: number;
  name: string;
  sku: string;
  success: boolean;
  error?: string;
}

interface Props {
  onClose: () => void;
  onComplete: () => void;
}

const TEMPLATE_HEADERS = ['name', 'description', 'sku', 'price', 'discountPrice', 'categoryId', 'stockQuantity', 'minOrderQty', 'imageUrl'];

const SAMPLE_DATA = [
  ['Sunflower Oil 2L', 'Pure sunflower cooking oil 2 litre bottle', 'OIL-SUN-2L', '45.99', '', 'c1', '500', '12', 'https://example.com/oil.jpg'],
  ['White Sugar 2.5kg', 'Fine white sugar 2.5kg bag', 'SUG-WHT-25', '39.99', '34.99', 'c3', '300', '6', 'https://example.com/sugar.jpg'],
];

export default function BulkUploadModal({ onClose, onComplete }: Props) {
  const [step, setStep] = useState<'upload' | 'preview' | 'progress' | 'done'>('upload');
  const [parsedRows, setParsedRows] = useState<ParsedRow[]>([]);
  const [results, setResults] = useState<UploadResult[]>([]);
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [dragOver, setDragOver] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Download CSV template
  const downloadTemplate = () => {
    const csvContent = [
      TEMPLATE_HEADERS.join(','),
      ...SAMPLE_DATA.map((row) => row.map((cell) => `"${cell}"`).join(',')),
    ].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'spazasure-product-template.csv';
    a.click();
    URL.revokeObjectURL(url);
  };

  // Parse CSV content
  const parseCSV = useCallback((text: string): ParsedRow[] => {
    const lines = text.split('\n').map((l) => l.trim()).filter(Boolean);
    if (lines.length < 2) {
      toast.error('CSV must have a header row and at least one data row');
      return [];
    }

    // Parse header
    const header = parseCSVLine(lines[0]).map((h) => h.trim().toLowerCase());
    const nameIdx = header.indexOf('name');
    const descIdx = header.indexOf('description');
    const skuIdx = header.indexOf('sku');
    const priceIdx = header.indexOf('price');
    const discountIdx = header.indexOf('discountprice');
    const catIdx = header.indexOf('categoryid');
    const stockIdx = header.indexOf('stockquantity');
    const moqIdx = header.indexOf('minorderqty');
    const imgIdx = header.indexOf('imageurl');

    if (nameIdx === -1 || skuIdx === -1 || priceIdx === -1) {
      toast.error('CSV must have at least: name, sku, price columns');
      return [];
    }

    const rows: ParsedRow[] = [];
    for (let i = 1; i < lines.length; i++) {
      const cells = parseCSVLine(lines[i]);
      const name = cells[nameIdx]?.trim() ?? '';
      const sku = cells[skuIdx]?.trim() ?? '';
      const priceStr = cells[priceIdx]?.trim() ?? '';
      const price = parseFloat(priceStr);

      let error: string | undefined;
      if (!name) error = 'Name is required';
      else if (!sku) error = 'SKU is required';
      else if (isNaN(price) || price <= 0) error = 'Invalid price';

      rows.push({
        name,
        description: cells[descIdx]?.trim() ?? '',
        sku,
        price: isNaN(price) ? 0 : price,
        discountPrice: discountIdx >= 0 ? parseFloat(cells[discountIdx]) || undefined : undefined,
        categoryId: cells[catIdx]?.trim() ?? '',
        stockQuantity: stockIdx >= 0 ? parseInt(cells[stockIdx]) || 0 : 0,
        minOrderQty: moqIdx >= 0 ? parseInt(cells[moqIdx]) || 1 : 1,
        imageUrl: cells[imgIdx]?.trim() ?? '',
        error,
      });
    }
    return rows;
  }, []);

  // Parse a single CSV line respecting quoted fields
  function parseCSVLine(line: string): string[] {
    const result: string[] = [];
    let current = '';
    let inQuotes = false;
    for (let i = 0; i < line.length; i++) {
      const char = line[i];
      if (char === '"') {
        if (inQuotes && line[i + 1] === '"') {
          current += '"';
          i++;
        } else {
          inQuotes = !inQuotes;
        }
      } else if (char === ',' && !inQuotes) {
        result.push(current);
        current = '';
      } else {
        current += char;
      }
    }
    result.push(current);
    return result;
  }

  // Handle file selection
  const handleFile = (file: File) => {
    if (!file.name.endsWith('.csv') && !file.name.endsWith('.txt')) {
      toast.error('Please upload a CSV file');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      toast.error('File must be under 5MB');
      return;
    }
    const reader = new FileReader();
    reader.onload = (e) => {
      const text = e.target?.result as string;
      const rows = parseCSV(text);
      if (rows.length > 0) {
        setParsedRows(rows);
        setStep('preview');
      }
    };
    reader.readAsText(file);
  };

  // Submit all valid rows
  const handleBulkSubmit = async () => {
    const validRows = parsedRows.filter((r) => !r.error);
    if (validRows.length === 0) {
      toast.error('No valid rows to upload');
      return;
    }

    setStep('progress');
    setUploading(true);
    setProgress(0);
    const uploadResults: UploadResult[] = [];

    for (let i = 0; i < validRows.length; i++) {
      const row = validRows[i];
      try {
        await productsApi.create({
          name: row.name,
          description: row.description,
          sku: row.sku,
          price: row.price,
          discountPrice: row.discountPrice,
          categoryId: row.categoryId,
          stockQuantity: row.stockQuantity,
          minOrderQty: row.minOrderQty,
          imageUrl: row.imageUrl,
        } as ProductFormData);
        uploadResults.push({ row: i + 1, name: row.name, sku: row.sku, success: true });
      } catch (err: any) {
        const msg = err?.response?.data?.message ?? 'Failed to create';
        uploadResults.push({ row: i + 1, name: row.name, sku: row.sku, success: false, error: msg });
      }
      setProgress(Math.round(((i + 1) / validRows.length) * 100));
    }

    setResults(uploadResults);
    setUploading(false);
    setStep('done');

    const successCount = uploadResults.filter((r) => r.success).length;
    if (successCount === validRows.length) {
      toast.success(`All ${successCount} products uploaded successfully!`);
    } else {
      toast(`${successCount}/${validRows.length} products uploaded`, { icon: '⚠️' });
    }
  };

  const validCount = parsedRows.filter((r) => !r.error).length;
  const errorCount = parsedRows.filter((r) => r.error).length;

  return (
    <Modal title="Bulk Upload Products" onClose={onClose} size="lg">
      <div className="p-6">

        {/* Step 1: Upload */}
        {step === 'upload' && (
          <div className="space-y-5">
            {/* Instructions */}
            <div className="bg-blue-50 border border-blue-100 rounded-xl p-4">
              <h3 className="text-sm font-bold text-blue-900 mb-2">How it works</h3>
              <ol className="text-xs text-blue-700 space-y-1.5 list-decimal list-inside">
                <li>Download the CSV template below</li>
                <li>Fill in your product data (one product per row)</li>
                <li>Upload the completed CSV file</li>
                <li>Review and confirm the upload</li>
              </ol>
            </div>

            {/* Download template */}
            <button onClick={downloadTemplate}
              className="flex items-center gap-2 px-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-semibold text-gray-700 hover:border-primary hover:text-primary transition-all w-full justify-center">
              <Download size={16} /> Download CSV Template
            </button>

            {/* Drop zone */}
            <div
              className={clsx(
                'border-2 border-dashed rounded-2xl p-10 text-center cursor-pointer transition-all',
                dragOver
                  ? 'border-primary bg-emerald-50 scale-[1.01]'
                  : 'border-gray-200 hover:border-primary hover:bg-gray-50'
              )}
              onClick={() => fileInputRef.current?.click()}
              onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
              onDragLeave={() => setDragOver(false)}
              onDrop={(e) => { e.preventDefault(); setDragOver(false); const f = e.dataTransfer.files[0]; if (f) handleFile(f); }}
            >
              <FileSpreadsheet size={40} className={clsx('mx-auto mb-3', dragOver ? 'text-primary' : 'text-gray-300')} />
              <p className="text-sm font-bold text-gray-700">
                {dragOver ? 'Drop your CSV here!' : 'Drag & drop your CSV file here'}
              </p>
              <p className="text-xs text-gray-400 mt-1">or click to browse · CSV format · max 5MB</p>
              <input
                ref={fileInputRef}
                type="file"
                accept=".csv,.txt"
                className="hidden"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f); e.target.value = ''; }}
              />
            </div>

            {/* Category reference */}
            <details className="text-xs">
              <summary className="cursor-pointer text-gray-500 font-semibold hover:text-gray-700">
                View Category IDs for the CSV
              </summary>
              <div className="mt-2 bg-gray-50 rounded-lg p-3 grid grid-cols-3 gap-1.5">
                {[
                  { id: 'c1', name: 'Cooking Oils' },
                  { id: 'c2', name: 'Grains & Cereals' },
                  { id: 'c3', name: 'Sugar & Sweeteners' },
                  { id: 'c4', name: 'Sauces & Condiments' },
                  { id: 'c5', name: 'Beverages' },
                  { id: 'c6', name: 'Dairy' },
                  { id: 'c7', name: 'Snacks' },
                  { id: 'c8', name: 'Personal Care' },
                  { id: 'c9', name: 'Cleaning Products' },
                ].map((c) => (
                  <div key={c.id} className="flex items-center gap-1.5">
                    <code className="bg-white px-1.5 py-0.5 rounded border border-gray-200 text-primary font-bold">{c.id}</code>
                    <span className="text-gray-600">{c.name}</span>
                  </div>
                ))}
              </div>
            </details>
          </div>
        )}

        {/* Step 2: Preview */}
        {step === 'preview' && (
          <div className="space-y-4">
            {/* Summary */}
            <div className="flex items-center gap-3">
              <div className="flex items-center gap-2 bg-emerald-50 text-emerald-700 px-3 py-1.5 rounded-lg">
                <CheckCircle2 size={14} />
                <span className="text-xs font-bold">{validCount} valid</span>
              </div>
              {errorCount > 0 && (
                <div className="flex items-center gap-2 bg-red-50 text-red-700 px-3 py-1.5 rounded-lg">
                  <AlertCircle size={14} />
                  <span className="text-xs font-bold">{errorCount} errors</span>
                </div>
              )}
              <span className="text-xs text-gray-400 ml-auto">{parsedRows.length} total rows</span>
            </div>

            {/* Table preview */}
            <div className="max-h-[320px] overflow-auto border border-gray-200 rounded-xl">
              <table className="w-full text-xs">
                <thead className="sticky top-0 bg-gray-50 z-10">
                  <tr>
                    <th className="px-3 py-2 text-left font-bold text-gray-500">#</th>
                    <th className="px-3 py-2 text-left font-bold text-gray-500">Name</th>
                    <th className="px-3 py-2 text-left font-bold text-gray-500">SKU</th>
                    <th className="px-3 py-2 text-right font-bold text-gray-500">Price</th>
                    <th className="px-3 py-2 text-right font-bold text-gray-500">Stock</th>
                    <th className="px-3 py-2 text-center font-bold text-gray-500">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {parsedRows.map((row, i) => (
                    <tr key={i} className={clsx('border-t border-gray-100', row.error && 'bg-red-50/50')}>
                      <td className="px-3 py-2 text-gray-400">{i + 1}</td>
                      <td className="px-3 py-2 font-medium text-gray-900 max-w-[180px] truncate">{row.name || '—'}</td>
                      <td className="px-3 py-2 font-mono text-gray-500">{row.sku || '—'}</td>
                      <td className="px-3 py-2 text-right font-semibold text-gray-900">
                        {row.price > 0 ? `R${row.price.toFixed(2)}` : '—'}
                      </td>
                      <td className="px-3 py-2 text-right text-gray-600">{row.stockQuantity}</td>
                      <td className="px-3 py-2 text-center">
                        {row.error ? (
                          <span className="text-red-600 font-semibold text-[10px]" title={row.error}>
                            ⚠ {row.error}
                          </span>
                        ) : (
                          <CheckCircle2 size={14} className="text-emerald-500 mx-auto" />
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Actions */}
            <div className="flex items-center justify-between pt-3 border-t border-gray-100">
              <button onClick={() => { setStep('upload'); setParsedRows([]); }} className="btn-secondary text-sm">
                ← Back
              </button>
              <div className="flex items-center gap-2">
                {errorCount > 0 && (
                  <p className="text-xs text-amber-600 font-medium">
                    {errorCount} row{errorCount > 1 ? 's' : ''} with errors will be skipped
                  </p>
                )}
                <button
                  onClick={handleBulkSubmit}
                  disabled={validCount === 0}
                  className="btn-primary flex items-center gap-2 text-sm"
                >
                  <Upload size={14} /> Upload {validCount} Product{validCount !== 1 ? 's' : ''}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Step 3: Progress */}
        {step === 'progress' && (
          <div className="py-10 text-center space-y-5">
            <Loader2 size={40} className="mx-auto text-primary animate-spin" />
            <div>
              <p className="text-lg font-bold text-gray-900">Uploading Products...</p>
              <p className="text-sm text-gray-500 mt-1">Please don't close this window</p>
            </div>
            {/* Progress bar */}
            <div className="max-w-sm mx-auto">
              <div className="flex items-center justify-between text-xs text-gray-500 mb-1.5">
                <span>Progress</span>
                <span className="font-bold">{progress}%</span>
              </div>
              <div className="w-full h-2.5 bg-gray-100 rounded-full overflow-hidden">
                <div
                  className="h-full bg-gradient-to-r from-primary to-emerald-400 rounded-full transition-all duration-300"
                  style={{ width: `${progress}%` }}
                />
              </div>
            </div>
          </div>
        )}

        {/* Step 4: Done */}
        {step === 'done' && (
          <div className="space-y-5">
            {/* Summary */}
            <div className="text-center py-4">
              <CheckCircle2 size={48} className="mx-auto text-emerald-500 mb-3" />
              <h3 className="text-lg font-bold text-gray-900">Upload Complete!</h3>
              <p className="text-sm text-gray-500 mt-1">
                {results.filter((r) => r.success).length} of {results.length} products uploaded successfully
              </p>
            </div>

            {/* Results table */}
            <div className="max-h-[250px] overflow-auto border border-gray-200 rounded-xl">
              <table className="w-full text-xs">
                <thead className="sticky top-0 bg-gray-50">
                  <tr>
                    <th className="px-3 py-2 text-left font-bold text-gray-500">#</th>
                    <th className="px-3 py-2 text-left font-bold text-gray-500">Product</th>
                    <th className="px-3 py-2 text-left font-bold text-gray-500">SKU</th>
                    <th className="px-3 py-2 text-center font-bold text-gray-500">Result</th>
                  </tr>
                </thead>
                <tbody>
                  {results.map((r, i) => (
                    <tr key={i} className={clsx('border-t border-gray-100', !r.success && 'bg-red-50/50')}>
                      <td className="px-3 py-2 text-gray-400">{r.row}</td>
                      <td className="px-3 py-2 font-medium text-gray-900">{r.name}</td>
                      <td className="px-3 py-2 font-mono text-gray-500">{r.sku}</td>
                      <td className="px-3 py-2 text-center">
                        {r.success ? (
                          <span className="inline-flex items-center gap-1 text-emerald-600 font-semibold">
                            <CheckCircle2 size={12} /> Success
                          </span>
                        ) : (
                          <span className="text-red-600 font-semibold" title={r.error}>
                            ⚠ {r.error}
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Actions */}
            <div className="flex justify-end gap-3 pt-3 border-t border-gray-100">
              <button onClick={onClose} className="btn-secondary">Close</button>
              <button
                onClick={() => { onComplete(); onClose(); }}
                className="btn-primary"
              >
                Done — Refresh Products
              </button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
