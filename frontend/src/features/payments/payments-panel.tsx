import { t } from "@/i18n"
import { useQueryClient } from "@tanstack/react-query"
import { Plus, Undo2 } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { Money } from "@/components/money"
import { api, errorMessage } from "@/services/api"
import { formatDate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { PAYMENT_METHODS, type Invoice } from "@/features/invoices/api"
import { PaymentDialog } from "./payment-dialog"

/** Payment history of one invoice, with record and reverse actions. */
export function PaymentsPanel({ invoice }: { invoice: Invoice }) {
  const qc = useQueryClient()
  const paidRatio = invoice.total > 0 ? Math.min(1, invoice.paidAmount / invoice.total) : 0

  return (
    <section className="rounded-xl border bg-paper p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold">{t("پرداخت‌ها")}</h2>
        {invoice.balance > 0 && <PaymentDialog invoice={invoice} trigger={<Button size="sm"><Plus /> {t("ثبت پرداخت")}</Button>} />}
      </div>
      <div className="mb-4 h-2 overflow-hidden rounded-full bg-muted" role="progressbar" aria-valuenow={Math.round(paidRatio * 100)} aria-label={t("نسبت پرداخت‌شده")}>
        <div className="h-full rounded-full bg-paid transition-[width]" style={{ width: `${paidRatio * 100}%` }} />
      </div>
      {invoice.payments.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("هنوز پرداختی ثبت نشده است.")}</p>
      ) : (
        <ul className="grid gap-2">
          {invoice.payments.map((p) => (
            <li key={p.id} className={cn("rounded-lg border p-2.5 text-sm", p.status === "Reversed" && "bg-muted/50")}>
              <div className="flex items-center justify-between gap-2">
                <Money value={p.amount} currency={invoice.currency} className={cn("font-semibold", p.status === "Reversed" && "text-muted-foreground line-through")} />
                {p.status === "Active" ? (
                  <ConfirmDialog
                    trigger={<Button variant="ghost" size="icon" className="size-7" aria-label={t("برگشت پرداخت")} title={t("برگشت پرداخت")}><Undo2 className="size-4" /></Button>}
                    title={t("برگشت این پرداخت؟")}
                    description={t("پرداخت حذف نمی‌شود؛ با دلیل در تاریخچه می‌ماند و مانده فاکتور دوباره زیاد می‌شود.")}
                    confirmLabel={t("برگشت پرداخت")} destructive reasonLabel={t("دلیل (مثلاً: چک برگشت خورد)")}
                    onConfirm={async (reason) => {
                      try {
                        const updated = await api.post<Invoice>(`/payments/${p.id}/reverse`, { reason })
                        qc.setQueryData(["invoices", "one", invoice.id], updated)
                        await qc.invalidateQueries()
                        toast.success(t("پرداخت برگشت خورد."))
                      } catch (e) { toast.error(errorMessage(e)) }
                    }}
                  />
                ) : <span className="text-xs text-overdue">{t("برگشت‌خورده")}</span>}
              </div>
              <div className="mt-0.5 text-xs text-muted-foreground">
                {formatDate(p.paymentDate)} — {PAYMENT_METHODS[p.method]}{p.reference && <> — <bdi dir="ltr">{p.reference}</bdi></>}
              </div>
              {p.status === "Reversed" && p.reversalReason && <div className="mt-0.5 text-xs text-overdue">{t("دلیل:")} {p.reversalReason}</div>}
              {p.notes && <div className="mt-0.5 text-xs">{p.notes}</div>}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
