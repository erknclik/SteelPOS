import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { reconciliationApi } from "@/features/reconciliation/api";
import { Button, Card, Field, Input, Table } from "@/shared/components/ui";
import { toast } from "@/shared/components/Toast";
import { getErrorMessage } from "@/shared/lib/axiosClient";
import { formatMoney } from "@/shared/lib/formatters";

function yesterdayIso(): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - 1);
  return d.toISOString().slice(0, 10);
}

/** Gün sonu mutabakatı: toplamlar bankaya (ISO 8583 0500 batch-close) gönderilir. */
export function ReconciliationPage() {
  const { t } = useTranslation();
  const [day, setDay] = useState(yesterdayIso());
  const [providerCode, setProviderCode] = useState("");

  const run = useMutation({
    mutationFn: () => reconciliationApi.run(day, providerCode || undefined),
    onSuccess: (results) => {
      if (results.length === 0) toast.success(t("reconciliation.noTransactions"));
      else if (results.every((r) => r.isBalanced)) toast.success(t("reconciliation.allBalanced"));
      else toast.error(t("reconciliation.outOfBalance"));
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  });

  return (
    <div className="max-w-5xl space-y-4">
      <h1 className="text-xl font-semibold">{t("nav.reconciliation")}</h1>

      <Card>
        <div className="flex flex-wrap items-end gap-3">
          <Field label={t("reconciliation.day")}>
            <Input type="date" value={day} max={new Date().toISOString().slice(0, 10)}
              onChange={(e) => setDay(e.target.value)} />
          </Field>
          <Field label={t("reconciliation.provider")}>
            <Input placeholder={t("reconciliation.allProviders")} value={providerCode}
              onChange={(e) => setProviderCode(e.target.value)} />
          </Field>
          <Button onClick={() => run.mutate()} disabled={run.isPending || !day}>
            {t("reconciliation.run")}
          </Button>
        </div>
      </Card>

      {run.data && run.data.length > 0 && (
        <Card title={t("reconciliation.results")}>
          <Table
            headers={[
              t("reconciliation.provider"),
              t("payments.currency"),
              t("reconciliation.sales"),
              t("reconciliation.refunds"),
              t("reconciliation.voids"),
              t("reconciliation.result"),
            ]}
          >
            {run.data.map((r) => (
              <tr key={`${r.providerCode}-${r.currency}`}>
                <td className="px-3 py-2 font-medium">{r.providerCode}</td>
                <td className="px-3 py-2">{r.currency}</td>
                <td className="px-3 py-2">
                  {r.saleCount} / {formatMoney(r.saleAmount, r.currency)}
                </td>
                <td className="px-3 py-2">
                  {r.refundCount} / {formatMoney(r.refundAmount, r.currency)}
                </td>
                <td className="px-3 py-2">
                  {r.voidCount} / {formatMoney(r.voidAmount, r.currency)}
                </td>
                <td className="px-3 py-2">
                  {r.isBalanced ? (
                    <span className="rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700">
                      {t("reconciliation.balanced")}
                    </span>
                  ) : (
                    <span
                      className="rounded-full bg-red-100 px-2 py-0.5 text-xs font-medium text-red-700"
                      title={r.reasonMessage ?? undefined}
                    >
                      {t("reconciliation.unbalanced")} ({r.reasonCode})
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </Table>
        </Card>
      )}

      {run.data && run.data.length === 0 && (
        <Card>
          <p className="py-4 text-center text-sm text-gray-500">{t("reconciliation.noTransactions")}</p>
        </Card>
      )}
    </div>
  );
}
