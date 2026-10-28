import axios from 'axios';
import toast from 'react-hot-toast';
import { emptyProductInfo } from '../types';
import type { Product, ProductFormData, ProductInfo, ProductQrCodeData, Order, OrderStatus, AnalyticsSummary, RevenueDataPoint, TopProduct, SupplierProfile, PaginatedResponse, SubscribeRequest, GroupBuy, GroupBuyFilter, SupplierGroupBuyFilter, NewGroupBuyRequest, DeliveryDispute, SupplierRating, SupplierBranch } from '../types';
import env from '../config/env';
import { AUTH_STORAGE_KEY } from '../config/target';
import type { AccessLevel, AdminArea, AdminProfile } from '../utils/adminAccess';
import type { AdminReport } from '../types/adminReport';
import type { Kind, RawRecord } from '../pages/admin/projects/types';

const BASE_URL = env.apiUrl;

const api = axios.create({ 
  baseURL: BASE_URL, 
  timeout: env.apiTimeout 
});
const productApi = api; // products route through the same Gateway

// Attach JWT to both instances
const attachToken = (config: any) => {
  const stored = localStorage.getItem(AUTH_STORAGE_KEY);
  if (stored) {
    try {
      const { state } = JSON.parse(stored);
      if (state?.user?.token) config.headers.Authorization = `Bearer ${state.user.token}`;
    } catch {}
  }
  return config;
};
api.interceptors.request.use(attachToken);

/** Reads the product information block the API returns, tolerating missing fields. */
export function mapProductInfo(d: any): ProductInfo {
  const text = (v: unknown) => (typeof v === 'string' ? v : '');
  d = d ?? {};
  return {
    ...emptyProductInfo(),
    brand: text(d.brand), packSize: text(d.packSize), countryOfOrigin: text(d.countryOfOrigin),
    manufacturer: text(d.manufacturer), ingredients: text(d.ingredients),
    allergens: Array.isArray(d.allergens) ? d.allergens : [],
    servingSize: text(d.servingSize), servingsPerPack: text(d.servingsPerPack),
    nutrition: Array.isArray(d.nutrition)
      ? d.nutrition.map((n: any) => ({ nutrient: text(n.nutrient), per100: text(n.per100), perServing: text(n.perServing) }))
      : [],
    usageInstructions: text(d.usageInstructions), storageInstructions: text(d.storageInstructions),
    warnings: text(d.warnings),
    specifications: Array.isArray(d.specifications)
      ? d.specifications.map((x: any) => ({ label: text(x.label), value: text(x.value) }))
      : [],
    authenticityTips: Array.isArray(d.authenticityTips) ? d.authenticityTips : [],
  };
}

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
    unit:          p.unit ?? undefined,
    isFoodItem:    p.isFoodItem ?? undefined,
    status:        p.isApproved ? (p.isAvailable === false ? 'archived' : 'active') : (p.status ?? 'pending_approval'),
    rating:        p.rating     ?? 0,
    reviewCount:   p.reviewCount ?? 0,
    createdAt:     p.createdAt  ?? new Date().toISOString(),
  };
}

