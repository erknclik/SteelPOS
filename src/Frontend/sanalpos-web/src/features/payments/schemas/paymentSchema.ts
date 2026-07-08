import { z } from "zod";

// Backend FluentValidation kurallarıyla birebir paralel tutulur
// (bkz. docs/04-validasyon.md §7). Backend her zaman son otoritedir.
export const createPaymentSchema = z.object({
  merchantId: z.string().uuid("Geçersiz merchant id"),
  terminalId: z.string().uuid("Geçersiz terminal id"),
  orderReference: z.string().min(1, "Sipariş no zorunludur").max(100),
  amount: z.number({ invalid_type_error: "Tutar sayı olmalıdır" }).positive("Tutar sıfırdan büyük olmalıdır").max(1_000_000, "Tutar üst limiti aşıyor"),
  currency: z.enum(["TRY", "USD", "EUR"]),
  installmentCount: z.number().int().min(1).max(12),
  cardNumber: z
    .string()
    .transform((v) => v.replace(/\s/g, ""))
    .pipe(z.string().regex(/^\d{12,19}$/, "Geçersiz kart numarası")),
  cardHolderName: z.string().min(1, "Kart sahibi zorunludur").max(150),
  expireMonth: z.number().int().min(1).max(12),
  expireYear: z.number().int().min(new Date().getFullYear(), "Yıl geçmiş olamaz"),
  cvv: z.string().regex(/^\d{3,4}$/, "CVV 3 veya 4 haneli olmalıdır"),
});

export type CreatePaymentFormValues = z.infer<typeof createPaymentSchema>;
