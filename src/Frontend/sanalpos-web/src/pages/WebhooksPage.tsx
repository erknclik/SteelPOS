import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { webhooksApi } from "@/features/webhooks/api";
import { useAuthStore } from "@/features/auth/store";
import { Button, Card, Field, Input, Select, Spinner, StatusBadge, Table } from "@/shared/components/ui";
import { toast } from "@/shared/components/Toast";
import { getErrorMessage } from "@/shared/lib/axiosClient";

const eventTypes = ["PaymentCompleted", "PaymentFailed", "RefundCompleted"];

interface FormValues {
  eventType: string;
  targetUrl: string;
  secret: string;
}

export function WebhooksPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const merchantId = useAuthStore((s) => s.user?.merchantId ?? undefined);

  const { data, isLoading } = useQuery({
    queryKey: ["webhooks", merchantId],
    queryFn: () => webhooksApi.list(merchantId),
  });

  const { register, handleSubmit, reset, formState: { errors } } = useForm<FormValues>({
    defaultValues: { eventType: eventTypes[0] },
  });

  const create = useMutation({
    mutationFn: (values: FormValues) => webhooksApi.create({ ...values, merchantId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["webhooks"] });
      reset({ eventType: eventTypes[0], targetUrl: "", secret: "" });
      toast.success("Webhook aboneliği oluşturuldu.");
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  });

  const remove = useMutation({
    mutationFn: webhooksApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["webhooks"] }),
  });

  const test = useMutation({
    mutationFn: webhooksApi.test,
    onSuccess: () => toast.success("Test event'i kuyruğa alındı."),
    onError: (err) => toast.error(getErrorMessage(err)),
  });

  return (
    <div className="max-w-3xl space-y-4">
      <h1 className="text-xl font-semibold">{t("nav.webhooks")}</h1>

      <Card title="Yeni Abonelik">
        <form onSubmit={handleSubmit((v) => create.mutate(v))} className="grid gap-4 sm:grid-cols-3">
          <Field label="Event" error={errors.eventType?.message}>
            <Select {...register("eventType", { required: true })}>
              {eventTypes.map((e) => (
                <option key={e} value={e}>
                  {e}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Hedef URL (https)" error={errors.targetUrl?.message}>
            <Input placeholder="https://..." {...register("targetUrl", { required: "Zorunlu" })} />
          </Field>
          <Field label="Secret (min 16)" error={errors.secret?.message}>
            <Input type="password" {...register("secret", { required: "Zorunlu", minLength: { value: 16, message: "En az 16 karakter" } })} />
          </Field>
          <div className="sm:col-span-3">
            <Button type="submit" disabled={create.isPending}>{t("common.save")}</Button>
          </div>
        </form>
      </Card>

      <Card title="Abonelikler">
        {isLoading ? (
          <Spinner />
        ) : (
          <Table headers={["Event", "URL", "Durum", t("common.actions")]}>
            {(data ?? []).map((w) => (
              <tr key={w.id}>
                <td className="px-3 py-2">{w.eventType}</td>
                <td className="max-w-xs truncate px-3 py-2 font-mono text-xs">{w.targetUrl}</td>
                <td className="px-3 py-2">
                  <StatusBadge status={w.isActive ? "Active" : "Suspended"} />
                </td>
                <td className="px-3 py-2">
                  <div className="flex gap-2">
                    <Button variant="secondary" onClick={() => test.mutate(w.id)}>Test</Button>
                    <Button variant="danger" onClick={() => remove.mutate(w.id)}>{t("common.delete")}</Button>
                  </div>
                </td>
              </tr>
            ))}
          </Table>
        )}
      </Card>
    </div>
  );
}