const isGuid = (value?: string | null): value is string =>
  !!value && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);

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
      const stored = localStorage.getItem(AUTH_STORAGE_KEY);
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
          localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(parsed));

          // Retry original request with new token
          original.headers = original.headers ?? {};
          original.headers.Authorization = `Bearer ${newToken}`;
          return api(original);
        }
      }
    } catch {
      // Refresh failed — clear session and redirect to login
    }
    localStorage.removeItem(AUTH_STORAGE_KEY);
    if (window.location.pathname !== '/login') {
      window.location.href = '/login';
    }
    return Promise.reject(err);
  }

  // Maintenance mode (set by an admin): one persistent, de-duplicated message
  if (err.response?.status === 503 && err.response?.data?.maintenance) {
    toast.error(err.response.data.message ?? 'SpazaSure is under maintenance. Please try again shortly.', { id: 'maintenance', duration: 10000 });
    return Promise.reject(err);
  }

  // Staff without access to an admin area: background reads (dashboard cards, badges, search)
  // stay quiet, and a refused change shows one de-duplicated message.
  if (err.response?.status === 403 && err.response?.data?.noAccess) {
    if ((original?.method ?? 'get').toLowerCase() !== 'get') {
      toast.error(err.response.data.message ?? "You don't have access to do that.", { id: 'no-access' });
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
  } else if (!err.response && err.code !== 'ERR_CANCELED') {
    // Network errors (ERR_NETWORK) and timeouts (ECONNABORTED) have no response.
    // Dedupe so a page firing several requests shows one toast, not five.
    toast.error(
      err.code === 'ECONNABORTED'
        ? 'The server took too long to respond. Please try again.'
        : 'Could not reach SpazaSure. Check your internet connection.',
      { id: 'network-error' },
    );
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
      categoryId:  isGuid(data.categoryId) ? data.categoryId : null,
      stockQty:    Number(data.stockQuantity) || 0,
      minOrderQty: Number(data.minOrderQty) || 1,
      unit:        'unit',
      images:      data.imageUrl ? JSON.stringify([data.imageUrl]) : '[]',
      // null: the pack has no barcode, so the server makes one
      barcode:     data.barcode?.trim() || null,
      isAvailable: true,
      isFoodItem:  data.isFoodItem ?? false,
      info:        data.info ?? null,
    });
    return mapProduct(res.data?.data ?? res.data);
  },
  /**
   * The backend PUT overwrites every field, so anything the form doesn't edit
   * (availability, unit, food flag, extra images) is carried over from `existing`.
   */
  /** Specifications, nutrition, ingredients and counterfeit checks for one product. */
  getInfo: async (id: string): Promise<ProductInfo> => {
    const res = await productApi.get(`/supplier/products/${id}/info`);
    return mapProductInfo(res.data?.data ?? res.data);
  },
  update: async (id: string, data: Partial<ProductFormData>, existing?: Product) => {
    const otherImages = (existing?.images ?? []).filter((img) => img !== existing?.imageUrl);
    const res = await productApi.put(`/supplier/products/${id}`, {
      name:        data.name,
      description: data.description || null,
      sku:         data.sku,
      price:       Number(data.price),
      categoryId:  isGuid(data.categoryId) ? data.categoryId : (isGuid(existing?.categoryId) ? existing!.categoryId : null),
      stockQty:    Number(data.stockQuantity) || 0,
      minOrderQty: Number(data.minOrderQty) || 1,
      unit:        existing?.unit ?? 'unit',
      // null tells the backend to keep the current images
      images:      data.imageUrl ? JSON.stringify([data.imageUrl, ...otherImages]) : null,
      // null keeps the current barcode; '' means the pack has none, so the server makes one
      barcode:     data.barcode === undefined || data.barcode.trim() === (existing?.barcode ?? '')
                     ? null : data.barcode.trim(),
      isAvailable: existing?.isAvailable ?? true,
      isFoodItem:  data.isFoodItem ?? existing?.isFoodItem ?? false,
      info:        data.info ?? null,
    });
    const updated = mapProduct(res.data?.data ?? res.data);
    // The PUT response is the raw entity without the category name or ratings
    return {
      ...updated,
      categoryName: updated.categoryName
        || (data as { categoryName?: string }).categoryName
        || (updated.categoryId === existing?.categoryId ? existing?.categoryName ?? '' : ''),
      rating: existing?.rating ?? updated.rating,
      reviewCount: existing?.reviewCount ?? updated.reviewCount,
    };
  },
  categories: async (): Promise<{ id: string; name: string }[]> => {
    const res = await api.get('/shop/marketplace/categories');
    const d = res.data?.data ?? res.data;
    return Array.isArray(d) ? d.map((c: any) => ({ id: c.id, name: c.name })) : [];
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

export interface IssuedCode { code: string; pin: string }
export interface CodeBatch {
  batchNumber: string;
  expiryDate?: string | null;
  total: number;
  verified: number;
  compromised: number;
  recalled: number;
}

// Per-unit authenticity codes (open code plus scratch-off PIN)
export const productCodesApi = {
  generate: (productId: string, body: { batchNumber: string; expiryDate: string | null; quantity: number }) =>
    api.post(`/supplier/products/${productId}/codes`, body).then((r) => (r.data?.data ?? r.data) as { batchNumber: string; codes: IssuedCode[] }),
  batches: (productId: string) =>
    api.get(`/supplier/products/${productId}/codes/batches`).then((r) => (r.data?.data ?? r.data ?? []) as CodeBatch[]),
  recall: (productId: string, batchNumber: string) =>
    api.patch(`/supplier/products/${productId}/codes/batches/${encodeURIComponent(batchNumber)}/recall`).then((r) => r.data),
};

// Admin Products — admin-level product management
export const adminProductsApi = {
  list: async (params?: { page?: number; pageSize?: number; search?: string; status?: string; categoryId?: string }) => {
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
  getInfo: (id: string) =>
    api.get(`/admin/products/${id}`).then((r) => mapProductInfo((r.data?.data ?? r.data)?.info)),
};

// Group buys
const uploadGroupBuyBanner = (url: string, file: File): Promise<string> => {
  const form = new FormData();
  form.append('banner', file);
  return api.post(url, form, { headers: { 'Content-Type': 'multipart/form-data' } })
    .then((r) => (r.data?.data ?? r.data).bannerImageUrl as string);
};

// Suppliers do not create group buys. They see the campaigns SpazaSure runs for their products,
// accept the request once shops reach a target, and dispatch the resulting orders.
export const groupBuyApi = {
  list: async (status: SupplierGroupBuyFilter = 'all'): Promise<GroupBuy[]> => {
    const res = await api.get('/supplier/group-buy', { params: { status } });
    const data = res.data?.data ?? res.data;
    return Array.isArray(data) ? data : (data?.items ?? []);
  },
  accept: (id: string) =>
    api.post(`/supplier/group-buy/${id}/accept`).then((r) => r.data?.data ?? r.data),
  dispatch: (id: string) =>
    api.post(`/supplier/group-buy/${id}/dispatch`).then((r) => r.data?.data ?? r.data),
};

/** Where an admin can run a supplier's campaign from: the head office or one of its branches. */
export interface SupplierBranchOptions {
  defaultRadiusKm: number;
  maxRadiusKm: number;
  headOffice: { address?: string | null; city?: string | null; province?: string | null; latitude?: number | null; longitude?: number | null };
  branches: Omit<SupplierBranch, 'isActive' | 'postalCode'>[];
}

export interface SupplierProductOption { id: string; name: string; price: number; stockQty: number; minOrderQty: number; isApproved?: boolean; isAvailable?: boolean }

// Admin group buys: create for any supplier, clear older supplier proposals, monitor, cancel
export const adminGroupBuyApi = {
  list: async (status: GroupBuyFilter = 'all'): Promise<GroupBuy[]> => {
    const res = await api.get('/admin/group-buy', { params: { status } });
    const data = res.data?.data ?? res.data;
    return Array.isArray(data) ? data : [];
  },
  supplierBranches: (supplierId: string) =>
    api.get(`/admin/group-buy/suppliers/${supplierId}/branches`).then((r) => (r.data?.data ?? r.data) as SupplierBranchOptions),
  supplierProducts: (supplierId: string) =>
    api.get(`/admin/group-buy/suppliers/${supplierId}/products`).then((r) => (r.data?.data ?? r.data ?? []) as SupplierProductOption[]),
  create: (body: NewGroupBuyRequest) => api.post('/admin/group-buy', body).then((r) => r.data),
  uploadBanner: (file: File) => uploadGroupBuyBanner('/admin/group-buy-banner', file),
  approve: (id: string) => api.post(`/admin/group-buy/${id}/approve`).then((r) => r.data),
  reject: (id: string, note: string) => api.post(`/admin/group-buy/${id}/reject`, { note }).then((r) => r.data),
  cancel: (id: string, note?: string) => api.post(`/admin/group-buy/${id}/cancel`, { note }).then((r) => r.data),
};

// Product categories. Admins manage them; suppliers file products under them; shops filter by them.
export interface AdminCategory {
  id: string; name: string; slug: string; sortOrder: number; isActive: boolean;
  productCount: number; liveProductCount: number;
}

export const adminCategoriesApi = {
  list: () => api.get('/admin/categories').then((r) => ((r.data?.data ?? r.data ?? []) as AdminCategory[])),
  create: (body: { name: string; sortOrder?: number }) => api.post('/admin/categories', body).then((r) => r.data),
  update: (id: string, body: { name?: string; sortOrder?: number; isActive?: boolean }) =>
    api.put(`/admin/categories/${id}`, body).then((r) => r.data),
  remove: (id: string) => api.delete(`/admin/categories/${id}`).then((r) => r.data),
};

// Admin console staff: who am I, and (master admin only) the team and their access
export interface TeamMember {
  id: string;
  email: string;
  displayName: string;
  title: string;
  isMaster: boolean;
  access: Record<AdminArea, AccessLevel>;
  active: boolean;
  isYou: boolean;
  lastLoginAt?: string | null;
  createdAt: string;
}

export interface StaffAccessInput { isMaster: boolean; areas: Partial<Record<AdminArea, AccessLevel>> }

const unwrap = <T,>(r: { data: any }): T => (r.data?.data ?? r.data) as T;

/** The server's explanation for a refused request (e.g. "Didit hasn't passed yet"), or the fallback. */
export const apiErrorMessage = (err: unknown, fallback: string): string =>
  (err as { response?: { data?: { message?: string } } })?.response?.data?.message || fallback;

/** Didit status: Not Started, In Progress, In Review, Approved, Declined, Abandoned, Expired... */
export interface IdentityCheck {
  status: string;
  verified: boolean;
  canStart: boolean;
  updatedAt: string | null;
  /** Why the check didn't pass, e.g. the CIPC number doesn't match the profile. */
  reason: string | null;
  /** Profile details to fix before the check can start: 'registration_number', 'address'. */
  missing: string[];
}

/** A request to delete an account (POPIA s24): pending, approved, rejected or cancelled. */
export interface DeletionRequest {
  id: string;
  status: 'pending' | 'approved' | 'rejected' | 'cancelled';
  reason: string | null;
  decisionNote: string | null;
  createdAt: string;
  decidedAt: string | null;
}

/** The signed-in supplier's POPIA rights: a copy of their data, and asking for deletion. */
export const privacyApi = {
  consent: () => api.get('/supplier/profile/privacy/consent').then((r) => unwrap<{ accepted: boolean; version: string }>(r)),
  acceptConsent: () => api.post('/supplier/profile/privacy/consent').then((r) => r.data),
  myData: () => api.get('/supplier/profile/privacy/my-data').then((r) => unwrap<unknown>(r)),
  deletionRequest: () => api.get('/supplier/profile/privacy/deletion-request').then((r) => unwrap<DeletionRequest | null>(r)),
  requestDeletion: (reason: string) =>
    api.post('/supplier/profile/privacy/deletion-request', { reason }).then((r) => r.data as { message?: string }),
  cancelDeletion: () => api.delete('/supplier/profile/privacy/deletion-request').then((r) => r.data),
};

export interface AdminDeletionRequest extends DeletionRequest {
  userId: string;
  role: 'spaza_owner' | 'supplier' | 'customer';
  name: string | null;
  email: string | null;
  phone: string | null;
  /** What still has to happen before the account can be deleted. */
  blocker: string | null;
}

export const adminDeletionApi = {
  list: () => api.get('/admin/deletion-requests').then((r) => unwrap<AdminDeletionRequest[]>(r)),
  approve: (id: string) => api.post(`/admin/deletion-requests/${id}/approve`).then((r) => r.data),
  reject: (id: string, note: string) => api.post(`/admin/deletion-requests/${id}/reject`, { note }).then((r) => r.data),
};

// ── Testing phase: feedback, waitlist and automated emails ─────────────────────

export type FeedbackType = 'bug' | 'idea' | 'question' | 'praise';
export type FeedbackStatus = 'new' | 'planned' | 'in_progress' | 'done' | 'wont_fix';

/** Suppliers sending feedback from the portal (bug, idea, question or praise). */
export const feedbackApi = {
  send: (f: { type: FeedbackType; message: string; rating?: number; screen?: string; screenshot?: File | null }) => {
    const form = new FormData();
    form.append('type', f.type);
    form.append('message', f.message);
    form.append('source', 'supplier_portal');
    form.append('platform', 'web');
    if (f.rating) form.append('rating', String(f.rating));
    if (f.screen) form.append('screen', f.screen);
    if (f.screenshot) form.append('screenshot', f.screenshot);
    return api.post('/supplier/profile/feedback', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      .then((r) => r.data);
  },
};

export interface AdminFeedbackItem {
  id: string;
  type: FeedbackType;
  message: string;
  rating: number | null;
  screen: string | null;
  source: string;
  appVersion: string | null;
  platform: string | null;
  status: FeedbackStatus;
  adminNote: string | null;
  role: string;
  createdAt: string;
  senderEmail: string | null;
  senderPhone: string | null;
  senderName: string | null;
  /** Signed link, valid for 30 minutes. */
  screenshotUrl: string | null;
}

export interface AdminFeedbackPage {
  items: AdminFeedbackItem[];
  total: number;
  page: number;
  pageSize: number;
  counts: { status: FeedbackStatus; count: number }[];
  averageRating: number | null;
  ratingCount: number;
}

export const adminFeedbackApi = {
  list: (params: { status?: string; type?: string; role?: string; page?: number }) =>
    api.get('/admin/feedback', { params }).then((r) => unwrap<AdminFeedbackPage>(r)),
  update: (id: string, body: { status?: FeedbackStatus; adminNote?: string }) =>
    api.patch(`/admin/feedback/${id}`, body).then((r) => r.data),
};

export interface WaitlistSignup {
  id: string;
  email: string;
  name: string | null;
  phone: string | null;
  interest: 'customer' | 'spaza_owner' | 'supplier';
  area: string | null;
  source: string | null;
  marketingConsent: boolean;
  createdAt: string;
  confirmedAt: string | null;
  unsubscribedAt: string | null;
}

export interface WaitlistPage {
  items: WaitlistSignup[];
  total: number;
  page: number;
  pageSize: number;
  summary: {
    total: number;
    confirmed: number;
    lastSevenDays: number;
    byInterest: { interest: string; count: number }[];
  };
}

export const adminWaitlistApi = {
  list: (params: { interest?: string; confirmed?: boolean; page?: number }) =>
    api.get('/admin/waitlist', { params }).then((r) => unwrap<WaitlistPage>(r)),
  /** Downloads the list as a CSV spreadsheet. */
  exportCsv: async (params: { interest?: string; confirmed?: boolean }) => {
    const r = await api.get('/admin/waitlist/export', { params, responseType: 'blob' });
    const url = URL.createObjectURL(r.data as Blob);
    const link = Object.assign(document.createElement('a'), {
      href: url,
      download: `spazasure-waitlist-${new Date().toISOString().slice(0, 10)}.csv`,
    });
    link.click();
    URL.revokeObjectURL(url);
  },
  remove: (id: string) => api.delete(`/admin/waitlist/${id}`).then((r) => r.data),
};

export interface EmailSequenceSummary {
  emailConfigured: boolean;
  enabled: boolean;
  dryRun: boolean;
  onlyTo: string | null;
  dailyLimit: number;
  sendingHours: string;
  sentToday: number;
  unsubscribed: number;
  byEmail: { step: string; sent: number; failed: number }[];
  latest: { key: string; subject: string; email: string; status: string; error: string | null; sentAt: string }[];
}

export const adminEmailApi = {
  summary: () => api.get('/admin/email-sequences').then((r) => unwrap<EmailSequenceSummary>(r)),
  preview: (step: string, role: string) =>
    api.get('/admin/email-sequences/preview', { params: { step, role } })
      .then((r) => unwrap<{ subject: string; html: string; text: string }>(r)),
  sendTest: (step: string, role: string) =>
    api.post('/admin/email-sequences/test', { step, role }).then((r) => r.data as { message?: string }),
};

/** Saves data as a .json file in the browser. */
export const downloadJson = (data: unknown, fileName: string) => {
  const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }));
  const link = Object.assign(document.createElement('a'), { href: url, download: fileName });
  link.click();
  URL.revokeObjectURL(url);
};

