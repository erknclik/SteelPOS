# SanalPOS - Frontend Mimarisi (React)

## 1. Genel Yaklaşım

- **React 18 + TypeScript**, **Vite** ile build
- Mimari: **Feature-based (modüler) klasörleme** — sayfa/özellik bazlı gruplama, katman bazlı değil
- Server state: **TanStack Query** (cache, retry, invalidation otomatik)
- Client state: **Zustand** (auth durumu, UI tercihleri gibi hafif global state için)
- Form yönetimi: **React Hook Form + Zod**
- Stil: **TailwindCSS + shadcn/ui**

## 2. Klasör Yapısı

```
src/
  app/
    App.tsx
    router.tsx
    providers/ (QueryClientProvider, AuthProvider, ThemeProvider)
  features/
    auth/
      components/ (LoginForm, ChangePasswordForm)
      hooks/ (useLogin, useRefreshToken)
      api/ (authApi.ts)
      types/
    payments/
      components/ (PaymentForm, TransactionTable, TransactionDetailDrawer)
      hooks/ (useCreatePayment, useTransactions, useRefund)
      api/ (paymentsApi.ts)
      schemas/ (paymentSchema.ts - Zod)
      types/
    merchants/
      components/, hooks/, api/, types/
    reporting/
      components/ (DailySummaryChart, ReconciliationTable)
      hooks/, api/
    webhooks/
      components/, hooks/, api/
  shared/
    components/ (Button, Input, DataTable, Modal, Toast - shadcn tabanlı)
    hooks/ (useDebounce, usePagination)
    lib/ (axiosClient.ts, queryClient.ts, formatters.ts)
    constants/
  layouts/
    MainLayout.tsx
    AuthLayout.tsx
  locales/
    tr/, en/ (i18next çeviri dosyaları)
  types/
    api.d.ts (backend'den üretilen OpenAPI tipleri)
```

## 3. Sayfa (Route) Haritası

| Route | Sayfa | Yetki |
|---|---|---|
| `/login` | Giriş | Herkes |
| `/` | Dashboard (günlük özet, grafik) | Operator+ |
| `/payments` | İşlem listesi | Operator+ |
| `/payments/new` | Yeni ödeme oluşturma | Operator+ |
| `/payments/:id` | İşlem detayı | Operator+ |
| `/merchants` | Merchant listesi | SystemAdmin |
| `/merchants/:id` | Merchant detay/düzenleme | SystemAdmin, MerchantAdmin |
| `/reports/daily` | Günlük mutabakat raporu | MerchantAdmin+ |
| `/reports/export` | Rapor dışa aktarma | MerchantAdmin+ |
| `/webhooks` | Webhook yönetimi | MerchantAdmin |
| `/settings` | Kullanıcı/şifre/tercih ayarları | Herkes (giriş yapmış) |
| `/403` , `/404` | Hata sayfaları | - |

## 4. API İstemci Katmanı

```ts
// shared/lib/axiosClient.ts
import axios from "axios";
import { useAuthStore } from "@/features/auth/store";

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: 15000,
});

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  config.headers["X-Correlation-Id"] = crypto.randomUUID();
  return config;
});

apiClient.interceptors.response.use(
  (res) => res,
  async (error) => {
    if (error.response?.status === 401) {
      await useAuthStore.getState().refreshToken();
      return apiClient.request(error.config);
    }
    return Promise.reject(error);
  }
);
```

## 5. TanStack Query Kullanım Örneği

```ts
// features/payments/hooks/useTransactions.ts
export function useTransactions(filters: TransactionFilters) {
  return useQuery({
    queryKey: ["transactions", filters],
    queryFn: () => paymentsApi.list(filters),
    staleTime: 30_000,
  });
}

export function useCreatePayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: paymentsApi.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["transactions"] });
      toast.success("Ödeme başarıyla oluşturuldu.");
    },
    onError: (err) => toast.error(getErrorMessage(err)),
  });
}
```

## 6. Form + Zod Doğrulama (Backend ile Paralel Kurallar)

```ts
// features/payments/schemas/paymentSchema.ts
export const createPaymentSchema = z.object({
  merchantId: z.string().uuid(),
  amount: z.number().positive().max(1_000_000),
  currency: z.enum(["TRY", "USD", "EUR"]),
  installmentCount: z.number().int().min(1).max(12),
  cardNumber: z.string().regex(/^\d{16}$/, "Geçersiz kart numarası"),
  expireMonth: z.number().int().min(1).max(12),
  expireYear: z.number().int().min(new Date().getFullYear()),
  cvv: z.string().regex(/^\d{3,4}$/),
});
export type CreatePaymentFormValues = z.infer<typeof createPaymentSchema>;
```

## 7. Kart Bilgisi Güvenliği (Frontend)

- Kart numarası input'u **otomatik maskeleme** ile gösterilir (kullanıcı yazarken `4021 2200 0000 1234` formatında)
- CVV alanı `type="password"` ile gizlenir
- Kart bilgisi **hiçbir zaman** browser `localStorage`/`sessionStorage`'a yazılmaz
- Form gönderiminden hemen sonra state'ten temizlenir (memory'de kalıcı tutulmaz)
- İdeal senaryoda (ileri faz) kart bilgisi doğrudan bankanın/PSP'nin **hosted field / iframe** çözümüne yönlendirilir, backend'e hiç dokunmaz (PCI-DSS SAQ-A kapsamını daraltmak için)

## 8. Erişilebilirlik & Responsive

- Tüm formlar klavye ile tam erişilebilir (shadcn/ui + Radix primitives sayesinde)
- Mobil öncelikli (mobile-first) Tailwind breakpoint kullanımı
- Renk kontrastı WCAG AA seviyesinde

## 9. Çoklu Dil (i18n)

`i18next` ile `tr` (varsayılan) ve `en` dil desteği; para birimi ve tarih formatları `Intl.NumberFormat` / `Intl.DateTimeFormat` ile lokalize edilir.

## 10. Test Stratejisi (Frontend)

- **Vitest + Testing Library**: Bileşen ve hook unit testleri
- **MSW**: API mock'lama ile entegrasyon testleri
- **Playwright** (opsiyonel, e2e): Kritik akışlar (giriş, ödeme oluşturma, iade) uçtan uca test edilir

## 11. Ortam Değişkenleri

```
VITE_API_BASE_URL=https://api.sanalpos.local/api/v1
VITE_APP_ENV=development
```

## 12. Build & Dağıtım

- `npm run build` → `dist/` klasörü statik dosyalar üretir
- Nginx ile servis edilir; API çağrıları reverse proxy üzerinden backend'e yönlendirilir (CORS ihtiyacını azaltır)
- CI/CD'de `npm run lint`, `npm run test`, `npm run build` adımları zorunlu kalite kapıları olarak çalışır
