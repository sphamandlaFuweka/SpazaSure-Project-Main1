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