/** The supplier's business check with Didit (CIPC registration, directors, sanctions). */
export const businessCheckApi = {
  get: () => api.get('/compliance/identity/supplier').then((r) => unwrap<IdentityCheck>(r)),
  /** Starts the check. It runs on the server (CIPC register and sanctions screening); nothing opens. */
  start: () => api.post('/compliance/identity/supplier/session').then((r) => unwrap<{ url: string | null; status: string }>(r)),
};

export const adminTeamApi = {
  me: () => api.get('/admin/me').then((r) => unwrap<AdminProfile>(r)),
  list: () => api.get('/admin/team').then((r) => unwrap<{ members: TeamMember[] }>(r)),
  create: (body: { email: string; displayName: string; title?: string; password?: string; access: StaffAccessInput }) =>
    api.post('/admin/team', body).then((r) => ({ ...unwrap<{ member: TeamMember; temporaryPassword?: string | null }>(r), message: r.data?.message as string | undefined })),
  update: (id: string, body: { displayName?: string; title?: string; access?: StaffAccessInput }) =>
    api.put(`/admin/team/${id}`, body).then((r) => unwrap<TeamMember>(r)),
  setActive: (id: string, active: boolean) =>
    api.patch(`/admin/team/${id}/status`, { active }).then((r) => unwrap<TeamMember>(r)),
  resetPassword: (id: string) =>
    api.post(`/admin/team/${id}/reset-password`).then((r) => unwrap<{ temporaryPassword: string }>(r)),
};

