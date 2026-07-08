import { describe, expect, it } from "vitest";
import { createPaymentSchema } from "./paymentSchema";

const valid = {
  merchantId: "3f1c9e2a-1111-2222-3333-444455556666",
  terminalId: "3f1c9e2a-1111-2222-3333-444455556667",
  orderReference: "SIP-2026-000123",
  amount: 1250.5,
  currency: "TRY" as const,
  installmentCount: 3,
  cardNumber: "4021 2200 0000 1234",
  cardHolderName: "AHMET YILMAZ",
  expireMonth: 12,
  expireYear: new Date().getFullYear() + 2,
  cvv: "123",
};

describe("createPaymentSchema", () => {
  it("accepts a valid payment and strips card number spaces", () => {
    const result = createPaymentSchema.parse(valid);
    expect(result.cardNumber).toBe("4021220000001234");
  });

  it("rejects zero or negative amount", () => {
    expect(createPaymentSchema.safeParse({ ...valid, amount: 0 }).success).toBe(false);
    expect(createPaymentSchema.safeParse({ ...valid, amount: -5 }).success).toBe(false);
  });

  it("rejects amount above upper limit", () => {
    expect(createPaymentSchema.safeParse({ ...valid, amount: 1_000_001 }).success).toBe(false);
  });

  it("rejects unsupported currency", () => {
    expect(createPaymentSchema.safeParse({ ...valid, currency: "GBP" }).success).toBe(false);
  });

  it("rejects invalid cvv", () => {
    expect(createPaymentSchema.safeParse({ ...valid, cvv: "12" }).success).toBe(false);
    expect(createPaymentSchema.safeParse({ ...valid, cvv: "abcd" }).success).toBe(false);
  });

  it("rejects installments outside 1-12", () => {
    expect(createPaymentSchema.safeParse({ ...valid, installmentCount: 0 }).success).toBe(false);
    expect(createPaymentSchema.safeParse({ ...valid, installmentCount: 13 }).success).toBe(false);
  });

  it("rejects past expiry year", () => {
    expect(
      createPaymentSchema.safeParse({ ...valid, expireYear: new Date().getFullYear() - 1 }).success
    ).toBe(false);
  });
});
