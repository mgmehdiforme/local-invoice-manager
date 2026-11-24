import { useState } from "react"
import { Check, ChevronsUpDown, Plus } from "lucide-react"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import { fa } from "@/lib/format"
import { t } from "@/i18n"
import { useCustomers, type Customer } from "@/features/customers/api"
import { CustomerFormDialog } from "@/features/customers/customer-form-dialog"

/** Searchable customer select with "add new customer" right in the list. */
export function CustomerPicker({ value, onChange, invalid }: { value: number | null; onChange: (c: Customer) => void; invalid?: boolean }) {
  const [open, setOpen] = useState(false)
  const [creating, setCreating] = useState(false)
  const { data } = useCustomers({})
  const selected = data?.items.find((c) => c.id === value)

  return (
    <>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button type="button" variant="outline" role="combobox" aria-expanded={open} aria-invalid={invalid}
            className={cn("h-auto min-h-9 w-full justify-between bg-paper py-1.5 font-normal", invalid && "border-destructive")}>
            {selected ? (
              <span className="text-start">
                <span className="font-medium">{selected.name}</span>
                {selected.companyName && <span className="text-muted-foreground"> — {selected.companyName}</span>}
              </span>
            ) : (
              <span className="text-muted-foreground">{t("انتخاب مشتری…")}</span>
            )}
            <ChevronsUpDown className="opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-[--radix-popover-trigger-width] min-w-72 p-0" align="start">
          <Command filter={(v, s) => (normalize(v).includes(normalize(s)) ? 1 : 0)}>
            <CommandInput placeholder={t("جستجوی نام یا موبایل…")} />
            <CommandList>
              <CommandEmpty>{t("مشتری‌ای پیدا نشد.")}</CommandEmpty>
              <CommandGroup>
                {data?.items.map((c) => (
                  <CommandItem key={c.id} value={`${c.name} ${c.companyName ?? ""} ${c.mobile ?? ""} ${c.phone ?? ""} #${c.id}`}
                    onSelect={() => { onChange(c); setOpen(false) }}>
                    <Check className={cn(c.id === value ? "opacity-100" : "opacity-0")} />
                    <div className="min-w-0">
                      <div>{c.name}</div>
                      {(c.companyName || c.mobile) && <div className="text-xs text-muted-foreground">{[c.companyName, fa(c.mobile ?? "")].filter(Boolean).join(" — ")}</div>}
                    </div>
                  </CommandItem>
                ))}
              </CommandGroup>
              <CommandGroup>
                <CommandItem value="__new__" onSelect={() => { setOpen(false); setCreating(true) }}>
                  <Plus /> {t("مشتری جدید")}
                </CommandItem>
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      <CustomerFormDialog open={creating} onOpenChange={setCreating} onSaved={onChange} />
    </>
  )
}

export function normalize(s: string) {
  return s.toLowerCase().replace(/ي/g, "ی").replace(/ك/g, "ک").replace(/[۰-۹]/g, (d) => String(d.charCodeAt(0) - 0x06f0)) // i18n-ignore
}