// Orders
export const ordersApi = {
  list: (params?: { page?: number; pageSize?: number; status?: OrderStatus }) =>
    api.get<PaginatedResponse<Order>>('/supplier/orders', { params }).then((r) => r.data),
  get: (id: string) => api.get<Order>(`/supplier/orders/${id}`).then((r) => r.data),
  updateStatus: (id: string, status: OrderStatus, reason?: string) =>
    api.patch(`/supplier/orders/${id}/status`, { status, reason }).then((r) => r.data),
};

export const supplierFeedbackApi = {
  ratings: () =>
    api.get('/supplier/orders/ratings').then(unwrap<{ averageDeliveryRating: number; averageSupplierRating: number; totalRatings: number; ratings: SupplierRating[] }>),
  disputes: () =>
    api.get('/supplier/orders/disputes').then(unwrap<{ openCount: number; disputes: DeliveryDispute[] }>),
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
  getDispute: (id: string) =>
    api.get(`/admin/orders/${id}/dispute`).then(unwrap<DeliveryDispute>),
  resolveDispute: (id: string, resolution: 'release_supplier' | 'refund_shop' | 'confirm_refund', note?: string) =>
    api.post(`/admin/orders/${id}/resolve-dispute`, { resolution, note }).then(unwrap<{
      id: string;
      status: string;
      disputeStatus: string;
      refundStatus?: string;
      message?: string;
    }>),
};

