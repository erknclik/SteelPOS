import { Navigate } from "react-router-dom";
import { LoginForm } from "@/features/auth/components/LoginForm";
import { useAuthStore } from "@/features/auth/store";

export function LoginPage() {
  const isAuthenticated = useAuthStore((s) => Boolean(s.accessToken));
  if (isAuthenticated) return <Navigate to="/" replace />;
  return <LoginForm />;
}
