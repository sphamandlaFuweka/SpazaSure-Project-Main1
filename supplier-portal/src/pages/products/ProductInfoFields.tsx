import { Plus, ShieldCheck, Trash2 } from 'lucide-react';
import type { ProductInfo } from '../../types';

type Props = { info: ProductInfo; onChange: (next: ProductInfo) => void };

/** The allergy options customers pick in the app, so matching warnings line up exactly. */
const ALLERGENS = ['Peanuts', 'Tree nuts', 'Dairy', 'Eggs', 'Gluten/Wheat', 'Soy', 'Fish', 'Shellfish', 'Sesame'];

/** Rows of a typical South African nutritional information table. */
const STANDARD_NUTRIENTS = [
  'Energy (kJ)', 'Protein (g)', 'Glycaemic carbohydrate (g)', 'of which total sugar (g)',
  'Total fat (g)', 'of which saturated fat (g)', 'Dietary fibre (g)', 'Total sodium (mg)',
];

const SPEC_SUGGESTIONS = ['Weight', 'Dimensions', 'Shelf life', 'Units per case', 'Material', 'Colour', 'Volume', 'Grade'];

/** Starting points only: the supplier should rewrite them to describe their own genuine pack. */
const TIP_SUGGESTIONS = [
  'The seal or cap is intact and has not been re-glued or re-taped.',
  'Batch number and expiry date are printed on the pack itself, not on a separate sticker.',
  'Logo, colours and lettering are sharp and match the product photos exactly.',
  'Spelling on the pack is correct, including the manufacturer name and address.',
  'Every pack carries our SpazaSure code. Scan it in the app to confirm it is genuine.',
  'A price far below the usual price is a warning sign.',
];

const MAX_TIPS = 12;

const Field = ({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) => (
  <div>
    <label className="label">{label}</label>
    {children}
    {hint && <p className="text-xs text-gray-400 mt-1">{hint}</p>}
  </div>
);

const RemoveButton = ({ onClick, label }: { onClick: () => void; label: string }) => (
  <button type="button" onClick={onClick} aria-label={label} title={label}
    className="w-9 h-9 flex-shrink-0 rounded-lg border border-gray-200 text-gray-400 hover:text-red-600 hover:border-red-200 flex items-center justify-center">
    <Trash2 size={14} />
  </button>
);

export function DetailsSection({ info, onChange }: Props) {
  const set = (patch: Partial<ProductInfo>) => onChange({ ...info, ...patch });
  const setSpec = (i: number, patch: Partial<ProductInfo['specifications'][number]>) =>
    set({ specifications: info.specifications.map((s, idx) => (idx === i ? { ...s, ...patch } : s)) });
  const unusedSpecs = SPEC_SUGGESTIONS.filter((label) => !info.specifications.some((s) => s.label === label));

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Field label="Brand"><input className="input" value={info.brand} maxLength={120} onChange={(e) => set({ brand: e.target.value })} placeholder="White Star" /></Field>
        <Field label="Pack size" hint="As printed on the pack"><input className="input" value={info.packSize} maxLength={60} onChange={(e) => set({ packSize: e.target.value })} placeholder="10 kg or 6 x 500 ml" /></Field>
        <Field label="Manufacturer or importer"><input className="input" value={info.manufacturer} maxLength={160} onChange={(e) => set({ manufacturer: e.target.value })} placeholder="Name and town on the pack" /></Field>
        <Field label="Country of origin"><input className="input" value={info.countryOfOrigin} maxLength={80} onChange={(e) => set({ countryOfOrigin: e.target.value })} placeholder="South Africa" /></Field>
      </div>

      <div>
        <div className="flex items-center justify-between mb-2">
          <p className="label mb-0">Specifications</p>
          <button type="button" className="text-xs font-bold text-primary flex items-center gap-1"
            onClick={() => set({ specifications: [...info.specifications, { label: '', value: '' }] })}
            disabled={info.specifications.length >= 30}>
            <Plus size={12} /> Add row
          </button>
        </div>
        {info.specifications.length === 0 && (
          <p className="text-xs text-gray-400 mb-2">Add details shops ask about, such as weight, shelf life or units per case.</p>
        )}
        <div className="space-y-2">
          {info.specifications.map((spec, i) => (
            <div key={i} className="flex gap-2">
              <input className="input w-2/5" value={spec.label} maxLength={60} placeholder="Name, e.g. Shelf life"
                onChange={(e) => setSpec(i, { label: e.target.value })} aria-label={`Specification ${i + 1} name`} />
              <input className="input flex-1" value={spec.value} maxLength={200} placeholder="Value, e.g. 12 months"
                onChange={(e) => setSpec(i, { value: e.target.value })} aria-label={`Specification ${i + 1} value`} />
              <RemoveButton label="Remove specification" onClick={() => set({ specifications: info.specifications.filter((_, idx) => idx !== i) })} />
            </div>
          ))}
        </div>
        {unusedSpecs.length > 0 && info.specifications.length < 30 && (
          <div className="flex flex-wrap gap-1.5 mt-2">
            {unusedSpecs.map((label) => (
              <button key={label} type="button"
                className="text-[11px] font-semibold px-2.5 py-1 rounded-full bg-gray-100 text-gray-600 hover:bg-primary-50 hover:text-primary"
                onClick={() => set({ specifications: [...info.specifications, { label, value: '' }] })}>
                + {label}
              </button>
            ))}
          </div>
        )}
      </div>

      <Field label="How to use / serving suggestions">
        <textarea className="input resize-none" rows={3} maxLength={2000} value={info.usageInstructions}
          onChange={(e) => set({ usageInstructions: e.target.value })} placeholder="Preparation steps, dosage or serving ideas" />
      </Field>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Field label="Storage instructions">
          <textarea className="input resize-none" rows={2} maxLength={1000} value={info.storageInstructions}
            onChange={(e) => set({ storageInstructions: e.target.value })} placeholder="Store in a cool, dry place" />
        </Field>
        <Field label="Warnings">
          <textarea className="input resize-none" rows={2} maxLength={1000} value={info.warnings}
            onChange={(e) => set({ warnings: e.target.value })} placeholder="Keep out of reach of children" />
        </Field>
      </div>
    </div>
  );
}

