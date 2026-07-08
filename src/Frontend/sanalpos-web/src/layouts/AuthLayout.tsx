import { Outlet } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ToastContainer } from "@/shared/components/Toast";

export function AuthLayout() {
  const { t } = useTranslation();

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-100 p-4">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h1 className="mb-6 text-center text-xl font-bold text-brand-700">{t("app.title")}</h1>
        <Outlet />
      </div>
      <ToastContainer />
    </div>
  );
}
