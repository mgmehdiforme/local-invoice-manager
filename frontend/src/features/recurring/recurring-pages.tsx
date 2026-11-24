import { useMemo, useState } from "react"
import { Link, Navigate, useNavigate, useParams } from "react-router"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { ArrowLeft, ArrowRight, Play, Plus, Repeat, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Switch } from "@/components/ui/switch"
import { Skeleton } from "@/components/ui/skeleton"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { PageHeader } from "@/components/page-header"
import { DataTable, StopRowClick } from "@/components/data-table"
import { EmptyState } from "@/components/empty-state"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { FormField } from "@/components/form-field"
import { NumberInput } from "@/components/number-input"
import { Money } from "@/components/money"
import { StatusBadge } from "@/components/status-badge"
import { DocumentEditor } from "@/features/documents/document-editor"
import { emptyLine, type DocumentForm, type DiscountType, type LineInput } from "@/features/documents/types"
import { api, ApiError, errorMessage } from "@/services/api"
import { fa, formatDate } from "@/lib/format"
import { todayIso } from "@/lib/jalali"
import { useCurrencies, useSettings } from "@/hooks/use-settings"
import { i18n, t } from "@/i18n"

type Frequency = "Weekly" | "Monthly" | "Quarterly" | "Yearly"
const FREQ: Record<Frequency, string> = { Weekly: t("هفتگی"), Monthly: t("ماهانه"), Quarterly: t("سه‌ماهه"), Yearly: t("سالانه") }

interface Recurring {
  id: number; name: string; customerId: number; customerName: string; frequency: Frequency; startDate: string; endDate: string | null
  nextRunDate: string; autoIssue: boolean; isActive: boolean; dueDays: number; total: number; generatedCount: number; lastInvoiceId: number | null
  template: { discountType: DiscountType; discountValue: number; taxRate: number; notes: string | null; paymentTerms: string | null; templateKey: string | null; currencyCode?: string | null; items: LineInput[] }
}

export function RecurringListPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { byCode } = useCurrencies()
  const { data, isLoading } = useQuery({ queryKey: ["recurring"], queryFn: () => api.get<Recurring[]>("/recurring-invoices") })

  const runNow = async (r: Recurring) => {
    try {
      const res = await api.post<{ invoiceId: number }>(`/recurring-invoices/${r.id}/run-now`)
      await qc.invalidateQueries()
      toast.success(r.autoIssue ? t("فاکتور صادر شد.") : t("پیش‌نویس فاکتور ساخته شد."))
      navigate(`/invoices/${res.invoiceId}`)
    } catch (e) { toast.error(errorMessage(e)) }
  }

  return (
    <>
      <PageHeader title={t("فاکتورهای تکرارشونده")}
        description={i18n.calendar === "Jalali" ? t("برای قراردادهای ماهانه، سه‌ماهه یا سالانه؛ زمان‌بندی بر اساس تقویم شمسی است.") : t("برای قراردادهای ماهانه، سه‌ماهه یا سالانه؛ زمان‌بندی بر اساس تقویم میلادی است.")}
        actions={<Button asChild><Link to="/recurring/new"><Plus /> {t("برنامه جدید")}</Link></Button>} />
      <DataTable
        rows={data}
        loading={isLoading}
        rowKey={(r) => r.id}
        onRowClick={(r) => navigate(`/recurring/${r.id}`)}
        empty={<EmptyState icon={Repeat} title={t("برنامه تکرارشونده‌ای ندارید.")}
          description={t("مثلاً «پشتیبانی ماهانه»: هر اول ماه یک فاکتور خودکار ساخته یا صادر می‌شود.")}
          action={<Button asChild><Link to="/recurring/new"><Plus /> {t("برنامه جدید")}</Link></Button>} />}
        columns={[
          { key: "name", header: t("نام"), cell: (r) => <div><div className="font-medium">{r.name}</div><div className="text-xs text-muted-foreground">{r.customerName}</div></div> },
          { key: "freq", header: t("تکرار"), cell: (r) => FREQ[r.frequency] },
          { key: "next", header: t("نوبت بعد"), cell: (r) => r.isActive ? <span className="num">{formatDate(r.nextRunDate)}</span> : <StatusBadge tone="muted">{t("متوقف")}</StatusBadge> },
          { key: "mode", header: t("حالت"), hideOnMobile: true, cell: (r) => r.autoIssue ? t("صدور خودکار") : t("ساخت پیش‌نویس") },
          { key: "count", header: t("ساخته‌شده"), hideOnMobile: true, align: "end", cell: (r) => <span className="num">{fa(r.generatedCount)}</span> },
          { key: "total", header: t("مبلغ هر دوره"), align: "end", cell: (r) => <Money value={r.total} currency={byCode(r.template.currencyCode)} className="font-medium" /> },
          {
            key: "run", header: <span className="sr-only">{t("اجرا")}</span>, className: "w-10",
            cell: (r) => <StopRowClick><Button variant="ghost" size="icon" title={t("ساخت فاکتور همین حالا")} aria-label={t("ساخت فاکتور همین حالا")} onClick={() => runNow(r)}><Play /></Button></StopRowClick>,
          },
        ]}
      />
    </>
  )
}

