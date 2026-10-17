import { create } from 'zustand';
import api from '../services/api';
import { adminSuppliersApi, adminReportsApi, walletTopUpsApi, walletWithdrawalsApi } from '../services/api';
import { useAuthStore } from './authStore';
import { levelFor } from '../utils/adminAccess';

interface AdminBadgeState {
  pendingSuppliers: number;
  pendingDocuments: number;
  pendingReports: number;
  pendingDeposits: number;
  pendingWithdrawals: number;
  lastFetched: number;
  fetchBadges: () => Promise<void>;
}

export const useAdminBadgeStore = create<AdminBadgeState>((set, get) => ({
  pendingSuppliers: 0,
  pendingDocuments: 0,
  pendingReports: 0,
  pendingDeposits: 0,
  pendingWithdrawals: 0,
  lastFetched: 0,

  fetchBadges: async () => {
    // Throttle: don't fetch more than once per 30 seconds
    const now = Date.now();
    if (now - get().lastFetched < 30_000) return;

    // Only count queues this staff member can open; the server refuses the rest anyway.
    const profile = useAuthStore.getState().adminProfile;
    const trust = levelFor(profile, 'trust') !== 'none';
    const finance = levelFor(profile, 'finance') !== 'none';

    try {
      // Fetch pending suppliers count
      let pendingSuppliers = 0;
      if (trust) try {
        // Use the server-side total; counting items only covered the first page (max 20)
        const suppRes = await adminSuppliersApi.list({ status: 'pending', pageSize: 1 });
        const suppData = suppRes?.data ?? suppRes;
        pendingSuppliers = Number(suppData?.total ?? (Array.isArray(suppData) ? suppData.length : suppData?.items?.length ?? 0));
      } catch {}

      // Fetch pending documents count
      let pendingDocuments = 0;
      if (trust) try {
        const docRes = await api.get('/compliance/documents', { params: { status: 'pending' } });
        pendingDocuments = docRes.data?.data?.summary?.pending ?? 0;
      } catch {}

      // New reports waiting for review
      let pendingReports = 0;
      if (trust) try {
        pendingReports = (await adminReportsApi.list({ status: 'submitted', pageSize: 1 })).total;
      } catch {}

      // Shop deposits waiting to be matched against the bank account
      let pendingDeposits = 0;
      if (finance) try {
        pendingDeposits = (await walletTopUpsApi.list('pending')).filter((t) => t.method !== 'paystack').length;
      } catch {}

      // Withdrawals waiting to be paid out by EFT
      let pendingWithdrawals = 0;
      if (finance) try {
        pendingWithdrawals = (await walletWithdrawalsApi.list('pending')).length;
      } catch {}

      set({ pendingSuppliers, pendingDocuments, pendingReports, pendingDeposits, pendingWithdrawals, lastFetched: now });
    } catch {
      // Silently fail
    }
  },
}));
