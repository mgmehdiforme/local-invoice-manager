import { useEffect, useMemo, useState, type ReactNode } from "react"
import { Controller, useFieldArray, useForm, useWatch, type Control, type UseFormRegister } from "react-hook-form"
import { useQuery } from "@tanstack/react-query"
import { useBlocker } from "react-router"
import { Eye, PencilLine, Plus, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Checkbox } from "@/components/ui/checkbox"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Spinner } from "@/components/ui/spinner"
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { FormField, applyServerErrors } from "@/components/form-field"
import { NumberInput } from "@/components/number-input"
import { JalaliDatePicker } from "@/components/jalali-date-picker"
import { Money } from "@/components/money"
import { useDebounced } from "@/hooks/use-debounced"
import { useCurrencies, useSettings, type CurrencyOption } from "@/hooks/use-settings"
import { i18n, t } from "@/i18n"
import { api, errorMessage } from "@/services/api"
import { fa, formatDate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useCustomer } from "@/features/customers/api"
import { UNITS, type Product } from "@/features/products/api"
import { CustomerPicker } from "./customer-picker"
import { ProductPicker } from "./product-picker"
import { DocumentPaper } from "./document-paper"
import { TEMPLATE_LABELS, emptyLine, type DocumentForm, type LineInput, type SellerSnapshot, type Totals } from "./types"

export interface EditorProps {
  kind: "invoice" | "quotation"
  heading: ReactNode
  documentTitle: string
  numberLabel: string
  endDateLabel: string
  initial: DocumentForm
  /** Saves the draft; returns the saved id. */
  onSave: (values: DocumentForm) => Promise<number>
  /** Optional main action after saving (issue invoice / send quotation). */
  primary?: { label: string; confirmTitle: string; confirmText: ReactNode; run: (id: number) => Promise<void> }
  extraActions?: ReactNode
  /** Extra panel shown first (e.g. recurring schedule). */
  top?: ReactNode
  issueDateLabel?: string
  /** Hide the live preview (e.g. recurring templates). */
  hidePreview?: boolean
}

const toPayload = (v: DocumentForm) => ({
  currencyCode: v.currencyCode || null,
  discountType: v.discountType,
  discountValue: Number(v.discountValue) || 0,
  taxRate: Number(v.taxRate) || 0,
  items: v.items.map((l) => ({ ...l, quantity: Number(l.quantity) || 0, unitPrice: Number(l.unitPrice) || 0, discountValue: Number(l.discountValue) || 0 })),
})

