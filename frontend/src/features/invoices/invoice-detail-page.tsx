import { Link, useNavigate, useParams } from "react-router"
import { ArrowLeft, ArrowRight, Ban, Copy, Download, FileDown, Pencil, Printer, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { InvoiceStatusBadge } from "@/components/status-badge"
import { Money } from "@/components/money"
import { DocumentPaper } from "@/features/documents/document-paper"
import { TEMPLATE_LABELS } from "@/features/documents/types"
import { PaymentsPanel } from "@/features/payments/payments-panel"
import { ShareMenu } from "@/features/sharing/share-menu"
import { downloadFile, errorMessage } from "@/services/api"
import { formatDate, formatDateTime } from "@/lib/format"
import { useState } from "react"
import { useDeleteInvoice, useInvoice, useInvoiceAction } from "./api"
import { i18n, t } from "@/i18n"

const BackIcon = i18n.dir === "rtl" ? ArrowRight : ArrowLeft

export default function InvoiceDetailPage() {
  const id = Number(useParams().id)
  const navigate = useNavigate()
  const { data: inv, isLoading } = useInvoice(id)
  const action = useInvoiceAction()
  const del = useDeleteInvoice()
  const [template, setTemplate] = useState<string | null>(null)

  if (isLoading || !inv) return <Skeleton className="h-[480px] w-full" />
  const tpl = template ?? inv.templateKey
  const pdfUrl = `/api/v1/invoices/${inv.id}/pdf?template=${tpl}`
  const label = inv.invoiceNumber ?? t("پیش‌نویس")

  const duplicate = async () => {
    try {
      const copy = await action.mutateAsync({ id: inv.id, action: "duplicate" })
      toast.success(t("یک پیش‌نویس جدید از این فاکتور ساخته شد."))
      navigate(`/invoices/${copy.id}/edit`)
    } catch (e) {
      toast.error(errorMessage(e))
    }
  }

  return (
    <>
      <Link to="/invoices" className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <BackIcon className="size-4" /> {t("فاکتورها")}
      </Link>
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-bold" dir="ltr">{label}</h1>
            <InvoiceStatusBadge status={inv.status} paymentStatus={inv.paymentStatus} isOverdue={inv.isOverdue} />
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            <Link to={`/customers/${inv.customerId}`} className="hover:text-foreground hover:underline">{inv.customer.name}</Link>
            {" — "}{t("صدور {0}", [formatDate(inv.issueDate)])}{inv.dueDate && <> — {t("سررسید {0}", [formatDate(inv.dueDate)])}</>}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {inv.status === "Draft" && (
            <>
              <Button asChild><Link to={`/invoices/${inv.id}/edit`}><Pencil /> {t("ویرایش و صدور")}</Link></Button>
              <ConfirmDialog
                trigger={<Button variant="outline"><Trash2 /> {t("حذف پیش‌نویس")}</Button>}
                title={t("پیش‌نویس حذف شود؟")} description={t("این پیش‌نویس برای همیشه حذف می‌شود.")} confirmLabel={t("حذف")} destructive
                onConfirm={async () => {
                  try { await del.mutateAsync(inv.id); toast.success(t("پیش‌نویس حذف شد.")); navigate("/invoices") } catch (e) { toast.error(errorMessage(e)) }
                }}
              />
            </>
          )}
          <Button variant="outline" onClick={() => window.open(pdfUrl, "_blank", "noopener")}><Printer /> {t("چاپ")}</Button>
          <Button variant="outline" onClick={() => downloadFile(`/invoices/${inv.id}/pdf?template=${tpl}&download=true`, `${label}.pdf`).catch((e) => toast.error(errorMessage(e)))}>
            <FileDown /> PDF
          </Button>
          {inv.status === "Issued" && <ShareMenu invoice={inv} />}
          <Button variant="outline" onClick={duplicate}><Copy /> {t("کپی")}</Button>
          {inv.status === "Issued" && (
            <ConfirmDialog
              trigger={<Button variant="ghost" className="text-destructive"><Ban /> {t("ابطال")}</Button>}
              title={t("ابطال فاکتور {0}", [label])}
              description={t("فاکتور ابطال‌شده در گزارش‌ها حساب نمی‌شود، ولی شماره‌اش دوباره استفاده نمی‌شود. اگر پرداختی ثبت شده، اول آن را برگشت بزنید.")}
              confirmLabel={t("ابطال فاکتور")} destructive reasonLabel={t("دلیل ابطال")}
              onConfirm={async (reason) => {
                try { await action.mutateAsync({ id: inv.id, action: "cancel", body: { reason } }); toast.success(t("فاکتور ابطال شد.")) } catch (e) { toast.error(errorMessage(e)) }
              }}
            />
          )}
        </div>
      </div>

      {inv.status === "Cancelled" && (
        <div className="mb-5 rounded-lg border border-overdue/30 bg-overdue-soft p-3 text-sm text-overdue">
          {t("این فاکتور در {0} ابطال شد. دلیل: {1}", [formatDateTime(inv.cancelledAtUtc), inv.cancellationReason ?? ""])}
        </div>
      )}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
        <div>
          <div className="mb-2 flex items-center justify-between">
            <span className="text-xs text-muted-foreground">{inv.status === "Draft" ? t("پیش‌نمایش با اطلاعات فعلی فروشنده") : t("همان‌طور که صادر شد")}</span>
            <Select value={tpl} onValueChange={setTemplate} dir={i18n.dir}>
              <SelectTrigger size="sm" className="w-32 bg-paper"><SelectValue /></SelectTrigger>
              <SelectContent>{Object.entries(TEMPLATE_LABELS).map(([k, v]) => <SelectItem key={k} value={k}>{v}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <DocumentPaper
            title={t("فاکتور فروش")}
            number={label}
            meta={[{ label: t("تاریخ صدور"), value: formatDate(inv.issueDate) }, ...(inv.dueDate ? [{ label: t("سررسید"), value: formatDate(inv.dueDate) }] : [])]}
            seller={inv.seller}
            customer={inv.customer}
            items={inv.items}
            totals={{ lineTotals: inv.items.map((l) => l.lineTotal), lineDiscounts: inv.items.map((l) => l.discountAmount), subtotal: inv.subtotal, discountAmount: inv.discountAmount, taxableAmount: inv.taxableAmount, taxAmount: inv.taxAmount, total: inv.total }}
            currency={inv.currency}
            taxRate={inv.taxRate}
            discountType={inv.discountType}
            discountValue={inv.discountValue}
            extraTotals={inv.status === "Issued" && inv.paidAmount > 0 ? [{ label: t("پرداخت‌شده"), value: inv.paidAmount }, { label: t("مانده"), value: inv.balance, strong: true }] : undefined}
            notes={inv.notes}
            paymentTerms={inv.paymentTerms}
            template={tpl}
            logoUrl={inv.seller.logoFileId ? "/api/v1/settings/logo" : null}
            watermark={inv.status === "Draft" ? t("پیش‌نویس") : inv.status === "Cancelled" ? t("ابطال شده") : null}
          />
        </div>
        <aside className="grid content-start gap-4">
          <section className="rounded-xl border bg-paper p-4">
            <h2 className="mb-3 text-sm font-semibold">{t("خلاصه")}</h2>
            <dl className="grid gap-2 text-sm">
              <Row label={t("مبلغ فاکتور")} value={<Money value={inv.total} currency={inv.currency} className="font-semibold" />} />
              <Row label={t("پرداخت‌شده")} value={<Money value={inv.paidAmount} currency={inv.currency} />} />
              <Row label={t("مانده")} value={<Money value={inv.balance} currency={inv.currency} className={inv.isOverdue ? "font-bold text-overdue" : "font-bold"} />} />
              {inv.sourceQuotationId && <Row label={t("از پیش‌فاکتور")} value={<Link className="text-brand hover:underline" to={`/quotations/${inv.sourceQuotationId}`} dir="ltr">{inv.sourceQuotationNumber}</Link>} />}
              {inv.issuedAtUtc && <Row label={t("زمان صدور")} value={<span className="num">{formatDateTime(inv.issuedAtUtc)}</span>} />}
            </dl>
          </section>
          {inv.status === "Issued" && <PaymentsPanel invoice={inv} />}
          {inv.status === "Issued" && (
            <a href={pdfUrl} target="_blank" rel="noopener" className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground">
              <Download className="size-3.5" /> {t("باز کردن PDF در زبانه جدید")}
            </a>
          )}
        </aside>
      </div>
    </>
  )
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-muted-foreground">{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}
