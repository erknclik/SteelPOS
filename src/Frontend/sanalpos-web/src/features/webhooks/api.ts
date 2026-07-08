import { apiClient } from "@/shared/lib/axiosClient";
import type { WebhookSubscription } from "@/types/api";

export const webhooksApi = {
  list: (merchantId?: string) =>
    apiClient
      .get<WebhookSubscription[]>("/webhooks", { params: { merchantId } })
      .then((r) => r.data),

  create: (payload: { merchantId?: string; eventType: string; targetUrl: string; secret: string }) =>
    apiClient.post<WebhookSubscription>("/webhooks", payload).then((r) => r.data),

  remove: (id: string) => apiClient.delete(`/webhooks/${id}`),

  test: (id: string) => apiClient.post(`/webhooks/${id}/test`),
};
