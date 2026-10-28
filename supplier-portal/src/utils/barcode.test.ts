import { describe, expect, it } from 'vitest';
import { barcodeProblem } from './barcode';

describe('barcodeProblem', () => {
  it('accepts barcodes printed on packs', () => {
    for (const code of ['6009523606526', '6 009523 606526', '96385074', '036000291452', '10012345678902'])
      expect(barcodeProblem(code)).toBeNull();
  });

  it('treats an empty field as no barcode', () => {
    expect(barcodeProblem('')).toBeNull();
    expect(barcodeProblem('   ')).toBeNull();
  });

  it('catches typos', () => {
    expect(barcodeProblem('6009523606527')).toMatch(/last digit/);
    expect(barcodeProblem('600952360652')).toMatch(/12 digits|last digit/);
    expect(barcodeProblem('60095236065')).toMatch(/11 digits/);
    expect(barcodeProblem('6009523606O26')).toMatch(/only the numbers/);
  });

  it("refuses SpazaSure's own generated codes", () => {
    // 290 + 9 digits + valid check digit
    expect(barcodeProblem('2900000000001')).toMatch(/made by SpazaSure/);
  });
});
