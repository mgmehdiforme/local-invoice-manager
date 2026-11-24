import { StatusBadge } from "@/components/status-badge"
import { STATUS_LABEL, type QuotationStatus } from "./api"
import { t } from "@/i18n"

export function QuotationStatusBadge({ status, isExpired }: { status: QuotationStatus; isExpired?: boolean }) {
  if (isExpired) return <StatusBadge tone="overdue">{t("منقضی‌شده")}</StatusBadge>
  const tone = status === "Accepted" ? "paid" : status === "Converted" ? "brand" : status === "Rejected" ? "muted" : status === "Sent" ? "partial" : "draft"
  return <StatusBadge tone={tone}>{STATUS_LABEL[status]}</StatusBadge>
}
