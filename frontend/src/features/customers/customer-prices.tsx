import { i18n, t } from "@/i18n"
import { useEffect, useState } from "react"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { Plus, Tag, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Spinner } from "@/components/ui/spinner"
import { NumberInput } from "@/components/number-input"
import { Money } from "@/components/money"
import { useCurrency } from "@/hooks/use-settings"
import { api, errorMessage } from "@/services/api"
import { useProducts } from "@/features/products/api"

interface PriceRow { productId: number; productName: string; unit: string; defaultPrice: number; price: number }

/** Special prices for one customer; picked automatically when that customer's invoice uses the product. */
export function CustomerPrices({ customerId }: { customerId: number }) {
  const qc = useQueryClient()
  const currency = useCurrency()
  const key = ["customers", "prices", customerId]
  const { data } = useQuery({ queryKey: key, queryFn: () => api.get<PriceRow[]>(`/customers/${customerId}/prices`) })
  const { data: products } = useProducts({})
  const [rows, setRows] = useState<PriceRow[]>([])
  const [adding, setAdding] = useState<string>("")
  const [busy, setBusy] = useState(false)
  useEffect(() => { if (data) setRows(data) }, [data])
  const dirty = JSON.stringify(rows.map((r) => [r.productId, r.price])) !== JSON.stringify((data ?? []).map((r) => [r.productId, r.price]))
  const available = products?.items.filter((p) => !rows.some((r) => r.productId === p.id)) ?? []

  const save = async () => {
    setBusy(true)
    try {
      const saved = await api.put<PriceRow[]>(`/customers/${customerId}/prices`, rows.map((r) => ({ productId: r.productId, price: r.price })))
      qc.setQueryData(key, saved)
      qc.invalidateQueries({ queryKey: ["products"] })
      toast.success(t("قیمت‌های ویژه ذخیره شد."))
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="rounded-xl border bg-paper p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold"><Tag className="size-4" /> {t("قیمت‌های ویژه این مشتری")}</h2>
        {dirty && <Button size="sm" onClick={save} disabled={busy}>{busy && <Spinner />} {t("ذخیره")}</Button>}
      </div>
      {rows.length === 0 && <p className="mb-3 text-sm text-muted-foreground">{t("برای این مشتری قیمت ویژه‌ای تعریف نشده؛ قیمت پیش‌فرض کالاها استفاده می‌شود.")}</p>}
      <ul className="grid gap-2">
        {rows.map((r, i) => (
          <li key={r.productId} className="grid grid-cols-[1fr_9rem_auto] items-center gap-2">
            <div className="min-w-0">
              <div className="truncate text-sm">{r.productName}</div>
              <div className="text-xs text-muted-foreground">{t("پیش‌فرض:")} <Money value={r.defaultPrice} symbol={false} /></div>
            </div>
            <NumberInput value={r.price} decimals={currency.currencyDecimalPlaces} aria-label={t("قیمت ویژه {0}", [r.productName])}
              onChange={(v) => setRows(rows.map((x, j) => (j === i ? { ...x, price: v } : x)))} />
            <Button variant="ghost" size="icon" aria-label={t("حذف")} onClick={() => setRows(rows.filter((_, j) => j !== i))}><Trash2 /></Button>
          </li>
        ))}
      </ul>
      {available.length > 0 && (
        <div className="mt-3 flex gap-2">
          <Select value={adding} onValueChange={setAdding} dir={i18n.dir}>
            <SelectTrigger className="flex-1"><SelectValue placeholder={t("افزودن کالا یا خدمت…")} /></SelectTrigger>
            <SelectContent>{available.map((p) => <SelectItem key={p.id} value={String(p.id)}>{p.name}</SelectItem>)}</SelectContent>
          </Select>
          <Button variant="outline" disabled={!adding} onClick={() => {
            const p = available.find((x) => String(x.id) === adding)!
            setRows([...rows, { productId: p.id, productName: p.name, unit: p.unit, defaultPrice: p.defaultPrice, price: p.defaultPrice }])
            setAdding("")
          }}><Plus /> {t("افزودن")}</Button>
        </div>
      )}
    </section>
  )
}
