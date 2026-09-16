import axios from 'axios';
import toast from 'react-hot-toast';
import type { Product, ProductFormData, ProductQrCodeData, Order, OrderStatus, AnalyticsSummary, RevenueDataPoint, TopProduct, SupplierProfile, PaginatedResponse, SubscribeRequest, GroupBuy, GroupBuyFilter, GroupBuyApprovalRequest } from '../types';
import env from '../config/env';

const BASE_URL = env.apiUrl;

const api = axios.create({ 
  baseURL: BASE_URL, 
  timeout: env.apiTimeout 
});
const productApi = api; // products route through the same Gateway

// Attach JWT to both instances
const attachToken = (config: any) => {
  const stored = localStorage.getItem('spazasure-auth-v2');
  if (stored) {
    try {
      const { state } = JSON.parse(stored);
      if (state?.user?.token) config.headers.Authorization = `Bearer ${state.user.token}`;
    } catch {}
  }
  return config;
};
api.interceptors.request.use(attachToken);

// Map backend product shape → frontend Product type
function mapProduct(p: any): Product {
  return {
    id:            p.id,
    name:          p.name,
    description:   p.description ?? '',
    sku:           p.sku,
    price:         p.price,
    discountPrice: p.discountPrice,
    imageUrl:      p.imageUrl ?? (p.images ? tryParseImages(p.images)[0] ?? '' : ''),
    images:        tryParseImages(p.images),
    categoryId:    p.categoryId ?? '',
    categoryName:  p.category ?? p.categoryName ?? '',
    supplierId:    p.supplierId ?? '',
    supplierName:  p.supplierName ?? '',
    stockQuantity: p.stockQty   ?? p.stockQuantity ?? 0,
    minOrderQty:   p.minOrderQty ?? 1,
    barcode:       p.barcode ?? '',
    qrCode:        p.barcode ?? '',
    hasQrCode:     p.hasQrCode ?? false,
    isAvailable:   p.isAvailable ?? true,
    status:        p.isApproved ? 'active' : (p.status ?? 'pending_approval'),
    rating:        p.rating     ?? 0,
    reviewCount:   p.reviewCount ?? 0,
    createdAt:     p.createdAt  ?? new Date().toISOString(),
  };
}

function tryParseImages(images: any): string[] {
  if (!images) return [];
  if (Array.isArray(images)) return images;
  try { return JSON.parse(images); } catch { return []; }
}

// Resolves a relative upload path into a full URL served by the UserService
export const resolveUploadUrl = (path?: string | null): string | undefined => {
  if (!path) return undefined;
  if (path.startsWith('http') || path.startsWith('data:')) return path;
  return `${BASE_URL.replace(/\/api$/, '')}${path}`;
};

const handleError = async (err: any) => {
  const original = err.config;

  // Avoid retrying refresh calls or already-retried requests
  if (err.response?.status === 401 && original && !original._retry) {
    original._retry = true;
    try {
      const stored = localStorage.getItem('spazasure-auth-v2');
      if (stored) {
        const parsed = JSON.parse(stored);
        const refreshToken = parsed?.state?.user?.refreshToken;
        if (refreshToken) {
          // Use bare axios (not api) so this call doesn't re-enter the interceptor
          const { data } = await axios.post(
            `${BASE_URL}/supplier/auth/refresh`,
            { refreshToken },
            { timeout: 5000 }
          );
          // Backend wraps in ApiResponse: { success, data: { accessToken, refreshToken } }
          const newToken = data?.data?.accessToken ?? data?.accessToken;
          const newRefresh = data?.data?.refreshToken ?? data?.refreshToken;
          if (!newToken) throw new Error('No access token in refresh response');

          parsed.state.user.token = newToken;
          parsed.state.user.refreshToken = newRefresh;
          localStorage.setItem('spazasure-auth-v2', JSON.stringify(parsed));

          // Retry original request with new token
          original.headers = original.headers ?? {};
          original.headers.Authorization = `Bearer ${newToken}`;
          return api(original);
        }
      }
    } catch {
      // Refresh failed — clear session and redirect to login
    }
    localStorage.removeItem('spazasure-auth-v2');
    if (window.location.pathname !== '/login') {
      window.location.href = '/login';
    }
    return Promise.reject(err);
  }

  // Don't show toast for 401 (handled above) or cancelled requests
  if (err.response?.status && err.response.status !== 401) {
    const msg = err.response?.data?.message
      ?? err.response?.data?.error
      ?? err.response?.data?.errors?.join?.(', ')
      ?? `Request failed (${err.response.status})`;
    toast.error(msg);
  } else if (!err.response && !err.code) {
    toast.error('Could not reach the API. Check the backend connection.');
  }

  return Promise.reject(err);
};
api.interceptors.response.use((res) => res, handleError);

