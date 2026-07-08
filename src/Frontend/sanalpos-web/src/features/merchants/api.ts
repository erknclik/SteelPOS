import { apiClient } from "@/shared/lib/axiosClient";
import type { Merchant, Store, Terminal } from "@/types/api";

export const merchantsApi = {
  list: (page = 1, pageSize = 50) =>
    apiClient.get<Merchant[]>("/merchants", { params: { page, pageSize } }).then((r) => r.data),

  getById: (id: string) => apiClient.get<Merchant>(`/merchants/${id}`).then((r) => r.data),

  getStores: (id: string) => apiClient.get<Store[]>(`/merchants/${id}/stores`).then((r) => r.data),

  getTerminals: (id: string) =>
    apiClient.get<Terminal[]>(`/merchants/${id}/terminals`).then((r) => r.data),

  create: (payload: { name: string; taxNumber: string; iban: string; defaultCommissionRate: number }) =>
    apiClient.post<Merchant>("/merchants", payload).then((r) => r.data),

  suspend: (id: string) => apiClient.post<Merchant>(`/merchants/${id}/suspend`).then((r) => r.data),
};
