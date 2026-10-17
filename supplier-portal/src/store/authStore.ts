import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthUser } from '../types';
import type { AdminProfile } from '../utils/adminAccess';
import { AUTH_STORAGE_KEY } from '../config/target';

interface AuthState {
  user: AuthUser | null;
  /** Admin console only: who the staff member is and which areas they can view or manage. */
  adminProfile: AdminProfile | null;
  setUser: (user: AuthUser) => void;
  setAdminProfile: (profile: AdminProfile | null) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      adminProfile: null,
      setUser: (user) => set({ user }),
      setAdminProfile: (adminProfile) => set({ adminProfile }),
      logout: () => set({ user: null, adminProfile: null }),
    }),
    { name: AUTH_STORAGE_KEY }
  )
);
