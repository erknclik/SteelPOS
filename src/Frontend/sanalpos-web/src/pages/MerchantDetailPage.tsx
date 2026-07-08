import { useParams } from "react-router-dom";
import { useMerchant, useMerchantStores, useMerchantTerminals } from "@/features/merchants/hooks";
import { Card, Spinner, StatusBadge, Table } from "@/shared/components/ui";

export function MerchantDetailPage() {
  const { id } = useParams();
  const { data: merchant, isLoading } = useMerchant(id);
  const { data: stores } = useMerchantStores(id);
  const { data: terminals } = useMerchantTerminals(id);

  if (isLoading || !merchant) return <Spinner />;

  return (
    <div className="max-w-3xl space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{merchant.name}</h1>
        <StatusBadge status={merchant.status} />
      </div>

      <Card>
        <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs font-medium uppercase text-gray-500">Vergi No</dt>
            <dd>{merchant.taxNumber}</dd>
          </div>
          <div>
            <dt className="text-xs font-medium uppercase text-gray-500">IBAN</dt>
            <dd className="font-mono text-xs">{merchant.iban}</dd>
          </div>
          <div>
            <dt className="text-xs font-medium uppercase text-gray-500">Varsayılan Komisyon</dt>
            <dd>%{merchant.defaultCommissionRate}</dd>
          </div>
        </dl>
      </Card>

      <Card title="Mağazalar">
        <Table headers={["Ad", "Adres"]}>
          {(stores ?? []).map((s) => (
            <tr key={s.id}>
              <td className="px-3 py-2">{s.name}</td>
              <td className="px-3 py-2 text-gray-500">{s.address ?? "-"}</td>
            </tr>
          ))}
        </Table>
      </Card>

      <Card title="Terminaller">
        <Table headers={["Kod", "Banka", "Durum"]}>
          {(terminals ?? []).map((term) => (
            <tr key={term.id}>
              <td className="px-3 py-2 font-mono text-xs">{term.terminalCode}</td>
              <td className="px-3 py-2">{term.bankProviderCode}</td>
              <td className="px-3 py-2">
                <StatusBadge status={term.isActive ? "Active" : "Suspended"} />
              </td>
            </tr>
          ))}
        </Table>
      </Card>
    </div>
  );
}