export function NutritionSection({ info, onChange, isFoodItem }: Props & { isFoodItem: boolean }) {
  const set = (patch: Partial<ProductInfo>) => onChange({ ...info, ...patch });
  const setRow = (i: number, patch: Partial<ProductInfo['nutrition'][number]>) =>
    set({ nutrition: info.nutrition.map((n, idx) => (idx === i ? { ...n, ...patch } : n)) });
  const toggleAllergen = (name: string) =>
    set({ allergens: info.allergens.includes(name) ? info.allergens.filter((a) => a !== name) : [...info.allergens, name] });
  const addStandardRows = () => set({
    nutrition: [
      ...info.nutrition,
      ...STANDARD_NUTRIENTS.filter((n) => !info.nutrition.some((r) => r.nutrient === n)).map((nutrient) => ({ nutrient, per100: '', perServing: '' })),
    ],
  });

  return (
    <div className="space-y-5">
      {!isFoodItem && (
        <div className="info-box">
          This product isn't marked as food or drink. Ingredients still help, but customers only get allergy warnings on food items. Tick the box on the Basics tab if it is food.
        </div>
      )}

      <Field label="Ingredients" hint="List them in the same order as the pack, including additives.">
        <textarea className="input resize-none" rows={4} maxLength={4000} value={info.ingredients}
          onChange={(e) => set({ ingredients: e.target.value })} placeholder="Maize, vitamin A, thiamine, riboflavin..." />
      </Field>

      <div>
        <p className="label">Contains allergens</p>
        <div className="flex flex-wrap gap-2">
          {ALLERGENS.map((name) => {
            const on = info.allergens.includes(name);
            return (
              <button key={name} type="button" onClick={() => toggleAllergen(name)} aria-pressed={on}
                className={`text-xs font-bold px-3 py-1.5 rounded-full border transition-colors ${on ? 'bg-red-50 border-red-200 text-red-700' : 'bg-white border-gray-200 text-gray-600 hover:border-gray-300'}`}>
                {name}
              </button>
            );
          })}
        </div>
        <p className="text-xs text-gray-400 mt-1">Customers with these allergies get a warning when they scan the product.</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Field label="Serving size"><input className="input" value={info.servingSize} maxLength={60} onChange={(e) => set({ servingSize: e.target.value })} placeholder="100 g" /></Field>
        <Field label="Servings per pack"><input className="input" value={info.servingsPerPack} maxLength={60} onChange={(e) => set({ servingsPerPack: e.target.value })} placeholder="100" /></Field>
      </div>

      <div>
        <div className="flex items-center justify-between mb-2">
          <p className="label mb-0">Nutritional information</p>
          <div className="flex gap-3">
            <button type="button" className="text-xs font-bold text-primary" onClick={addStandardRows}>Add standard rows</button>
            <button type="button" className="text-xs font-bold text-primary flex items-center gap-1"
              onClick={() => set({ nutrition: [...info.nutrition, { nutrient: '', per100: '', perServing: '' }] })}
              disabled={info.nutrition.length >= 30}>
              <Plus size={12} /> Add row
            </button>
          </div>
        </div>
        {info.nutrition.length === 0 ? (
          <p className="text-xs text-gray-400">Copy the table from the pack. "Add standard rows" fills in the usual nutrients.</p>
        ) : (
          <div className="space-y-2">
            <div className="grid grid-cols-[1fr_7rem_7rem_2.25rem] gap-2 text-[10px] font-bold uppercase tracking-wide text-gray-400">
              <span>Nutrient</span><span>Per 100 g/ml</span><span>Per serving</span><span />
            </div>
            {info.nutrition.map((row, i) => (
              <div key={i} className="grid grid-cols-[1fr_7rem_7rem_2.25rem] gap-2">
                <input className="input" value={row.nutrient} maxLength={60} onChange={(e) => setRow(i, { nutrient: e.target.value })} aria-label={`Nutrient ${i + 1}`} />
                <input className="input" value={row.per100} maxLength={30} onChange={(e) => setRow(i, { per100: e.target.value })} aria-label={`${row.nutrient || 'Nutrient'} per 100`} />
                <input className="input" value={row.perServing} maxLength={30} onChange={(e) => setRow(i, { perServing: e.target.value })} aria-label={`${row.nutrient || 'Nutrient'} per serving`} />
                <RemoveButton label="Remove nutrient" onClick={() => set({ nutrition: info.nutrition.filter((_, idx) => idx !== i) })} />
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export function AuthenticitySection({ info, onChange }: Props) {
  const tips = info.authenticityTips;
  const setTips = (next: string[]) => onChange({ ...info, authenticityTips: next });
  const unused = TIP_SUGGESTIONS.filter((t) => !tips.includes(t));

  return (
    <div className="space-y-4">
      <div className="flex items-start gap-3 rounded-xl border border-emerald-100 bg-emerald-50/60 p-4">
        <ShieldCheck size={18} className="text-emerald-600 flex-shrink-0 mt-0.5" />
        <p className="text-sm text-emerald-900">
          Tell shops and customers how to recognise your genuine product. These checks appear when someone scans the product,
          and next to it in the shop app. Be specific: where the batch number sits, what the seal looks like, any hologram or
          embossing, and how the genuine pack differs from fakes you have seen. Your product photos are shown as the reference.
        </p>
      </div>

      <div className="space-y-2">
        {tips.map((tip, i) => (
          <div key={i} className="flex gap-2">
            <span className="w-7 h-9 flex items-center justify-center text-xs font-black text-gray-400 flex-shrink-0">{i + 1}.</span>
            <textarea className="input resize-none flex-1" rows={2} maxLength={300} value={tip}
              onChange={(e) => setTips(tips.map((t, idx) => (idx === i ? e.target.value : t)))}
              aria-label={`Authenticity check ${i + 1}`} />
            <RemoveButton label="Remove check" onClick={() => setTips(tips.filter((_, idx) => idx !== i))} />
          </div>
        ))}
        {tips.length === 0 && <p className="text-xs text-gray-400">No checks yet. Add your own, or start from a suggestion below.</p>}
      </div>

      <button type="button" className="text-xs font-bold text-primary flex items-center gap-1"
        onClick={() => setTips([...tips, ''])} disabled={tips.length >= MAX_TIPS}>
        <Plus size={12} /> Add a check
      </button>

      {unused.length > 0 && tips.length < MAX_TIPS && (
        <div>
          <p className="text-[11px] font-bold uppercase tracking-wide text-gray-400 mb-1.5">Suggestions to edit</p>
          <div className="flex flex-col gap-1.5">
            {unused.map((tip) => (
              <button key={tip} type="button" onClick={() => setTips([...tips, tip])}
                className="text-left text-xs px-3 py-2 rounded-lg bg-gray-50 border border-gray-100 text-gray-600 hover:border-primary hover:text-primary">
                + {tip}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