// Auth
export const authApi = {
  login: (email: string, password: string, role?: 'supplier' | 'admin') =>
    api.post('/supplier/auth/login', { email, password, role }).then((r) => r.data),
  register: (data: Record<string, unknown>) =>
    api.post('/supplier/auth/register', data).then((r) => r.data),
  refresh: (refreshToken: string) =>
    api.post('/supplier/auth/refresh', { refreshToken }).then((r) => r.data),
  logout: (refreshToken: string) =>
    api.post('/supplier/auth/logout', { refreshToken }).then((r) => r.data),
  forgotPassword: (email: string) =>
    api.post('/supplier/auth/forgot-password', { email }).then((r) => r.data),
  resetPassword: (email: string, token: string, newPassword: string) =>
    api.post('/supplier/auth/reset-password', { email, token, newPassword }).then((r) => r.data),
};

// Products — uses ProductService on port 5002
export const productsApi = {
  list: async (params?: { page?: number; pageSize?: number; search?: string; status?: string }) => {
    const res = await productApi.get('/supplier/products', { params });
    const d = res.data?.data ?? res.data;
    const items = (d?.items ?? d?.data ?? d ?? []).map(mapProduct);
    return { data: items, total: d?.total ?? items.length, page: d?.page ?? 1, pageSize: d?.pageSize ?? 20 };
  },
  get: async (id: string) => {
    const res = await productApi.get(`/supplier/products/${id}`);
    return mapProduct(res.data?.data ?? res.data);
  },
  create: async (data: ProductFormData) => {
    const res = await productApi.post('/supplier/products', {
      name:        data.name,
      description: data.description || null,
      sku:         data.sku,
      price:       Number(data.price),
      categoryId:  null,
      stockQty:    Number(data.stockQuantity) || 0,
      minOrderQty: Number(data.minOrderQty) || 1,
      unit:        'unit',
      images:      data.imageUrl ? JSON.stringify([data.imageUrl]) : '[]',
      isAvailable: true,
      isFoodItem:  false,
    });
    return mapProduct(res.data?.data ?? res.data);
  },
  update: async (id: string, data: Partial<ProductFormData>) => {
    const res = await productApi.put(`/supplier/products/${id}`, {
      name:        data.name,
      description: data.description || null,
      sku:         data.sku,
      price:       Number(data.price),
      categoryId:  null,
      stockQty:    Number(data.stockQuantity) || 0,
      minOrderQty: Number(data.minOrderQty) || 1,
      unit:        'unit',
      images:      data.imageUrl ? JSON.stringify([data.imageUrl]) : '[]',
      isAvailable: true,
      isFoodItem:  false,
    });
    return mapProduct(res.data?.data ?? res.data);
  },
  delete: (id: string) =>
    productApi.delete(`/supplier/products/${id}`).then((r) => r.data),
  toggleAvailability: (id: string) =>
    productApi.patch(`/supplier/products/${id}/toggle`).then((r) => r.data),
  getBarcode: (id: string) =>
    productApi.get(`/supplier/products/${id}/barcode`).then((r) => r.data),
  getQrCode: (id: string): Promise<ProductQrCodeData> =>
    productApi.get(`/supplier/products/${id}/qr-code`).then((r) => r.data?.data ?? r.data),
};

// Admin Products — admin-level product management
export const adminProductsApi = {
  list: async (params?: { page?: number; pageSize?: number; search?: string; status?: string }) => {
    const res = await api.get('/admin/products', { params });
    const d = res.data?.data ?? res.data;
    const items = (d?.items ?? d?.data ?? d ?? []).map(mapProduct);
    return { data: items, total: d?.total ?? items.length, page: d?.page ?? 1, pageSize: d?.pageSize ?? 20 };
  },
  approve: (id: string) =>
    api.patch(`/admin/products/${id}/approve`).then((r) => r.data),
  reject: (id: string, reason?: string) =>
    api.patch(`/admin/products/${id}/reject`, { reason }).then((r) => r.data),
  toggleAvailability: (id: string) =>
    api.patch(`/admin/products/${id}/toggle`).then((r) => r.data),
};

// Group buys
export const groupBuyApi = {
  list: async (status: GroupBuyFilter = 'all'): Promise<GroupBuy[]> => {
    const res = await api.get('/supplier/group-buy', { params: { status } });
    const data = res.data?.data ?? res.data;
    return Array.isArray(data) ? data : (data?.items ?? []);
  },
  approve: (id: string, request: GroupBuyApprovalRequest) =>
    api.post(`/supplier/group-buy/${id}/approve`, request).then((r) => r.data?.data ?? r.data),
};

