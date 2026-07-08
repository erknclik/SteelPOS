import { apiClient } from "@/shared/lib/axiosClient";
import type { DailySummary } from "@/types/api";

export const reportingApi = {
  dailySummary: (day?: string, merchantId?: string) =>
    apiClient
      .get<DailySummary>("/reports/daily-summary", { params: { day, merchantId } })
      .then((r) => r.data),

  exportCsv: async (from?: string, to?: string) => {
    const response = await apiClient.get("/reports/export", {
      params: { from, to, format: "csv" },
      responseType: "blob",
    });
    const url = URL.createObjectURL(response.data as Blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `sanalpos-export-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  },
};