/** An order on SpazaSure's delivery run (ready at the supplier, or out for delivery). */
export interface AdminDelivery {
  id: string;
  orderNumber: string;
  status: 'confirmed' | 'processing' | 'ready' | 'dispatched';
  deliveryType: string;
  paymentMethod: string;
  paymentStatus: string;
  totalAmount: number;
  updatedAt: string;
  deliveryCode: string | null;
  items: number;
  /** branchName is set when the order ships from one of the supplier's branches. */
  supplier: { companyName: string; phone: string | null; address: string | null; city: string | null; branchName?: string | null };
  shop: { shopName: string; ownerName: string; phone: string | null; address: string | null; city: string | null };
  driver?: { id: string; fullName: string; phone: string; vehicleRegistration: string; lastSeenAt?: string | null } | null;
}

/** A driver SpazaSure delivers with. Admins register them and allocate them to orders. */
export interface Driver {
  id: string;
  fullName: string;
  phone: string;
  vehicleType: string;
  vehicleRegistration: string;
  area?: string | null;
  isActive: boolean;
  badgeCode: string;
  lastLatitude?: number | null;
  lastLongitude?: number | null;
  lastSeenAt?: string | null;
  openJobs: number;
  /** Private link the driver opens on their phone to see their jobs and share their location. */
  driverLink: string;
  /** Licence, PrDP and ID, in that order; docUrl is a signed link valid for 30 minutes. */
  documents: DriverDocument[];
  /** Why the driver can't be allocated deliveries yet; empty when their papers are in order. */
  paperworkProblems: string[];
}

export type DriverDocType = 'licence' | 'prdp' | 'id_document';

export interface DriverDocument {
  docType: DriverDocType;
  label: string;
  uploaded: boolean;
  docUrl: string | null;
  /** yyyy-MM-dd; set for the licence and PrDP. */
  expiryDate: string | null;
  uploadedAt: string | null;
}

export type DriverInput = Pick<Driver, 'fullName' | 'phone' | 'vehicleType' | 'vehicleRegistration' | 'area' | 'isActive'>;

export const adminDriversApi = {
  list: () => api.get('/admin/drivers').then((r) => unwrap<Driver[]>(r)),
  create: (body: DriverInput) => api.post('/admin/drivers', body).then((r) => unwrap<Driver>(r)),
  update: (id: string, body: DriverInput) => api.put(`/admin/drivers/${id}`, body).then((r) => unwrap<Driver>(r)),
  resetLink: (id: string) => api.post(`/admin/drivers/${id}/reset-link`).then((r) => unwrap<Driver>(r)),
  /** Uploads or replaces the licence, PrDP or ID; the licence and PrDP need their expiry date. */
  uploadDocument: (id: string, docType: DriverDocType, file: File, expiryDate?: string) => {
    const form = new FormData();
    form.append('docType', docType);
    form.append('file', file);
    if (expiryDate) form.append('expiryDate', expiryDate);
    return api.post(`/admin/drivers/${id}/documents`, form, { headers: { 'Content-Type': 'multipart/form-data' } })
      .then((r) => unwrap<Driver>(r));
  },
};

export const adminDeliveriesApi = {
  list: () => api.get('/admin/orders/deliveries').then((r) => unwrap<AdminDelivery[]>(r)),
  /** Allocates a driver to the order; null takes the driver off it. */
  assignDriver: (orderId: string, driverId: string | null) =>
    api.put(`/admin/deliveries/${orderId}/driver`, { driverId }).then((r) => r.data),
  updateStatus: (id: string, status: 'dispatched' | 'delivered') =>
    api.patch(`/admin/orders/${id}/status`, { status }).then((r) => r.data),
};

// Admin Reports (customer/retailer product & shop reports)
export const adminReportsApi = {
  list: async (params?: { page?: number; pageSize?: number; status?: string }) => {
    const res = await api.get('/admin/reports', { params });
    const d = res.data?.data ?? res.data;
    return {
      items: (d?.items ?? []) as AdminReport[],
      total: (d?.total ?? 0) as number,
    };
  },
  escalate: (id: string, body: { escalatedTo: string; resolutionNote?: string }) =>
    api.patch(`/admin/reports/${id}/escalate`, {
      ...body,
      status: 'escalated',
    }).then((r) => r.data),
};

