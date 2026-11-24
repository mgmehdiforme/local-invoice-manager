import { toGregorian, toJalaali, jalaaliMonthLength, isValidJalaaliDate } from "jalaali-js"

/** Backend dates are Gregorian ISO strings (yyyy-MM-dd). These helpers convert to/from Jalali for the UI. */

export const MONTHS = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"]
export const WEEKDAYS = ["ش", "ی", "د", "س", "چ", "پ", "ج"]

export interface JDate { jy: number; jm: number; jd: number }

export function isoToJalali(iso: string): JDate {
  const [y, m, d] = iso.slice(0, 10).split("-").map(Number)
  return toJalaali(y, m, d)
}

export function jalaliToIso({ jy, jm, jd }: JDate): string {
  const g = toGregorian(jy, jm, jd)
  return `${g.gy}-${String(g.gm).padStart(2, "0")}-${String(g.gd).padStart(2, "0")}`
}

export function todayIso(): string {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`
}

export function addDaysIso(iso: string, days: number): string {
  const [y, m, d] = iso.split("-").map(Number)
  const dt = new Date(Date.UTC(y, m - 1, d + days))
  return dt.toISOString().slice(0, 10)
}

export function monthLength(jy: number, jm: number): number {
  return jalaaliMonthLength(jy, jm)
}

/** Day of week for a Jalali date, 0 = Saturday … 6 = Friday. */
export function weekdayIndex(j: JDate): number {
  const [y, m, d] = jalaliToIso(j).split("-").map(Number)
  const jsDay = new Date(Date.UTC(y, m - 1, d)).getUTCDay() // 0 = Sunday
  return (jsDay + 1) % 7
}

/** Parses "1405/8/24", "۱۴۰۵-۰۸-۲۴" etc. Returns ISO or null. */
export function parseJalali(input: string): string | null {
  const latin = toLatinDigits(input).trim()
  const m = latin.match(/^(\d{4})[/\-.](\d{1,2})[/\-.](\d{1,2})$/)
  if (!m) return null
  const [jy, jm, jd] = [Number(m[1]), Number(m[2]), Number(m[3])]
  if (!isValidJalaaliDate(jy, jm, jd)) return null
  return jalaliToIso({ jy, jm, jd })
}

export function toLatinDigits(s: string): string {
  return s.replace(/[۰-۹]/g, (c) => String(c.charCodeAt(0) - 0x06f0)).replace(/[٠-٩]/g, (c) => String(c.charCodeAt(0) - 0x0660))
}
