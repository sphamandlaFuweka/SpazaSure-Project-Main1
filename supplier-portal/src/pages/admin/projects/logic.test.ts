import { describe, expect, it } from 'vitest';
import { initials, isoDate, nextOccurrence, riskScore, whatsappLink } from './logic';
import type { Risk } from './types';

describe('isoDate', () => {
  it('formats a local date as yyyy-mm-dd', () => {
    expect(isoDate(new Date(2026, 0, 5))).toBe('2026-01-05');
  });
});

describe('whatsappLink', () => {
  it('turns a local SA number into an international wa.me link', () => {
    expect(whatsappLink('082 123 4567', 'Hi there')).toBe('https://wa.me/27821234567?text=Hi%20there');
  });

  it('returns nothing when there is no number', () => {
    expect(whatsappLink('', 'Hi')).toBe('');
  });
});

describe('initials', () => {
  it('uses the first letters of up to two words', () => {
    expect(initials('thandi zulu mokoena')).toBe('TZ');
  });
});

describe('riskScore', () => {
  it('multiplies likelihood by impact', () => {
    expect(riskScore({ likelihood: 4, impact: 5 } as unknown as Risk)).toBe(20);
  });
});

describe('nextOccurrence', () => {
  it('finds the next weekly slot after the given time', () => {
    const monday9 = new Date(2026, 9, 26, 9, 0); // Monday
    const next = nextOccurrence({ time: '10:30', dow: 3 }, monday9); // Wednesday
    expect(next.getDay()).toBe(3);
    expect(isoDate(next)).toBe('2026-10-28');
    expect(next.getHours()).toBe(10);
    expect(next.getMinutes()).toBe(30);
  });

  it('moves to next week when today\'s slot has already passed', () => {
    const monday11 = new Date(2026, 9, 26, 11, 0);
    expect(isoDate(nextOccurrence({ time: '10:00', dow: 1 }, monday11))).toBe('2026-11-02');
  });
});
