import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useMerchants } from "@/features/merchants/hooks";
import { Card, Spinner, StatusBadge, Table } from "@/shared/components/ui";
import { formatDate } from "@/shared/lib/formatters";

export function MerchantsPage() {
  const { t } = useTranslation();
  const { data, isLoading } = useMerchants();

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t("nav.merchants")}</h1>
      <Card>
        {isLoading ? (
          <Spinner />
        ) : (
          <Table headers={["Ad", "Vergi No", "IBAN", "Komisyon %", "Durum", "Kayıt"]}>
            {(data ?? []).map((m) => (
              <tr key={m.id} className="hover:bg-gray-50">
                <td className="px-3 py-2">
                  <Link to={`/merchants/${m.id}`} className="font-medium text-brand-600 hover:underline">
                    {m.name}
                  </Link>
                </td>
                <td className="px-3 py-2">{m.taxNumber}</td>
                <td className="px-3 py-2 font-mono text-xs">{m.iban}</td>
                <td className="px-3 py-2">{m.defaultCommissionRate}</td>
                <td className="px-3 py-2">
                  <StatusBadge status={m.status} />
                </td>
                <td className="px-3 py-2 text-gray-500">{formatDate(m.createdAt)}</td>
              </tr>
            ))}
          </Table>
        )}
      </Card>
    </div>
  );
}
