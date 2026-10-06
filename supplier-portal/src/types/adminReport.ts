export type ReportStatus = 'submitted' | 'under_review' | 'escalated' | 'resolved' | 'dismissed';

export interface AdminReport {
  id: string;
  reportType?: string | null;
  productId?: string | null;
  barcode?: string | null;
  productName?: string | null;
  reporterName?: string | null; // null when the reporter chose to stay anonymous
  shopName?: string | null;
  isAnonymous: boolean;
  description: string;
  photoUrl?: string | null;
  batchNumber?: string | null;
  expiryDate?: string | null;
  purchaseLocation?: string | null;
  supplierName?: string | null;
  status: ReportStatus;
  escalatedTo?: string | null;
  escalatedAt?: string | null;
  resolutionNote?: string | null;
  relatedReports: number;
  createdAt: string;
}
