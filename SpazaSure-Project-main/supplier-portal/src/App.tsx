import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuthStore } from './store/authStore';

// Layouts
import AppLayout from './components/layout/AppLayout';
import AdminLayout from './components/layout/AdminLayout';

// Auth
import LoginPage from './pages/auth/LoginPage';
import RegisterPage from './pages/auth/RegisterPage';
import ForgotPasswordPage from './pages/auth/ForgotPasswordPage';

// Supplier pages
import DashboardPage from './pages/dashboard/DashboardPage';
import ProductsPage from './pages/products/ProductsPage';
import OrdersPage from './pages/orders/OrdersPage';
import AnalyticsPage from './pages/analytics/AnalyticsPage';
import ProfilePage from './pages/profile/ProfilePage';
import NotificationsPage from './pages/notifications/NotificationsPage';
import SubscriptionPage from './pages/subscription/SubscriptionPage';
import GroupBuyPage from './pages/group-buy/GroupBuyPage';

// Admin pages
import AdminDashboardPage from './pages/admin/AdminDashboardPage';
import AdminSuppliersPage from './pages/admin/AdminSuppliersPage';
import DocumentVerificationPage from './pages/admin/DocumentVerificationPage';
import AdminProductsPage from './pages/admin/AdminProductsPage';
import AdminOrdersPage from './pages/admin/AdminOrdersPage';
import AdminAnalyticsPage from './pages/admin/AdminAnalyticsPage';
import AdminNotificationsPage from './pages/admin/AdminNotificationsPage';
import AdminSettingsPage from './pages/admin/AdminSettingsPage';

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
        <Route path="documents" element={<DocumentVerificationPage />} />
        <Route path="products"      element={<AdminProductsPage />} />
        <Route path="orders"        element={<AdminOrdersPage />} />
        <Route path="analytics"     element={<AdminAnalyticsPage />} />
        <Route path="notifications" element={<AdminNotificationsPage />} />
        <Route path="settings"      element={<AdminSettingsPage />} />
      </Route>

      {/* Fallback */}
      <Route path="*" element={
        <Navigate to={user?.role === 'admin' ? '/admin/dashboard' : '/dashboard'} replace />
      } />
    </Routes>
  );
}
