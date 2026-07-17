import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { paymentsApi, type TransactionFilters } from "./api";
import { toast } from "@/shared/components/Toast";
import { getErrorMessage } from "@/shared/lib/axiosClient";

export function useTransactions(filters: TransactionFilters) {
  return useQuery({
    queryKey: ["transactions", filters],
    queryFn: () => paymentsApi.list(filters),
    staleTime: 30_000,
  });
}

export function useTransaction(id: string) {
  return useQuery({
    queryKey: ["transactions", id],
    queryFn: () => paymentsApi.getById(id),
    enabled: Boolean(id),
  });
}

export function useStatusHistory(id: string) {
  return useQuery({
    queryKey: ["transactions", id, "history"],
    queryFn: () => paymentsApi.getStatusHistory(id),
    enabled: Boolean(id),
  });
}

export function useCreatePayment() {
  const queryClient = useQueryClient();
  const { t } = useTranslation();

  return useMutation({
    mutationFn: paymentsApi.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      toast.success(t("payments.created"));
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  });
}

export function useInitiate3DS() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: paymentsApi.initiate3DS,
    // Yönlendirme gerekmeyen (kayıtsız kart) durumda liste tazelenir; toast'ı çağıran
    // bileşen sonuca göre gösterir (Approved/Declined ayrımı için).
    onSuccess: (data) => {
      if (!data.requiresRedirect) queryClient.invalidateQueries({ queryKey: ["transactions"] });
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  });
}

export function useCapture(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation();

  return useMutation({
    mutationFn: () => paymentsApi.capture(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      toast.success(t("payments.captured"));
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  });
}

export function useRefund(id: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ amount, reason }: { amount: number; reason?: string }) =>
      paymentsApi.refund(id, amount, reason),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["transactions"] }),
    onError: (err) => toast.error(getErrorMessage(err)),
  });
}

export function useVoid(id: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => paymentsApi.voidPayment(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["transactions"] }),
    onError: (err) => toast.error(getErrorMessage(err)),
  });
}
