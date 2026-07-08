import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { reportingApi } from "@/features/reporting/api";
import { DailySummaryChart } from "@/features/reporting/components/DailySummaryChart";
import { Button, Card, Input, Spinner } from "@/shared/components/ui";
import { formatMoney } from "@/shared/lib/formatters";

export function ReportsPage() {
  const { t } = useTranslation();
  const [day, setDay] = useState(() => new Date().toISOString().slice(0, 10));

  const { data, isLoading } = useQuery({
    queryKey: ["daily-summary", day],
    queryFn: () => reportingApi.dailySummary(day),
  });

  return (
    <div className="max-w-3xl space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-xl font-semibold">{t("nav.reports")}</h1>
        <div className="flex items-center gap-2">
          <Input type="date" value={day} onChange={(e) => setDay(e.target.value)} className="w-40" />
          <Button variant="secondary" onClick={() => reportingApi.exportCsv(day, day)}>
            CSV İndir
          </Button>
        </div>
      </div>

      {isLoading || !data ? (
        <Spinner />
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-3">
            <Stat label="Toplam İşlem" value={String(data.totalCount)} />
            <Stat label="Onaylı / Reddedilen" value={`${data.approvedCount} / ${data.declinedCount}`} />
            <Stat label="Ciro" value={formatMoney(data.totalAmount)} />
          </div>
          <Card title="Günlük Mutabakat Özeti">
            <DailySummaryChart summary={data} />
          </Card>
        </>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-gray-500">{label}</p>
      <p className="mt-1 text-xl font-semibold">{value}</p>
    </div>
  );
}
