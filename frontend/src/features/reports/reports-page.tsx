import { useMemo, useState } from "react"
import { Link } from "react-router"
import { FileDown } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { PageHeader } from "@/components/page-header"
import { JalaliDatePicker } from "@/components/jalali-date-picker"
import { Money } from "@/components/money"
import { downloadFile, errorMessage, withQuery } from "@/services/api"
import { fa } from "@/lib/format"
import { addDaysIso, todayIso } from "@/lib/jalali"
import { fromParts, toParts } from "@/lib/calendar"
import { useCurrencies } from "@/hooks/use-settings"
import { i18n, t } from "@/i18n"
import { CurrencySelect } from "./currency-select"
import { RevenueChart } from "./revenue-chart"
import { useCustomerReport, useRevenue } from "./api"

/** Date range shortcuts in the active calendar (Jalali or Gregorian). */
function presets() {
  const today = todayIso()
  const p = toParts(today)
  const monthStart = fromParts({ y: p.y, m: p.m, d: 1 })
  const prev = p.m === 1 ? { y: p.y - 1, m: 12 } : { y: p.y, m: p.m - 1 }
  const quarterStartMonth = Math.floor((p.m - 1) / 3) * 3 + 1
  return [
    { key: "month", label: t("این ماه"), from: monthStart, to: today },
    { key: "prev", label: t("ماه قبل"), from: fromParts({ ...prev, d: 1 }), to: addDaysIso(monthStart, -1) },
    { key: "quarter", label: t("این فصل"), from: fromParts({ y: p.y, m: quarterStartMonth, d: 1 }), to: today },
    { key: "year", label: t("سال {0}", [fa(p.y)]), from: fromParts({ y: p.y, m: 1, d: 1 }), to: today },
  ]
}

