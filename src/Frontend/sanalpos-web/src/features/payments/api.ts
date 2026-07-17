import { apiClient } from "@/shared/lib/axiosClient";
import type { PaymentResult, StatusHistoryEntry, ThreeDSInitiationResult, Transaction } from "@/types/api";
import type { CreatePaymentFormValues } from "./schemas/paymentSchema";

/** API base'ini mutlak URL'e çevirir (VITE_API_BASE_URL relatif olabilir). */
function apiAbsoluteBase(): URL {
  return new URL(apiClient.defaults.baseURL || "/api/v1", window.location.origin);
}

/**
 * ACS'in dönüşte form-post edeceği TermUrl: API'nin complete endpoint'i + tarayıcıyı
 * SPA sonuç sayfasına taşıyacak returnUrl (backend prefix allowlist ile doğrular).
 */
export function build3DSTermUrl(): string {
  const base = apiAbsoluteBase().toString().replace(/\/$/, "");
  const returnUrl = `${window.location.origin}/payments/3ds/result`;
  return `${base}/payments/3ds/complete?returnUrl=${encodeURIComponent(returnUrl)}`;
}

/** Relatif dönebilen acsUrl'i API origin'ine göre mutlaklaştırır. */
export function resolveAcsUrl(acsUrl: string): string {
  return new URL(acsUrl, apiAbsoluteBase()).toString();
}

export interface TransactionFilters {
  merchantId?: string;
  status?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export const paymentsApi = {
  create: (values: CreatePaymentFormValues) =>
    apiClient
      .post<PaymentResult>("/payments", values, {
        // Çift çekim koruması: her form gönderimi tek bir idempotency key taşır.
        headers: { "Idempotency-Key": crypto.randomUUID() },
      })
      .then((r) => r.data),

  list: (filters: TransactionFilters) =>
    apiClient
      .get<Transaction[]>("/payments", { params: filters })
      .then((r) => ({
        items: r.data,
        totalCount: Number(r.headers["x-total-count"] ?? r.data.length),
      })),

  getById: (id: string) => apiClient.get<Transaction>(`/payments/${id}`).then((r) => r.data),

  getStatusHistory: (id: string) =>
    apiClient.get<StatusHistoryEntry[]>(`/payments/${id}/status-history`).then((r) => r.data),

  initiate3DS: (values: CreatePaymentFormValues) =>
    apiClient
      .post<ThreeDSInitiationResult>(
        "/payments/3ds",
        { ...values, callbackUrl: build3DSTermUrl() },
        { headers: { "Idempotency-Key": crypto.randomUUID() } }
      )
      .then((r) => r.data),

  refund: (id: string, amount: number, reason?: string) =>
    apiClient.post(`/payments/${id}/refund`, { amount, reason }).then((r) => r.data),

  voidPayment: (id: string) => apiClient.post(`/payments/${id}/void`).then((r) => r.data),

  capture: (id: string) => apiClient.post<PaymentResult>(`/payments/${id}/capture`).then((r) => r.data),
};