// Orders
export const ordersApi = {
  list: (params?: { page?: number; pageSize?: number; status?: OrderStatus }) =>
    api.get<PaginatedResponse<Order>>('/supplier/orders', { params }).then((r) => r.data),
  get: (id: string) => api.get<Order>(`/supplier/orders/${id}`).then((r) => r.data),
  updateStatus: (id: string, status: OrderStatus, reason?: string) =>
    api.patch(`/supplier/orders/${id}/status`, { status, reason }).then((r) => r.data),
};

// Admin Orders (all platform orders)
export const adminOrdersApi = {
  list: async (params?: { page?: number; pageSize?: number; status?: string; search?: string }) => {
    const res = await api.get('/admin/orders', { params });
    const d = res.data?.data ?? res.data;
    return {
      data: d?.items ?? [],
      total: d?.total ?? 0,
      page: d?.page ?? 1,
      pageSize: d?.pageSize ?? 20,
      summary: d?.summary ?? {},
    };
  },
  get: (id: string) =>
    api.get(`/admin/orders/${id}`).then((r) => r.data?.data ?? r.data),
  updateStatus: (id: string, status: string, reason?: string) =>
    api.patch(`/admin/orders/${id}/status`, { status, reason }).then((r) => r.data),
};

// Analytics
export const analyticsApi = {
  summary: () => api.get<AnalyticsSummary>('/supplier/analytics/summary').then((r) => r.data),
  revenue: (period: 'week' | 'month' | 'year') =>
    api.get<RevenueDataPoint[]>('/supplier/analytics/revenue', { params: { period } }).then((r) => r.data),
  topProducts: () => api.get<TopProduct[]>('/supplier/analytics/top-products').then((r) => r.data),
};

// Admin Analytics (platform-wide)
export const adminAnalyticsApi = {
  summary: () => api.get('/admin/analytics/summary').then((r) => r.data?.data ?? r.data).catch(() => null),
  revenue: (period: 'week' | 'month' | 'year') =>
    api.get('/admin/analytics/revenue', { params: { period } }).then((r) => r.data?.data ?? r.data ?? []).catch(() => []),
};

// Maps the backend Supplier shape → frontend SupplierProfile shape
function mapProfile(raw: any): SupplierProfile {
  return {
    id:                 raw.id,
    companyName:        raw.companyName        ?? '',
    registrationNumber: raw.registrationNumber ?? '',
    // Backend stores as VatNumber / vatNumber
    taxNumber:          raw.taxNumber          ?? raw.vatNumber          ?? '',
    // Backend stores as ContactPerson / contactPerson
    contactName:        raw.contactName        ?? raw.contactPerson      ?? '',
    email:              raw.email              ?? '',
    phone:              raw.phone              ?? '',
    address:            raw.address            ?? '',
    tier:               raw.tier               ?? 'basic',
    isVerified:         raw.isVerified         ?? raw.status === 'verified',
    logoUrl:            raw.logoUrl,
    bankName:           raw.bankName           ?? '',
    bankAccount:        raw.bankAccount        ?? '',
    bankBranchCode:     raw.bankBranchCode     ?? '',
    latitude:           raw.latitude == null ? undefined : Number(raw.latitude),
    longitude:          raw.longitude == null ? undefined : Number(raw.longitude),
    documents: (raw.documents ?? []).map((d: any) => ({
      id:              d.id,
      docType:         d.docType,
      docUrl:          d.docUrl,
      status:          d.status,
      expiryDate:      d.expiryDate,
      // Backend calls this rejectionNote, frontend calls it rejectionReason
      rejectionReason: d.rejectionReason ?? d.rejectionNote ?? undefined,
    })),
  };
}

// Profile
export const profileApi = {
  get: () =>
    api.get('/supplier/profile').then((r) => {
      const raw = r.data?.data ?? r.data;
      return mapProfile(raw);
    }),
  update: (data: Partial<SupplierProfile>) =>
    api.put('/supplier/profile', {
      companyName:        data.companyName,
      contactPerson:      data.contactName,       // frontend → backend field name
      phone:              data.phone,
      email:              data.email,
      address:            data.address,
      registrationNumber: data.registrationNumber,
      vatNumber:          data.taxNumber,          // frontend → backend field name
      bankName:           data.bankName,
      bankAccount:        data.bankAccount,
      bankBranchCode:     data.bankBranchCode,
      latitude:           data.latitude,
      longitude:          data.longitude,
    }).then((r) => r.data),
  uploadDocument: (docType: string, file: File) => {
    const form = new FormData();
    form.append('file', file);
    form.append('docType', docType);
    return api.post('/supplier/profile/documents', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data);
  },
  uploadLogo: (file: File) => {
    const form = new FormData();
    form.append('logo', file);
    return api.post('/supplier/profile/logo', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data);
  },
};

export default api;

