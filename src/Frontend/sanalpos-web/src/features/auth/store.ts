import { create } from "zustand";
import { persist } from "zustand/middleware";
import axios from "axios";
import type { LoginResult, UserInfo } from "@/types/api";

// Access token sadece bellekte tutulur; refresh token localStorage'da persist edilir.
// Kart verisi hiçbir zaman store'a/storage'a yazılmaz (bkz. docs/09-frontend-react.md §7).

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: UserInfo | null;
  setSession: (result: LoginResult) => void;
  refresh: () => Promise<void>;
  logout: () => void;
  hasRole: (...roles: string[]) => boolean;
}

const baseURL = import.meta.env.VITE_API_BASE_URL || "/api/v1";

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      user: null,

      setSession: (result) =>
        set({
          accessToken: result.accessToken,
          refreshToken: result.refreshToken,
          user: result.user,
        }),

      refresh: async () => {
        const refreshToken = get().refreshToken;
        if (!refreshToken) throw new Error("Refresh token yok");
        // apiClient kullanılmaz: interceptor döngüsünü önlemek için çıplak axios.
        const { data } = await axios.post<LoginResult>(`${baseURL}/auth/refresh`, { refreshToken });
        get().setSession(data);
      },

      logout: () => set({ accessToken: null, refreshToken: null, user: null }),

      hasRole: (...roles) => {
        const userRoles = get().user?.roles ?? [];
        return roles.some((r) => userRoles.includes(r));
      },
    }),
    {
      name: "sanalpos-auth",
      partialize: (state) => ({ refreshToken: state.refreshToken, user: state.user }),
    }
  )
);
