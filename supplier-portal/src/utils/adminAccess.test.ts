import { describe, expect, it } from 'vitest';
import {
  ADMIN_AREAS, ROLE_PRESETS, areaForAdminPath, canOpenAdminPath, fullAccessMap, levelFor, type AdminProfile,
} from './adminAccess';

const staff = (access: Partial<AdminProfile['access']>, isMaster = false): AdminProfile => ({
  id: '1', email: 'staff@spazasure.co.za', displayName: 'Staff', title: '', isMaster,
  access: fullAccessMap(access),
});

describe('areaForAdminPath', () => {
  it('maps admin pages to the area that guards them', () => {
    expect(areaForAdminPath('/admin/payouts')).toBe('finance');
    expect(areaForAdminPath('/admin/documents/123')).toBe('trust');
    expect(areaForAdminPath('/admin/orders')).toBe('marketplace');
  });

  it('treats the dashboard root as open to any staff member', () => {
    expect(areaForAdminPath('/admin')).toBe('any');
    expect(areaForAdminPath('/admin/dashboard')).toBe('any');
  });

  it('keeps unknown pages master-only so new pages are never exposed by accident', () => {
    expect(areaForAdminPath('/admin/something-new')).toBe('master');
  });
});

describe('canOpenAdminPath', () => {
  it('lets the master admin open everything', () => {
    const master = staff({}, true);
    expect(canOpenAdminPath(master, '/admin/team')).toBe(true);
    expect(canOpenAdminPath(master, '/admin/payouts')).toBe(true);
  });

  it('blocks areas a staff member has no access to', () => {
    const compliance = staff({ trust: 'manage' });
    expect(canOpenAdminPath(compliance, '/admin/documents')).toBe(true);
    expect(canOpenAdminPath(compliance, '/admin/payouts')).toBe(false);
  });

  it('allows view-only access to open a page', () => {
    expect(canOpenAdminPath(staff({ finance: 'view' }), '/admin/wallet-ledger')).toBe(true);
  });

  it('keeps master-only pages away from other staff', () => {
    const ops = staff({ trust: 'manage', marketplace: 'manage', finance: 'manage', projects: 'manage', settings: 'manage' });
    expect(canOpenAdminPath(ops, '/admin/team')).toBe(false);
    expect(canOpenAdminPath(ops, '/admin/deletion-requests')).toBe(false);
  });
});

describe('levelFor', () => {
  it('hides nothing until the profile has loaded (the server still enforces access)', () => {
    expect(levelFor(null, 'finance')).toBe('manage');
  });

  it('defaults a missing area to none', () => {
    expect(levelFor(staff({ trust: 'view' }), 'settings')).toBe('none');
  });
});

describe('ROLE_PRESETS', () => {
  it('only uses known areas', () => {
    const known = new Set(ADMIN_AREAS.map((a) => a.key));
    for (const preset of ROLE_PRESETS) {
      for (const area of Object.keys(preset.access)) expect(known.has(area as never)).toBe(true);
    }
  });

  it('fills every area when expanded to a full access map', () => {
    const map = fullAccessMap(ROLE_PRESETS.find((p) => p.key === 'finance')!.access);
    expect(Object.keys(map).sort()).toEqual(ADMIN_AREAS.map((a) => a.key).sort());
    expect(map.finance).toBe('manage');
    expect(map.trust).toBe('none');
  });
});
