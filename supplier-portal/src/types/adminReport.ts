export type ReportStatus = 'submitted' | 'under_review' | 'escalated' | 'resolved' | 'dismissed';

export interface AdminReport {
  id: string;
  reportType?: string | null;
  barcode?: string | null;
  productName?: string | null;
  reporterName?: string | null;
  shopName?: string | null;
  isAnonymous: boolean;
  description: string;
  photoUrl?: string | null;
  status: ReportStatus;
  escalatedTo?: string | null;
  escalatedAt?: string | null;
  resolutionNote?: string | null;
  createdAt: string;
}
