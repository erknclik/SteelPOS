import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { authApi } from "./api";
import { useAuthStore } from "./store";
import { toast } from "@/shared/components/Toast";
import { getErrorMessage } from "@/shared/lib/axiosClient";

export function useLogin() {
  const setSession = useAuthStore((s) => s.setSession);
  const navigate = useNavigate();

  return useMutation({
    mutationFn: ({ userName, password }: { userName: string; password: string }) =>
      authApi.login(userName, password),
    onSuccess: (result) => {
      setSession(result);
      navigate("/");
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  });
}

export function useLogout() {
  const { refreshToken, logout } = useAuthStore.getState();
  const navigate = useNavigate();

  return async () => {
    try {
      if (refreshToken) await authApi.logout(refreshToken);
    } finally {
      logout();
      navigate("/login");
    }
  };
}
