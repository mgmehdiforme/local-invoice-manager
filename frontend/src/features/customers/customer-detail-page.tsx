import { Link, useParams } from "react-router"
import { ArrowLeft, ArrowRight, FilePlus2, Mail, MapPin, Pencil, Phone } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { Money } from "@/components/money"
import { StatusBadge } from "@/components/status-badge"
import { fa, formatMoney } from "@/lib/format"
import { useCurrencies } from "@/hooks/use-settings"
import { i18n, t } from "@/i18n"
import { useCustomer, useCustomerBalance } from "./api"
import { CustomerFormDialog } from "./customer-form-dialog"
import { CustomerInvoices } from "./customer-invoices"
import { CustomerPrices } from "./customer-prices"

export default function CustomerDetailPage() {
  const id = Number(useParams().id)
  const { data: c, isLoading } = useCustomer(id)
  const { data: b } = useCustomerBalance(id)
  const { byCode } = useCurrencies()
  const BackIcon = i18n.dir === "rtl" ? ArrowRight : ArrowLeft

  if (isLoading || !c) return <Skeleton className="h-40 w-full" />

  return (
    <>
      <Link to="/customers" className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <BackIcon className="size-4" /> {t("مشتریان")}
      </Link>
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-bold">
            {c.name} {!c.isActive && <StatusBadge tone="muted">{t("بایگانی‌شده")}</StatusBadge>}
          </h1>
          {c.companyName && <p className="text-muted-foreground">{c.companyName}</p>}
          <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-sm text-muted-foreground">
            {(c.mobile || c.phone) && <span className="inline-flex items-center gap-1.5"><Phone className="size-3.5" /><span dir="ltr" className="num">{fa(c.mobile || c.phone)}</span></span>}
            {c.email && <span className="inline-flex items-center gap-1.5"><Mail className="size-3.5" />{c.email}</span>}
            {c.address && <span className="inline-flex items-center gap-1.5"><MapPin className="size-3.5" />{(i18n.lang === "fa" ? [c.city, c.address] : [c.address, c.city]).filter(Boolean).join(t("، "))}</span>}
          </div>
        </div>
        <div className="flex gap-2">
          <CustomerFormDialog customer={c} trigger={<Button variant="outline"><Pencil /> {t("ویرایش")}</Button>} />
          <Button asChild><Link to={`/invoices/new?customerId=${c.id}`}><FilePlus2 /> {t("فاکتور جدید")}</Link></Button>
        </div>
      </div>

      <div className="mb-8 grid grid-cols-2 divide-x rtl:divide-x-reverse rounded-xl border bg-paper md:grid-cols-4">
        <Figure label={t("جمع فاکتورها")} value={<Money value={b?.totalInvoiced} />} />
        <Figure label={t("دریافت‌شده")} value={<Money value={b?.totalPaid} />} />
        <Figure label={t("مانده بدهی")} value={<Money value={b?.balance} className="text-foreground" />} strong />
        <Figure label={t("سررسید گذشته")} value={<Money value={b?.overdueAmount} className={b?.overdueAmount ? "text-overdue" : undefined} />} />
      </div>

      {b && b.byCurrency.length > 1 && (
        <div className="-mt-5 mb-8 rounded-xl border bg-paper p-4">
          <h2 className="mb-2 text-sm font-semibold">{t("مانده به تفکیک ارز")}</h2>
          <table className="w-full text-sm">
            <thead className="text-xs text-muted-foreground">
              <tr>
                <th className="py-1 text-start font-normal">{t("ارز")}</th>
                <th className="py-1 text-end font-normal">{t("جمع فاکتورها")}</th>
                <th className="py-1 text-end font-normal">{t("دریافت‌شده")}</th>
                <th className="py-1 text-end font-normal">{t("مانده بدهی")}</th>
                <th className="py-1 text-end font-normal">{t("سررسید گذشته")}</th>
              </tr>
            </thead>
            <tbody>
              {b.byCurrency.map((r) => {
                const cur = byCode(r.currencyCode)
                return (
                  <tr key={r.currencyCode} className="border-t">
                    <td className="py-1.5 font-medium">{r.currencyCode}</td>
                    <td className="num py-1.5 text-end">{formatMoney(r.totalInvoiced, cur)}</td>
                    <td className="num py-1.5 text-end">{formatMoney(r.totalPaid, cur)}</td>
                    <td className="num py-1.5 text-end font-semibold">{formatMoney(r.balance, cur)}</td>
                    <td className={`num py-1.5 text-end ${r.overdueAmount ? "text-overdue" : ""}`}>{formatMoney(r.overdueAmount, cur)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {c.notes && <p className="mb-6 rounded-lg border border-dashed bg-paper p-3 text-sm text-muted-foreground">{c.notes}</p>}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
        <div>
          <h2 className="mb-3 text-lg font-semibold">{t("فاکتورها")}</h2>
          <CustomerInvoices customerId={c.id} />
        </div>
        <aside><CustomerPrices customerId={c.id} /></aside>
      </div>
    </>
  )
}

function Figure({ label, value, strong }: { label: string; value: React.ReactNode; strong?: boolean }) {
  return (
    <div className="px-5 py-4 [&:nth-child(n+3)]:border-t md:[&:nth-child(n+3)]:border-t-0">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={strong ? "mt-1 text-xl font-bold" : "mt-1 text-lg font-semibold"}>{value}</div>
    </div>
  )
}
