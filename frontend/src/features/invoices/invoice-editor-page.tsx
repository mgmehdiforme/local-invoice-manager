import { useMemo } from "react"
import { Link, Navigate, useNavigate, useParams, useSearchParams } from "react-router"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { ArrowLeft, ArrowRight } from "lucide-react"
import { i18n, t } from "@/i18n"
import { Skeleton } from "@/components/ui/skeleton"
import { DocumentEditor } from "@/features/documents/document-editor"
import { emptyLine, type DocumentForm } from "@/features/documents/types"
import { api } from "@/services/api"
import { useSettings } from "@/hooks/use-settings"
import { fa } from "@/lib/format"
import { fromInvoice, invoiceKeys, toInvoiceInput, useInvoice, type Invoice } from "./api"

interface Defaults { issueDate: string; dueDate: string | null; taxRate: number; defaultPaymentTerms: string | null; defaultNotes: string | null; templateKey: string; currencyCode?: string | null }

const BackIcon = i18n.dir === "rtl" ? ArrowRight : ArrowLeft

export default function InvoiceEditorPage() {
  const params = useParams()
  const id = params.id ? Number(params.id) : undefined
  const [search] = useSearchParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { data: settings } = useSettings()
  const existing = useInvoice(id)
  const defaults = useQuery({ queryKey: ["invoice-defaults"], queryFn: () => api.get<Defaults>("/invoices/defaults"), enabled: !id, staleTime: 0 })

  const initial = useMemo<DocumentForm | null>(() => {
    if (id) return existing.data ? fromInvoice(existing.data) : null
    if (!defaults.data) return null
    return {
      customerId: Number(search.get("customerId")) || 0,
      issueDate: defaults.data.issueDate,
      endDate: defaults.data.dueDate,
      discountType: "None",
      discountValue: 0,
      taxRate: defaults.data.taxRate,
      notes: defaults.data.defaultNotes ?? "",
      paymentTerms: defaults.data.defaultPaymentTerms ?? "",
      templateKey: defaults.data.templateKey,
      currencyCode: defaults.data.currencyCode ?? null,
      items: [emptyLine()],
    }
  }, [id, existing.data, defaults.data, search])

  if (existing.data && existing.data.status !== "Draft") return <Navigate to={`/invoices/${id}`} replace />
  if (!initial) return <Skeleton className="h-[480px] w-full" />

  let savedId = id
  const save = async (v: DocumentForm) => {
    const body = toInvoiceInput(v)
    const inv = savedId ? await api.put<Invoice>(`/invoices/${savedId}`, body) : await api.post<Invoice>("/invoices", body)
    qc.setQueryData([...invoiceKeys.all, "one", inv.id], inv)
    qc.invalidateQueries({ queryKey: invoiceKeys.all })
    if (!savedId) {
      savedId = inv.id
      navigate(`/invoices/${inv.id}/edit`, { replace: true })
    }
    return inv.id
  }

  return (
    <DocumentEditor
      kind="invoice"
      initial={initial}
      documentTitle={t("فاکتور فروش")}
      numberLabel={t("پس از صدور")}
      endDateLabel={t("سررسید")}
      heading={
        <div>
          <Link to="/invoices" className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"><BackIcon className="size-3.5" /> {t("فاکتورها")}</Link>
          <h1 className="text-xl font-bold">{id ? t("ویرایش پیش‌نویس") : t("فاکتور جدید")}</h1>
        </div>
      }
      onSave={save}
      primary={{
        label: t("صدور فاکتور"),
        confirmTitle: t("فاکتور صادر شود؟"),
        confirmText: (() => {
          const [before, after] = t("فاکتور شماره {0} (یا شماره آزاد بعدی) می‌گیرد و دیگر قابل ویرایش نیست. اطلاعات مشتری و فروشنده همین حالا روی فاکتور ثبت می‌شود.").split("{0}")
          return <>{before}<span dir="ltr" className="font-semibold">{fa(settings?.nextInvoiceNumberPreview ?? "")}</span>{after}</>
        })(),
        run: async (savedInvoiceId) => {
          await api.post(`/invoices/${savedInvoiceId}/issue`)
          await qc.invalidateQueries()
          navigate(`/invoices/${savedInvoiceId}`, { replace: true })
        },
      }}
    />
  )
}
