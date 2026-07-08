import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useTransactions } from "@/features/payments/hooks";
import { TransactionTable } from "@/features/payments/components/TransactionTable";
import { Button, Card, Select, Spinner } from "@/shared/components/ui";

const statuses = ["", "Pending", "Approved", "Declined", "Reversed", "Refunded", "PartiallyRefunded"];

export function PaymentsPage() {
  const { t } = useTranslation();
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data, isLoading } = useTransactions({ status: status || undefined, page, pageSize });
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / pageSize)) : 1;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t("nav.payments")}</h1>
        <div className="w-44">
          <Select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
            {statuses.map((s) => (
              <option key={s} value={s}>
                {s || "Tüm durumlar"}
              </option>
            ))}
          </Select>
        </div>
      </div>

      <Card>
        {isLoading ? <Spinner /> : <TransactionTable transactions={data?.items ?? []} />}
        <div className="mt-4 flex items-center justify-between text-sm text-gray-600">
          <span>
            {t("common.total")}: {data?.totalCount ?? 0}
          </span>
          <div className="flex gap-2">
            <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              ‹
            </Button>
            <span className="self-center">
              {page} / {totalPages}
            </span>
            <Button variant="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
              ›
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
}