export function RecurringEditorPage() {
  const params = useParams()
  const id = params.id ? Number(params.id) : undefined
  const navigate = useNavigate()
  const qc = useQueryClient()
  const existing = useQuery({ queryKey: ["recurring", id], queryFn: () => api.get<Recurring>(`/recurring-invoices/${id}`), enabled: !!id })
  const settings = useSettings().data
  const [meta, setMeta] = useState<{ name: string; frequency: Frequency; autoIssue: boolean; isActive: boolean; dueDays: number } | null>(null)
  const BackIcon = i18n.dir === "rtl" ? ArrowRight : ArrowLeft

  const loaded = existing.data
  const m = meta ?? (loaded
    ? { name: loaded.name, frequency: loaded.frequency, autoIssue: loaded.autoIssue, isActive: loaded.isActive, dueDays: loaded.dueDays }
    : { name: "", frequency: "Monthly" as Frequency, autoIssue: false, isActive: true, dueDays: 10 })
  const set = (patch: Partial<typeof m>) => setMeta({ ...m, ...patch })

  const initial = useMemo<DocumentForm | null>(() => {
    if (id && !loaded) return null
    if (loaded) {
      const tpl = loaded.template
      return { customerId: loaded.customerId, issueDate: loaded.startDate, endDate: loaded.endDate, discountType: tpl.discountType, discountValue: tpl.discountValue,
        taxRate: tpl.taxRate, notes: tpl.notes ?? "", paymentTerms: tpl.paymentTerms ?? "", templateKey: tpl.templateKey ?? "modern", currencyCode: tpl.currencyCode ?? null, items: tpl.items }
    }
    return { customerId: 0, issueDate: todayIso(), endDate: null, discountType: "None", discountValue: 0, taxRate: settings?.defaultTaxRate ?? 0, notes: "", paymentTerms: "", templateKey: settings?.defaultTemplate ?? "modern", currencyCode: null, items: [emptyLine()] }
  }, [id, loaded, settings?.defaultTaxRate, settings?.defaultTemplate])

  if (existing.isError) return <Navigate to="/recurring" replace />
  if (!initial) return <Skeleton className="h-96 w-full" />

  const save = async (v: DocumentForm) => {
    if (!m.name.trim()) throw new ApiError(400, "validation", t("یک نام برای برنامه بنویسید."))
    const body = {
      name: m.name, customerId: v.customerId, frequency: m.frequency, startDate: v.issueDate, endDate: v.endDate, autoIssue: m.autoIssue,
      isActive: m.isActive, dueDays: m.dueDays,
      template: { discountType: v.discountType, discountValue: Number(v.discountValue) || 0, taxRate: Number(v.taxRate) || 0, notes: v.notes, paymentTerms: v.paymentTerms, templateKey: v.templateKey, currencyCode: v.currencyCode || null,
        items: v.items.map((l) => ({ ...l, quantity: Number(l.quantity), unitPrice: Number(l.unitPrice), discountValue: Number(l.discountValue) || 0 })) },
    }
    const r = id ? await api.put<Recurring>(`/recurring-invoices/${id}`, body) : await api.post<Recurring>("/recurring-invoices", body)
    await qc.invalidateQueries({ queryKey: ["recurring"] })
    if (!id) navigate(`/recurring/${r.id}`, { replace: true })
    return r.id
  }

  return (
    <DocumentEditor
      kind="invoice"
      initial={initial}
      documentTitle={t("فاکتور فروش")}
      numberLabel={t("هنگام ساخت")}
      issueDateLabel={t("شروع از")}
      endDateLabel={t("پایان (اختیاری)")}
      hidePreview
      heading={
        <div>
          <Link to="/recurring" className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"><BackIcon className="size-3.5" /> {t("فاکتورهای تکرارشونده")}</Link>
          <h1 className="text-xl font-bold">{id ? m.name || t("ویرایش برنامه") : t("برنامه تکرارشونده جدید")}</h1>
        </div>
      }
      extraActions={id && (
        <ConfirmDialog trigger={<Button type="button" variant="ghost" className="text-destructive"><Trash2 /> {t("حذف")}</Button>}
          title={t("این برنامه حذف شود؟")} description={t("فاکتورهایی که قبلاً ساخته شده‌اند باقی می‌مانند.")} confirmLabel={t("حذف")} destructive
          onConfirm={async () => { try { await api.del(`/recurring-invoices/${id}`); await qc.invalidateQueries({ queryKey: ["recurring"] }); navigate("/recurring") } catch (e) { toast.error(errorMessage(e)) } }} />
      )}
      top={
        <section className="rounded-xl border bg-paper/60 p-4 md:p-5">
          <h2 className="mb-3 text-sm font-semibold">{t("زمان‌بندی")}</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label={t("نام برنامه")} required><Input value={m.name} onChange={(e) => set({ name: e.target.value })} placeholder={t("مثلاً پشتیبانی ماهانه سایت")} className="bg-paper" /></FormField>
            <FormField label={t("تکرار")}>
              <Select value={m.frequency} onValueChange={(v) => set({ frequency: v as Frequency })} dir={i18n.dir}>
                <SelectTrigger className="w-full bg-paper"><SelectValue /></SelectTrigger>
                <SelectContent>{Object.entries(FREQ).map(([k, v]) => <SelectItem key={k} value={k}>{v}</SelectItem>)}</SelectContent>
              </Select>
            </FormField>
            <FormField label={t("مهلت پرداخت هر فاکتور (روز)")}><NumberInput value={m.dueDays} onChange={(v) => set({ dueDays: v })} className="bg-paper" /></FormField>
            <div className="grid gap-3">
              <label className="flex items-center gap-2 text-sm"><Switch checked={m.autoIssue} onCheckedChange={(v) => set({ autoIssue: v })} /> {t("صدور خودکار (بدون بررسی)")}</label>
              <label className="flex items-center gap-2 text-sm"><Switch checked={m.isActive} onCheckedChange={(v) => set({ isActive: v })} /> {t("فعال")}</label>
            </div>
          </div>
          {loaded && <p className="mt-3 text-xs text-muted-foreground">{t("نوبت بعدی: {0} — تاکنون {1} فاکتور ساخته شده.", [formatDate(loaded.nextRunDate), fa(loaded.generatedCount)])}</p>}
        </section>
      }
      onSave={save}
    />
  )
}
