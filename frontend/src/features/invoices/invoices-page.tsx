import { useState } from "react"
import { Link, useSearchParams } from "react-router"
import { FilePlus2, FileText } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { PageHeader } from "@/components/page-header"
import { SearchInput } from "@/components/search-input"
import { EmptyState } from "@/components/empty-state"
import { JalaliDatePicker } from "@/components/jalali-date-picker"
import { Money } from "@/components/money"
import { useDebounced } from "@/hooks/use-debounced"
import { fa } from "@/lib/format"
import { useInvoices, type InvoiceFilters, type InvoiceListItem } from "./api"
import { i18n, t } from "@/i18n"
import { InvoiceList } from "./invoice-list"

const FILTERS: { key: string; label: string; f: InvoiceFilters }[] = [
  { key: "all", label: t("همه"), f: {} },
  { key: "unpaid", label: t("پرداخت‌نشده"), f: { unpaid: true } },
  { key: "overdue", label: t("سررسید گذشته"), f: { overdue: true } },
  { key: "paid", label: t("پرداخت‌شده"), f: { paymentStatus: "Paid" } },
  { key: "draft", label: t("پیش‌نویس"), f: { status: "Draft" } },
  { key: "cancelled", label: t("ابطال‌شده"), f: { status: "Cancelled" } },
]

export default function InvoicesPage() {
  const [params, setParams] = useSearchParams()
  const tab = params.get("filter") ?? "all"
  const [search, setSearch] = useState("")
  const [from, setFrom] = useState<string | null>(null)
  const [to, setTo] = useState<string | null>(null)
  const q = useDebounced(search)
  const filter = FILTERS.find((x) => x.key === tab) ?? FILTERS[0]
  const { data, isLoading } = useInvoices({ ...filter.f, search: q, from: from ?? undefined, to: to ?? undefined })
  // One sum per currency: amounts in different currencies are never added together.
  const sums = new Map<string, { value: number; currency: InvoiceListItem["currency"] }>()
  for (const i of data?.items ?? []) {
    if (i.status !== "Issued") continue
    const v = tab === "unpaid" || tab === "overdue" ? i.balance : i.total
    const cur = sums.get(i.currency.currencyCode)
    sums.set(i.currency.currencyCode, { value: (cur?.value ?? 0) + v, currency: i.currency })
  }
  const sumList = [...sums.values()].filter((x) => x.value > 0)

  return (
    <>
      <PageHeader
        title={t("فاکتورها")}
        description={data ? t("{0} فاکتور", [fa(data.total)]) : undefined}
        actions={<Button asChild><Link to="/invoices/new"><FilePlus2 /> {t("فاکتور جدید")}</Link></Button>}
      />
      <Tabs value={tab} onValueChange={(v) => setParams(v === "all" ? {} : { filter: v })} dir={i18n.dir} className="mb-4">
        <TabsList className="h-auto flex-wrap justify-start">
          {FILTERS.map((x) => <TabsTrigger key={x.key} value={x.key}>{x.label}</TabsTrigger>)}
        </TabsList>
      </Tabs>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchInput value={search} onChange={setSearch} placeholder={t("شماره فاکتور یا نام مشتری")} />
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <span>{t("از")}</span><JalaliDatePicker value={from} onChange={setFrom} clearable className="w-36" />
          <span>{t("تا")}</span><JalaliDatePicker value={to} onChange={setTo} clearable className="w-36" />
        </div>
        {sumList.length > 0 && (
          <div className="ms-auto flex flex-wrap items-center gap-x-3 text-sm text-muted-foreground">
            <span>{tab === "unpaid" || tab === "overdue" ? t("جمع مانده:") : t("جمع مبلغ:")}</span>
            {sumList.map((x) => <Money key={x.currency.currencyCode} value={x.value} currency={x.currency} className="font-semibold text-foreground" />)}
          </div>
        )}
      </div>
      <InvoiceList
        rows={data?.items}
        loading={isLoading}
        empty={
          <EmptyState
            icon={FileText}
            title={q || tab !== "all" || from || to ? t("فاکتوری با این شرایط پیدا نشد.") : t("هنوز فاکتوری صادر نکرده‌اید.")}
            description={!q && tab === "all" ? t("اولین فاکتور را بسازید؛ پیش‌نمایش همان لحظه کنار فرم دیده می‌شود.") : undefined}
            action={!q && tab === "all" && <Button asChild><Link to="/invoices/new"><FilePlus2 /> {t("فاکتور جدید")}</Link></Button>}
          />
        }
      />
    </>
  )
}
