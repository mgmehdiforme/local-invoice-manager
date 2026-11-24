import type { CurrencyDef } from "@/services/types"

/** Common currencies offered when adding one in settings or on first run. Any ISO code can still be typed. */
export const CURRENCY_PRESETS: CurrencyDef[] = [
  { code: "USD", symbol: "$", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "EUR", symbol: "€", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "GBP", symbol: "£", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "CAD", symbol: "CA$", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "AUD", symbol: "A$", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "CHF", symbol: "CHF", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "JPY", symbol: "¥", decimalPlaces: 0, symbolPosition: "Before" },
  { code: "CNY", symbol: "CN¥", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "INR", symbol: "₹", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "TRY", symbol: "₺", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "AED", symbol: "AED", decimalPlaces: 2, symbolPosition: "Before" },
  { code: "IRR", symbol: "ریال", decimalPlaces: 0, symbolPosition: "After" }, // i18n-ignore
  { code: "IRT", symbol: "تومان", decimalPlaces: 0, symbolPosition: "After" }, // i18n-ignore
]
