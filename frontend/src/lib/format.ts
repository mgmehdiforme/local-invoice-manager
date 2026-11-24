import { i18n, t } from "@/i18n"
import { formatParts, monthNames, toParts } from "./calendar"

const FA_DIGITS = "۰۱۲۳۴۵۶۷۸۹"

/** Localizes digits: Persian digits in the Persian UI, Latin digits in English. */
export function fa(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return ""
  if (i18n.lang !== "fa") return String(value)
  return String(value).replace(/\d/g, (d) => FA_DIGITS[Number(d)])
}

export interface CurrencyFormat {
  currencySymbol: string
  currencyDecimalPlaces: number
  thousandsSeparator: string
  decimalSeparator: string
  currencySymbolPosition: "Before" | "After"
}

const FA_DEFAULT: CurrencyFormat = { currencySymbol: "ریال", currencyDecimalPlaces: 0, thousandsSeparator: "٬", decimalSeparator: "٫", currencySymbolPosition: "After" }
const EN_DEFAULT: CurrencyFormat = { currencySymbol: "$", currencyDecimalPlaces: 2, thousandsSeparator: ",", decimalSeparator: ".", currencySymbolPosition: "Before" }

/** Fallback currency format for the current language (used until settings load). */
export function defaultCurrency(): CurrencyFormat {
  return i18n.lang === "fa" ? FA_DEFAULT : EN_DEFAULT
}

export const DEFAULT_CURRENCY: CurrencyFormat = defaultCurrency()

/** Formats a number with the configured separators and Persian digits (no symbol). */
export function formatNumber(value: number, decimals: number, thousands = defaultCurrency().thousandsSeparator, decimal = defaultCurrency().decimalSeparator): string {
  const negative = value < 0
  const fixed = Math.abs(value).toFixed(decimals)
  const [int, frac] = fixed.split(".")
  const grouped = int.replace(/\B(?=(\d{3})+(?!\d))/g, "\u0000").replaceAll("\u0000", thousands)
  const text = frac ? `${grouped}${decimal}${frac}` : grouped
  return (negative ? "−" : "") + fa(text)
}

export function formatAmount(value: number | null | undefined, c: CurrencyFormat = defaultCurrency()): string {
  return formatNumber(value ?? 0, c.currencyDecimalPlaces, c.thousandsSeparator, c.decimalSeparator)
}

export function formatMoney(value: number | null | undefined, c: CurrencyFormat = defaultCurrency()): string {
  const n = formatAmount(value, c)
  if (c.currencySymbolPosition !== "Before") return `${n} ${c.currencySymbol}`
  // Short symbols such as $ or € sit directly against the number.
  return c.currencySymbol.length === 1 ? `${c.currencySymbol}${n}` : `${c.currencySymbol} ${n}`
}

/** Quantity: up to 3 decimals, trailing zeros removed. */
export function formatQuantity(value: number): string {
  const s = Number(value.toFixed(3)).toString()
  return fa(s.replace(".", defaultCurrency().decimalSeparator))
}

/** 1405/08/24 (Jalali) or 2026-11-15 (Gregorian), digits localized. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return "—"
  return fa(formatParts(toParts(iso)))
}

/** ۲۴ آبان ۱۴۰۵ / 15 November 2026 */
export function formatDateLong(iso: string | null | undefined): string {
  if (!iso) return "—"
  const p = toParts(iso)
  return fa(`${p.d} ${monthNames()[p.m - 1]} ${p.y}`)
}

/** UTC timestamp → local Jalali date + time. */
export function formatDateTime(utc: string | null | undefined): string {
  if (!utc) return "—"
  const d = new Date(utc.endsWith("Z") ? utc : utc + "Z")
  const iso = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`
  return `${formatDate(iso)}${i18n.lang === "fa" ? "،" : ","} ${fa(`${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`)}`
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${fa(bytes)} ${t("بایت")}`
  if (bytes < 1024 * 1024) return `${fa((bytes / 1024).toFixed(1))} ${t("کیلوبایت")}`
  return `${fa((bytes / 1024 / 1024).toFixed(1))} ${t("مگابایت")}`
}
