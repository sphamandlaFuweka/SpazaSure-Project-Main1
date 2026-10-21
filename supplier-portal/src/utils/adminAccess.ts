/**
 * Dashboard staff roles. The master admin decides, per area, whether each staff member can view
 * or manage it. The server enforces this on every admin request; the console uses the same map to
 * hide what someone can't open and to say when a page is view only.
 * Keep the areas in sync with SpazaSure.Shared.Helpers.AdminAccess on the backend.
 */
export type AdminArea = 'trust' | 'marketplace' | 'finance' | 'projects' | 'settings';
export type AccessLevel = 'none' | 'view' | 'manage';

export interface AdminProfile {
  id: string;
  email: string;
  displayName: string;
  title: string;
  isMaster: boolean;
  access: Record<AdminArea, AccessLevel>;
}

export const ADMIN_AREAS: { key: AdminArea; label: string; description: string }[] = [
  { key: 'trust', label: 'Trust & safety', description: 'Suppliers and shops, documents, customer reports, shops map' },
  { key: 'marketplace', label: 'Marketplace', description: 'Products, orders, group buys and analytics' },
  { key: 'finance', label: 'Finance', description: 'Supplier payouts, wallet deposits and withdrawals, ledger, finance reports' },
  { key: 'projects', label: 'Projects & impact', description: 'Project management, MEL and reporting workspace' },
  { key: 'settings', label: 'Platform settings', description: 'Platform rules, fees, maintenance mode and FAQs' },
];

/** Which area guards each admin page (the segment after /admin/). */
const PAGE_AREAS: Record<string, AdminArea | 'any' | 'master'> = {
  dashboard: 'any',
  notifications: 'any',
  suppliers: 'trust',
  documents: 'trust',
  reports: 'trust',
  'shops-map': 'trust',
  products: 'marketplace',
  orders: 'marketplace',
  deliveries: 'marketplace',
  'group-buys': 'marketplace',
  analytics: 'marketplace',
  payouts: 'finance',
  deposits: 'finance',
  withdrawals: 'finance',
  'wallet-ledger': 'finance',
  finance: 'finance',
  projects: 'projects',
  settings: 'settings',
  team: 'master',
  'deletion-requests': 'master',
};

export const areaForAdminPath = (path: string): AdminArea | 'any' | 'master' => {
  const segment = path.split('/').filter(Boolean)[1] ?? 'dashboard';
  return PAGE_AREAS[segment] ?? 'master';
};

/** Until the profile has loaded, nothing is hidden; the server still refuses what isn't allowed. */
export const levelFor = (profile: AdminProfile | null | undefined, area: AdminArea): AccessLevel =>
  !profile || profile.isMaster ? 'manage' : profile.access?.[area] ?? 'none';

export const canOpenAdminPath = (profile: AdminProfile | null | undefined, path: string): boolean => {
  const area = areaForAdminPath(path);
  if (area === 'any') return true;
  if (area === 'master') return !profile || profile.isMaster;
  return levelFor(profile, area) !== 'none';
};

export const areaLabel = (area: AdminArea) => ADMIN_AREAS.find((a) => a.key === area)?.label ?? area;

/** Starting points for common jobs. The master admin can still adjust any area afterwards. */
export const ROLE_PRESETS: { key: string; label: string; isMaster: boolean; access: Partial<Record<AdminArea, AccessLevel>> }[] = [
  { key: 'master', label: 'Master admin', isMaster: true, access: {} },
  { key: 'operations', label: 'Operations manager', isMaster: false, access: { trust: 'manage', marketplace: 'manage', finance: 'view', projects: 'view' } },
  { key: 'compliance', label: 'Compliance officer', isMaster: false, access: { trust: 'manage', marketplace: 'view' } },
  { key: 'finance', label: 'Finance officer', isMaster: false, access: { finance: 'manage', marketplace: 'view' } },
  { key: 'projects', label: 'Project coordinator', isMaster: false, access: { projects: 'manage' } },
  { key: 'viewer', label: 'Read-only viewer', isMaster: false, access: { trust: 'view', marketplace: 'view', finance: 'view', projects: 'view', settings: 'view' } },
];

export const fullAccessMap = (partial: Partial<Record<AdminArea, AccessLevel>>): Record<AdminArea, AccessLevel> =>
  Object.fromEntries(ADMIN_AREAS.map((a) => [a.key, partial[a.key] ?? 'none'])) as Record<AdminArea, AccessLevel>;
