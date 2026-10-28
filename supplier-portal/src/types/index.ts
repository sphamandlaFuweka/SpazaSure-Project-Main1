// Auth
export type UserRole = 'supplier' | 'admin';

export interface AuthUser {
  id: string;
  email: string;
  companyName: string;
  role: UserRole;
  tier: 'basic' | 'bronze' | 'silver' | 'gold';
  isVerified: boolean;
  logoUrl?: string;
  token: string;
  refreshToken: string;
  tokenExpiresAt: string;
}

// Product
export interface Product {
  id: string;
  name: string;
  description: string;
  sku: string;
  price: number;
  discountPrice?: number;
  imageUrl: string;
  images: string[];
  categoryId: string;
  categoryName: string;
  supplierId: string;
  supplierName: string;
  stockQuantity: number;
  minOrderQty: number;
  barcode: string;
  /** @deprecated Legacy alias for the EAN barcode. Use barcode instead. */
  qrCode: string;
  hasQrCode: boolean;
  isAvailable: boolean;
  unit?: string;
  isFoodItem?: boolean;
  status: 'draft' | 'pending_approval' | 'active' | 'archived';
  rating: number;
  reviewCount: number;
  createdAt: string;
}

export interface ProductQrCodeData {
  productId: string;
  productName: string;
  sku: string;
  supplierName: string;
  supplierStatus: string;
  supplierVerified: boolean;
  productApproved: boolean;
  isVerified: boolean;
  qrToken: string;
  qrSvg: string;
  batchNumber?: string | null;
  expiryDate?: string | null;
  isRecalled: boolean;
  barcode: string;
  barcodeSvg: string;
}

/** Product page information: specifications, nutrition, ingredients and counterfeit checks. */
export interface ProductInfo {
  brand: string;
  packSize: string;
  countryOfOrigin: string;
  manufacturer: string;
  ingredients: string;
  allergens: string[];
  servingSize: string;
  servingsPerPack: string;
  nutrition: { nutrient: string; per100: string; perServing: string }[];
  usageInstructions: string;
  storageInstructions: string;
  warnings: string;
  specifications: { label: string; value: string }[];
  authenticityTips: string[];
}

export const emptyProductInfo = (): ProductInfo => ({
  brand: '', packSize: '', countryOfOrigin: '', manufacturer: '', ingredients: '', allergens: [],
  servingSize: '', servingsPerPack: '', nutrition: [], usageInstructions: '', storageInstructions: '',
  warnings: '', specifications: [], authenticityTips: [],
});

export interface ProductFormData {
  name: string;
  description: string;
  sku: string;
  price: number;
  discountPrice?: number;
  categoryId: string;
  stockQuantity: number;
  minOrderQty: number;
  imageUrl: string;
  /** The barcode printed on the pack. Empty means it has none, so SpazaSure makes one. */
  barcode?: string;
  isFoodItem?: boolean;
  /** Sent only when the form edited it; otherwise the server keeps what it has. */
  info?: ProductInfo;
}

// Category
export interface Category {
  id: string;
  name: string;
  iconName: string;
}

// Order
/** ready: packed by the supplier for SpazaSure to collect. dispatched: collected by SpazaSure, out for delivery. */
export type OrderStatus = 'pending' | 'confirmed' | 'processing' | 'ready' | 'dispatched' | 'delivered' | 'cancelled' | 'disputed';
export type PaymentStatus = 'initiated' | 'pending' | 'paid' | 'held' | 'released' | 'failed' | 'refunded';

export interface OrderItem {
  productId: string;
  productName: string;
  productImage: string;
  quantity: number;
  /** @deprecated Use unitPrice so approved group-buy pricing remains explicit. */
  price: number;
  originalUnitPrice?: number;
  unitPrice?: number;
  discountPct?: number;
  discountAmount?: number;
  originalLineTotal?: number;
  discountLineAmount?: number;
  lineTotal?: number;
}

export interface Order {
  id: string;
  orderNumber: string;
  shopId: string;
  shopName: string;
  shopAddress: string;
  items: OrderItem[];
  originalSubtotal?: number;
  totalDiscount?: number;
  subtotal: number;
  deliveryFee?: number;
  platformCommission?: number;
  total: number;
  status: OrderStatus;
  deliveryOption: 'standard' | 'express' | 'pickup';
  paymentMethod: string;
  paymentStatus: PaymentStatus;
  directPaystackSplit?: boolean;
  createdAt: string;
  estimatedDelivery?: string;
  groupBuyId?: string;
  isGroupBuy: boolean;
  deliveryDistanceKm?: number;
  deliveryLatitude?: number;
  deliveryLongitude?: number;
  /** The branch (warehouse) the order ships from, or "Head office". */
  shipsFrom?: string;
  /** The driver SpazaSure allocated to deliver it. */
  driverName?: string | null;
}

export interface DeliveryDispute {
  id: string;
  orderId: string;
  orderNumber: string;
  shopName: string;
  issueType: string;
  description: string;
  status: string;
  createdAt: string;
  resolution?: string | null;
  resolutionNote?: string | null;
  resolvedAt?: string | null;
  refundReference?: string | null;
  refundStatus?: string | null;
}

export interface SupplierRating {
  id: string;
  orderId: string;
  orderNumber: string;
  shopName: string;
  deliveryRating: number;
  supplierRating: number;
  comment: string;
  createdAt: string;
}

