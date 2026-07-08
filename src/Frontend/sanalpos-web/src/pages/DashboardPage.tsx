import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { reportingApi } from "@/features/reporting/api";
import { DailySummaryChart } from "@/features/reporting/components/DailySummaryChart";
import { useTransactions } from "@/features/payments/hooks";
import { TransactionTable } from "@/features/payments/components/TransactionTable";
import { Card, Spinner } from "@/shared/components/ui";
import { formatMoney } from "@/shared/lib/formatters";
import { useAuthStore } from "@/features/auth/store";

export function DashboardPage() {
  const { t } = useTranslation();
  const canSeeReports = useAuthStore((s) => s.hasRole("SystemAdmin", "MerchantAdmin"));

  const summary = useQuery({
    queryKey: ["daily-summary"],
    queryFn: () => reportingApi.dailySummary(),
    enabled: canSeeReports,
  });
  const recent = useTransactions({ page: 1, pageSize: 10 });

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