// Analytics
export const analyticsApi = {
  summary: () => api.get<AnalyticsSummary>('/supplier/analytics/summary').then((r) => r.data),
  revenue: (period: 'week' | 'month' | 'year') =>
    api.get<RevenueDataPoint[]>('/supplier/analytics/revenue', { params: { period } }).then((r) => r.data),
  topProducts: () => api.get<TopProduct[]>('/supplier/analytics/top-products').then((r) => r.data),
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
    city:               raw.city               ?? undefined,
    province:           raw.province           ?? undefined,
    postalCode:         raw.postalCode         ?? undefined,
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

export type SupplierBranchInput = Omit<SupplierBranch, 'id'>;

/** Branch management for one supplier: the supplier's own, or an admin's on their behalf. */
export interface BranchesApi {
  list: () => Promise<SupplierBranch[]>;
  create: (body: SupplierBranchInput) => Promise<SupplierBranch>;
  update: (id: string, body: SupplierBranchInput) => Promise<SupplierBranch>;
  remove: (id: string) => Promise<unknown>;
}

export const adminBranchesApi = (supplierId: string): BranchesApi => ({
  list: () => api.get(`/admin/suppliers/${supplierId}/branches`).then((r) => (r.data?.data ?? r.data ?? []) as SupplierBranch[]),
  create: (body) => api.post(`/admin/suppliers/${supplierId}/branches`, body).then((r) => (r.data?.data ?? r.data) as SupplierBranch),
  update: (id, body) => api.put(`/admin/suppliers/${supplierId}/branches/${id}`, body).then((r) => (r.data?.data ?? r.data) as SupplierBranch),
  remove: (id) => api.delete(`/admin/suppliers/${supplierId}/branches/${id}`).then((r) => r.data),
});

/** How a product's stock is spread over the head office and branches. */
export interface ProductBranchStock {
  id: string;
  totalStock: number;
  trackPerBranch: boolean;
  headOfficeQty: number;
  branches: { supplierBranchId: string; name: string; city: string; isActive: boolean; stockQty: number }[];
}

export const branchStockApi = {
  get: (productId: string) => api.get(`/supplier/products/${productId}/branch-stock`).then((r) => unwrap<ProductBranchStock>(r)),
  set: (productId: string, body: { trackPerBranch: boolean; headOfficeQty: number; branches: { supplierBranchId: string; stockQty: number }[] }) =>
    api.put(`/supplier/products/${productId}/branch-stock`, body).then((r) => unwrap<ProductBranchStock>(r)),
};

/** The invoice for an order's goods, issued in the supplier's name. */
export interface OrderInvoice {
  id: string;
  invoiceNumber: string;
  isTaxInvoice: boolean;
  supplierVatNumber?: string | null;
  issuedAt: string;
  goodsTotalInclVat: number;
  vatAmount: number;
  orderNumber: string;
  orderId: string;
  supplierName: string;
  shopName: string;
  /** Signed link to the PDF, valid for 30 minutes. */
  downloadUrl: string;
  verifyUrl: string;
}

export const invoicesApi = {
  /** Issues the invoice the first time it's asked for, once the supplier has confirmed the order. */
  forOrder: (orderId: string) => api.get(`/invoices/orders/${orderId}`).then((r) => unwrap<OrderInvoice>(r)),
  /** Opens the PDF in a new tab. */
  open: async (orderId: string) => {
    const invoice = await invoicesApi.forOrder(orderId);
    window.open(invoice.downloadUrl, '_blank', 'noopener');
    return invoice;
  },
  supplierList: (page = 1) => api.get('/invoices/supplier', { params: { page } })
    .then((r) => unwrap<{ total: number; items: OrderInvoice[] }>(r)),
  adminList: (params?: { supplierId?: string; from?: string; to?: string; page?: number }) =>
    api.get('/admin/invoices', { params }).then((r) => unwrap<{ total: number; items: OrderInvoice[] }>(r)),
};

// Branches: the supplier's warehouses and depots
export const branchesApi = {
  list: () => api.get('/supplier/profile/branches').then((r) => (r.data?.data ?? r.data ?? []) as SupplierBranch[]),
  create: (body: SupplierBranchInput) =>
    api.post('/supplier/profile/branches', body).then((r) => (r.data?.data ?? r.data) as SupplierBranch),
  update: (id: string, body: SupplierBranchInput) =>
    api.put(`/supplier/profile/branches/${id}`, body).then((r) => (r.data?.data ?? r.data) as SupplierBranch),
  remove: (id: string) => api.delete(`/supplier/profile/branches/${id}`).then((r) => r.data),
};

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
      city:               data.city,
      province:           data.province,
      postalCode:         data.postalCode,
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
  /** Same as list, but rejects on failure so a page can show an error state */
  listStrict: (params?: { page?: number; pageSize?: number }) =>
    api.get('/supplier/notifications', { params }).then((r) => r.data?.data ?? r.data ?? []),
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

  cancel: (data: { reason?: string }) =>
    api.post('/supplier/subscription/cancel', data).then((r) => r.data),

  getHistory: () =>
    api.get('/supplier/subscription/history').then((r) => r.data),
};

// Payment (Paystack)
export const paymentApi = {
  paystackCheckout: (subscriptionId: string) =>
    api.post('/supplier/payment/paystack/checkout-session', { subscriptionId }).then((r) => r.data),

  /** Check payment/subscription status after redirect */
  getStatus: (subscriptionId: string) =>
    api.get(`/supplier/payment/status/${subscriptionId}`).then((r) => r.data),
};

// Admin Suppliers & Spaza Owners
export const adminSuppliersApi = {
  list: (params?: { page?: number; pageSize?: number; search?: string; status?: string }) =>
    api.get('/admin/suppliers', { params }).then((r) => r.data),
  verify: (id: string) =>
    api.patch(`/admin/suppliers/${id}/verify`).then((r) => r.data),
  suspend: (id: string, reason?: string) =>
    api.patch(`/admin/suppliers/${id}/suspend`, { reason }).then((r) => r.data),
};

/** A customer of the SpazaSure customer app, as admins see them. */
export interface AdminCustomer {
  id: string;
  fullName: string;
  preferredName?: string | null;
  phone: string;
  email: string;
  age?: number | null;
  province?: string | null;
  city?: string | null;
  suburb?: string | null;
  preferredLanguage: string;
  allergies: string[];
  marketingOptIn: boolean;
  /** "free" or the paid plan's key, e.g. "plus". */
  plan: string;
  scanCount: number;
  reportCount: number;
  rewardPoints: number;
  isSuspended: boolean;
  status: string;
  profilePhotoUrl?: string | null;
  joinedAt: string;
  lastLoginAt?: string | null;
}