export function DocumentEditor({ kind, heading, documentTitle, numberLabel, endDateLabel, initial, onSave, primary, extraActions, top, issueDateLabel = t("تاریخ صدور"), hidePreview }: EditorProps) {
  const currencies = useCurrencies()
  const { data: settings } = useSettings()
  const [view, setView] = useState<"edit" | "preview">("edit")
  const [confirming, setConfirming] = useState(false)
  const [busy, setBusy] = useState<null | "save" | "primary">(null)
  const form = useForm<DocumentForm>({ defaultValues: initial })
  const { control, register, handleSubmit, setError, reset, formState: { errors, isDirty } } = form
  const lines = useFieldArray({ control, name: "items" })
  const values = useWatch({ control }) as DocumentForm
  const customer = useCustomer(values.customerId || undefined)
  const currencyCode = values.currencyCode || currencies.defaultCode
  const currency = currencies.byCode(currencyCode)

  useEffect(() => reset(initial), [initial, reset])

  // Live totals from the server (the browser never calculates money).
  const payload = useDebounced(JSON.stringify(toPayload(values)), 300)
  const calc = useQuery({
    queryKey: ["calculate", payload],
    queryFn: () => api.post<Totals>("/invoices/calculate", JSON.parse(payload)),
    placeholderData: (prev) => prev,
    retry: false,
  })
  const totals = calc.data ?? null

  // Warn before leaving with unsaved changes.
  const blocker = useBlocker(({ currentLocation, nextLocation }) => isDirty && !busy && currentLocation.pathname !== nextLocation.pathname)
  useEffect(() => {
    const h = (e: BeforeUnloadEvent) => { if (isDirty) e.preventDefault() }
    window.addEventListener("beforeunload", h)
    return () => window.removeEventListener("beforeunload", h)
  }, [isDirty])

  const save = async (v: DocumentForm): Promise<number | null> => {
    try {
      const id = await onSave(v)
      reset(v)
      return id
    } catch (err) {
      if (applyServerErrors(err, setError)) toast.error(t("بعضی از فیلدها نیاز به اصلاح دارند."))
      else toast.error(errorMessage(err))
      return null
    }
  }

  function onInvalid() {
    setConfirming(false)
    toast.error(t("بعضی از فیلدها کامل نیستند؛ موارد قرمز را بررسی کنید."))
  }

  const onSaveDraft = handleSubmit(async (v) => {
    setBusy("save")
    const id = await save(v)
    setBusy(null)
    if (id) toast.success(t("پیش‌نویس ذخیره شد."))
  }, onInvalid)

  const onPrimary = handleSubmit(async (v) => {
    if (!primary) return
    setBusy("primary")
    const id = await save(v)
    if (id) {
      try {
        await primary.run(id)
      } catch (err) {
        toast.error(errorMessage(err))
      }
    }
    setBusy(null)
    setConfirming(false)
  }, onInvalid)

  const pickProduct = (index: number, p: Product) => {
    const current = values.items[index]
    lines.update(index, {
      ...current,
      productId: p.id,
      description: p.name + (p.description ? `\n${p.description}` : ""),
      sku: p.sku ?? null,
      unit: p.unit,
      unitPrice: p.customerPrice ?? p.defaultPrice,
      isTaxable: p.isTaxable,
    })
  }

  const seller: SellerSnapshot = useMemo(() => ({
    companyName: settings?.companyName ?? "", legalName: settings?.legalName, economicCode: settings?.economicCode, nationalId: settings?.nationalId,
    registrationNumber: settings?.registrationNumber, phone: settings?.phone, email: settings?.email, website: settings?.website,
    address: settings?.address, postalCode: settings?.postalCode, bankName: settings?.bankName, bankAccountNumber: settings?.bankAccountNumber,
    bankCardNumber: settings?.bankCardNumber, bankIban: settings?.bankIban, bankAccountHolder: settings?.bankAccountHolder,
    taxLabel: settings?.taxLabel ?? t("مالیات"), accentColor: settings?.accentColor ?? "#0e6b5c",
  }), [settings])

  const paper = (
    <DocumentPaper
      title={documentTitle}
      number={numberLabel}
      meta={[{ label: t("تاریخ"), value: formatDate(values.issueDate) }, ...(values.endDate ? [{ label: endDateLabel, value: formatDate(values.endDate) }] : [])]}
      seller={seller}
      customer={customer.data ?? null}
      items={values.items ?? []}
      totals={totals}
      currency={currency}
      taxRate={Number(values.taxRate) || 0}
      discountType={values.discountType}
      discountValue={Number(values.discountValue) || 0}
      notes={values.notes}
      paymentTerms={values.paymentTerms}
      template={values.templateKey}
      logoUrl={settings?.hasLogo ? "/api/v1/settings/logo" : null}
      watermark={t("پیش‌نویس")}
    />
  )

  return (
    <form onSubmit={onSaveDraft} noValidate>
      {/* Sticky action bar */}
      <div className="sticky top-14 z-[5] -mx-4 mb-5 flex flex-wrap items-center justify-between gap-3 border-b bg-background/95 px-4 py-3 backdrop-blur md:-mx-8 md:px-8">
        <div className="min-w-0">{heading}</div>
        <div className="flex flex-wrap items-center gap-2">
          <Tabs value={view} onValueChange={(v) => setView(v as "edit" | "preview")} className={cn("xl:hidden", hidePreview && "hidden")}>
            <TabsList>
              <TabsTrigger value="edit"><PencilLine /> {t("ویرایش")}</TabsTrigger>
              <TabsTrigger value="preview"><Eye /> {t("پیش‌نمایش")}</TabsTrigger>
            </TabsList>
          </Tabs>
          {extraActions}
          <Button type="submit" variant="outline" disabled={!!busy}>{busy === "save" && <Spinner />} {t("ذخیره پیش‌نویس")}</Button>
          {primary && <Button type="button" disabled={!!busy} onClick={() => setConfirming(true)}>{primary.label}</Button>}
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,0.85fr)]">
        <div className={cn("grid content-start gap-5", view === "preview" && "hidden xl:grid", hidePreview && "xl:col-span-2 max-w-3xl")}>
          {top}
          <Panel title={t("مشتری و تاریخ")}>
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField label={t("مشتری")} error={errors.customerId?.message} required className="sm:col-span-2">
                <Controller control={control} name="customerId" rules={{ validate: (v) => v > 0 || t("مشتری را انتخاب کنید.") }}
                  render={({ field }) => <CustomerPicker value={field.value || null} onChange={(c) => field.onChange(c.id)} invalid={!!errors.customerId} />} />
              </FormField>
              <FormField label={issueDateLabel} error={errors.issueDate?.message} required>
                <Controller control={control} name="issueDate" render={({ field }) => <JalaliDatePicker value={field.value} onChange={(v) => v && field.onChange(v)} invalid={!!errors.issueDate} />} />
              </FormField>
              <FormField label={endDateLabel} error={(errors.endDate?.message) ?? (kind === "invoice" ? (errors as Record<string, { message?: string }>).dueDate?.message : (errors as Record<string, { message?: string }>).validUntil?.message)}>
                <Controller control={control} name="endDate" render={({ field }) => <JalaliDatePicker value={field.value} onChange={field.onChange} clearable />} />
              </FormField>
              {currencies.list.length > 1 && (
                <FormField label={t("ارز")} error={(errors as Record<string, { message?: string }>).currencyCode?.message}>
                  <Controller control={control} name="currencyCode" render={({ field }) => (
                    <Select value={field.value || currencies.defaultCode} onValueChange={field.onChange} dir={i18n.dir}>
                      <SelectTrigger className="w-full bg-paper"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {currencies.list.map((c) => (
                          <SelectItem key={c.currencyCode} value={c.currencyCode}>
                            <span dir="ltr">{c.currencyCode}</span> <span className="text-muted-foreground">{c.currencySymbol}</span>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )} />
                </FormField>
              )}
            </div>
          </Panel>

          <Panel title={t("ردیف‌ها")} note={t("از فهرست کالاها انتخاب کنید یا شرح دلخواه بنویسید.")}>
            <div className="grid gap-3">
              {lines.fields.map((f, i) => (
                <LineRow key={f.id} index={i} control={control} register={register} errors={errors.items?.[i] as LineErrors | undefined}
                  total={totals?.lineTotals[i]} customerId={values.customerId} onPick={(p) => pickProduct(i, p)}
                  onRemove={() => lines.remove(i)} canRemove={lines.fields.length > 1} currency={currency} />
              ))}
            </div>
            <datalist id="units">{UNITS.map((u) => <option key={u} value={u} />)}</datalist>
            <Button type="button" variant="outline" className="mt-3 w-full border-dashed" onClick={() => lines.append(emptyLine())}>
              <Plus /> {t("افزودن ردیف")}
            </Button>
          </Panel>

          <Panel title={t("تخفیف و مالیات")}>
            <div className="grid gap-4 sm:grid-cols-3">
              <FormField label={t("نوع تخفیف")}>
                <Controller control={control} name="discountType" render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange} dir={i18n.dir}>
                    <SelectTrigger className="w-full bg-paper"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="None">{t("بدون تخفیف")}</SelectItem>
                      <SelectItem value="Percent">{t("درصدی")}</SelectItem>
                      <SelectItem value="Fixed">{t("مبلغ ثابت")}</SelectItem>
                    </SelectContent>
                  </Select>
                )} />
              </FormField>
              <FormField label={values.discountType === "Percent" ? t("درصد تخفیف") : t("مبلغ تخفیف ({0})", [currency.currencySymbol])} error={errors.discountValue?.message}>
                <Controller control={control} name="discountValue" render={({ field }) => (
                  <NumberInput value={field.value} onChange={field.onChange} decimals={values.discountType === "Percent" ? 2 : currency.currencyDecimalPlaces}
                    className={cn("bg-paper", values.discountType === "None" && "opacity-50")} invalid={!!errors.discountValue} />
                )} />
              </FormField>
              <FormField label={t("نرخ مالیات (٪)")} error={errors.taxRate?.message}>
                <Controller control={control} name="taxRate" render={({ field }) => <NumberInput value={field.value} onChange={field.onChange} decimals={2} className="bg-paper" invalid={!!errors.taxRate} />} />
              </FormField>
            </div>
            <TotalsSummary totals={totals} loading={calc.isFetching} error={calc.isError ? errorMessage(calc.error) : null} taxRate={Number(values.taxRate) || 0} currency={currency} />
          </Panel>

          <Panel title={t("توضیحات و چاپ")}>
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField label={t("شرایط پرداخت")} error={errors.paymentTerms?.message}><Textarea rows={3} className="bg-paper" {...register("paymentTerms")} /></FormField>
              <FormField label={t("توضیحات")} error={errors.notes?.message}><Textarea rows={3} className="bg-paper" {...register("notes")} /></FormField>
              <FormField label={t("قالب چاپ")}>
                <Controller control={control} name="templateKey" render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange} dir={i18n.dir}>
                    <SelectTrigger className="w-full bg-paper"><SelectValue /></SelectTrigger>
                    <SelectContent>{Object.entries(TEMPLATE_LABELS).map(([k, v]) => <SelectItem key={k} value={k}>{v}</SelectItem>)}</SelectContent>
                  </Select>
                )} />
              </FormField>
            </div>
          </Panel>
        </div>

        <div className={cn("xl:block", view === "edit" && "hidden", hidePreview && "xl:hidden")}>
          <div className="sticky top-32">
            <div className="mb-2 text-xs text-muted-foreground">{t("پیش‌نمایش زنده")}</div>
            {paper}
          </div>
        </div>
      </div>

      <AlertDialog open={confirming} onOpenChange={setConfirming}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{primary?.confirmTitle}</AlertDialogTitle>
            <AlertDialogDescription asChild><div>{primary?.confirmText}</div></AlertDialogDescription>
          </AlertDialogHeader>
          {totals && <div className="rounded-lg bg-muted p-3 text-center text-lg font-bold"><Money value={totals.total} currency={currency} /></div>}
          <AlertDialogFooter>
            <AlertDialogCancel>{t("انصراف")}</AlertDialogCancel>
            <AlertDialogAction disabled={!!busy} onClick={(e) => { e.preventDefault(); onPrimary() }}>
              {busy === "primary" && <Spinner />} {primary?.label}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={blocker.state === "blocked"}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("تغییرات ذخیره نشده‌اند")}</AlertDialogTitle>
            <AlertDialogDescription>{t("اگر خارج شوید، تغییرات این صفحه از بین می‌رود.")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => blocker.reset?.()}>{t("ماندن در صفحه")}</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-white hover:bg-destructive/90" onClick={() => blocker.proceed?.()}>{t("خروج بدون ذخیره")}</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </form>
  )
}

