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
  batchNumber?: string | null;
  serialCode?: string | null;
  shopAddress?: string | null;
  purchaseLocation?: string | null;
  purchasePrice?: number | null;
  scratchPanelIntact?: boolean | null;
  sealTampered?: boolean | null;
  latitude?: number | null;
  longitude?: number | null;
  reporterIp?: string | null;
  deviceId?: string | null;
  status: ReportStatus;
  escalatedTo?: string | null;
  escalatedAt?: string | null;
  resolutionNote?: string | null;
  createdAt: string;
}