// Group Buy
/** Admin list filters. */
export type GroupBuyFilter = 'all' | 'active' | 'pending' | 'completed' | 'rejected' | 'cancelled';
/** Supplier list filters: requests are campaigns with a product at target, waiting for the supplier to accept. */
export type SupplierGroupBuyFilter = 'all' | 'requests' | 'active' | 'to_dispatch' | 'completed' | 'cancelled';

export interface NewGroupBuyRequest {
  supplierId?: string;
  title: string;
  description?: string;
  bannerImageUrl?: string;
  durationDays: number;
  products: { productId: string; targetQty: number; discountPct: number }[];
  /** The branch (warehouse) that supplies the campaign; omitted for the head office. */
  supplierBranchId?: string;
  /** Shops further than this from the branch can't see or join the campaign. */
  areaRadiusKm?: number;
}

/** A supplier warehouse or depot, e.g. its Durban, Johannesburg and Cape Town warehouses. */
export interface SupplierBranch {
  id: string;
  name: string;
  address: string;
  city: string;
  province: string;
  postalCode?: string | null;
  latitude: number;
  longitude: number;
  isActive: boolean;
}

/** Where shops must be to join a group buy. */
export interface GroupBuyArea {
  supplierBranchId?: string | null;
  branchName: string;
  city?: string | null;
  province?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  radiusKm: number;
  /** e.g. "shops within 40 km of Durban, KwaZulu-Natal" */
  description: string;
}

export interface GroupBuyProduct {
  id: string;
  productId: string;
  productName: string;
  originalPrice: number;
  discountPrice: number;
  discountPct: number;
  targetQty: number;
  currentQty: number;
  participantCount: number;
  progress: number;
  status: string;
}

export interface GroupBuyParticipantItem {
  groupBuyProductId: string;
  productId: string;
  productName: string;
  quantity: number;
  status: string;
}

export interface GroupBuyParticipant {
  id: string;
  shopId: string;
  shopName: string;
  status: string;
  items: GroupBuyParticipantItem[];
}

export interface GroupBuy {
  id: string;
  title: string;
  description: string;
  bannerImageUrl?: string | null;
  supplierId: string;
  supplierName: string;
  createdByShopName: string;
  createdByRole?: 'supplier' | 'admin' | 'shop';
  /** Null for campaigns from before group buys had an area. */
  area?: GroupBuyArea | null;
  rejectionNote?: string | null;
  approvedAt?: string | null;
  expiresAt: string;
  status: string;
  participantCount: number;
  createdAt: string;
  products: GroupBuyProduct[];
  participants: GroupBuyParticipant[];
  /** Orders created when the supplier accepted the request. */
  orders?: GroupBuyOrder[] | null;
}

export interface GroupBuyOrder {
  id: string;
  orderNumber: string;
  shopId: string;
  shopName: string;
  status: string;
  paymentStatus: string;
  subtotal: number;
}

// Analytics
export interface AnalyticsSummary {
  totalRevenue: number;
  totalOrders: number;
  activeProducts: number;
  avgOrderValue: number;
  revenueChange: number;
  ordersChange: number;
}

export interface RevenueDataPoint {
  month: string;
  revenue: number;
  orders: number;
}

export interface TopProduct {
  productId: string;
  productName: string;
  totalSold: number;
  revenue: number;
}

// Supplier Profile
export interface SupplierProfile {
  id: string;
  companyName: string;
  registrationNumber: string;
  taxNumber: string;
  contactName: string;
  email: string;
  phone: string;
  address: string;
  city?: string;
  province?: string;
  postalCode?: string;
  tier: 'basic' | 'bronze' | 'silver' | 'gold';
  isVerified: boolean;
  logoUrl?: string;
  bankName: string;
  bankAccount: string;
  bankBranchCode: string;
  latitude?: number;
  longitude?: number;
  documents: ComplianceDoc[];
}

export interface ComplianceDoc {
  id: string;
  docType: 'cipc_certificate' | 'tax_clearance' | 'bee_certificate' | 'product_license';
  docUrl: string;
  status: 'pending' | 'approved' | 'rejected';
  expiryDate?: string;
  rejectionReason?: string;
}

// Pagination
export interface PaginatedResponse<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
}

// Subscription
export interface SubscriptionPlan {
  id: string;
  tier: 'basic' | 'bronze' | 'silver' | 'gold';
  name: string;
  description: string;
  monthlyPrice: number;
  annualPrice: number;
  commissionRate: number;
  maxListings: number;
  maxOrders: number;
  hasAnalytics: boolean;
  hasPrioritySupport: boolean;
  hasBulkPricing: boolean;
  hasApiAccess: boolean;
  hasCustomBranding: boolean;
  sortOrder: number;
}

export interface SubscriptionRecord {
  id: string;
  billingCycle: 'monthly' | 'annual';
  status: 'active' | 'past_due' | 'unpaid' | 'cancelled' | 'expired' | 'pending';
  startDate: string;
  endDate?: string;
  nextBillingDate?: string;
  cancelAtPeriodEnd?: boolean;
  amountPaid: number;
  paymentMethod?: string;
  paymentReference?: string;
}

export interface CurrentSubscription {
  currentTier: 'basic' | 'bronze' | 'silver' | 'gold';
  commissionRate: number;
  subscription?: SubscriptionRecord;
  plan?: SubscriptionPlan;
}

export interface SubscribeRequest {
  tier: string;
  billingCycle?: 'monthly' | 'annual';
  paymentMethod?: string;
  paymentReference?: string;
  confirmPayment?: boolean;
}