export const adminCustomersApi = {
  list: (params?: { page?: number; pageSize?: number; search?: string; status?: 'active' | 'suspended' }) =>
    api.get('/admin/customers', { params }).then((r) => unwrap<{ items: AdminCustomer[]; total: number; page: number; pageSize: number }>(r)),
  suspend: (id: string) => api.patch(`/admin/customers/${id}/suspend`).then((r) => r.data),
  reinstate: (id: string) => api.patch(`/admin/customers/${id}/reinstate`).then((r) => r.data),
};

export const adminSpazaOwnersApi = {
  list: (params?: { page?: number; pageSize?: number; search?: string; status?: string }) =>
    api.get('/admin/spaza-owners', { params }).then((r) => r.data),
  verify: (id: string) =>
    api.patch(`/admin/spaza-owners/${id}/verify`).then((r) => r.data),
  suspend: (id: string, reason?: string) =>
    api.patch(`/admin/spaza-owners/${id}/suspend`, { reason }).then((r) => r.data),
};

// Platform settings
/** Admin-configurable platform rules (mirrors PlatformRules in the backend). */
export interface PlatformRules {
  standardDeliveryFee: number;
  expressDeliveryFee: number;
  groupBuyBaseDeliveryFee: number;
  groupBuyPerKmRate: number;
  /** Normal orders: R per km from the shipping branch, charged when more than the standard fee. */
  deliveryPerKmRate: number;
  shopServiceFeePercent: number;
  payoutDelayDays: number;
  /** Furthest a shop can be from a supplier (km) to see and order from it; same province is also required */
  supplierReachKm: number;
  maintenanceMode: boolean;
  maintenanceMessage: string;
  requireSupplierApproval: boolean;
  autoApproveVerifiedSupplierProducts: boolean;
  cashOnDeliveryEnabled: boolean;
  /** Largest checkout total (R) allowed as cash on delivery. 0 = no limit. */
  cashOnDeliveryMaxOrderTotal: number;
  /** SpazaSure's bank account that shops deposit into to top up their wallet */
  depositBankName: string;
  depositAccountHolder: string;
  depositAccountNumber: string;
  depositBranchCode: string;
}

export const adminSettingsApi = {
  getPlatform: () =>
    api.get('/admin/settings/platform').then((r) => (r.data?.data ?? r.data) as PlatformRules),
  updatePlatform: (rules: PlatformRules) =>
    api.put('/admin/settings/platform', rules).then((r) => (r.data?.data ?? r.data) as PlatformRules),
  getOnboardingFee: () =>
    api.get('/admin/settings/onboarding-fee').then((r) => r.data?.data ?? r.data),
  updateOnboardingFee: (amount: number) =>
    api.put('/admin/settings/onboarding-fee', { amount }).then((r) => r.data?.data ?? r.data),
};

// Supplier payouts: SpazaSure delivers, and pays suppliers for the goods after the payout delay
// (automatically through Paystack when automatic payouts are on)
export type PayoutStage = 'awaiting_delivery' | 'pending' | 'available' | 'processing' | 'failed' | 'paid_out';
export interface SupplierPayoutSummary {
  payoutDelayDays: number;
  awaitingDelivery: number;
  pending: number;
  available: number;
  /** Being sent to the supplier's bank by Paystack */
  processing: number;
  /** A Paystack payout that failed (usually wrong bank details) */
  failed: number;
  paidOut: number;
  nextAvailableAt: string | null;
  hasBankDetails: boolean;
  recent: {
    id: string; orderNumber: string; amount: number; stage: PayoutStage; deliveredAt: string | null;
    availableAt: string | null; paidOutAt: string | null; payoutReference: string | null; failureReason: string | null;
  }[];
}
export interface AdminPayoutSupplier {
  supplierId: string;
  companyName: string;
  email: string;
  bankName: string | null;
  bankAccount: string | null;
  bankBranchCode: string | null;
  availableAmount: number;
  availableOrders: number;
  pendingAmount: number;
  pendingOrders: number;
  hasBankDetails: boolean;
  processingAmount: number;
  failedAmount: number;
  failureReason: string | null;
}

export const payoutsApi = {
  supplierSummary: () =>
    api.get('/supplier/payouts').then((r) => (r.data?.data ?? r.data) as SupplierPayoutSummary),
  /** South African banks Paystack can pay into (name + universal branch code). */
  banks: () =>
    api.get('/supplier/payouts/banks').then((r) => (r.data?.data ?? r.data) as { name: string; code: string }[]),
};

export const adminPayoutsApi = {
  list: () =>
    api.get('/admin/payouts').then((r) => (r.data?.data ?? r.data) as {
      payoutDelayDays: number; automaticPayouts: boolean; totalAvailable: number; totalPending: number; suppliers: AdminPayoutSupplier[];
    }),
  retry: (supplierId: string) => api.post(`/admin/payouts/${supplierId}/retry`).then((r) => r.data),
  markPaid: (supplierId: string, reference: string) =>
    api.post(`/admin/payouts/${supplierId}/mark-paid`, { reference })
      .then((r) => (r.data?.data ?? r.data) as { total: number; orders: number; reference: string }),
};

// Shop wallet top-ups (direct deposits that an admin confirms)
export type WalletTopUpStatus = 'pending' | 'approved' | 'rejected' | 'cancelled' | 'failed';
export interface WalletTopUp {
  id: string;
  amount: number;
  method: string;
  status: WalletTopUpStatus;
  reference?: string | null;
  notes?: string | null;
  createdAt: string;
  approvedAt?: string | null;
  shopId: string;
  shopName: string;
  ownerName?: string | null;
  shopPhone?: string | null;
}

