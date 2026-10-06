import { create } from 'zustand';
import api from '../services/api';
import { adminSuppliersApi, adminReportsApi } from '../services/api';

interface AdminBadgeState {
  pendingSuppliers: number;
  pendingDocuments: number;
  pendingReports: number;
  lastFetched: number;
  fetchBadges: () => Promise<void>;
}

export const useAdminBadgeStore = create<AdminBadgeState>((set, get) => ({
  pendingSuppliers: 0,
  pendingDocuments: 0,
  pendingReports: 0,
  lastFetched: 0,

  fetchBadges: async () => {
    // Throttle: don't fetch more than once per 30 seconds
    const now = Date.now();
    if (now - get().lastFetched < 30_000) return;

    try {
      // Fetch pending suppliers count
      let pendingSuppliers = 0;
      try {
        const suppRes = await adminSuppliersApi.list({ status: 'pending' });
        const suppData = suppRes?.data ?? suppRes;
        const items = Array.isArray(suppData) ? suppData : suppData?.items ?? [];
        pendingSuppliers = items.filter((s: any) => !s.isVerified && s.status !== 'verified').length;
      } catch {}

      // Fetch pending documents count
      let pendingDocuments = 0;
      try {
        const docRes = await api.get('/compliance/documents', { params: { status: 'pending' } });
        pendingDocuments = docRes.data?.data?.summary?.pending ?? 0;
      } catch {}

      // New reports waiting for review
      let pendingReports = 0;
      try {
        pendingReports = (await adminReportsApi.list({ status: 'submitted', pageSize: 1 })).total;
      } catch {}

      set({ pendingSuppliers, pendingDocuments, pendingReports, lastFetched: now });
    } catch {
      // Silently fail
    }
  },
}));
