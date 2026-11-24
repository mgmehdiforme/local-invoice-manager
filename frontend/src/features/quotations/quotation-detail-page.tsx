import { useState } from "react"
import { Link, useNavigate, useParams } from "react-router"
import { useQueryClient } from "@tanstack/react-query"
import { ArrowLeft, ArrowRight, Check, Copy, FileCheck2, FileDown, Pencil, Printer, Send, Trash2, X } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { Money } from "@/components/money"
import { DocumentPaper } from "@/features/documents/document-paper"
import { api, downloadFile, errorMessage } from "@/services/api"
import { formatDate } from "@/lib/format"
import type { Invoice } from "@/features/invoices/api"
import { useQuotation, type Quotation } from "./api"
import { QuotationStatusBadge } from "./quotation-status-badge"
import { i18n, t } from "@/i18n"

const BackIcon = i18n.dir === "rtl" ? ArrowRight : ArrowLeft

export default function QuotationDetailPage() {
  const id = Number(useParams().id)
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { data: q, isLoading } = useQuotation(id)
  const [busy, setBusy] = useState(false)

  if (isLoading || !q) return <Skeleton className="h-[480px] w-full" />
  const editable = q.status === "Draft" || q.status === "Sent"

  const act = async (action: string, success: string) => {
    setBusy(true)
    try {
      const updated = await api.post<Quotation>(`/quotations/${q.id}/${action}`)
      qc.setQueryData(["quotations", "one", q.id], updated)
      qc.invalidateQueries({ queryKey: ["quotations"] })
      toast.success(success)
      return updated
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const convert = async () => {
    try {
      const inv = await api.post<Invoice>(`/quotations/${q.id}/convert`)
      await qc.invalidateQueries()
      toast.success(t("پیش‌نویس فاکتور ساخته شد. بررسی و صادر کنید."))
      navigate(`/invoices/${inv.id}/edit`)
    } catch (e) {
      toast.error(errorMessage(e))
    }
  }

  return (
    <>
      <Link to="/quotations" className="mb-3 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"><BackIcon className="size-4" /> {t("پیش‌فاکتورها")}</Link>
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold" dir="ltr">{q.quotationNumber}</h1>
            <QuotationStatusBadge status={q.status} isExpired={q.isExpired} />
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            <Link to={`/customers/${q.customerId}`} className="hover:text-foreground hover:underline">{q.customer.name}</Link>
            {" — "}{formatDate(q.issueDate)}{q.validUntil && <> — {t("معتبر تا {0}", [formatDate(q.validUntil)])}</>} — <Money value={q.total} currency={q.currency} />
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {q.status === "Accepted" && (
            <ConfirmDialog trigger={<Button><FileCheck2 /> {t("تبدیل به فاکتور")}</Button>} title={t("تبدیل به فاکتور")}
              description={t("یک پیش‌نویس فاکتور با همین ردیف‌ها و قیمت‌ها ساخته می‌شود. این کار فقط یک بار ممکن است.")}
              confirmLabel={t("ساخت فاکتور")} onConfirm={convert} />
          )}
          {q.status === "Converted" && q.convertedInvoiceId && (
            <Button asChild><Link to={`/invoices/${q.convertedInvoiceId}`}><FileCheck2 /> {t("فاکتور")} <bdi dir="ltr">{q.convertedInvoiceNumber}</bdi></Link></Button>
          )}
          {editable && <Button variant="outline" asChild><Link to={`/quotations/${q.id}/edit`}><Pencil /> {t("ویرایش")}</Link></Button>}
          {q.status === "Draft" && <Button variant="outline" disabled={busy} onClick={() => act("send", t("به عنوان «ارسال‌شده» علامت خورد."))}><Send /> {t("ارسال شد")}</Button>}
          {editable && <Button variant="outline" disabled={busy} onClick={() => act("accept", t("پیش‌فاکتور تأیید شد."))}><Check /> {t("مشتری تأیید کرد")}</Button>}
          {editable && (
            <ConfirmDialog trigger={<Button variant="ghost"><X /> {t("رد شد")}</Button>} title={t("مشتری پیش‌فاکتور را رد کرد؟")}
              description={t("بعد از رد، پیش‌فاکتور قابل ویرایش یا تبدیل نیست. می‌توانید از آن کپی بگیرید.")} confirmLabel={t("ثبت رد")}
              onConfirm={async () => { await act("reject", t("به عنوان «ردشده» ثبت شد.")) }} />
          )}
          <Button variant="outline" onClick={() => window.open(`/api/v1/quotations/${q.id}/pdf`, "_blank", "noopener")}><Printer /> {t("چاپ")}</Button>
          <Button variant="outline" onClick={() => downloadFile(`/quotations/${q.id}/pdf?download=true`, `${q.quotationNumber}.pdf`).catch((e) => toast.error(errorMessage(e)))}><FileDown /> PDF</Button>
          <Button variant="ghost" disabled={busy} onClick={async () => { const d = await act("duplicate", t("کپی ساخته شد.")); if (d) navigate(`/quotations/${d.id}/edit`) }}><Copy /> {t("کپی")}</Button>
          {q.status === "Draft" && (
            <ConfirmDialog trigger={<Button variant="ghost" className="text-destructive"><Trash2 /> {t("حذف")}</Button>} title={t("پیش‌فاکتور حذف شود؟")}
              description={t("این پیش‌نویس برای همیشه حذف می‌شود.")} confirmLabel={t("حذف")} destructive
              onConfirm={async () => {
                try { await api.del(`/quotations/${q.id}`); qc.invalidateQueries({ queryKey: ["quotations"] }); toast.success(t("حذف شد.")); navigate("/quotations") }
                catch (e) { toast.error(errorMessage(e)) }
              }} />
          )}
        </div>
      </div>
      <div className="mx-auto max-w-[820px]">
        <DocumentPaper
          title={t("پیش‌فاکتور")}
          number={q.quotationNumber}
          meta={[{ label: t("تاریخ"), value: formatDate(q.issueDate) }, ...(q.validUntil ? [{ label: t("معتبر تا"), value: formatDate(q.validUntil) }] : [])]}
          seller={{ ...q.seller, bankName: null, bankAccountNumber: null, bankCardNumber: null, bankIban: null, bankAccountHolder: null }}
          customer={q.customer}
          items={q.items}
          totals={{ lineTotals: q.items.map((l) => l.lineTotal), lineDiscounts: q.items.map((l) => l.discountAmount), subtotal: q.subtotal, discountAmount: q.discountAmount, taxableAmount: q.taxableAmount, taxAmount: q.taxAmount, total: q.total }}
          currency={q.currency}
          taxRate={q.taxRate}
          discountType={q.discountType}
          discountValue={q.discountValue}
          notes={q.notes}
          paymentTerms={q.paymentTerms}
          template={q.templateKey}
          logoUrl={q.seller.logoFileId ? "/api/v1/settings/logo" : null}
          watermark={q.status === "Rejected" ? t("رد شده") : null}
        />
      </div>
    </>
  )
}