export const walletTopUpsApi = {
  list: (status: WalletTopUpStatus | '' = 'pending') =>
    api.get('/admin/wallet-top-ups', { params: { status } }).then((r) => (r.data?.data ?? r.data) as WalletTopUp[]),
  approve: (id: string) => api.patch(`/admin/wallet-top-ups/${id}/approve`).then((r) => r.data),
  reject: (id: string, reason: string) => api.patch(`/admin/wallet-top-ups/${id}/reject`, { reason }).then((r) => r.data),
};

// Shop wallet withdrawals: money a shop asks to have paid into its bank account
export type WalletWithdrawalStatus = 'pending' | 'paid' | 'rejected' | 'cancelled';
export interface WalletWithdrawal {
  id: string;
  amount: number;
  status: WalletWithdrawalStatus;
  reference?: string | null;
  notes?: string | null;
  createdAt: string;
  paidAt?: string | null;
  bankName?: string | null;
  accountHolder?: string | null;
  accountNumber?: string | null;
  branchCode?: string | null;
  shopId: string;
  shopName: string;
  ownerName?: string | null;
  shopPhone?: string | null;
}

export const walletWithdrawalsApi = {
  list: (status: WalletWithdrawalStatus | '' = 'pending') =>
    api.get('/admin/wallet-withdrawals', { params: { status } }).then((r) => (r.data?.data ?? r.data) as WalletWithdrawal[]),
  markPaid: (id: string, paymentReference: string) =>
    api.patch(`/admin/wallet-withdrawals/${id}/paid`, { paymentReference }).then((r) => r.data),
  reject: (id: string, reason: string) => api.patch(`/admin/wallet-withdrawals/${id}/reject`, { reason }).then((r) => r.data),
};

// Admin finance: every money movement in one place
export type FinanceCategory =
  | 'wallet_top_up' | 'order_paystack' | 'order_cash_on_delivery'
  | 'onboarding_fee' | 'supplier_subscription' | 'supplier_payout' | 'shop_withdrawal';
export type FinanceStatus = 'confirmed' | 'pending' | 'rejected' | 'cancelled' | 'failed' | 'refund_due';

export interface FinanceEntry {
  id: string;
  date: string;
  direction: 'in' | 'out';
  category: FinanceCategory;
  amount: number;
  status: FinanceStatus;
  reference?: string | null;
  party: string;
  description: string;
}

export interface FinanceSummary {
  from: string;
  to: string;
  moneyIn: number;
  moneyOut: number;
  net: number;
  platformEarnings: number;
  byCategory: { category: FinanceCategory; direction: 'in' | 'out'; count: number; amount: number }[];
  walletBalancesHeld: number;
  owedToSuppliers: number;
  pendingDeposits: { count: number; amount: number };
  pendingWithdrawals: { count: number; amount: number };
  codOutstanding: { count: number; amount: number };
  refundsDue: { count: number; amount: number };
}

export interface FinanceFilters {
  from?: string;
  to?: string;
  direction?: 'in' | 'out' | '';
  category?: FinanceCategory | '';
  status?: FinanceStatus | '';
}

const cleanParams = (p: Record<string, unknown>) =>
  Object.fromEntries(Object.entries(p).filter(([, v]) => v !== '' && v !== undefined));

export interface WalletLedgerEntry {
  id: string;
  date: string;
  shopId: string;
  shopName: string;
  type: string;
  description: string;
  reference?: string | null;
  amount: number;
}

export const adminFinanceApi = {
  walletLedger: (params: { search?: string } = {}, page = 1, pageSize = 25) =>
    api.get('/admin/finance/wallet-ledger', { params: cleanParams({ ...params, page, pageSize }) })
      .then((r) => (r.data?.data ?? r.data) as { total: number; page: number; pageSize: number; balance: number; items: WalletLedgerEntry[] }),
  summary: (filters: Pick<FinanceFilters, 'from' | 'to'> = {}) =>
    api.get('/admin/finance/summary', { params: cleanParams(filters) }).then((r) => (r.data?.data ?? r.data) as FinanceSummary),
  transactions: (filters: FinanceFilters = {}, page = 1, pageSize = 25) =>
    api.get('/admin/finance/transactions', { params: cleanParams({ ...filters, page, pageSize }) })
      .then((r) => (r.data?.data ?? r.data) as { total: number; page: number; pageSize: number; items: FinanceEntry[] }),
};

// Public shop verification (no sign-in): the green SpazaSure Verified sticker
export interface ShopVerificationResult {
  shopNumber: string;
  shopName: string;
  area: string;
  verified: boolean;
  message: string;
  validUntil?: string | null;
  ratingAvg?: number | null;
  checkedAt: string;
}

export const publicShopsApi = {
  /** Uses a plain request so the page can show its own messages instead of error toasts. */
  verify: async (code: string): Promise<ShopVerificationResult> => {
    const res = await axios.get(`${BASE_URL}/public/shops/${encodeURIComponent(code.trim())}`, { timeout: env.apiTimeout });
    return res.data?.data ?? res.data;
  },
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

};

export const adminFaqApi = {
  /** Get all FAQs including inactive */
  list: () =>
    api.get('/admin/faqs').then((r) => r.data),

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

/** Projects & Impact module (admin-only jsonb record store) */
export const adminProjectsApi = {
  list: (): Promise<RawRecord[]> => api.get('/admin/projects/records').then((r) => r.data?.data ?? []),
  create: (kind: Kind, data: object): Promise<RawRecord> => api.post('/admin/projects/records', { kind, data }).then((r) => r.data?.data),
  createMany: (records: { kind: Kind; data: object }[]): Promise<RawRecord[]> => api.post('/admin/projects/records/bulk', { records }).then((r) => r.data?.data ?? []),
  update: (id: string, data: object): Promise<RawRecord> => api.put(`/admin/projects/records/${id}`, { data }).then((r) => r.data?.data),
  remove: (id: string): Promise<void> => api.delete(`/admin/projects/records/${id}`).then(() => undefined),
};
