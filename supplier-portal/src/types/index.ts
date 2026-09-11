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
}

// Category
export interface Category {
  id: string;
  name: string;
  iconName: string;
}

// Order
export type OrderStatus = 'pending' | 'confirmed' | 'processing' | 'dispatched' | 'delivered' | 'cancelled' | 'disputed';
export type PaymentStatus = 'initiated' | 'pending' | 'held' | 'released' | 'failed' | 'refunded';

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
  deliveryFee: number;
  platformCommission: number;
  total: number;
  status: OrderStatus;
  deliveryOption: 'standard' | 'express' | 'pickup';
  paymentMethod: string;
  paymentStatus: PaymentStatus;
  createdAt: string;
  estimatedDelivery?: string;
  groupBuyId?: string;
  isGroupBuy: boolean;
  deliveryDistanceKm?: number;
  deliveryLatitude?: number;
  deliveryLongitude?: number;
}

// Group Buy
export type GroupBuyFilter = 'all' | 'active' | 'completed';

export interface GroupBuyApprovalProduct {
  groupBuyProductId: string;
  discountPct: number;
}

export interface GroupBuyApprovalRequest {
  products: GroupBuyApprovalProduct[];
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
  supplierId: string;
  supplierName: string;
  createdByShopName: string;
  expiresAt: string;
  status: string;
  participantCount: number;
  createdAt: string;
  products: GroupBuyProduct[];
  participants: GroupBuyParticipant[];
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
  status: 'active' | 'cancelled' | 'expired' | 'pending';
  startDate: string;
  endDate?: string;
  nextBillingDate?: string;
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
