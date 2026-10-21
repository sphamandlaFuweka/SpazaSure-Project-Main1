export const formatCurrency = (amount: number, currency = 'R') =>
  `${currency}${amount.toLocaleString('en-ZA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

export const formatDate = (date: string | Date, short = false) => {
  const d = typeof date === 'string' ? new Date(date) : date;
  return d.toLocaleDateString('en-ZA', short
    ? { day: '2-digit', month: 'short', year: 'numeric' }
    : { day: '2-digit', month: 'long', year: 'numeric' });
};

export const truncate = (str: string, max = 40) =>
  str.length > max ? `${str.slice(0, max)}…` : str;

export const generateSKU = (name: string) =>
  (name ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '-').replace(/-+/g, '-').slice(0, 12);

/**
 * Long order numbers (group-buy ones carry a 32-character code) shortened for tight spaces,
 * e.g. "SPZ-GB-0623699635954B6AB1C4C4CAA3DF045C" → "SPZ-GB-0623…F045C". Show the full one on hover.
 */
export const shortOrderNumber = (orderNumber: string, max = 18) =>
  orderNumber.length <= max ? orderNumber : `${orderNumber.slice(0, 11)}…${orderNumber.slice(-5)}`;
