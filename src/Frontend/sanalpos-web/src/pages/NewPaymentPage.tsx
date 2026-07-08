import { useState } from "react";
import { useTranslation } from "react-i18next";
import { PaymentForm } from "@/features/payments/components/PaymentForm";
import { useMerchants, useMerchantTerminals } from "@/features/merchants/hooks";
import { Card, Spinner } from "@/shared/components/ui";

export function NewPaymentPage() {
  const { t } = useTranslation();
  const merchants = useMerchants();
  const [selectedMerchantId, setSelectedMerchantId] = useState<string>();
  const terminals = useMerchantTerminals(selectedMerchantId);

  return (
    <div className="max-w-3xl space-y-4">
      <h1 className="text-xl font-semibold">{t("nav.newPayment")}</h1>
      <Card>
        {merchants.isLoading ? (
          <Spinner />
        ) : (
          <PaymentForm
            merchants={merchants.data ?? []}
            terminals={terminals.data ?? []}
            onMerchantChange={setSelectedMerchantId}
          />
        )}
      </Card>
    </div>
  );
}