function Panel({ title, note, children }: { title: string; note?: string; children: ReactNode }) {
  return (
    <section className="rounded-xl border bg-paper/60 p-4 md:p-5">
      <div className="mb-3">
        <h2 className="text-sm font-semibold">{title}</h2>
        {note && <p className="text-xs text-muted-foreground">{note}</p>}
      </div>
      {children}
    </section>
  )
}

type LineErrors = Partial<Record<keyof LineInput, { message?: string }>>

function LineRow({ index, control, register, errors, total, customerId, onPick, onRemove, canRemove, currency }: {
  index: number; control: Control<DocumentForm>; register: UseFormRegister<DocumentForm>; errors?: LineErrors; total?: number
  customerId: number; onPick: (p: Product) => void; onRemove: () => void; canRemove: boolean; currency: CurrencyOption
}) {
  const decimals = currency.currencyDecimalPlaces
  const discountType = useWatch({ control, name: `items.${index}.discountType` })
  return (
    <div className="rounded-lg border bg-paper p-3">
      <div className="flex items-start gap-2">
        <span className="num mt-2 w-5 shrink-0 text-center text-xs text-muted-foreground">{fa(index + 1)}</span>
        <div className="grid min-w-0 flex-1 gap-2">
          <div className="flex gap-1">
            <Textarea rows={1} placeholder={t("شرح کالا یا خدمت")} className="min-h-9 resize-y py-1.5" aria-invalid={!!errors?.description}
              {...register(`items.${index}.description`, { required: t("شرح ردیف را وارد کنید.") })} />
            <ProductPicker customerId={customerId} onPick={onPick} />
          </div>
          {errors?.description && <p className="text-xs text-destructive">{errors.description.message}</p>}
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-[1fr_1fr_1.6fr_1.6fr_auto]">
            <Mini label={t("تعداد")} error={errors?.quantity?.message}>
              <Controller control={control} name={`items.${index}.quantity`} render={({ field }) => <NumberInput value={field.value} onChange={field.onChange} decimals={3} invalid={!!errors?.quantity} aria-label={t("تعداد")} />} />
            </Mini>
            <Mini label={t("واحد")} error={errors?.unit?.message}>
              <Input list="units" aria-label={t("واحد")} {...register(`items.${index}.unit`, { required: t("واحد را وارد کنید.") })} />
            </Mini>
            <Mini label={t("قیمت واحد")} error={errors?.unitPrice?.message}>
              <Controller control={control} name={`items.${index}.unitPrice`} render={({ field }) => <NumberInput value={field.value} onChange={field.onChange} decimals={decimals} invalid={!!errors?.unitPrice} aria-label={t("قیمت واحد")} />} />
            </Mini>
            <Mini label={t("تخفیف ردیف")} error={errors?.discountValue?.message}>
              <div className="flex gap-1">
                <Controller control={control} name={`items.${index}.discountType`} render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange} dir={i18n.dir}>
                    <SelectTrigger className="w-16 shrink-0 px-2" aria-label={t("نوع تخفیف ردیف")}><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="None">—</SelectItem>
                      <SelectItem value="Percent">%</SelectItem>
                      <SelectItem value="Fixed">{t("مبلغ")}</SelectItem>
                    </SelectContent>
                  </Select>
                )} />
                <Controller control={control} name={`items.${index}.discountValue`} render={({ field }) => (
                  <NumberInput value={field.value} onChange={field.onChange} decimals={discountType === "Percent" ? 2 : decimals} className={discountType === "None" ? "opacity-40" : undefined} aria-label={t("مقدار تخفیف ردیف")} />
                )} />
              </div>
            </Mini>
            <Mini label={t("مالیات")}>
              <Controller control={control} name={`items.${index}.isTaxable`} render={({ field }) => (
                <label className="flex h-9 items-center gap-1.5 text-xs text-muted-foreground">
                  <Checkbox checked={field.value} onCheckedChange={(v) => field.onChange(v === true)} /> {t("مشمول")}
                </label>
              )} />
            </Mini>
          </div>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-1">
          <Button type="button" variant="ghost" size="icon" onClick={onRemove} disabled={!canRemove} aria-label={t("حذف ردیف")}><Trash2 /></Button>
        </div>
      </div>
      <div className="mt-2 flex justify-end border-t pt-2 text-sm">
        <span className="text-muted-foreground">{t("جمع ردیف:")}&nbsp;</span>
        {total === undefined ? <span className="text-muted-foreground">…</span> : <Money value={total} currency={currency} className="font-semibold" />}
      </div>
    </div>
  )
}

