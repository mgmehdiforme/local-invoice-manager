import { cn } from "@/lib/utils"
import { formatAmount, formatMoney, type CurrencyFormat } from "@/lib/format"
import { useCurrency } from "@/hooks/use-settings"

/** An amount in the app's (or a document's own) currency, Persian tabular digits. */
export function Money({ value, currency, className, symbol = true }: { value: number | null | undefined; currency?: CurrencyFormat; className?: string; symbol?: boolean }) {
  const app = useCurrency()
  const c = currency ?? app
  return <span className={cn("num whitespace-nowrap", className)}>{symbol ? formatMoney(value, c) : formatAmount(value, c)}</span>
}
