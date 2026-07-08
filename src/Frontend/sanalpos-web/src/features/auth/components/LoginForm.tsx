import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslation } from "react-i18next";
import { Button, Field, Input } from "@/shared/components/ui";
import { useLogin } from "../hooks";

const loginSchema = z.object({
  userName: z.string().min(1),
  password: z.string().min(1),
});

type LoginValues = z.infer<typeof loginSchema>;

export function LoginForm() {
  const { t } = useTranslation();
  const login = useLogin();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginValues>({ resolver: zodResolver(loginSchema) });

  return (
    <form onSubmit={handleSubmit((values) => login.mutate(values))} className="space-y-4">
      <Field label={t("auth.userName")} error={errors.userName?.message}>
        <Input autoComplete="username" {...register("userName")} />
      </Field>
      <Field label={t("auth.password")} error={errors.password?.message}>
        <Input type="password" autoComplete="current-password" {...register("password")} />
      </Field>
      <Button type="submit" className="w-full" disabled={login.isPending}>
        {t("auth.login")}
      </Button>
    </form>
  );
}
