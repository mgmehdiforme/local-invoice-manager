import { t } from "@/i18n"
import { Link } from "react-router"
import { FilePlus2, FileText } from "lucide-react"
import { Button } from "@/components/ui/button"
import { EmptyState } from "@/components/empty-state"
import { useInvoices } from "@/features/invoices/api"
import { InvoiceList } from "@/features/invoices/invoice-list"

export function CustomerInvoices({ customerId }: { customerId: number }) {
  const { data, isLoading } = useInvoices({ customerId })
  return (
    <InvoiceList
      rows={data?.items}
      loading={isLoading}
      hideCustomer
      empty={<EmptyState icon={FileText} title={t("برای این مشتری فاکتوری ثبت نشده است.")}
        action={<Button asChild><Link to={`/invoices/new?customerId=${customerId}`}><FilePlus2 /> {t("فاکتور جدید")}</Link></Button>} />}
    />
  )
}
