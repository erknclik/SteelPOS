import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslation } from "react-i18next";
import { Button, Field, Input, Select } from "@/shared/components/ui";
import { formatCardNumberInput } from "@/shared/lib/formatters";
import { createPaymentSchema, type CreatePaymentFormValues } from "../schemas/paymentSchema";
import { useCreatePayment } from "../hooks";
import type { Merchant, Terminal } from "@/types/api";

interface Props {
  merchants: Merchant[];
  terminals: Terminal[];
  onMerchantChange: (merchantId: string) => void;
}

export function PaymentForm({ merchants, terminals, onMerchantChange }: Props) {
  const { t } = useTranslation();
  const createPayment = useCreatePayment();

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

  const onSubmit = (values: CreatePaymentFormValues) => {
    createPayment.mutate(values, {
      // Kart bilgisi gönderim sonrası state'ten temizlenir (docs/09-frontend-react.md §7).
      onSettled: () => reset({ ...values, cardNumber: "", cvv: "", cardHolderName: "" }),
    });
  };

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

      <div className="sm:col-span-2">
        <Button type="submit" disabled={createPayment.isPending} className="w-full sm:w-auto">
          {t("payments.submit")}
        </Button>
      </div>
    </form>
  );
}
