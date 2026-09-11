import { create } from 'zustand';
import { ordersApi } from '../services/api';

interface OrderState {
  pendingCount: number;
  lastFetched: number;
  fetchPendingCount: () => Promise<void>;
}

export const useOrderStore = create<OrderState>((set, get) => ({
  pendingCount: 0,
  lastFetched: 0,

  fetchPendingCount: async () => {
    // Throttle: don't fetch more than once per 30 seconds
    const now = Date.now();
    if (now - get().lastFetched < 30_000) return;

    try {
      // Fetch all orders (backend 500s when filtering by status)
      // and count pending ones client-side
      const res: any = await ordersApi.list({ page: 1, pageSize: 100 });
      const data = res?.data ?? res;
      const items = data?.items ?? data?.data ?? [];
      const pending = Array.isArray(items)
        ? items.filter((o: any) => o.status === 'pending' || o.status === 'Pending').length
        : 0;
      set({ pendingCount: pending, lastFetched: now });
    } catch {
      // Silently fail — badge just won't update
    }
  },
}));
