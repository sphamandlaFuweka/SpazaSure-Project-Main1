import { lazy, Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import PageLoader from './components/ui/PageLoader';
import { useAuthStore } from './store/authStore';

// Layouts
import AppLayout from './components/layout/AppLayout';
import AdminLayout from './components/layout/AdminLayout';

// Auth
import LoginPage from './pages/auth/LoginPage';
import RegisterPage from './pages/auth/RegisterPage';
import ForgotPasswordPage from './pages/auth/ForgotPasswordPage';

// Supplier pages
const DashboardPage = lazy(() => import('./pages/dashboard/DashboardPage'));
const ProductsPage = lazy(() => import('./pages/products/ProductsPage'));
const OrdersPage = lazy(() => import('./pages/orders/OrdersPage'));
const AnalyticsPage = lazy(() => import('./pages/analytics/AnalyticsPage'));
const ProfilePage = lazy(() => import('./pages/profile/ProfilePage'));
const NotificationsPage = lazy(() => import('./pages/notifications/NotificationsPage'));
const SubscriptionPage = lazy(() => import('./pages/subscription/SubscriptionPage'));
const GroupBuyPage = lazy(() => import('./pages/group-buy/GroupBuyPage'));

// Admin pages
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
const AdminSettingsPage = lazy(() => import('./pages/admin/AdminSettingsPage'));

function RequireAuth({ children, role }: { children: React.ReactNode; role?: 'supplier' | 'admin' }) {
  const { user } = useAuthStore();
  if (!user) return <Navigate to="/login" replace />;
  if (role && user.role !== role) {
    return <Navigate to={user.role === 'admin' ? '/admin/dashboard' : '/dashboard'} replace />;
  }
  return <>{children}</>;
}

export default function App() {
  const { user } = useAuthStore();

  return (
    <Suspense fallback={<PageLoader variant="dashboard" />}>
    <Routes>
      {/* Auth */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />

      {/* Supplier Portal */}
      <Route path="/" element={<RequireAuth role="supplier"><AppLayout /></RequireAuth>}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="products" element={<ProductsPage />} />
        <Route path="orders" element={<OrdersPage />} />
        <Route path="group-buy" element={<GroupBuyPage />} />
        <Route path="analytics" element={<AnalyticsPage />} />
        <Route path="notifications" element={<NotificationsPage />} />
        <Route path="profile" element={<ProfilePage />} />
        <Route path="subscription" element={<SubscriptionPage />} />
      </Route>

      {/* Admin Portal */}
      <Route path="/admin" element={<RequireAuth role="admin"><AdminLayout /></RequireAuth>}>
        <Route index element={<Navigate to="/admin/dashboard" replace />} />
        <Route path="dashboard" element={<AdminDashboardPage />} />
        <Route path="suppliers" element={<AdminSuppliersPage />} />
        <Route path="shops-map" element={<AdminShopsMapPage />} />
        <Route path="group-buys" element={<AdminGroupBuysPage />} />
        <Route path="documents" element={<DocumentVerificationPage />} />
        <Route path="products"      element={<AdminProductsPage />} />
        <Route path="orders"        element={<AdminOrdersPage />} />
        <Route path="reports"       element={<AdminReportsPage />} />
        <Route path="analytics"     element={<AdminAnalyticsPage />} />
        <Route path="notifications" element={<AdminNotificationsPage />} />
        <Route path="payouts"       element={<AdminPayoutsPage />} />
        <Route path="settings"      element={<AdminSettingsPage />} />
      </Route>

      {/* Fallback */}
      <Route path="*" element={
        <Navigate to={user?.role === 'admin' ? '/admin/dashboard' : '/dashboard'} replace />
      } />
    </Routes>
    </Suspense>
  );
}
