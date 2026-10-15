export interface AddressSuggestion {
  label: string;
  street: string;
  suburb: string;
  city: string;
  province: string;
  postalCode: string;
  latitude: number;
  longitude: number;
}

interface NominatimPlace { lat: string; lon: string; display_name?: string; address?: Record<string, string> }

const pick = (a: Record<string, string | undefined>, keys: string[]) => {
  for (const k of keys) {
    const v = a[k]?.trim();
    if (v) return v;
  }
  return '';
};

function toSuggestion(item: NominatimPlace): AddressSuggestion {
  const a = item.address ?? {};
  const street = [pick(a, ['house_number']), pick(a, ['road', 'pedestrian', 'residential'])].filter(Boolean).join(' ');
  const suburb = pick(a, ['suburb', 'neighbourhood', 'quarter', 'township']);
  const city = pick(a, ['city', 'town', 'village', 'municipality', 'county']);
  const province = pick(a, ['state']);
  const postalCode = pick(a, ['postcode']);
  const label = [street, suburb, city, province, postalCode].filter(Boolean).join(', ') || item.display_name || '';
  return { label, street, suburb, city, province, postalCode, latitude: Number(item.lat), longitude: Number(item.lon) };
}

async function search(q: string, signal?: AbortSignal): Promise<AddressSuggestion[]> {
  const params = new URLSearchParams({
    q, format: 'jsonv2', addressdetails: '1', limit: '6', countrycodes: 'za',
  });
  const res = await fetch(`https://nominatim.openstreetmap.org/search?${params}`, { signal });
  if (!res.ok) throw new Error('Address search failed');
  const data = (await res.json()) as NominatimPlace[];
  return data
    .map(toSuggestion)
    .filter((s) => s.label && Number.isFinite(s.latitude) && Number.isFinite(s.longitude));
}

/**
 * Autocomplete for South African addresses via OpenStreetMap Nominatim. OpenStreetMap often
 * lacks township house numbers, so when nothing matches it retries without the leading
 * stand/house number to at least find the street.
 */
export async function searchAddresses(query: string, signal?: AbortSignal): Promise<AddressSuggestion[]> {
  const q = query.trim();
  if (q.length < 3) return [];
  const results = await search(q, signal);
  if (results.length > 0) return results;
  const withoutNumber = q.replace(/^\s*(no\.?\s*)?\d+[a-z]?[\s,]+/i, '');
  return withoutNumber !== q && withoutNumber.length >= 3 ? search(withoutNumber, signal) : results;
}

/** The address at a map point, or null if nothing is known there. */
export async function reverseAddress(lat: number, lng: number): Promise<AddressSuggestion | null> {
  const params = new URLSearchParams({ lat: String(lat), lon: String(lng), format: 'jsonv2', addressdetails: '1' });
  const res = await fetch(`https://nominatim.openstreetmap.org/reverse?${params}`);
  if (!res.ok) return null;
  const data = (await res.json()) as NominatimPlace & { error?: string };
  if (data.error || !data.lat) return null;
  const s = toSuggestion(data);
  return s.label ? s : null;
}
