import { useEffect } from "react"
import { useQuery } from "@tanstack/react-query"
import { api } from "@/services/api"
import type { CurrencyDef, Settings } from "@/services/types"
import { defaultCurrency, type CurrencyFormat } from "@/lib/format"

export const settingsKey = ["settings"]

export function useSettings() {
  return useQuery({ queryKey: settingsKey, queryFn: () => api.get<Settings>("/settings"), staleTime: 5 * 60_000 })
}

/** Currency format from settings (falls back to Rial defaults while loading). */
export function useCurrency(): CurrencyFormat {
  const { data } = useSettings()
  return data ?? defaultCurrency()
}

export interface CurrencyOption extends CurrencyFormat {
  currencyCode: string
}

function toFormat(def: CurrencyDef, s: Settings): CurrencyOption {
  return {
    currencyCode: def.code,
    currencySymbol: def.symbol,
    currencyDecimalPlaces: def.decimalPlaces,
    currencySymbolPosition: def.symbolPosition,
    thousandsSeparator: s.thousandsSeparator,
    decimalSeparator: s.decimalSeparator,
  }
}

/**
 * Enabled currencies as ready-to-use formats. `byCode` falls back to the default currency.
 * Show a currency picker only when `list.length > 1`.
 */
export function useCurrencies() {
  const { data } = useSettings()
  const list = data ? (data.currencies.length ? data.currencies : [{ code: data.currencyCode, symbol: data.currencySymbol, decimalPlaces: data.currencyDecimalPlaces, symbolPosition: data.currencySymbolPosition }]).map((c) => toFormat(c, data)) : []
  const fallback: CurrencyOption = list.find((c) => c.currencyCode === data?.currencyCode) ?? { ...defaultCurrency(), currencyCode: data?.currencyCode ?? "" }
  return {
    list,
    defaultCode: data?.currencyCode ?? "",
    byCode: (code: string | null | undefined): CurrencyOption => list.find((c) => c.currencyCode === code) ?? fallback,
  }
}

/** Applies the brand accent from settings to the whole UI. */
export function useBrandColor() {
  const { data } = useSettings()
  useEffect(() => {
    if (data?.accentColor) document.documentElement.style.setProperty("--brand", data.accentColor)
  }, [data?.accentColor])
}
