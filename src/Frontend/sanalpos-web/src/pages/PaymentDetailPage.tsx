import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useCapture, useRefund, useStatusHistory, useTransaction, useVoid } from "@/features/payments/hooks";
import { Button, Card, Spinner, StatusBadge, Table } from "@/shared/components/ui";
import { formatDate, formatMoney } from "@/shared/lib/formatters";
import { useAuthStore } from "@/features/auth/store";

export function PaymentDetailPage() {
  const { t } = useTranslation();
  const { id = "" } = useParams();
  const { data: tx, isLoading } = useTransaction(id);
  const { data: history } = useStatusHistory(id);
  const refund = useRefund(id);
  const voidPayment = useVoid(id);
  const capture = useCapture(id);
  const canRefund = useAuthStore((s) => s.hasRole("SystemAdmin", "MerchantAdmin"));

  if (isLoading || !tx) return <Spinner />;

  const remaining = tx.amount - tx.refundedTotal;

  const onRefund = () => {
    const input = window.prompt(`İade tutarı (kalan: ${remaining}):`, String(remaining));
    if (!input) return;
    const amount = Number(input.replace(",", "."));
    if (Number.isNaN(amount) || amount <= 0) return;
    refund.mutate({ amount });
  };

  return (
    <div className="max-w-3xl space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{tx.orderReference}</h1>
        <StatusBadge status={tx.status} />
      </div>

      <Card>
        <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
          <Item label={t("payments.amount")} value={formatMoney(tx.amount, tx.currency)} />
          <Item label={t("payments.installments")} value={String(tx.installmentCount)} />
          <Item label={t("payments.type")} value={tx.transactionType} />
          <Item label={t("payments.cardNumber")} value={tx.maskedCardNumber} mono />
          <Item label={t("payments.cardHolder")} value={tx.cardHolderName} />
          <Item label="Banka Onay Kodu" value={tx.bankAuthCode ?? "-"} mono />
          <Item label="Banka Referansı (RRN)" value={tx.bankRrn ?? "-"} mono />
          <Item label={t("payments.commission")} value={formatMoney(tx.commissionAmount, tx.currency)} />
          <Item label={t("payments.net")} value={formatMoney(tx.netAmount, tx.currency)} />
          <Item label="İade Edilen" value={formatMoney(tx.refundedTotal, tx.currency)} />
          <Item label={t("payments.date")} value={formatDate(tx.requestedAt)} />
        </dl>

        <div className="mt-5 flex gap-2">
          {tx.transactionType === "PreAuth" && tx.status === "Approved" && (
            <Button onClick={() => capture.mutate()} disabled={capture.isPending}>
              {t("payments.capture")}
            </Button>
          )}
          {canRefund && remaining > 0 && ["Approved", "PartiallyRefunded"].includes(tx.status) && (
            <Button variant="secondary" onClick={onRefund} disabled={refund.isPending}>
              {t("payments.refund")}
            </Button>
          )}
          {tx.status === "Approved" && (
            <Button variant="danger" onClick={() => voidPayment.mutate()} disabled={voidPayment.isPending}>
              {t("payments.void")}
            </Button>
          )}
        </div>
      </Card>

      <Card title={t("payments.statusHistory")}>
        <Table headers={["Eski", "Yeni", "Tarih", "Kim"]}>
          {(history ?? []).map((h, i) => (
            <tr key={i}>
              <td className="px-3 py-2">
                <StatusBadge status={h.oldStatus} />
              </td>
              <td className="px-3 py-2">
                <StatusBadge status={h.newStatus} />
              </td>
              <td className="px-3 py-2 text-gray-500">{formatDate(h.changedAt)}</td>
              <td className="px-3 py-2">{h.changedBy}</td>
            </tr>
          ))}
        </Table>
      </Card>
    </div>
  );
}

function Item({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase tracking-wide text-gray-500">{label}</dt>
      <dd className={mono ? "font-mono" : ""}>{value}</dd>
    </div>
  );
}
