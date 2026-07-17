import { NavLink, Outlet } from "react-router-dom";
import { useTranslation } from "react-i18next";
import clsx from "clsx";
import { useAuthStore } from "@/features/auth/store";
import { useLogout } from "@/features/auth/hooks";
import { ToastContainer } from "@/shared/components/Toast";

const navItems = [
  { to: "/", key: "dashboard", roles: undefined },
  { to: "/payments", key: "payments", roles: undefined },
  { to: "/payments/new", key: "newPayment", roles: undefined },
  { to: "/merchants", key: "merchants", roles: ["SystemAdmin"] },
  { to: "/reconciliation", key: "reconciliation", roles: ["SystemAdmin"] },
  { to: "/reports/daily", key: "reports", roles: ["SystemAdmin", "MerchantAdmin"] },
  { to: "/webhooks", key: "webhooks", roles: ["SystemAdmin", "MerchantAdmin"] },
  { to: "/settings", key: "settings", roles: undefined },
] as const;

export function MainLayout() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const hasRole = useAuthStore((s) => s.hasRole);
  const logout = useLogout();

  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-56 flex-col border-r border-gray-200 bg-white p-4 sm:flex">
        <h1 className="mb-6 text-lg font-bold text-brand-700">{t("app.title")}</h1>
        <nav className="flex flex-1 flex-col gap-1">
          {navItems
            .filter((item) => !item.roles || hasRole(...item.roles))
            .map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === "/" || item.to === "/payments"}
                className={({ isActive }) =>
                  clsx(
                    "rounded-md px-3 py-2 text-sm font-medium",
                    isActive ? "bg-brand-50 text-brand-700" : "text-gray-600 hover:bg-gray-100"
                  )
                }
              >
                {t(`nav.${item.key}`)}
              </NavLink>
            ))}
        </nav>
        <div className="border-t border-gray-200 pt-3 text-sm">
          <p className="mb-2 truncate font-medium text-gray-700">{user?.fullName}</p>
          <button onClick={logout} className="text-red-600 hover:underline">
            {t("nav.logout")}
          </button>
        </div>
      </aside>
      <main className="flex-1 p-4 sm:p-6">
        <Outlet />
      </main>
      <ToastContainer />
    </div>
  );
}