function Mini({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <div className="grid content-start gap-1">
      <span className="text-[11px] text-muted-foreground">{label}</span>
      {children}
      {error && <span className="text-[11px] text-destructive">{error}</span>}
    </div>
  )
}

function TotalsSummary({ totals, loading, error, taxRate, currency }: { totals: Totals | null; loading: boolean; error: string | null; taxRate: number; currency: CurrencyOption }) {
  return (
    <div className="mt-4 rounded-lg border bg-paper p-3 text-sm">
      {error ? <p className="text-destructive">{error}</p> : (
        <dl className="grid gap-1.5">
          <SumRow label={t("جمع ردیف‌ها")} value={totals?.subtotal} currency={currency} />
          {(totals?.discountAmount ?? 0) > 0 && <SumRow label={t("تخفیف")} value={totals?.discountAmount} currency={currency} negative />}
          {taxRate > 0 && <SumRow label={t("مالیات ({0}٪)", [fa(taxRate)])} value={totals?.taxAmount} currency={currency} />}
          <div className="mt-1 flex items-center justify-between border-t pt-2">
            <dt className="font-semibold">{t("مبلغ قابل پرداخت")} {loading && <Spinner className="inline size-3" />}</dt>
            <dd className="text-lg font-bold"><Money value={totals?.total} currency={currency} /></dd>
          </div>
        </dl>
      )}
    </div>
  )
}

function SumRow({ label, value, negative, currency }: { label: string; value?: number; negative?: boolean; currency: CurrencyOption }) {
  return (
    <div className="flex items-center justify-between">
      <dt className="text-muted-foreground">{label}</dt>
      <dd>{negative && "− "}<Money value={value} currency={currency} /></dd>
    </div>
  )
}
