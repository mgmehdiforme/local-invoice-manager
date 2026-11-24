import { i18n, t } from "@/i18n"
import { useState } from "react"
import { useNavigate } from "react-router"
import { Wallet } from "lucide-react"
import { useQuery } from "@tanstack/react-query"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { PageHeader } from "@/components/page-header"
import { SearchInput } from "@/components/search-input"
import { DataTable } from "@/components/data-table"
import { EmptyState } from "@/components/empty-state"
import { JalaliDatePicker } from "@/components/jalali-date-picker"
import { Money } from "@/components/money"
import { StatusBadge } from "@/components/status-badge"
import { useDebounced } from "@/hooks/use-debounced"
import { api } from "@/services/api"
import type { Paged } from "@/services/types"
import { fa, formatDate, formatMoney } from "@/lib/format"
import { useCurrencies } from "@/hooks/use-settings"
import { PAYMENT_METHODS, type Payment } from "@/features/invoices/api"

export default function PaymentsPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState("")
  const [from, setFrom] = useState<string | null>(null)
  const [to, setTo] = useState<string | null>(null)
  const [method, setMethod] = useState<string>("all")
  const [showReversed, setShowReversed] = useState(false)
  const q = useDebounced(search)
  const params = { search: q, from: from ?? undefined, to: to ?? undefined, method: method === "all" ? undefined : method, includeReversed: showReversed, pageSize: 500 }
  const { data, isLoading } = useQuery({ queryKey: ["payments", params], queryFn: () => api.get<Paged<Payment>>("/payments", params), placeholderData: (p) => p })
  const { byCode } = useCurrencies()
  // Amounts in different currencies are never added together: one total per currency.
  const sums = new Map<string, number>()
  for (const p of data?.items ?? []) if (p.status === "Active") sums.set(p.currencyCode, (sums.get(p.currencyCode) ?? 0) + p.amount)
  const totals = [...sums.entries()].map(([code, amount]) => formatMoney(amount, byCode(code)))

  return (
    <>
      <PageHeader title={t("پرداخت‌ها")} description={data ? t("{0} پرداخت — جمع:", [fa(data.total)]) : undefined}
        actions={data && <span className="num text-lg font-bold">{totals.length ? totals.join(" + ") : formatMoney(0, byCode(null))}</span>} />
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchInput value={search} onChange={setSearch} placeholder={t("شماره فاکتور، مشتری یا شماره پیگیری")} />
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <span>{t("از")}</span><JalaliDatePicker value={from} onChange={setFrom} clearable className="w-36" />
          <span>{t("تا")}</span><JalaliDatePicker value={to} onChange={setTo} clearable className="w-36" />
        </div>
        <Select value={method} onValueChange={setMethod} dir={i18n.dir}>
          <SelectTrigger className="w-44 bg-paper"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("همه روش‌ها")}</SelectItem>
            {Object.entries(PAYMENT_METHODS).map(([k, v]) => <SelectItem key={k} value={k}>{v}</SelectItem>)}
          </SelectContent>
        </Select>
        <div className="flex items-center gap-2">
          <Switch id="rev" checked={showReversed} onCheckedChange={setShowReversed} />
          <Label htmlFor="rev" className="text-sm text-muted-foreground">{t("برگشت‌خورده‌ها")}</Label>
        </div>
      </div>
      <DataTable
        rows={data?.items}
        loading={isLoading}
        rowKey={(p) => p.id}
        onRowClick={(p) => navigate(`/invoices/${p.invoiceId}`)}
        empty={<EmptyState icon={Wallet} title={t("پرداختی پیدا نشد.")} description={t("پرداخت‌ها از صفحه هر فاکتور صادرشده ثبت می‌شوند.")} />}
        columns={[
          { key: "date", header: t("تاریخ"), cell: (p) => <span className="num">{formatDate(p.paymentDate)}</span> },
          { key: "invoice", header: t("فاکتور"), cell: (p) => <span dir="ltr" className="num">{p.invoiceNumber}</span> },
          { key: "customer", header: t("مشتری"), hideOnMobile: true, cell: (p) => p.customerName },
          { key: "method", header: t("روش"), hideOnMobile: true, cell: (p) => PAYMENT_METHODS[p.method] },
          { key: "ref", header: t("پیگیری"), hideOnMobile: true, cell: (p) => <bdi dir="ltr" className="text-muted-foreground">{p.reference}</bdi> },
          {
            key: "amount", header: t("مبلغ"), align: "end",
            cell: (p) => p.status === "Reversed"
              ? <div className="flex flex-col items-end gap-1"><Money value={p.amount} currency={byCode(p.currencyCode)} className="text-muted-foreground line-through" /><StatusBadge tone="overdue">{t("برگشت‌خورده")}</StatusBadge></div>
              : <Money value={p.amount} currency={byCode(p.currencyCode)} className="font-medium" />,
          },
        ]}
      />
    </>
  )
}
