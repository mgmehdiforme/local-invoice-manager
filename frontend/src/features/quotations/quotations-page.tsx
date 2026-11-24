import { useState } from "react"
import { Link, useNavigate, useSearchParams } from "react-router"
import { FilePlus2, ReceiptText } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { PageHeader } from "@/components/page-header"
import { SearchInput } from "@/components/search-input"
import { DataTable } from "@/components/data-table"
import { EmptyState } from "@/components/empty-state"
import { Money } from "@/components/money"
import { useDebounced } from "@/hooks/use-debounced"
import { fa, formatDate } from "@/lib/format"
import { STATUS_LABEL, useQuotations, type QuotationStatus } from "./api"
import { QuotationStatusBadge } from "./quotation-status-badge"
import { i18n, t } from "@/i18n"

const TABS: (QuotationStatus | "all")[] = ["all", "Draft", "Sent", "Accepted", "Converted", "Rejected"]

export default function QuotationsPage() {
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const tab = (params.get("status") ?? "all") as QuotationStatus | "all"
  const [search, setSearch] = useState("")
  const q = useDebounced(search)
  const { data, isLoading } = useQuotations({ search: q, status: tab === "all" ? undefined : tab })

  return (
    <>
      <PageHeader title={t("پیش‌فاکتورها")} description={data ? t("{0} پیش‌فاکتور", [fa(data.total)]) : undefined}
        actions={<Button asChild><Link to="/quotations/new"><FilePlus2 /> {t("پیش‌فاکتور جدید")}</Link></Button>} />
      <Tabs value={tab} onValueChange={(v) => setParams(v === "all" ? {} : { status: v })} dir={i18n.dir} className="mb-4">
        <TabsList className="h-auto flex-wrap justify-start">
          {TABS.map((s) => <TabsTrigger key={s} value={s}>{s === "all" ? t("همه") : STATUS_LABEL[s]}</TabsTrigger>)}
        </TabsList>
      </Tabs>
      <div className="mb-4"><SearchInput value={search} onChange={setSearch} placeholder={t("شماره یا نام مشتری")} /></div>
      <DataTable
        rows={data?.items}
        loading={isLoading}
        rowKey={(x) => x.id}
        onRowClick={(x) => navigate(`/quotations/${x.id}`)}
        empty={<EmptyState icon={ReceiptText} title={q || tab !== "all" ? t("پیش‌فاکتوری پیدا نشد.") : t("هنوز پیش‌فاکتوری نساخته‌اید.")}
          description={!q && tab === "all" ? t("پیش‌فاکتور را برای مشتری بفرستید؛ بعد از تأیید با یک کلیک به فاکتور تبدیل می‌شود.") : undefined}
          action={!q && tab === "all" && <Button asChild><Link to="/quotations/new"><FilePlus2 /> {t("پیش‌فاکتور جدید")}</Link></Button>} />}
        columns={[
          { key: "number", header: t("شماره"), cell: (x) => <span dir="ltr" className="num font-medium">{x.quotationNumber}</span> },
          { key: "customer", header: t("مشتری"), cell: (x) => <span className="font-medium">{x.customerName}</span> },
          { key: "date", header: t("تاریخ"), hideOnMobile: true, cell: (x) => <span className="num">{formatDate(x.issueDate)}</span> },
          { key: "valid", header: t("معتبر تا"), hideOnMobile: true, cell: (x) => <span className={x.isExpired ? "num text-overdue" : "num text-muted-foreground"}>{formatDate(x.validUntil)}</span> },
          { key: "status", header: t("وضعیت"), cell: (x) => <QuotationStatusBadge status={x.status} isExpired={x.isExpired} /> },
          { key: "total", header: t("مبلغ"), align: "end", cell: (x) => <Money value={x.total} currency={x.currency} className="font-medium" /> },
        ]}
      />
    </>
  )
}
