import { describe, expect, it } from "vitest"
import { fromParts, monthLength, parseDate, toParts, weekdayIndex } from "./calendar"

describe("calendar", () => {
  it("Gregorian parts round-trip", () => expect(fromParts(toParts("2026-02-28", "Gregorian"), "Gregorian")).toBe("2026-02-28"))
  it("Gregorian leap February", () => expect(monthLength(2028, 2, "Gregorian")).toBe(29))
  it("parses Gregorian input", () => expect(parseDate("2026-2-3", "Gregorian")).toBe("2026-02-03"))
  it("rejects 31 April", () => expect(parseDate("2026-04-31", "Gregorian")).toBeNull())
  it("Gregorian week starts Monday", () => expect(weekdayIndex({ y: 2026, m: 11, d: 23 }, "Gregorian")).toBe(0)) // Monday
  it("Jalali parts", () => expect(toParts("2026-11-15", "Jalali")).toEqual({ y: 1405, m: 8, d: 24 }))
  it("parses Jalali input", () => expect(parseDate("۱۴۰۵/۸/۲۴", "Jalali")).toBe("2026-11-15"))
})
