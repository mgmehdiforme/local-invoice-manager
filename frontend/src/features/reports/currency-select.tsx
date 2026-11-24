import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { useCurrencies } from "@/hooks/use-settings"
import { i18n, t } from "@/i18n"

/** Picks which currency the figures are shown in. Renders nothing when only one currency is enabled. */
export function CurrencySelect({ value, onChange }: { value: string; onChange: (code: string) => void }) {
  const { list, defaultCode } = useCurrencies()
  if (list.length < 2) return null
  return (
    <Select value={value || defaultCode} onValueChange={onChange} dir={i18n.dir}>
      <SelectTrigger className="w-32 bg-paper" aria-label={t("ارز")}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {list.map((c) => (
          <SelectItem key={c.currencyCode} value={c.currencyCode}>
            {c.currencyCode} · {c.currencySymbol}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
