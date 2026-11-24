import { cn } from "@/lib/utils"
import { t } from "@/i18n"

type Tone = "paid" | "partial" | "overdue" | "draft" | "brand" | "muted"

const tones: Record<Tone, string> = {
  paid: "bg-paid-soft text-paid",
  partial: "bg-partial-soft text-partial",
  overdue: "bg-overdue-soft text-overdue",
  draft: "bg-draft-soft text-draft",
  brand: "bg-brand-soft text-brand",
  muted: "bg-muted text-muted-foreground",
}

export function StatusBadge({ tone, children, className }: { tone: Tone; children: React.ReactNode; className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap", tones[tone], className)}>
      <span className="size-1.5 rounded-full bg-current" aria-hidden />
      {children}
    </span>
  )
}

export interface DocStatusInput {
  status: "Draft" | "Issued" | "Cancelled"
  paymentStatus?: "Unpaid" | "PartiallyPaid" | "Paid"
  isOverdue?: boolean
}

/** One badge that tells the invoice's most important state. */
export function InvoiceStatusBadge({ status, paymentStatus, isOverdue }: DocStatusInput) {
  if (status === "Draft") return <StatusBadge tone="draft">{t("پیش‌نویس")}</StatusBadge>
  if (status === "Cancelled") return <StatusBadge tone="muted">{t("ابطال‌شده")}</StatusBadge>
  if (paymentStatus === "Paid") return <StatusBadge tone="paid">{t("پرداخت‌شده")}</StatusBadge>
  if (isOverdue) return <StatusBadge tone="overdue">{t("سررسید گذشته")}</StatusBadge>
  if (paymentStatus === "PartiallyPaid") return <StatusBadge tone="partial">{t("پرداخت ناقص")}</StatusBadge>
  return <StatusBadge tone="brand">{t("پرداخت‌نشده")}</StatusBadge>
}
