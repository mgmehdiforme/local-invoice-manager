import { useMemo } from "react"
import { Link, Navigate, useNavigate, useParams, useSearchParams } from "react-router"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { ArrowLeft, ArrowRight } from "lucide-react"
import { i18n, t } from "@/i18n"
import { Skeleton } from "@/components/ui/skeleton"
import { DocumentEditor } from "@/features/documents/document-editor"
import { emptyLine, type DocumentForm } from "@/features/documents/types"
import { api } from "@/services/api"
import { addDaysIso } from "@/lib/jalali"
import { fromQuotation, toQuotationInput, useQuotation, type Quotation } from "./api"

interface Defaults { issueDate: string; taxRate: number; defaultPaymentTerms: string | null; defaultNotes: string | null; templateKey: string; currencyCode?: string | null }

const BackIcon = i18n.dir === "rtl" ? ArrowRight : ArrowLeft

export default function QuotationEditorPage() {
  const params = useParams()
  const id = params.id ? Number(params.id) : undefined
  const [search] = useSearchParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const existing = useQuotation(id)
  const defaults = useQuery({ queryKey: ["invoice-defaults"], queryFn: () => api.get<Defaults>("/invoices/defaults"), enabled: !id, staleTime: 0 })

  const initial = useMemo<DocumentForm | null>(() => {
    if (id) return existing.data ? fromQuotation(existing.data) : null
    if (!defaults.data) return null
    return {
      customerId: Number(search.get("customerId")) || 0,
      issueDate: defaults.data.issueDate,
      endDate: addDaysIso(defaults.data.issueDate, 14),
      discountType: "None", discountValue: 0, taxRate: defaults.data.taxRate,
      notes: defaults.data.defaultNotes ?? "", paymentTerms: defaults.data.defaultPaymentTerms ?? "",
      templateKey: defaults.data.templateKey, currencyCode: defaults.data.currencyCode ?? null, items: [emptyLine()],
    }
  }, [id, existing.data, defaults.data, search])

  if (existing.data && !["Draft", "Sent"].includes(existing.data.status)) return <Navigate to={`/quotations/${id}`} replace />
  if (!initial) return <Skeleton className="h-[480px] w-full" />

  let savedId = id
  const save = async (v: DocumentForm) => {
    const body = toQuotationInput(v)
    const q = savedId ? await api.put<Quotation>(`/quotations/${savedId}`, body) : await api.post<Quotation>("/quotations", body)
    qc.setQueryData(["quotations", "one", q.id], q)
    qc.invalidateQueries({ queryKey: ["quotations"] })
    if (!savedId) {
      savedId = q.id
      navigate(`/quotations/${q.id}/edit`, { replace: true })
    }
    return q.id
  }

  return (
    <DocumentEditor
      kind="quotation"
      initial={initial}
      documentTitle={t("پیش‌فاکتور")}
      numberLabel={existing.data?.quotationNumber ?? t("پس از ذخیره")}
      endDateLabel={t("معتبر تا")}
      heading={
        <div>
          <Link to="/quotations" className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"><BackIcon className="size-3.5" /> {t("پیش‌فاکتورها")}</Link>
          <h1 className="text-xl font-bold">{id ? t("ویرایش {0}", [existing.data?.quotationNumber ?? ""]) : t("پیش‌فاکتور جدید")}</h1>
        </div>
      }
      onSave={save}
      primary={{
        label: t("ذخیره و نمایش"),
        confirmTitle: t("پیش‌فاکتور ذخیره شود؟"),
        confirmText: t("بعد از ذخیره می‌توانید PDF را چاپ یا برای مشتری بفرستید و وضعیتش را ثبت کنید."),
        run: async (qid) => navigate(`/quotations/${qid}`),
      }}
    />
  )
}
