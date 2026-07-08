import { useForm } from "react-hook-form";
import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { authApi } from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/store";
import { Button, Card, Field, Input } from "@/shared/components/ui";
import { toast } from "@/shared/components/Toast";
import { getErrorMessage } from "@/shared/lib/axiosClient";
import i18n from "@/i18n";

interface FormValues {
  currentPassword: string;
  newPassword: string;
  newPasswordConfirm: string;
}

export function SettingsPage() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);

  const { register, handleSubmit, reset, watch, formState: { errors } } = useForm<FormValues>();

  const changePassword = useMutation({
    mutationFn: (v: FormValues) => authApi.changePassword(v.currentPassword, v.newPassword),
    onSuccess: () => {
      reset();
      toast.success("Parola değiştirildi. Diğer oturumlar sonlandırıldı.");
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  });

  return (
    <div className="max-w-lg space-y-4">
      <h1 className="text-xl font-semibold">{t("nav.settings")}</h1>

      <Card title="Hesap">
        <dl className="grid gap-3 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-xs font-medium uppercase text-gray-500">{t("auth.userName")}</dt>
            <dd>{user?.userName}</dd>
          </div>
          <div>
            <dt className="text-xs font-medium uppercase text-gray-500">Roller</dt>
            <dd>{user?.roles.join(", ")}</dd>
          </div>
        </dl>
        <div className="mt-4">
          <span className="mr-2 text-sm text-gray-600">Dil:</span>
          <Button variant="secondary" onClick={() => i18n.changeLanguage(i18n.language === "tr" ? "en" : "tr")}>
            {i18n.language === "tr" ? "English" : "Türkçe"}
          </Button>
        </div>
      </Card>

      <Card title="Parola Değiştir">
        <form onSubmit={handleSubmit((v) => changePassword.mutate(v))} className="space-y-4">
          <Field label="Mevcut Parola" error={errors.currentPassword?.message}>
            <Input type="password" autoComplete="current-password" {...register("currentPassword", { required: "Zorunlu" })} />
          </Field>
          <Field label="Yeni Parola" error={errors.newPassword?.message}>
            <Input
              type="password"
              autoComplete="new-password"
              {...register("newPassword", {
                required: "Zorunlu",
                minLength: { value: 10, message: "En az 10 karakter" },
              })}
            />
          </Field>
          <Field label="Yeni Parola (Tekrar)" error={errors.newPasswordConfirm?.message}>
            <Input
              type="password"
              autoComplete="new-password"
              {...register("newPasswordConfirm", {
                validate: (v) => v === watch("newPassword") || "Parolalar eşleşmiyor",
              })}
            />
          </Field>
          <Button type="submit" disabled={changePassword.isPending}>
            {t("common.save")}
          </Button>
        </form>
      </Card>
    </div>
  );
}
