import { lazy, Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import PageLoader from './components/ui/PageLoader';
import { useAuthStore } from './store/authStore';
import AdminLayout from './components/layout/AdminLayout';
import LoginPage from './pages/auth/LoginPage';
import ForgotPasswordPage from './pages/auth/ForgotPasswordPage';

const AdminDashboardPage = lazy(() => import('./pages/admin/AdminDashboardPage'));
const AdminSuppliersPage = lazy(() => import('./pages/admin/AdminSuppliersPage'));
const DocumentVerificationPage = lazy(() => import('./pages/admin/DocumentVerificationPage'));
const AdminProductsPage = lazy(() => import('./pages/admin/AdminProductsPage'));
const AdminOrdersPage = lazy(() => import('./pages/admin/AdminOrdersPage'));
const AdminReportsPage = lazy(() => import('./pages/admin/AdminReportsPage'));
const AdminShopsMapPage = lazy(() => import('./pages/admin/AdminShopsMapPage'));
const AdminGroupBuysPage = lazy(() => import('./pages/admin/AdminGroupBuysPage'));
const AdminAnalyticsPage = lazy(() => import('./pages/admin/AdminAnalyticsPage'));
const AdminNotificationsPage = lazy(() => import('./pages/admin/AdminNotificationsPage'));
const AdminPayoutsPage = lazy(() => import('./pages/admin/AdminPayoutsPage'));
const AdminWalletDepositsPage = lazy(() => import('./pages/admin/AdminWalletDepositsPage'));
const AdminWalletWithdrawalsPage = lazy(() => import('./pages/admin/AdminWalletWithdrawalsPage'));
const AdminWalletLedgerPage = lazy(() => import('./pages/admin/AdminWalletLedgerPage'));
const AdminFinancePage = lazy(() => import('./pages/admin/AdminFinancePage'));
const AdminSettingsPage = lazy(() => import('./pages/admin/AdminSettingsPage'));
const AdminProjectsPage = lazy(() => import('./pages/admin/AdminProjectsPage'));
const AdminTeamPage = lazy(() => import('./pages/admin/AdminTeamPage'));
const AdminDeliveriesPage = lazy(() => import('./pages/admin/AdminDeliveriesPage'));
const AdminDeletionRequestsPage = lazy(() => import('./pages/admin/AdminDeletionRequestsPage'));

function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { user } = useAuthStore();
  if (!user || user.role !== 'admin') return <Navigate to="/login" replace />;
  return <>{children}</>;
}

export default function AdminApp() {
  return (
    <Suspense fallback={<PageLoader variant="dashboard" />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />

        <Route path="/admin" element={<RequireAdmin><AdminLayout /></RequireAdmin>}>
          <Route index element={<Navigate to="/admin/dashboard" replace />} />
          <Route path="dashboard" element={<AdminDashboardPage />} />
          <Route path="suppliers" element={<AdminSuppliersPage />} />
          <Route path="shops-map" element={<AdminShopsMapPage />} />
          <Route path="group-buys" element={<AdminGroupBuysPage />} />
          <Route path="documents" element={<DocumentVerificationPage />} />
          <Route path="products" element={<AdminProductsPage />} />
          <Route path="orders" element={<AdminOrdersPage />} />
          <Route path="deliveries" element={<AdminDeliveriesPage />} />
          <Route path="reports" element={<AdminReportsPage />} />
          <Route path="analytics" element={<AdminAnalyticsPage />} />
          <Route path="notifications" element={<AdminNotificationsPage />} />
          <Route path="payouts" element={<AdminPayoutsPage />} />
          <Route path="deposits" element={<AdminWalletDepositsPage />} />
          <Route path="withdrawals" element={<AdminWalletWithdrawalsPage />} />
          <Route path="wallet-ledger" element={<AdminWalletLedgerPage />} />
          <Route path="finance" element={<AdminFinancePage />} />
          <Route path="settings" element={<AdminSettingsPage />} />
          <Route path="projects" element={<AdminProjectsPage />} />
          <Route path="projects/:tab" element={<AdminProjectsPage />} />
          <Route path="team" element={<AdminTeamPage />} />
          <Route path="deletion-requests" element={<AdminDeletionRequestsPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/admin/dashboard" replace />} />
      </Routes>
    </Suspense>
  );
}
