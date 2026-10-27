import { afterEach, describe, expect, it, vi } from 'vitest';
import { reverseAddress, searchAddresses } from './address';

const place = (road: string) => ({
  lat: '-26.2', lon: '27.9',
  address: { house_number: '12', road, suburb: 'Orlando East', city: 'Soweto', state: 'Gauteng', postcode: '1804' },
});

const respond = (body: unknown, ok = true) =>
  Promise.resolve({ ok, json: () => Promise.resolve(body) } as Response);

afterEach(() => vi.unstubAllGlobals());

describe('searchAddresses', () => {
  it('does not search for fewer than three characters', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    expect(await searchAddresses('ab')).toEqual([]);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('builds a readable label from the address parts', async () => {
    vi.stubGlobal('fetch', vi.fn(() => respond([place('Mooki Street')])));
    const [result] = await searchAddresses('12 Mooki Street');
    expect(result.label).toBe('12 Mooki Street, Orlando East, Soweto, Gauteng, 1804');
    expect(result.latitude).toBe(-26.2);
  });

  it('retries without the house number when nothing matches', async () => {
    const fetchMock = vi.fn()
      .mockReturnValueOnce(respond([]))
      .mockReturnValueOnce(respond([place('Mooki Street')]));
    vi.stubGlobal('fetch', fetchMock);

    const results = await searchAddresses('No. 4521 Mooki Street');

    expect(results).toHaveLength(1);
    expect(String(fetchMock.mock.calls[1][0])).toContain('q=Mooki+Street');
  });

  it('only searches South Africa', async () => {
    const fetchMock = vi.fn((_url: string) => respond([]));
    vi.stubGlobal('fetch', fetchMock);
    await searchAddresses('Mooki Street');
    expect(String(fetchMock.mock.calls[0][0])).toContain('countrycodes=za');
  });
});

describe('reverseAddress', () => {
  it('returns null when the lookup fails or finds nothing', async () => {
    vi.stubGlobal('fetch', vi.fn(() => respond({}, false)));
    expect(await reverseAddress(-26, 28)).toBeNull();
    vi.stubGlobal('fetch', vi.fn(() => respond({ error: 'Unable to geocode' })));
    expect(await reverseAddress(-26, 28)).toBeNull();
  });
});
