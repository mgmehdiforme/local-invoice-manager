import { useState } from "react"
import { PackageSearch, Plus } from "lucide-react"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command"
import { Button } from "@/components/ui/button"
import { Money } from "@/components/money"
import { useProducts, type Product } from "@/features/products/api"
import { ProductFormDialog } from "@/features/products/product-form-dialog"
import { normalize } from "./customer-picker"
import { t } from "@/i18n"

/** Picks a catalog item for a line. Uses the customer's own price when one is set. */
export function ProductPicker({ customerId, onPick }: { customerId?: number | null; onPick: (p: Product) => void }) {
  const [open, setOpen] = useState(false)
  const [creating, setCreating] = useState(false)
  const { data } = useProducts({ customerId: customerId ?? undefined })

  return (
    <>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button type="button" variant="ghost" size="icon" aria-label={t("انتخاب از فهرست کالاها")} title={t("انتخاب از فهرست کالاها")}>
            <PackageSearch />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-80 p-0" align="start">
          <Command filter={(v, s) => (normalize(v).includes(normalize(s)) ? 1 : 0)}>
            <CommandInput placeholder={t("جستجوی کالا یا خدمت…")} />
            <CommandList>
              <CommandEmpty>{t("موردی پیدا نشد.")}</CommandEmpty>
              <CommandGroup>
                {data?.items.map((p) => (
                  <CommandItem key={p.id} value={`${p.name} ${p.sku ?? ""} #${p.id}`} onSelect={() => { onPick(p); setOpen(false) }}>
                    <div className="flex w-full items-center justify-between gap-3">
                      <div className="min-w-0">
                        <div className="truncate">{p.name}</div>
                        <div className="text-xs text-muted-foreground">{p.unit}{p.customerPrice != null && ` — ${t("قیمت ویژه این مشتری")}`}</div>
                      </div>
                      <Money value={p.customerPrice ?? p.defaultPrice} className="text-xs" symbol={false} />
                    </div>
                  </CommandItem>
                ))}
              </CommandGroup>
              <CommandGroup>
                <CommandItem value="__new__" onSelect={() => { setOpen(false); setCreating(true) }}><Plus /> {t("کالا یا خدمت جدید")}</CommandItem>
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      <ProductFormDialog open={creating} onOpenChange={setCreating} onSaved={onPick} />
    </>
  )
}
