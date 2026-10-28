/** Digits of a typed barcode, without the spaces or dashes people copy from the pack. */
export const barcodeDigits = (value: string) => value.replace(/[\s-]/g, '');

/**
 * Why a barcode typed off a pack can't be right, or null when it is a valid EAN-8, UPC-A,
 * EAN-13 or GTIN-14 (or empty: the pack has none and SpazaSure makes one).
 */
export function barcodeProblem(value: string): string | null {
  const digits = barcodeDigits(value);
  if (!digits) return null;
  if (!/^\d+$/.test(digits)) return 'Use only the numbers printed under the bars';
  if (![8, 12, 13, 14].includes(digits.length))
    return `${digits.length} digits: product barcodes have 8, 12, 13 or 14`;
  let sum = 0;
  for (let i = 0; i < digits.length - 1; i++)
    sum += Number(digits[digits.length - 2 - i]) * (i % 2 === 0 ? 3 : 1);
  if ((10 - (sum % 10)) % 10 !== Number(digits[digits.length - 1]))
    return "The last digit doesn't match the rest. Check for a typo against the pack";
  // SpazaSure's own codes (GS1 in-store range) are made for packs without a barcode
  if (digits.startsWith('290')) return 'Codes starting with 290 are made by SpazaSure. Type the one on the pack';
  return null;
}
