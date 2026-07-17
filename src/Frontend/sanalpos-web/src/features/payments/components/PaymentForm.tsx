import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Button, Field, Input, Select } from "@/shared/components/ui";
import { toast } from "@/shared/components/Toast";
import { formatCardNumberInput } from "@/shared/lib/formatters";
import { createPaymentSchema, type CreatePaymentFormValues } from "../schemas/paymentSchema";
import { useCreatePayment, useInitiate3DS } from "../hooks";
import { AcsRedirect } from "./AcsRedirect";
import type { Merchant, Terminal } from "@/types/api";

interface Props {
  merchants: Merchant[];
  terminals: Terminal[];
  onMerchantChange: (merchantId: string) => void;
}

export function PaymentForm({ merchants, terminals, onMerchantChange }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const createPayment = useCreatePayment();
  const initiate3DS = useInitiate3DS();
  const [use3DSecure, setUse3DSecure] = useState(true);
  const [acsRedirect, setAcsRedirect] = useState<{ acsUrl: string; md: string; paReq: string } | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    formState: { errors },
  } = useForm<CreatePaymentFormValues>({
    resolver: zodResolver(createPaymentSchema),
    defaultValues: { currency: "TRY", installmentCount: 1 },
  });

  // Kart bilgisi gönderim sonrası state'ten temizlenir (docs/09-frontend-react.md §7).
  const clearCard = (values: CreatePaymentFormValues) =>
    reset({ ...values, cardNumber: "", cvv: "", cardHolderName: "" });

  const onSubmit = (values: CreatePaymentFormValues) => {
    if (!use3DSecure) {
      createPayment.mutate(values, { onSettled: () => clearCard(values) });
      return;
    }

    initiate3DS.mutate(values, {
      onSuccess: (data) => {
        if (data.requiresRedirect) {
          // Kart bilgisi artık gerekli değil; ACS'e yönlendirmeden önce temizlenir.
          clearCard(values);
          setAcsRedirect({ acsUrl: data.acsUrl!, md: data.md!, paReq: data.paReq! });
          return;
        }

        // Kart 3DS'e kayıtlı değil: işlem doğrudan sonuçlandı.
        if (data.payment?.status === "Approved") toast.success(t("payments.created"));
        else toast.error(t("threeDs.declined"));
        navigate(`/payments/${data.transactionId}`);
      },
      onSettled: () => clearCard(values),
    });
  };

  if (acsRedirect) return <AcsRedirect {...acsRedirect} />;

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="grid gap-4 sm:grid-cols-2">
      <Field label="Merchant" error={errors.merchantId?.message}>
        <Select {...register("merchantId", { onChange: (e) => onMerchantChange(e.target.value) })}>
          <option value="">Seçiniz</option>
          {merchants.map((m) => (
            <option key={m.id} value={m.id}>
              {m.name}
            </option>
          ))}
        </Select>
      </Field>

      <Field label="Terminal" error={errors.terminalId?.message}>
        <Select {...register("terminalId")}>
          <option value="">Seçiniz</option>
          {terminals.map((term) => (
            <option key={term.id} value={term.id}>
              {term.terminalCode}
            </option>
          ))}
        </Select>
      </Field>

      <Field label={t("payments.orderReference")} error={errors.orderReference?.message}>
        <Input {...register("orderReference")} />
      </Field>

      <div className="grid grid-cols-3 gap-2">
        <div className="col-span-2">
          <Field label={t("payments.amount")} error={errors.amount?.message}>
            <Input type="number" step="0.01" min="0" {...register("amount", { valueAsNumber: true })} />
          </Field>
        </div>
        <Field label={t("payments.currency")} error={errors.currency?.message}>
          <Select {...register("currency")}>
            <option value="TRY">TRY</option>
            <option value="USD">USD</option>
            <option value="EUR">EUR</option>
          </Select>
        </Field>
      </div>

      <Field label={t("payments.installments")} error={errors.installmentCount?.message}>
        <Select {...register("installmentCount", { valueAsNumber: true })}>
          {Array.from({ length: 12 }, (_, i) => i + 1).map((n) => (
            <option key={n} value={n}>
              {n === 1 ? "Peşin" : `${n} taksit`}
            </option>
          ))}
        </Select>
      </Field>

      <Field label={t("payments.cardHolder")} error={errors.cardHolderName?.message}>
        <Input autoComplete="off" {...register("cardHolderName")} />
      </Field>

      <Field label={t("payments.cardNumber")} error={errors.cardNumber?.message}>
        <Input
          inputMode="numeric"
          autoComplete="off"
          placeholder="0000 0000 0000 0000"
          {...register("cardNumber", {
            onChange: (e) => setValue("cardNumber", formatCardNumberInput(e.target.value)),
          })}
        />
      </Field>

      <div className="grid grid-cols-3 gap-2">
        <Field label="Ay" error={errors.expireMonth?.message}>
          <Input type="number" min={1} max={12} placeholder="MM" {...register("expireMonth", { valueAsNumber: true })} />
        </Field>
        <Field label="Yıl" error={errors.expireYear?.message}>
          <Input type="number" placeholder="YYYY" {...register("expireYear", { valueAsNumber: true })} />
        </Field>
        <Field label={t("payments.cvv")} error={errors.cvv?.message}>
          <Input type="password" inputMode="numeric" maxLength={4} autoComplete="off" {...register("cvv")} />
        </Field>
      </div>

      <div className="sm:col-span-2 flex flex-col gap-3 sm:flex-row sm:items-center">
        <Button type="submit" disabled={createPayment.isPending || initiate3DS.isPending} className="w-full sm:w-auto">
          {t("payments.submit")}
        </Button>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={use3DSecure}
            onChange={(e) => setUse3DSecure(e.target.checked)}
            className="h-4 w-4 rounded border-gray-300"
          />
          {t("threeDs.payWith3ds")}
        </label>
      </div>
    </form>
  );
}