// Notifications (supplier-facing)
export const notificationsApi = {
  list: (params?: { page?: number; pageSize?: number }) =>
    api.get('/supplier/notifications', { params }).then((r) => r.data?.data ?? r.data ?? []).catch(() => []),
  unreadCount: () =>
    api.get('/supplier/notifications/unread-count').then((r) => r.data?.count ?? r.data?.data?.count ?? 0).catch(() => 0),
  markRead: (id: string) =>
    api.put(`/supplier/notifications/${id}/read`).then((r) => r.data),
  markAllRead: () =>
    api.put('/supplier/notifications/read-all').then((r) => r.data),
};

// Subscription
export const subscriptionApi = {
  getPlans: () =>
    api.get('/supplier/subscription/plans').then((r) => r.data),

  getCurrent: () =>
    api.get('/supplier/subscription/current').then((r) => r.data),

  subscribe: (data: SubscribeRequest) =>
    api.post('/supplier/subscription/subscribe', data).then((r) => r.data),

  confirmPayment: (subscriptionId: string, paymentReference?: string) =>
    api.post(`/supplier/subscription/${subscriptionId}/confirm-payment`, { paymentReference }).then((r) => r.data),

  cancel: (data: { reason?: string }) =>
    api.post('/supplier/subscription/cancel', data).then((r) => r.data),

  getHistory: () =>
    api.get('/supplier/subscription/history').then((r) => r.data),
};

// Payment (Stripe Checkout)
export const paymentApi = {
  stripeCheckout: (subscriptionId: string) =>
    api.post('/supplier/payment/stripe/checkout-session', { subscriptionId }).then((r) => r.data),

  /** Initiate a PayFast payment — returns form data + PayFast URL to redirect to */
  initiate: (subscriptionId: string) =>
    api.post('/supplier/payment/initiate', { subscriptionId }).then((r) => r.data),

  /** Check payment/subscription status after redirect */
  getStatus: (subscriptionId: string) =>
    api.get(`/supplier/payment/status/${subscriptionId}`).then((r) => r.data),
};

// Admin Suppliers & Spaza Owners
export const adminSuppliersApi = {
  list: (params?: { page?: number; pageSize?: number; search?: string; status?: string }) =>
    api.get('/admin/suppliers', { params }).then((r) => r.data),
  get: (id: string) =>
    api.get(`/admin/suppliers/${id}`).then((r) => r.data),
  verify: (id: string) =>
    api.patch(`/admin/suppliers/${id}/verify`).then((r) => r.data),
  suspend: (id: string, reason?: string) =>
    api.patch(`/admin/suppliers/${id}/suspend`, { reason }).then((r) => r.data),
};

export const adminSpazaOwnersApi = {
  list: (params?: { page?: number; pageSize?: number; search?: string; status?: string }) =>
    api.get('/admin/spaza-owners', { params }).then((r) => r.data),
  get: (id: string) =>
    api.get(`/admin/spaza-owners/${id}`).then((r) => r.data),
  verify: (id: string) =>
    api.patch(`/admin/spaza-owners/${id}/verify`).then((r) => r.data),
  suspend: (id: string, reason?: string) =>
    api.patch(`/admin/spaza-owners/${id}/suspend`, { reason }).then((r) => r.data),
};

// Platform settings
export const adminSettingsApi = {
  getOnboardingFee: () =>
    api.get('/admin/settings/onboarding-fee').then((r) => r.data?.data ?? r.data),
  updateOnboardingFee: (amount: number) =>
    api.put('/admin/settings/onboarding-fee', { amount }).then((r) => r.data?.data ?? r.data),
};

// FAQs (Subscription page — admin-managed)
export interface FaqItem {
  id: string;
  question: string;
  answer: string;
  sortOrder: number;
  isActive: boolean;
}

export const faqApi = {
  /** Get all active FAQs (supplier-facing) */
  list: () =>
    api.get('/supplier/faqs').then((r) => r.data).catch(() => ({ data: [] })),

  /** Get all FAQs including inactive (admin) */
  listAll: () =>
    api.get('/admin/faqs').then((r) => r.data).catch(() => ({ data: [] })),

  /** Create a new FAQ */
  create: (data: { question: string; answer: string; sortOrder?: number }) =>
    api.post('/admin/faqs', data).then((r) => r.data),

  /** Update an existing FAQ */
  update: (id: string, data: Partial<{ question: string; answer: string; sortOrder: number; isActive: boolean }>) =>
    api.put(`/admin/faqs/${id}`, data).then((r) => r.data),

  /** Delete a FAQ */
  delete: (id: string) =>
    api.delete(`/admin/faqs/${id}`).then((r) => r.data),

  /** Reorder FAQs */
  reorder: (ids: string[]) =>
    api.patch('/admin/faqs/reorder', { ids }).then((r) => r.data),
};
