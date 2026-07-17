// Backend DTO'larına karşılık gelen tipler (docs/08-api-tasarimi.md).
// İleri fazda OpenAPI şemasından otomatik üretim önerilir.

export interface LoginResult {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: UserInfo;
}

export interface UserInfo {
  id: string;
  userName: string;
  fullName: string;
  merchantId: string | null;
  roles: string[];
}

export interface PaymentResult {
  transactionId: string;
  status: string;
  bankAuthCode: string | null;
  commissionAmount: number;
  netAmount: number;
  completedAt: string | null;
}

export interface Transaction {
  id: string;
  merchantId: string;
  terminalId: string;
  orderReference: string;
  amount: number;
  currency: string;
  installmentCount: number;
  transactionType: string;
  status: string;
  maskedCardNumber: string;
  cardHolderName: string;
  bankAuthCode: string | null;
  bankRrn: string | null;
  bankProviderCode: string;
  commissionAmount: number;
  netAmount: number;
  refundedTotal: number;
  requestedAt: string;
  completedAt: string | null;
}

export interface ThreeDSInitiationResult {
  transactionId: string;
  requiresRedirect: boolean;
  md: string | null;
  acsUrl: string | null;
  paReq: string | null;
  payment: PaymentResult | null;
}

export interface ReconciliationResult {
  providerCode: string;
  currency: string;
  day: string;
  saleCount: number;
  saleAmount: number;
  refundCount: number;
  refundAmount: number;
  voidCount: number;
  voidAmount: number;
  isBalanced: boolean;
  reasonCode: string | null;
  reasonMessage: string | null;
}

export interface StatusHistoryEntry {
  oldStatus: string;
  newStatus: string;
  changedAt: string;
  changedBy: string;
}

export interface Merchant {
  id: string;
  name: string;
  taxNumber: string;
  iban: string;
  status: string;
  defaultCommissionRate: number;
  createdAt: string;
}

export interface Terminal {
  id: string;
  storeId: string;
  terminalCode: string;
  bankProviderCode: string;
  isActive: boolean;
}

export interface Store {
  id: string;
  merchantId: string;
  name: string;
  address: string | null;
}

export interface DailySummary {
  day: string;
  totalCount: number;
  approvedCount: number;
  declinedCount: number;
  totalAmount: number;
  totalCommission: number;
  totalNet: number;
  totalRefunded: number;
}

export interface WebhookSubscription {
  id: string;
  merchantId: string;
  eventType: string;
  targetUrl: string;
  isActive: boolean;
}
