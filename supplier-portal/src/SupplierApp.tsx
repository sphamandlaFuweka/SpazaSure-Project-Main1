import { lazy, Suspense } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import PageLoader from './components/ui/PageLoader';
import { useAuthStore } from './store/authStore';
import AppLayout from './components/layout/AppLayout';
import LoginPage from './pages/auth/LoginPage';
import RegisterPage from './pages/auth/RegisterPage';
import ForgotPasswordPage from './pages/auth/ForgotPasswordPage';

const DashboardPage = lazy(() => import('./pages/dashboard/DashboardPage'));
const ProductsPage = lazy(() => import('./pages/products/ProductsPage'));
const OrdersPage = lazy(() => import('./pages/orders/OrdersPage'));
const FeedbackPage = lazy(() => import('./pages/feedback/FeedbackPage'));
const AnalyticsPage = lazy(() => import('./pages/analytics/AnalyticsPage'));
const ProfilePage = lazy(() => import('./pages/profile/ProfilePage'));
const NotificationsPage = lazy(() => import('./pages/notifications/NotificationsPage'));
const SubscriptionPage = lazy(() => import('./pages/subscription/SubscriptionPage'));
const GroupBuyPage = lazy(() => import('./pages/group-buy/GroupBuyPage'));
const VerifyShopPage = lazy(() => import('./pages/public/VerifyShopPage'));
const PrivacyNoticePage = lazy(() => import('./pages/public/PrivacyNoticePage'));
const DeleteAccountPage = lazy(() => import('./pages/public/DeleteAccountPage'));
const VerificationCompletePage = lazy(() => import('./pages/public/VerificationCompletePage'));

function RequireSupplier({ children }: { children: React.ReactNode }) {
  const { user } = useAuthStore();
  // A non-supplier session (for example a leftover one) is never let into the supplier portal
  if (!user || user.role !== 'supplier') return <Navigate to="/login" replace />;
  return <>{children}</>;
}

export default function SupplierApp() {
  return (
    <Suspense fallback={<PageLoader variant="dashboard" />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />

        {/* Public: anyone can check a shop's SpazaSure sticker (QR code or shop number) */}
        <Route path="/verify" element={<VerifyShopPage />} />
        <Route path="/privacy" element={<PrivacyNoticePage />} />
        {/* Public: delete an account without the app (Google Play requirement) */}
        <Route path="/delete-account" element={<DeleteAccountPage />} />
        <Route path="/verification-complete" element={<VerificationCompletePage />} />
        <Route path="/verify/:code" element={<VerifyShopPage />} />

        <Route path="/" element={<RequireSupplier><AppLayout /></RequireSupplier>}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="products" element={<ProductsPage />} />
          <Route path="orders" element={<OrdersPage />} />
          <Route path="feedback" element={<FeedbackPage />} />
          <Route path="group-buy" element={<GroupBuyPage />} />
          <Route path="analytics" element={<AnalyticsPage />} />
          <Route path="notifications" element={<NotificationsPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="subscription" element={<SubscriptionPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </Suspense>
  );
}
