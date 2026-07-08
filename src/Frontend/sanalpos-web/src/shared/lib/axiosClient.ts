import axios from "axios";
import { useAuthStore } from "@/features/auth/store";

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "/api/v1",
  timeout: 15000,
});

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  config.headers["X-Correlation-Id"] = crypto.randomUUID();
  return config;
});

let refreshing: Promise<void> | null = null;

apiClient.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;
    if (error.response?.status === 401 && !original._retried) {
      original._retried = true;
      const { refreshToken, logout } = useAuthStore.getState();
      if (!refreshToken) {
        logout();
        return Promise.reject(error);
      }
      try {
        // Eşzamanlı 401'lerde tek refresh isteği yapılır.
        refreshing ??= useAuthStore.getState().refresh();
        await refreshing;
        refreshing = null;
        return apiClient.request(original);
      } catch {
        refreshing = null;
        logout();
      }
    }
    return Promise.reject(error);
  }
);

export function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as
      | { title?: string; errors?: Record<string, string[]> }
      | undefined;
    if (data?.errors) return Object.values(data.errors).flat().join(" ");
    if (data?.title) return data.title;
    return error.message;
  }
  return String(error);
}
