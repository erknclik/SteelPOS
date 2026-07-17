import { apiClient } from "@/shared/lib/axiosClient";
import type { ReconciliationResult } from "@/types/api";

export const reconciliationApi = {
  /** Gün sonu mutabakatını manuel tetikler (SystemAdmin). day: yyyy-MM-dd. */
  run: (day: string, providerCode?: string) =>
    apiClient
      .post<ReconciliationResult[]>("/reconciliation/run", {
        day,
        providerCode: providerCode || null,
      })
      .then((r) => r.data),
};
