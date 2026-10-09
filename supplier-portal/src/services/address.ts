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

const pick = (a: Record<string, string | undefined>, keys: string[]) => {
  for (const k of keys) {
    const v = a[k]?.trim();
    if (v) return v;
  }
  return '';
};

/** Autocomplete for South African addresses via OpenStreetMap Nominatim. */
export async function searchAddresses(query: string, signal?: AbortSignal): Promise<AddressSuggestion[]> {
  const q = query.trim();
  if (q.length < 3) return [];
  const params = new URLSearchParams({
    q, format: 'jsonv2', addressdetails: '1', limit: '6', countrycodes: 'za',
  });
  const res = await fetch(`https://nominatim.openstreetmap.org/search?${params}`, { signal });
  if (!res.ok) throw new Error('Address search failed');
  const data = (await res.json()) as Array<{ lat: string; lon: string; display_name?: string; address?: Record<string, string> }>;
  return data
    .map((item) => {
      const a = item.address ?? {};
      const street = [pick(a, ['house_number']), pick(a, ['road', 'pedestrian', 'residential'])].filter(Boolean).join(' ');
      const suburb = pick(a, ['suburb', 'neighbourhood', 'quarter', 'township']);
      const city = pick(a, ['city', 'town', 'village', 'municipality', 'county']);
      const province = pick(a, ['state']);
      const postalCode = pick(a, ['postcode']);
      const label = [street, suburb, city, province, postalCode].filter(Boolean).join(', ') || item.display_name || '';
      return { label, street, suburb, city, province, postalCode, latitude: Number(item.lat), longitude: Number(item.lon) };
    })
    .filter((s) => s.label && Number.isFinite(s.latitude) && Number.isFinite(s.longitude));
}
