import { ShieldCheck } from 'lucide-react';
import type { ProductInfo } from '../../types';

/** Read-only product information, as shops and customers see it. Renders nothing when empty. */
export default function ProductInfoView({ info }: { info: ProductInfo }) {
  const facts = [
    ['Brand', info.brand], ['Pack size', info.packSize],
    ['Manufacturer', info.manufacturer], ['Country of origin', info.countryOfOrigin],
    ...info.specifications.map((s) => [s.label, s.value]),
  ].filter(([, v]) => v);
  const texts = [
    ['How to use', info.usageInstructions], ['Storage', info.storageInstructions], ['Warnings', info.warnings],
  ].filter(([, v]) => v);
  const empty = facts.length === 0 && texts.length === 0 && !info.ingredients && info.allergens.length === 0
    && info.nutrition.length === 0 && info.authenticityTips.length === 0;

  if (empty) {
    return <p className="text-sm text-gray-400 bg-gray-50 rounded-xl p-4 border border-gray-100">The supplier hasn't added product information yet.</p>;
  }

  return (
    <div className="space-y-4">
      {facts.length > 0 && (
        <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-2 text-sm">
          {facts.map(([label, value], i) => (
            <div key={i} className="flex justify-between gap-3 border-b border-gray-100 py-1.5">
              <dt className="text-gray-500">{label}</dt><dd className="font-semibold text-gray-900 text-right">{value}</dd>
            </div>
          ))}
        </dl>
      )}

      {(info.ingredients || info.allergens.length > 0) && (
        <div>
          <p className="text-xs font-bold uppercase tracking-wide text-gray-400 mb-1">Ingredients</p>
          {info.ingredients && <p className="text-sm text-gray-700">{info.ingredients}</p>}
          {info.allergens.length > 0 && (
            <p className="text-sm text-red-700 font-semibold mt-1">Contains: {info.allergens.join(', ')}</p>
          )}
        </div>
      )}

      {info.nutrition.length > 0 && (
        <div>
          <p className="text-xs font-bold uppercase tracking-wide text-gray-400 mb-1">
            Nutritional information{info.servingSize ? ` (serving ${info.servingSize}${info.servingsPerPack ? `, ${info.servingsPerPack} per pack` : ''})` : ''}
          </p>
          <table className="w-full text-sm">
            <thead><tr className="text-left text-xs text-gray-500"><th className="py-1 font-semibold">Nutrient</th><th className="py-1 font-semibold">Per 100</th><th className="py-1 font-semibold">Per serving</th></tr></thead>
            <tbody>
              {info.nutrition.map((n, i) => (
                <tr key={i} className="border-t border-gray-100"><td className="py-1">{n.nutrient}</td><td className="py-1">{n.per100 || '-'}</td><td className="py-1">{n.perServing || '-'}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {texts.map(([label, value]) => (
        <div key={label}>
          <p className="text-xs font-bold uppercase tracking-wide text-gray-400 mb-1">{label}</p>
          <p className="text-sm text-gray-700 whitespace-pre-line">{value}</p>
        </div>
      ))}

      {info.authenticityTips.length > 0 && (
        <div className="rounded-xl border border-emerald-100 bg-emerald-50/60 p-4">
          <p className="text-sm font-bold text-emerald-900 flex items-center gap-2"><ShieldCheck size={15} /> How to spot a fake</p>
          <ol className="mt-2 space-y-1 text-sm text-emerald-900 list-decimal pl-5">
            {info.authenticityTips.map((t, i) => <li key={i}>{t}</li>)}
          </ol>
        </div>
      )}
    </div>
  );
}
