import { apiClient } from "@/shared/lib/axiosClient";
import type { PaymentResult, StatusHistoryEntry, Transaction } from "@/types/api";
import type { CreatePaymentFormValues } from "./schemas/paymentSchema";

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

  refund: (id: string, amount: number, reason?: string) =>
    apiClient.post(`/payments/${id}/refund`, { amount, reason }).then((r) => r.data),

  voidPayment: (id: string) => apiClient.post(`/payments/${id}/void`).then((r) => r.data),
};
