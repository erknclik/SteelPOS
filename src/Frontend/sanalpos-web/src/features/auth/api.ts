import { apiClient } from "@/shared/lib/axiosClient";
import type { LoginResult } from "@/types/api";

export const authApi = {
  login: (userName: string, password: string) =>
    apiClient.post<LoginResult>("/auth/login", { userName, password }).then((r) => r.data),

  logout: (refreshToken: string) => apiClient.post("/auth/logout", { refreshToken }),

  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post("/auth/change-password", { currentPassword, newPassword }),
};
