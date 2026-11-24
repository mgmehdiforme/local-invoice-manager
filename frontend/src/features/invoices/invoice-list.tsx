import { useNavigate } from "react-router"
import { FileText } from "lucide-react"
import { DataTable } from "@/components/data-table"
import { EmptyState } from "@/components/empty-state"
import { Money } from "@/components/money"
import { InvoiceStatusBadge } from "@/components/status-badge"
import { formatDate } from "@/lib/format"
import type { ReactNode } from "react"
import { t } from "@/i18n"
import type { InvoiceListItem } from "./api"

/** Invoice table used on the invoices page and the customer page. */
export function InvoiceList({ rows, loading, empty, hideCustomer }: { rows?: InvoiceListItem[]; loading?: boolean; empty?: ReactNode; hideCustomer?: boolean }) {
  const navigate = useNavigate()
  return (
    <DataTable
      rows={rows}
      loading={loading}
      rowKey={(i) => i.id}
      onRowClick={(i) => navigate(i.status === "Draft" ? `/invoices/${i.id}/edit` : `/invoices/${i.id}`)}
      empty={empty ?? <EmptyState icon={FileText} title={t("فاکتوری پیدا نشد.")} />}
      columns={[
        {
          key: "number",
          header: t("شماره"),
          cell: (i) => i.invoiceNumber ? <span dir="ltr" className="num font-medium">{i.invoiceNumber}</span> : <span className="text-muted-foreground">{t("بدون شماره")}</span>,
        },
        ...(hideCustomer ? [] : [{ key: "customer", header: t("مشتری"), cell: (i: InvoiceListItem) => <span className="font-medium">{i.customerName}</span> }]),
        { key: "date", header: t("تاریخ"), hideOnMobile: true, cell: (i) => <span className="num">{formatDate(i.issueDate)}</span> },
        {
          key: "due",
          header: t("سررسید"),
          hideOnMobile: true,
          cell: (i) => <span className={i.isOverdue ? "num font-medium text-overdue" : "num text-muted-foreground"}>{formatDate(i.dueDate)}</span>,
        },
        { key: "status", header: t("وضعیت"), cell: (i) => <InvoiceStatusBadge status={i.status} paymentStatus={i.paymentStatus} isOverdue={i.isOverdue} /> },
        {
          key: "total",
          header: t("مبلغ"),
          align: "end",
          cell: (i) => (
            <div className="flex flex-col items-end">
              <Money value={i.total} currency={i.currency} className="font-medium" />
              {i.status === "Issued" && i.paidAmount > 0 && i.balance > 0 && (
                <span className="text-xs text-muted-foreground">{t("مانده:")} <Money value={i.balance} currency={i.currency} symbol={false} /></span>
              )}
            </div>
          ),
        },
      ]}
    />
  )
}
