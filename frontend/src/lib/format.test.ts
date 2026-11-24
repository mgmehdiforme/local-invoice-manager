import { beforeAll, describe, expect, it } from "vitest"
import { configureI18n } from "@/i18n"
import { fa, formatAmount, formatDate, formatMoney, formatNumber, formatQuantity } from "./format"
import { addDaysIso, isoToJalali, jalaliToIso, parseJalali, weekdayIndex } from "./jalali"

describe("format", () => {
  beforeAll(() => configureI18n("fa", "Jalali"))
  it("uses Persian digits", () => expect(fa("2024-1")).toBe("۲۰۲۴-۱"))
  it("groups thousands", () => expect(formatNumber(1234567, 0)).toBe("۱٬۲۳۴٬۵۶۷"))
  it("keeps decimals", () => expect(formatNumber(1234.5, 2, ",", ".")).toBe("۱,۲۳۴.۵۰"))
  it("formats negatives", () => expect(formatNumber(-50, 0)).toBe("−۵۰"))
  it("places symbol after by default", () => expect(formatMoney(1000)).toBe("۱٬۰۰۰ ریال"))
  it("places symbol before when set", () =>
    expect(formatMoney(5, { currencySymbol: "$", currencyDecimalPlaces: 2, thousandsSeparator: ",", decimalSeparator: ".", currencySymbolPosition: "Before" })).toBe("$۵.۰۰"))
  it("handles null", () => expect(formatAmount(null)).toBe("۰"))
  it("trims quantity zeros", () => expect(formatQuantity(2.5)).toBe("۲٫۵"))
})

describe("jalali", () => {
  it("converts 2026-11-15 to 1405/08/24", () => expect(isoToJalali("2026-11-15")).toEqual({ jy: 1405, jm: 8, jd: 24 }))
  it("round-trips Nowruz", () => expect(jalaliToIso({ jy: 1405, jm: 1, jd: 1 })).toBe("2026-03-21"))
  it("formats dates", () => expect(formatDate("2026-11-15")).toBe("۱۴۰۵/۰۸/۲۴"))
  it("parses Persian digits", () => expect(parseJalali("۱۴۰۵/۸/۲۴")).toBe("2026-11-15"))
  it("rejects invalid dates", () => expect(parseJalali("1405/07/31")).toBeNull())
  it("handles leap year 1403 Esfand 30", () => expect(parseJalali("1403/12/30")).toBe("2025-03-20"))
  it("rejects Esfand 30 in non-leap 1404", () => expect(parseJalali("1404/12/30")).toBeNull())
  it("adds days across months", () => expect(addDaysIso("2026-01-30", 3)).toBe("2026-02-02"))
  it("computes weekday (Saturday = 0)", () => expect(weekdayIndex({ jy: 1405, jm: 1, jd: 1 })).toBe(0)) // 2026-03-21 is a Saturday
})

describe("english + gregorian", () => {
  beforeAll(() => configureI18n("en", "Gregorian"))
  it("keeps Latin digits", () => expect(fa("2024")).toBe("2024"))
  it("formats Gregorian dates", () => expect(formatDate("2026-11-15")).toBe("2026-11-15"))
  it("puts a one-letter symbol against the number", () =>
    expect(formatMoney(1234.5, { currencySymbol: "$", currencyDecimalPlaces: 2, thousandsSeparator: ",", decimalSeparator: ".", currencySymbolPosition: "Before" })).toBe("$1,234.50"))
})
