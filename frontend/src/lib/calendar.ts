import { i18n, type CalendarKind } from "@/i18n"
import { isoToJalali, jalaliToIso, monthLength as jalaliMonthLength, toLatinDigits } from "./jalali"
import { isValidJalaaliDate } from "jalaali-js"

/**
 * Calendar-neutral date helpers for the UI. The backend always uses Gregorian ISO dates (yyyy-MM-dd);
 * these convert to and from the calendar chosen in settings (Jalali or Gregorian).
 */
export interface DateParts { y: number; m: number; d: number }

const JALALI_FA = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"]
const JALALI_EN = ["Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar", "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand"]
const GREG_EN = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"]
const GREG_FA = ["ژانویه", "فوریه", "مارس", "آوریل", "مه", "ژوئن", "ژوئیه", "اوت", "سپتامبر", "اکتبر", "نوامبر", "دسامبر"]

export function monthNames(cal: CalendarKind = i18n.calendar): string[] {
  if (cal === "Jalali") return i18n.lang === "fa" ? JALALI_FA : JALALI_EN
  return i18n.lang === "fa" ? GREG_FA : GREG_EN
}

/** Week starts on Saturday for Jalali, Monday for Gregorian. */
export function weekdayNames(cal: CalendarKind = i18n.calendar): string[] {
  if (cal === "Jalali") return i18n.lang === "fa" ? ["ش", "ی", "د", "س", "چ", "پ", "ج"] : ["Sa", "Su", "Mo", "Tu", "We", "Th", "Fr"]
  return i18n.lang === "fa" ? ["د", "س", "چ", "پ", "ج", "ش", "ی"] : ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"]
}

export function toParts(iso: string, cal: CalendarKind = i18n.calendar): DateParts {
  if (cal === "Jalali") {
    const j = isoToJalali(iso)
    return { y: j.jy, m: j.jm, d: j.jd }
  }
  const [y, m, d] = iso.slice(0, 10).split("-").map(Number)
  return { y, m, d }
}

export function fromParts(p: DateParts, cal: CalendarKind = i18n.calendar): string {
  if (cal === "Jalali") return jalaliToIso({ jy: p.y, jm: p.m, jd: p.d })
  return `${String(p.y).padStart(4, "0")}-${String(p.m).padStart(2, "0")}-${String(p.d).padStart(2, "0")}`
}

export function monthLength(y: number, m: number, cal: CalendarKind = i18n.calendar): number {
  if (cal === "Jalali") return jalaliMonthLength(y, m)
  return new Date(Date.UTC(y, m, 0)).getUTCDate()
}

/** Column of a date in the month grid (0 = first day of the week). */
export function weekdayIndex(p: DateParts, cal: CalendarKind = i18n.calendar): number {
  const [y, m, d] = fromParts(p, cal).split("-").map(Number)
  const jsDay = new Date(Date.UTC(y, m - 1, d)).getUTCDay() // 0 = Sunday
  return cal === "Jalali" ? (jsDay + 1) % 7 : (jsDay + 6) % 7
}

/** yyyy/MM/dd for Jalali, yyyy-MM-dd for Gregorian (Latin digits; callers localize). */
export function formatParts(p: DateParts, cal: CalendarKind = i18n.calendar): string {
  const sep = cal === "Jalali" ? "/" : "-"
  return `${p.y}${sep}${String(p.m).padStart(2, "0")}${sep}${String(p.d).padStart(2, "0")}`
}

/** Parses "1405/8/24", "2026-11-24", "۱۴۰۵-۰۸-۲۴" in the active calendar. Returns ISO or null. */
export function parseDate(input: string, cal: CalendarKind = i18n.calendar): string | null {
  const latin = toLatinDigits(input).trim()
  const m = latin.match(/^(\d{4})[/\-.](\d{1,2})[/\-.](\d{1,2})$/)
  if (!m) return null
  const [y, mo, d] = [Number(m[1]), Number(m[2]), Number(m[3])]
  if (cal === "Jalali") return isValidJalaaliDate(y, mo, d) ? jalaliToIso({ jy: y, jm: mo, jd: d }) : null
  if (mo < 1 || mo > 12 || d < 1 || d > monthLength(y, mo, "Gregorian")) return null
  return fromParts({ y, m: mo, d }, "Gregorian")
}

/** Example shown as the date input placeholder. */
export function datePlaceholder(cal: CalendarKind = i18n.calendar): string {
  return cal === "Jalali" ? "1405/01/01" : "2026-01-31"
}
