import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { reportingApi } from "@/features/reporting/api";
import { reconciliationApi } from "@/features/reconciliation/api";
import { DailySummaryChart } from "@/features/reporting/components/DailySummaryChart";
import { useTransactions } from "@/features/payments/hooks";
import { TransactionTable } from "@/features/payments/components/TransactionTable";
import { Card, Spinner } from "@/shared/components/ui";
import { formatMoney } from "@/shared/lib/formatters";
import { useAuthStore } from "@/features/auth/store";

export function DashboardPage() {
  const { t } = useTranslation();
  const canSeeReports = useAuthStore((s) => s.hasRole("SystemAdmin", "MerchantAdmin"));
  const isSystemAdmin = useAuthStore((s) => s.hasRole("SystemAdmin"));

  const summary = useQuery({
    queryKey: ["daily-summary"],
    queryFn: () => reportingApi.dailySummary(),
    enabled: canSeeReports,
  });
  const reconciliation = useQuery({
    queryKey: ["reconciliation-history", "dashboard"],
    queryFn: () => reconciliationApi.history(5),
    enabled: isSystemAdmin,
  });
  const recent = useTransactions({ page: 1, pageSize: 10 });

  const lastRuns = reconciliation.data ?? [];
  const unbalancedCount = lastRuns.filter((r) => !r.isBalanced).length;

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">{t("nav.dashboard")}</h1>

      {canSeeReports && summary.data && (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard label="İşlem Adedi" value={String(summary.data.totalCount)} />
            <StatCard label="Onaylı" value={String(summary.data.approvedCount)} />
            <StatCard label="Günlük Ciro" value={formatMoney(summary.data.totalAmount)} />
            <StatCard label="Net Tutar" value={formatMoney(summary.data.totalNet)} />
          </div>
          <Card title="Günlük Özet">
            <DailySummaryChart summary={summary.data} />
          </Card>
        </>
      )}

      {isSystemAdmin && lastRuns.length > 0 && (
        <Link to="/reconciliation" className="block">
          <div
            className={`rounded-lg border p-4 shadow-sm ${
              unbalancedCount === 0 ? "border-green-200 bg-green-50" : "border-red-200 bg-red-50"
            }`}
          >
            <p className="text-xs font-medium uppercase tracking-wide text-gray-500">
              {t("reconciliation.lastRuns")}
            </p>
            <p className={`mt-1 text-sm font-semibold ${unbalancedCount === 0 ? "text-green-700" : "text-red-700"}`}>
              {unbalancedCount === 0
                ? t("reconciliation.allBalanced")
                : t("reconciliation.unbalancedSummary", { count: unbalancedCount })}
            </p>
            <p className="mt-1 text-xs text-gray-600">
              {lastRuns[0].day} · {lastRuns[0].providerCode} · {lastRuns[0].isBalanced
                ? t("reconciliation.balanced")
                : t("reconciliation.unbalanced")}
            </p>
          </div>
        </Link>
      )}

      <Card title={t("nav.payments")}>
        {recent.isLoading ? <Spinner /> : <TransactionTable transactions={recent.data?.items ?? []} />}
      </Card>
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-gray-500">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-gray-900">{value}</p>
    </div>
  );
}
