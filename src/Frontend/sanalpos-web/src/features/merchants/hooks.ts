import { useQuery } from "@tanstack/react-query";
import { merchantsApi } from "./api";
import { useAuthStore } from "@/features/auth/store";

export function useMerchants() {
  const isSystemAdmin = useAuthStore((s) => s.hasRole("SystemAdmin"));
  const ownMerchantId = useAuthStore((s) => s.user?.merchantId);

  return useQuery({
    queryKey: ["merchants", { isSystemAdmin, ownMerchantId }],
    queryFn: async () => {
      // SystemAdmin tüm merchant'ları listeler; merchant kullanıcısı sadece kendisini görür.
      if (isSystemAdmin) return merchantsApi.list();
      if (ownMerchantId) return [await merchantsApi.getById(ownMerchantId)];
      return [];
    },
  });
}

export function useMerchant(id: string | undefined) {
  return useQuery({
    queryKey: ["merchants", id],
    queryFn: () => merchantsApi.getById(id!),
    enabled: Boolean(id),
  });
}

export function useMerchantTerminals(merchantId: string | undefined) {
  return useQuery({
    queryKey: ["merchants", merchantId, "terminals"],
    queryFn: () => merchantsApi.getTerminals(merchantId!),
    enabled: Boolean(merchantId),
  });
}

export function useMerchantStores(merchantId: string | undefined) {
  return useQuery({
    queryKey: ["merchants", merchantId, "stores"],
    queryFn: () => merchantsApi.getStores(merchantId!),
    enabled: Boolean(merchantId),
  });
}
