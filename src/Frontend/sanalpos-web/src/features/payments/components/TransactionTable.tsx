import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { StatusBadge, Table } from "@/shared/components/ui";
import { formatDate, formatMoney } from "@/shared/lib/formatters";
import type { Transaction } from "@/types/api";

export function TransactionTable({ transactions }: { transactions: Transaction[] }) {
  const { t } = useTranslation();

  if (transactions.length === 0) {
    return <p className="p-4 text-sm text-gray-500">{t("common.noData")}</p>;
  }

  return (
    <Table
      headers={[
        t("payments.orderReference"),
        t("payments.amount"),
        t("payments.installments"),
        t("payments.type"),
        t("payments.status"),
        t("payments.cardNumber"),
        t("payments.date"),
      ]}
    >
      {transactions.map((tx) => (
        <tr key={tx.id} className="hover:bg-gray-50">
          <td className="px-3 py-2">
            <Link to={`/payments/${tx.id}`} className="font-medium text-brand-600 hover:underline">
              {tx.orderReference}
            </Link>
          </td>
          <td className="px-3 py-2">{formatMoney(tx.amount, tx.currency)}</td>
          <td className="px-3 py-2">{tx.installmentCount}</td>
          <td className="px-3 py-2">{tx.transactionType}</td>
          <td className="px-3 py-2">
            <StatusBadge status={tx.status} />
          </td>
          <td className="px-3 py-2 font-mono text-xs">{tx.maskedCardNumber}</td>
          <td className="px-3 py-2 text-gray-500">{formatDate(tx.requestedAt)}</td>
        </tr>
      ))}
    </Table>
  );
}
