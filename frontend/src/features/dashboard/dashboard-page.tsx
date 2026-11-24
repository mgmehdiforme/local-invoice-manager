import { useState } from "react"
import { Link } from "react-router"
import { AlertTriangle, ArrowLeft, ArrowRight, CalendarClock, FilePlus2, FileText } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { Money } from "@/components/money"
import { EmptyState } from "@/components/empty-state"
import { InvoiceList } from "@/features/invoices/invoice-list"
import { useInvoices } from "@/features/invoices/api"
import { RevenueChart } from "@/features/reports/revenue-chart"
import { useMonthly, useOverdue, useSummary } from "@/features/reports/api"
import { useCurrencies, useSettings } from "@/hooks/use-settings"
import { CurrencySelect } from "@/features/reports/currency-select"
import { i18n, t } from "@/i18n"
import { fa, formatDate, formatDateLong } from "@/lib/format"
import { todayIso } from "@/lib/jalali"
import { cn } from "@/lib/utils"

export default function DashboardPage() {
  const [currencyCode, setCurrencyCode] = useState("")
  const { byCode } = useCurrencies()
  const cur = byCode(currencyCode)
  const { data: s } = useSummary(currencyCode)
  const { data: monthly } = useMonthly(12, currencyCode)
  const Forward = i18n.dir === "rtl" ? ArrowLeft : ArrowRight
  const { data: overdue } = useOverdue()
  const recent = useInvoices({ pageSize: 6 })
  const { data: settings } = useSettings()
  const collected = s ? s.totalReceived : 0
  const total = s ? s.totalReceived + s.outstanding : 0

  return (
    <>
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm text-muted-foreground">{formatDateLong(todayIso())}</p>
          <h1 className="text-2xl font-bold tracking-tight">{settings?.companyName || t("داشبورد")}</h1>
        </div>
        <div className="flex items-center gap-2">
          <CurrencySelect value={currencyCode} onChange={setCurrencyCode} />
          <Button asChild size="lg"><Link to="/invoices/new"><FilePlus2 /> {t("فاکتور جدید")}</Link></Button>
        </div>
      </div>

      {!s ? <Skeleton className="h-36 w-full rounded-xl" /> : (
        <section className="mb-6 rounded-xl border bg-paper" aria-label={t("خلاصه مالی")}>
          <div className="grid grid-cols-2 lg:grid-cols-4">
            <Figure label={t("طلب از مشتریان")} value={<Money value={s.outstanding} currency={cur} />} sub={t("از {0} فاکتور صادرشده", [fa(s.issuedCount)])} strong />
            <Figure
              label={t("سررسید گذشته")}
              value={<Money value={s.overdueAmount} currency={cur} className={s.overdueCount ? "text-overdue" : undefined} />}
              sub={s.overdueCount ? t("{0} فاکتور", [fa(s.overdueCount)]) : t("موردی نیست")}
              href={s.overdueCount ? "/invoices?filter=overdue" : undefined}
            />
            <Figure label={t("دریافتی {0}", [fa(s.monthLabel)])} value={<Money value={s.monthReceived} currency={cur} />} sub={<>{t("فاکتورشده:")} <Money value={s.monthInvoiced} currency={cur} symbol={false} /></>} />
            <Figure label={t("جمع کل فاکتورها")} value={<Money value={s.totalInvoiced} currency={cur} />} sub={t("{0} پیش‌نویس، {1} مشتری", [fa(s.draftCount), fa(s.customerCount)])} />
          </div>
          {total > 0 && (
            <div className="border-t px-5 py-3">
              <div className="mb-1.5 flex justify-between text-xs text-muted-foreground">
                <span>{t("وصول‌شده {0}٪", [fa(Math.round((collected / total) * 100))])}</span>
                <span>{t("مانده {0}٪", [fa(Math.round((s.outstanding / total) * 100))])}</span>
              </div>
              <div className="flex h-2 gap-0.5 overflow-hidden rounded-full" aria-hidden>
                <div className="rounded-full bg-paid" style={{ width: `${(collected / total) * 100}%` }} />
                <div className="flex-1 rounded-full bg-muted" />
              </div>
            </div>
          )}
        </section>
      )}

      <div className="mb-6 grid gap-6 lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)]">
        <section className="rounded-xl border bg-paper p-5">
          <h2 className="mb-3 font-semibold">{t("درآمد ۱۲ ماه اخیر")}</h2>
          {monthly ? <RevenueChart data={monthly} currency={cur} /> : <Skeleton className="h-64 w-full" />}
        </section>
        <section className="rounded-xl border bg-paper p-5">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="font-semibold">{t("نیاز به پیگیری")}</h2>
            {s && s.dueSoonCount > 0 && (
              <span className="inline-flex items-center gap-1 text-xs text-muted-foreground"><CalendarClock className="size-3.5" />{t("{0} فاکتور به‌زودی سررسید", [fa(s.dueSoonCount)])}</span>
            )}
          </div>
          {!overdue ? <Skeleton className="h-40 w-full" /> : overdue.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">{t("هیچ فاکتوری از سررسید نگذشته است.")}</p>
          ) : (
            <ul className="grid gap-1">
              {overdue.slice(0, 6).map((i) => (
                <li key={i.id}>
                  <Link to={`/invoices/${i.id}`} className="flex items-center justify-between gap-3 rounded-lg px-2 py-2 hover:bg-accent">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-medium">{i.customerName}</div>
                      <div className="flex flex-wrap items-center gap-x-1 text-xs text-overdue">
                        <AlertTriangle className="size-3 shrink-0" />
                        <span dir="ltr" className="whitespace-nowrap">{i.invoiceNumber}</span>
                        <span className="whitespace-nowrap">— {t("سررسید {0}", [formatDate(i.dueDate)])}</span>
                      </div>
                    </div>
                    <Money value={i.balance} currency={i.currency} className="text-sm font-semibold" />
                  </Link>
                </li>
              ))}
              {overdue.length > 6 && (
                <li><Link to="/invoices?filter=overdue" className="flex items-center gap-1 px-2 py-1 text-xs text-brand hover:underline">{t("همه {0} مورد", [fa(overdue.length)])} <Forward className="size-3" /></Link></li>
              )}
            </ul>
          )}
        </section>
      </div>

      <div className="mb-3 flex items-center justify-between">
        <h2 className="font-semibold">{t("آخرین فاکتورها")}</h2>
        <Link to="/invoices" className="inline-flex items-center gap-1 text-sm text-brand hover:underline">{t("همه فاکتورها")} <Forward className="size-3.5" /></Link>
      </div>
      <InvoiceList
        rows={recent.data?.items.slice(0, 6)}
        loading={recent.isLoading}
        empty={<EmptyState icon={FileText} title={t("هنوز فاکتوری ثبت نشده است.")} description={t("با ساخت اولین فاکتور، این صفحه پر می‌شود.")}
          action={<Button asChild><Link to="/invoices/new"><FilePlus2 /> {t("فاکتور جدید")}</Link></Button>} />}
      />
    </>
  )
}

function Figure({ label, value, sub, strong, href }: { label: string; value: React.ReactNode; sub?: React.ReactNode; strong?: boolean; href?: string }) {
  const body = (
    <>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={cn("mt-1 font-bold tracking-tight", strong ? "text-2xl" : "text-xl")}>{value}</div>
      {sub && <div className="mt-0.5 text-xs text-muted-foreground">{sub}</div>}
    </>
  )
  const cls = "block px-5 py-4 border-border [&:nth-child(2n)]:border-s lg:[&:not(:first-child)]:border-s [&:nth-child(n+3)]:border-t lg:[&:nth-child(n+3)]:border-t-0"
  return href ? <Link to={href} className={cn(cls, "hover:bg-accent/50")}>{body}</Link> : <div className={cls}>{body}</div>
}