export default function ReportsPage() {
  const ranges = useMemo(presets, [])
  const [range, setRange] = useState(ranges[3])
  const [from, setFrom] = useState(range.from)
  const [to, setTo] = useState(range.to)
  const [groupBy, setGroupBy] = useState<"month" | "day">("month")
  const [currencyCode, setCurrencyCode] = useState("")
  const cur = useCurrencies().byCode(currencyCode)
  const revenue = useRevenue(from, to, groupBy, currencyCode)
  const customers = useCustomerReport(from, to, currencyCode)

  const pick = (key: string) => {
    const r = ranges.find((x) => x.key === key)!
    setRange(r); setFrom(r.from); setTo(r.to)
  }
  const sum = (k: "invoiced" | "received" | "tax") => revenue.data?.reduce((s, r) => s + r[k], 0) ?? 0
  const csv = (path: string, name: string) => downloadFile(withQuery(path, { from, to, groupBy, currency: currencyCode }), name).catch((e) => toast.error(errorMessage(e)))

  return (
    <>
      <PageHeader title={t("گزارش‌ها")} description={t("فقط فاکتورهای صادرشده حساب می‌شوند؛ پیش‌نویس و ابطال‌شده‌ها کنار گذاشته می‌شوند.")} />
      <div className="mb-6 flex flex-wrap items-center gap-3 rounded-xl border bg-paper p-3">
        <Tabs value={range.key} onValueChange={pick} dir={i18n.dir}>
          <TabsList>{ranges.map((r) => <TabsTrigger key={r.key} value={r.key}>{r.label}</TabsTrigger>)}</TabsList>
        </Tabs>
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <span>{t("از")}</span><JalaliDatePicker value={from} onChange={(v) => { if (v) { setFrom(v); setRange({ ...range, key: "custom" }) } }} className="w-36" />
          <span>{t("تا")}</span><JalaliDatePicker value={to} onChange={(v) => { if (v) { setTo(v); setRange({ ...range, key: "custom" }) } }} className="w-36" />
        </div>
        <div className="ms-auto flex items-center gap-2">
          <CurrencySelect value={currencyCode} onChange={setCurrencyCode} />
          <Tabs value={groupBy} onValueChange={(v) => setGroupBy(v as "month" | "day")} dir={i18n.dir}>
            <TabsList><TabsTrigger value="month">{t("ماهانه")}</TabsTrigger><TabsTrigger value="day">{t("روزانه")}</TabsTrigger></TabsList>
          </Tabs>
        </div>
      </div>

      <section className="mb-6 rounded-xl border bg-paper p-5">
        <div className="mb-4 flex flex-wrap items-start justify-between gap-4">
          <div className="flex flex-wrap gap-8">
            <Stat label={t("فاکتورشده")} value={<Money value={sum("invoiced")} currency={cur} />} />
            <Stat label={t("دریافت‌شده")} value={<Money value={sum("received")} currency={cur} />} />
            <Stat label={t("مالیات")} value={<Money value={sum("tax")} currency={cur} />} />
          </div>
          <Button variant="outline" size="sm" onClick={() => csv("/reports/revenue.csv", "revenue.csv")}><FileDown /> {t("خروجی Excel (CSV)")}</Button>
        </div>
        {revenue.data ? <RevenueChart data={revenue.data} height={280} currency={cur} /> : <Skeleton className="h-72 w-full" />}
        {revenue.data && revenue.data.length > 0 && (
          <details className="mt-4">
            <summary className="cursor-pointer text-sm text-muted-foreground">{t("نمایش جدول")}</summary>
            <Table className="mt-2">
              <TableHeader><TableRow><TableHead>{t("دوره")}</TableHead><TableHead className="text-end">{t("تعداد")}</TableHead><TableHead className="text-end">{t("فاکتورشده")}</TableHead><TableHead className="text-end">{t("مالیات")}</TableHead><TableHead className="text-end">{t("دریافت‌شده")}</TableHead></TableRow></TableHeader>
              <TableBody>
                {revenue.data.filter((r) => groupBy === "month" || r.invoiced || r.received).map((r) => (
                  <TableRow key={r.key}>
                    <TableCell>{fa(r.label)}</TableCell>
                    <TableCell className="num text-end">{fa(r.invoiceCount)}</TableCell>
                    <TableCell className="text-end"><Money value={r.invoiced} currency={cur} symbol={false} /></TableCell>
                    <TableCell className="text-end"><Money value={r.tax} currency={cur} symbol={false} /></TableCell>
                    <TableCell className="text-end"><Money value={r.received} currency={cur} symbol={false} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </details>
        )}
      </section>

      <section className="rounded-xl border bg-paper p-5">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-semibold">{t("درآمد به تفکیک مشتری")}</h2>
          <Button variant="outline" size="sm" onClick={() => csv("/reports/customers.csv", "customers.csv")}><FileDown /> {t("خروجی Excel (CSV)")}</Button>
        </div>
        {!customers.data ? <Skeleton className="h-40 w-full" /> : customers.data.length === 0 ? (
          <p className="py-8 text-center text-sm text-muted-foreground">{t("در این بازه فاکتوری صادر نشده است.")}</p>
        ) : (
          <Table>
            <TableHeader><TableRow><TableHead>{t("مشتری")}</TableHead><TableHead className="text-end">{t("فاکتور")}</TableHead><TableHead className="text-end">{t("جمع فاکتورها")}</TableHead><TableHead className="text-end">{t("پرداخت‌شده")}</TableHead><TableHead className="text-end">{t("مانده")}</TableHead></TableRow></TableHeader>
            <TableBody>
              {customers.data.map((r) => (
                <TableRow key={r.customerId}>
                  <TableCell><Link to={`/customers/${r.customerId}`} className="font-medium hover:underline">{r.customerName}</Link></TableCell>
                  <TableCell className="num text-end">{fa(r.invoiceCount)}</TableCell>
                  <TableCell className="text-end"><Money value={r.invoiced} currency={cur} symbol={false} /></TableCell>
                  <TableCell className="text-end"><Money value={r.paid} currency={cur} symbol={false} /></TableCell>
                  <TableCell className="text-end"><Money value={r.balance} currency={cur} symbol={false} className={r.overdue > 0 ? "font-semibold text-overdue" : undefined} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
            <TableFooter>
              <TableRow>
                <TableCell>{t("جمع")}</TableCell>
                <TableCell className="num text-end">{fa(customers.data.reduce((s, r) => s + r.invoiceCount, 0))}</TableCell>
                <TableCell className="text-end"><Money value={customers.data.reduce((s, r) => s + r.invoiced, 0)} currency={cur} symbol={false} /></TableCell>
                <TableCell className="text-end"><Money value={customers.data.reduce((s, r) => s + r.paid, 0)} currency={cur} symbol={false} /></TableCell>
                <TableCell className="text-end"><Money value={customers.data.reduce((s, r) => s + r.balance, 0)} currency={cur} symbol={false} /></TableCell>
              </TableRow>
            </TableFooter>
          </Table>
        )}
      </section>
    </>
  )
}

function Stat({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-xl font-bold">{value}</div>
    </div>
  )
}
