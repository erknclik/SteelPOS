export function formatMoney(amount: number, currency = "TRY", locale = "tr-TR"): string {
  return new Intl.NumberFormat(locale, { style: "currency", currency }).format(amount);
}

export function formatDate(iso: string | null | undefined, locale = "tr-TR"): string {
  if (!iso) return "-";
  return new Intl.DateTimeFormat(locale, {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(iso));
}

/** Kart numarası input'unu 4'lü gruplar halinde maskeler: "4021220000001234" -> "4021 2200 0000 1234" */
export function formatCardNumberInput(value: string): string {
  const digits = value.replace(/\D/g, "").slice(0, 19);
  return digits.replace(/(\d{4})(?=\d)/g, "$1 ").trim();
}
