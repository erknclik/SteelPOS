import { describe, expect, it } from "vitest";
import { formatCardNumberInput, formatMoney } from "./formatters";

describe("formatCardNumberInput", () => {
  it("groups digits by four", () => {
    expect(formatCardNumberInput("4021220000001234")).toBe("4021 2200 0000 1234");
  });

  it("strips non-digit characters", () => {
    expect(formatCardNumberInput("4021-2200 abc 0000")).toBe("4021 2200 0000");
  });

  it("caps at 19 digits", () => {
    expect(formatCardNumberInput("12345678901234567890123").replace(/\s/g, "")).toHaveLength(19);
  });
});

describe("formatMoney", () => {
  it("formats TRY with Turkish locale", () => {
    expect(formatMoney(1250.5)).toContain("1.250,50");
  });
});
