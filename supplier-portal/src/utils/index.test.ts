import { describe, expect, it } from 'vitest';
import { formatCurrency, generateSKU, shortOrderNumber, truncate } from './index';

describe('formatCurrency', () => {
  it('shows rands with two decimals and grouped thousands', () => {
    expect(formatCurrency(1234.5)).toMatch(/^R1\s?234[,.]50$/);
    expect(formatCurrency(0)).toMatch(/^R0[,.]00$/);
  });
});

describe('truncate', () => {
  it('leaves short text alone and cuts long text with an ellipsis', () => {
    expect(truncate('Maize meal', 40)).toBe('Maize meal');
    expect(truncate('abcdefghij', 4)).toBe('abcd…');
  });
});

describe('generateSKU', () => {
  it('uppercases, replaces symbols with single dashes and caps the length', () => {
    expect(generateSKU('Coke 2L (bottle)')).toBe('COKE-2L-BOTT');
    expect(generateSKU('a  &  b')).toBe('A-B');
  });
});

describe('shortOrderNumber', () => {
  it('keeps normal order numbers whole', () => {
    expect(shortOrderNumber('SPZ-000123')).toBe('SPZ-000123');
  });

  it('shortens long group-buy numbers but keeps both ends', () => {
    expect(shortOrderNumber('SPZ-GB-0623699635954B6AB1C4C4CAA3DF045C')).toBe('SPZ-GB-0623…F045C');
  });
});
