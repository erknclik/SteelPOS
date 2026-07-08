import { Navigate, Outlet, createBrowserRouter } from "react-router-dom";
import { useAuthStore } from "@/features/auth/store";
import { MainLayout } from "@/layouts/MainLayout";
import { AuthLayout } from "@/layouts/AuthLayout";
import { LoginPage } from "@/pages/LoginPage";
import { DashboardPage } from "@/pages/DashboardPage";
import { PaymentsPage } from "@/pages/PaymentsPage";
import { NewPaymentPage } from "@/pages/NewPaymentPage";
import { PaymentDetailPage } from "@/pages/PaymentDetailPage";
import { MerchantsPage } from "@/pages/MerchantsPage";
import { MerchantDetailPage } from "@/pages/MerchantDetailPage";
import { ReportsPage } from "@/pages/ReportsPage";
import { WebhooksPage } from "@/pages/WebhooksPage";
import { SettingsPage } from "@/pages/SettingsPage";
import { ForbiddenPage, NotFoundPage } from "@/pages/ErrorPages";

function RequireAuth() {
  const isAuthenticated = useAuthStore((s) => Boolean(s.accessToken || s.refreshToken));
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <Outlet />;
}

function RequireRole({ roles }: { roles: string[] }) {
  const hasRole = useAuthStore((s) => s.hasRole);
  if (!hasRole(...roles)) return <Navigate to="/403" replace />;
  return <Outlet />;
}

export const router = createBrowserRouter([
  {
    element: <AuthLayout />,
    children: [{ path: "/login", element: <LoginPage /> }],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <MainLayout />,
        children: [
          { path: "/", element: <DashboardPage /> },
          { path: "/payments", element: <PaymentsPage /> },
          { path: "/payments/new", element: <NewPaymentPage /> },
          { path: "/payments/:id", element: <PaymentDetailPage /> },
          {
            element: <RequireRole roles={["SystemAdmin"]} />,
            children: [{ path: "/merchants", element: <MerchantsPage /> }],
          },
          {
            element: <RequireRole roles={["SystemAdmin", "MerchantAdmin"]} />,
            children: [
              { path: "/merchants/:id", element: <MerchantDetailPage /> },
              { path: "/reports/daily", element: <ReportsPage /> },
              { path: "/webhooks", element: <WebhooksPage /> },
            ],
          },
          { path: "/settings", element: <SettingsPage /> },
        ],
      },
    ],
  },
  { path: "/403", element: <ForbiddenPage /> },
  { path: "*", element: <NotFoundPage /> },
]);
