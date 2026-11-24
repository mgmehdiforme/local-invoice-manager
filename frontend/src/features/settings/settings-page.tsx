import { useEffect, useRef, useState } from "react"
import { Controller, useFieldArray, useForm, type Control, type FieldErrors, type UseFormRegister } from "react-hook-form"
import { useQueryClient } from "@tanstack/react-query"
import { ImageUp, Plus, Save, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Switch } from "@/components/ui/switch"
import { Spinner } from "@/components/ui/spinner"
import { Skeleton } from "@/components/ui/skeleton"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { PageHeader } from "@/components/page-header"
import { FormField, applyServerErrors } from "@/components/form-field"
import { NumberInput } from "@/components/number-input"
import { api, errorMessage } from "@/services/api"
import type { CurrencyDef, Settings } from "@/services/types"
import { settingsKey, useSettings } from "@/hooks/use-settings"
import { fa, formatMoney } from "@/lib/format"
import { CURRENCY_PRESETS } from "@/lib/currencies"
import { applyPrefsAndReload, i18n, t } from "@/i18n"
import { ChangePasswordCard } from "./change-password-card"
import { BackupPanel } from "./backup-panel"

type F = { register: UseFormRegister<Settings>; control: Control<Settings>; errors: FieldErrors<Settings> }

const TEMPLATES = [
  { value: "modern", label: t("مدرن — نوار رنگی و جدول روشن") },
  { value: "classic", label: t("کلاسیک — کادر و خطوط جدول") },
  { value: "minimal", label: t("ساده — بدون رنگ زمینه") },
]

/** Language names are shown in their own language. */
const FARSI = "فارسی" // i18n-ignore

const ACCENTS = ["#0e6b5c", "#1d4ed8", "#7c3aed", "#b45309", "#be123c", "#334155"]

export default function SettingsPage() {
  const qc = useQueryClient()
  const { data, isLoading } = useSettings()
  const { register, control, handleSubmit, reset, setError, watch, formState: { errors, isDirty, isSubmitting } } = useForm<Settings>()

  useEffect(() => {
    if (data) reset({ ...data, smtpPassword: null })
  }, [data, reset])

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = await api.put<Settings>("/settings", values)
      qc.setQueryData(settingsKey, saved)
      reset({ ...saved, smtpPassword: null })
      toast.success(t("تنظیمات ذخیره شد."))
      // A new language or calendar is applied by reloading the page.
      applyPrefsAndReload({ language: saved.language, calendar: saved.calendar })
    } catch (err) {
      if (applyServerErrors(err, setError)) toast.error(t("بعضی از فیلدها نیاز به اصلاح دارند."))
      else toast.error(errorMessage(err))
    }
  })

  if (isLoading || !data) return <Skeleton className="h-96 w-full" />
  const f: F = { register, control, errors }
  const preview = watch()

  return (
    <form onSubmit={onSubmit}>
      <PageHeader
        title={t("تنظیمات")}
        description={t("اطلاعات کسب‌وکار، ارز، مالیات و شماره‌گذاری فاکتورها")}
        actions={
          <Button type="submit" disabled={!isDirty || isSubmitting}>
            {isSubmitting ? <Spinner /> : <Save />} {t("ذخیره تنظیمات")}
          </Button>
        }
      />
      <Tabs defaultValue="company" dir={i18n.dir}>
        <TabsList className="mb-4 h-auto flex-wrap justify-start">
          <TabsTrigger value="company">{t("کسب‌وکار")}</TabsTrigger>
          <TabsTrigger value="general">{t("زبان و تقویم")}</TabsTrigger>
          <TabsTrigger value="bank">{t("اطلاعات بانکی")}</TabsTrigger>
          <TabsTrigger value="money">{t("ارز و مالیات")}</TabsTrigger>
          <TabsTrigger value="invoice">{t("فاکتور")}</TabsTrigger>
          <TabsTrigger value="notify">{t("یادآوری و ایمیل")}</TabsTrigger>
          <TabsTrigger value="backup">{t("پشتیبان‌گیری")}</TabsTrigger>
          <TabsTrigger value="security">{t("امنیت")}</TabsTrigger>
        </TabsList>

        <TabsContent value="company"><Section title={t("مشخصات فروشنده")} note={t("این اطلاعات بالای هر فاکتور چاپ می‌شود.")}><CompanyFields {...f} /><LogoField hasLogo={data.hasLogo} /></Section></TabsContent>
        <TabsContent value="general"><Section title={t("زبان و تقویم")} note={t("زبان برنامه و فاکتورها، و تقویمی که تاریخ‌ها با آن نمایش داده می‌شوند. پس از ذخیره، صفحه دوباره بارگذاری می‌شود.")}><LanguageFields {...f} /></Section></TabsContent>
        <TabsContent value="bank"><Section title={t("اطلاعات حساب")} note={t("برای پرداخت مشتری، پایین فاکتور نمایش داده می‌شود.")}><BankFields {...f} /></Section></TabsContent>
        <TabsContent value="money">
          <Section title={t("ارزها")} note={t("نمونه: {0}", [samplePreview(preview)])}><CurrencyFields {...f} /></Section>
          <Section title={t("مالیات")}><TaxFields {...f} /></Section>
        </TabsContent>
        <TabsContent value="invoice">
          <Section title={t("شماره‌گذاری")} note={data.nextInvoiceNumberPreview ? t("شماره فاکتور بعدی: {0}", [data.nextInvoiceNumberPreview]) : undefined}><NumberingFields {...f} /></Section>
          <Section title={t("پیش‌فرض‌های فاکتور")}><InvoiceDefaults {...f} /></Section>
        </TabsContent>
        <TabsContent value="notify">
          <Section title={t("یادآوری سررسید")} note={t("یادآوری‌ها هر روز صبح در بخش اعلان‌ها (زنگوله بالای صفحه) نمایش داده می‌شوند.")}><ReminderFields {...f} /></Section>
          <Section title={t("ایمیل (اختیاری)")} note={t("برای ارسال فاکتور با ایمیل. بدون این بخش هم برنامه کامل کار می‌کند.")}><SmtpFields {...f} passwordSet={data.smtpPasswordSet} /></Section>
        </TabsContent>
        <TabsContent value="backup">
          <Section title={t("پشتیبان‌گیری خودکار")}><BackupFields {...f} /></Section>
          <BackupPanel />
        </TabsContent>
        <TabsContent value="security">
          <Section title={t("نشست")}><SecurityFields {...f} /></Section>
          <ChangePasswordCard />
        </TabsContent>
      </Tabs>
    </form>
  )
}

function Section({ title, note, children }: { title: string; note?: string; children: React.ReactNode }) {
  return (
    <section className="mb-5 rounded-xl border bg-paper p-5">
      <div className="mb-4">
        <h2 className="font-semibold">{title}</h2>
        {note && <p className="mt-0.5 text-sm text-muted-foreground">{note}</p>}
      </div>
      {children}
    </section>
  )
}

const grid = "grid gap-4 sm:grid-cols-2"
const ltr = { dir: "ltr" as const, className: "text-end" }

function CompanyFields({ register, errors }: F) {
  return (
    <div className={grid}>
      <FormField label={t("نام کسب‌وکار")} htmlFor="s-company" error={errors.companyName?.message}><Input id="s-company" {...register("companyName")} /></FormField>
      <FormField label={t("نام حقوقی")} htmlFor="s-legal" error={errors.legalName?.message}><Input id="s-legal" {...register("legalName")} /></FormField>
      <FormField label={t("کد اقتصادی")} htmlFor="s-eco" error={errors.economicCode?.message}><Input id="s-eco" {...ltr} {...register("economicCode")} /></FormField>
      <FormField label={t("شناسه ملی / کد ملی")} htmlFor="s-nid" error={errors.nationalId?.message}><Input id="s-nid" {...ltr} {...register("nationalId")} /></FormField>
      <FormField label={t("شماره ثبت")} htmlFor="s-reg" error={errors.registrationNumber?.message}><Input id="s-reg" {...ltr} {...register("registrationNumber")} /></FormField>
      <FormField label={t("تلفن")} htmlFor="s-phone" error={errors.phone?.message}><Input id="s-phone" {...ltr} {...register("phone")} /></FormField>
      <FormField label={t("ایمیل")} htmlFor="s-email" error={errors.email?.message}><Input id="s-email" {...ltr} {...register("email")} /></FormField>
      <FormField label={t("وب‌سایت")} htmlFor="s-web" error={errors.website?.message}><Input id="s-web" {...ltr} {...register("website")} /></FormField>
      <FormField label={t("کد پستی")} htmlFor="s-postal" error={errors.postalCode?.message}><Input id="s-postal" {...ltr} {...register("postalCode")} /></FormField>
      <FormField label={t("نشانی")} htmlFor="s-address" error={errors.address?.message} className="sm:col-span-2"><Textarea id="s-address" rows={2} {...register("address")} /></FormField>
    </div>
  )
}

function LogoField({ hasLogo }: { hasLogo: boolean }) {
  const qc = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [version, setVersion] = useState(0)

  const upload = async (file: File) => {
    setBusy(true)
    try {
      const form = new FormData()
      form.append("file", file)
      await api.upload("/settings/logo", form)
      await qc.invalidateQueries({ queryKey: settingsKey })
      setVersion((v) => v + 1)
      toast.success(t("لوگو ذخیره شد."))
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setBusy(false)
      if (input.current) input.current.value = ""
    }
  }

  const remove = async () => {
    try {
      await api.del("/settings/logo")
      await qc.invalidateQueries({ queryKey: settingsKey })
      toast.success(t("لوگو حذف شد."))
    } catch (e) {
      toast.error(errorMessage(e))
    }
  }

  return (
    <div className="mt-5 flex items-center gap-4 border-t pt-5">
      <div className="grid size-20 place-items-center overflow-hidden rounded-lg border bg-white">
        {hasLogo ? <img src={`/api/v1/settings/logo?v=${version}`} alt={t("لوگو")} className="size-full object-contain" /> : <span className="text-xs text-muted-foreground">{t("بدون لوگو")}</span>}
      </div>
      <div className="grid gap-2">
        <div className="text-sm font-medium">{t("لوگو")}</div>
        <p className="text-xs text-muted-foreground">{t("PNG، JPEG یا WebP، حداکثر ۱ مگابایت")}</p>
        <div className="flex gap-2">
          <input ref={input} type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={(e) => e.target.files?.[0] && upload(e.target.files[0])} />
          <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => input.current?.click()}>
            {busy ? <Spinner /> : <ImageUp />} {hasLogo ? t("تغییر لوگو") : t("انتخاب لوگو")}
          </Button>
          {hasLogo && <Button type="button" variant="ghost" size="sm" onClick={remove}><Trash2 /> {t("حذف")}</Button>}
        </div>
      </div>
    </div>
  )
}

function BankFields({ register, errors }: F) {
  return (
    <div className={grid}>
      <FormField label={t("نام بانک")} htmlFor="b-name" error={errors.bankName?.message}><Input id="b-name" {...register("bankName")} /></FormField>
      <FormField label={t("صاحب حساب")} htmlFor="b-holder" error={errors.bankAccountHolder?.message}><Input id="b-holder" {...register("bankAccountHolder")} /></FormField>
      <FormField label={t("شماره حساب")} htmlFor="b-acc" error={errors.bankAccountNumber?.message}><Input id="b-acc" {...ltr} {...register("bankAccountNumber")} /></FormField>
      <FormField label={t("شماره کارت")} htmlFor="b-card" error={errors.bankCardNumber?.message} hint={t("۱۶ رقم")}><Input id="b-card" {...ltr} {...register("bankCardNumber")} /></FormField>
      <FormField label={t("شماره شبا")} htmlFor="b-iban" error={errors.bankIban?.message} hint={t("۲۴ رقم، با یا بدون IR")} className="sm:col-span-2"><Input id="b-iban" {...ltr} {...register("bankIban")} /></FormField>
    </div>
  )
}

/** Sample amount in the default currency with the current separators. */
function samplePreview(v: Settings): string {
  const def = v.currencies?.find((c) => c.code === v.currencyCode) ?? v.currencies?.[0]
  return formatMoney(1234567.891, {
    currencySymbol: def?.symbol ?? v.currencySymbol,
    currencyDecimalPlaces: Number(def?.decimalPlaces ?? v.currencyDecimalPlaces) || 0,
    currencySymbolPosition: def?.symbolPosition ?? v.currencySymbolPosition,
    thousandsSeparator: v.thousandsSeparator,
    decimalSeparator: v.decimalSeparator,
  })
}

function LanguageFields({ control, errors }: F) {
  return (
    <div className={grid}>
      <FormField label={t("زبان")} error={errors.language?.message} hint={t("زبان صفحه‌ها، فاکتورها، PDF و پیام‌ها.")}>
        <Controller control={control} name="language" render={({ field }) => (
          <Select value={field.value} onValueChange={field.onChange} dir={i18n.dir}>
            <SelectTrigger className="w-full"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="en">English</SelectItem>
              <SelectItem value="fa">{FARSI}</SelectItem>
            </SelectContent>
          </Select>
        )} />
      </FormField>
      <FormField label={t("تقویم")} error={errors.calendar?.message} hint={t("تاریخ‌ها همیشه به میلادی ذخیره می‌شوند؛ این گزینه فقط نمایش، شماره‌گذاری سالانه و زمان‌بندی را تعیین می‌کند.")}>
        <Controller control={control} name="calendar" render={({ field }) => (
          <Select value={field.value} onValueChange={field.onChange} dir={i18n.dir}>
            <SelectTrigger className="w-full"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="Gregorian">{t("میلادی")}</SelectItem>
              <SelectItem value="Jalali">{t("شمسی (جلالی)")}</SelectItem>
            </SelectContent>
          </Select>
        )} />
      </FormField>
    </div>
  )
}

function CurrencyFields({ register, control, errors }: F) {
  const { fields, append, remove } = useFieldArray({ control, name: "currencies" })
  const [preset, setPreset] = useState("")
  return (
    <Controller control={control} name="currencyCode" render={({ field: def }) => {
      const codes = fields.map((c) => c.code)
      const available = CURRENCY_PRESETS.filter((p) => !codes.includes(p.code))
      const add = (c: CurrencyDef) => {
        append(c)
        if (!def.value || !codes.includes(def.value)) def.onChange(c.code)
      }
      return (
        <div className="grid gap-4">
          <div className="overflow-x-auto rounded-lg border">
            <table className="w-full text-sm">
              <thead className="bg-muted/40 text-xs text-muted-foreground">
                <tr>
                  <th className="px-2 py-2 text-start font-normal">{t("کد ارز")}</th>
                  <th className="px-2 py-2 text-start font-normal">{t("نماد")}</th>
                  <th className="px-2 py-2 text-start font-normal">{t("تعداد رقم اعشار")}</th>
                  <th className="px-2 py-2 text-start font-normal">{t("جای نماد")}</th>
                  <th className="px-2 py-2 text-start font-normal">{t("پیش‌فرض")}</th>
                  <th className="w-10" />
                </tr>
              </thead>
              <tbody>
                {fields.map((c, i) => (
                  <tr key={c.id} className="border-t">
                    <td className="px-2 py-1.5"><Input dir="ltr" className="w-24 uppercase" maxLength={10} aria-label={t("کد ارز")}
                      {...register(`currencies.${i}.code` as const, { required: true, setValueAs: (v: string) => v.trim().toUpperCase() })} /></td>
                    <td className="px-2 py-1.5"><Input className="w-24" maxLength={10} aria-label={t("نماد")} {...register(`currencies.${i}.symbol` as const, { required: true })} /></td>
                    <td className="px-2 py-1.5">
                      <Controller control={control} name={`currencies.${i}.decimalPlaces` as const} render={({ field }) => (
                        <Select value={String(field.value)} onValueChange={(v) => field.onChange(Number(v))} dir={i18n.dir}>
                          <SelectTrigger className="w-20" aria-label={t("تعداد رقم اعشار")}><SelectValue /></SelectTrigger>
                          <SelectContent>{[0, 1, 2, 3, 4].map((n) => <SelectItem key={n} value={String(n)}>{fa(n)}</SelectItem>)}</SelectContent>
                        </Select>
                      )} />
                    </td>
                    <td className="px-2 py-1.5">
                      <Controller control={control} name={`currencies.${i}.symbolPosition` as const} render={({ field }) => (
                        <Select value={field.value} onValueChange={field.onChange} dir={i18n.dir}>
                          <SelectTrigger className="w-32" aria-label={t("جای نماد")}><SelectValue /></SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Before">{t("قبل از مبلغ")}</SelectItem>
                            <SelectItem value="After">{t("بعد از مبلغ")}</SelectItem>
                          </SelectContent>
                        </Select>
                      )} />
                    </td>
                    <td className="px-2 py-1.5">
                      <input type="radio" name="default-currency" className="size-4 accent-[var(--brand)]" aria-label={t("ارز پیش‌فرض")}
                        checked={def.value === c.code} onChange={() => def.onChange(c.code)} />
                    </td>
                    <td className="px-1 py-1.5">
                      <Button type="button" variant="ghost" size="icon" aria-label={t("حذف")} disabled={fields.length <= 1 || def.value === c.code}
                        title={def.value === c.code ? t("ارز پیش‌فرض را نمی‌توان حذف کرد.") : undefined}
                        onClick={() => remove(i)}><Trash2 /></Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {errors.currencyCode?.message && <p className="text-sm text-destructive">{errors.currencyCode.message}</p>}
          {errors.currencies?.message && <p className="text-sm text-destructive">{errors.currencies.message}</p>}
          <div className="flex flex-wrap items-center gap-2">
            <Select value={preset} onValueChange={setPreset} dir={i18n.dir}>
              <SelectTrigger className="w-56"><SelectValue placeholder={t("افزودن ارز رایج…")} /></SelectTrigger>
              <SelectContent>{available.map((p) => <SelectItem key={p.code} value={p.code}>{p.code} — {p.symbol}</SelectItem>)}</SelectContent>
            </Select>
            <Button type="button" variant="outline" disabled={!preset} onClick={() => {
              const p = CURRENCY_PRESETS.find((x) => x.code === preset)
              if (p) add({ ...p })
              setPreset("")
            }}><Plus /> {t("افزودن")}</Button>
            <Button type="button" variant="ghost" onClick={() => add({ code: "", symbol: "", decimalPlaces: 2, symbolPosition: "Before" })}>
              <Plus /> {t("ارز دیگر")}
            </Button>
          </div>
          <p className="text-xs text-muted-foreground">{t("هر فاکتور با یکی از این ارزها صادر می‌شود. مبالغ ارزهای مختلف هیچ‌وقت با هم جمع نمی‌شوند.")}</p>
          <div className={grid}>
            <FormField label={t("جداکننده هزارگان")} htmlFor="m-th" error={errors.thousandsSeparator?.message}><Input id="m-th" maxLength={1} {...ltr} {...register("thousandsSeparator")} /></FormField>
            <FormField label={t("جداکننده اعشار")} htmlFor="m-dec" error={errors.decimalSeparator?.message}><Input id="m-dec" maxLength={1} {...ltr} {...register("decimalSeparator")} /></FormField>
          </div>
        </div>
      )
    }} />
  )
}

function TaxFields({ register, control, errors }: F) {
  return (
    <div className={grid}>
      <FormField label={t("نرخ پیش‌فرض مالیات (٪)")} htmlFor="t-rate" error={errors.defaultTaxRate?.message} hint={t("برای فاکتورهای جدید؛ در هر فاکتور قابل تغییر است.")}>
        <Controller control={control} name="defaultTaxRate" render={({ field }) => <NumberInput id="t-rate" value={field.value} onChange={field.onChange} decimals={2} />} />
      </FormField>
      <FormField label={t("عنوان مالیات روی فاکتور")} htmlFor="t-label" error={errors.taxLabel?.message}><Input id="t-label" {...register("taxLabel")} /></FormField>
    </div>
  )
}

function NumberingFields({ register, control, errors }: F) {
  return (
    <div className="grid gap-4 sm:grid-cols-3">
      <FormField label={t("پیشوند فاکتور")} htmlFor="n-inv" error={errors.invoicePrefix?.message}><Input id="n-inv" {...ltr} {...register("invoicePrefix")} /></FormField>
      <FormField label={t("پیشوند پیش‌فاکتور")} htmlFor="n-quo" error={errors.quotationPrefix?.message}><Input id="n-quo" {...ltr} {...register("quotationPrefix")} /></FormField>
      <FormField label={t("تعداد رقم شماره")} htmlFor="n-pad" error={errors.numberPadding?.message}>
        <Controller control={control} name="numberPadding" render={({ field }) => <NumberInput id="n-pad" value={field.value} onChange={field.onChange} />} />
      </FormField>
      <FormField label={t("شروع دوباره در هر سال")} className="sm:col-span-3" hint={i18n.calendar === "Jalali" ? t("اگر روشن باشد، سال شمسی در شماره می‌آید و شماره‌ها از اول فروردین از ۱ شروع می‌شوند (INV-1405-0001).") : t("اگر روشن باشد، سال میلادی در شماره می‌آید و شماره‌ها از اول ژانویه از ۱ شروع می‌شوند (INV-2026-0001).")}>
        <Controller control={control} name="resetNumberingYearly" render={({ field }) => <Switch checked={field.value} onCheckedChange={field.onChange} aria-label={t("شروع دوباره در هر سال")} />} />
      </FormField>
    </div>
  )
}

function InvoiceDefaults({ register, control, errors }: F) {
  return (
    <div className={grid}>
      <FormField label={t("مهلت پرداخت پیش‌فرض (روز)")} htmlFor="d-due" error={errors.defaultDueDays?.message}>
        <Controller control={control} name="defaultDueDays" render={({ field }) => <NumberInput id="d-due" value={field.value} onChange={field.onChange} />} />
      </FormField>
      <FormField label={t("قالب چاپ")} error={errors.defaultTemplate?.message}>
        <Controller control={control} name="defaultTemplate" render={({ field }) => (
          <Select value={field.value} onValueChange={field.onChange} dir={i18n.dir}>
            <SelectTrigger className="w-full"><SelectValue /></SelectTrigger>
            <SelectContent>{TEMPLATES.map((tp) => <SelectItem key={tp.value} value={tp.value}>{tp.label}</SelectItem>)}</SelectContent>
          </Select>
        )} />
      </FormField>
      <FormField label={t("رنگ برند")} error={errors.accentColor?.message} hint={t("روی فاکتورها و دکمه‌های برنامه استفاده می‌شود.")} className="sm:col-span-2">
        <Controller control={control} name="accentColor" render={({ field }) => (
          <div className="flex flex-wrap items-center gap-2">
            {ACCENTS.map((c) => (
              <button key={c} type="button" onClick={() => field.onChange(c)} aria-label={c}
                className="size-8 rounded-full border-2 border-white ring-1 ring-border transition data-[on=true]:ring-2 data-[on=true]:ring-foreground"
                data-on={field.value?.toLowerCase() === c} style={{ background: c }} />
            ))}
            <Input type="color" value={field.value} onChange={(e) => field.onChange(e.target.value)} className="h-8 w-14 cursor-pointer p-1" aria-label={t("رنگ دلخواه")} />
          </div>
        )} />
      </FormField>
      <FormField label={t("شرایط پرداخت پیش‌فرض")} htmlFor="d-terms" error={errors.defaultPaymentTerms?.message}><Textarea id="d-terms" rows={3} {...register("defaultPaymentTerms")} /></FormField>
      <FormField label={t("یادداشت پیش‌فرض")} htmlFor="d-notes" error={errors.defaultNotes?.message}><Textarea id="d-notes" rows={3} {...register("defaultNotes")} /></FormField>
    </div>
  )
}

function ReminderFields({ control, errors }: F) {
  return (
    <div className={grid}>
      <FormField label={t("یادآوری فعال باشد")}>
        <Controller control={control} name="remindersEnabled" render={({ field }) => <Switch checked={field.value} onCheckedChange={field.onChange} aria-label={t("یادآوری فعال")} />} />
      </FormField>
      <FormField label={t("چند روز قبل از سررسید")} htmlFor="r-days" error={errors.reminderDaysBefore?.message}>
        <Controller control={control} name="reminderDaysBefore" render={({ field }) => <NumberInput id="r-days" value={field.value} onChange={field.onChange} />} />
      </FormField>
      <FormField label={t("ارسال ایمیل یادآوری به مشتری")} hint={t("نیاز به تنظیم ایمیل در پایین همین صفحه دارد.")} className="sm:col-span-2">
        <Controller control={control} name="emailRemindersToCustomers" render={({ field }) => <Switch checked={field.value} onCheckedChange={field.onChange} aria-label={t("ایمیل یادآوری")} />} />
      </FormField>
    </div>
  )
}

function SmtpFields({ register, control, errors, passwordSet }: F & { passwordSet: boolean }) {
  return (
    <div className={grid}>
      <FormField label={t("سرور SMTP")} htmlFor="e-host" error={errors.smtpHost?.message}><Input id="e-host" {...ltr} {...register("smtpHost")} placeholder="smtp.example.com" /></FormField>
      <FormField label={t("پورت")} htmlFor="e-port" error={errors.smtpPort?.message}>
        <Controller control={control} name="smtpPort" render={({ field }) => <NumberInput id="e-port" value={field.value} onChange={field.onChange} />} />
      </FormField>
      <FormField label={t("نام کاربری")} htmlFor="e-user" error={errors.smtpUser?.message}><Input id="e-user" {...ltr} autoComplete="off" {...register("smtpUser")} /></FormField>
      <FormField label={t("رمز عبور")} htmlFor="e-pass" error={errors.smtpPassword?.message} hint={passwordSet ? t("ذخیره شده است. برای تغییر، رمز جدید را وارد کنید.") : undefined}>
        <Input id="e-pass" type="password" autoComplete="new-password" {...ltr} {...register("smtpPassword")} placeholder={passwordSet ? "••••••••" : ""} />
      </FormField>
      <FormField label={t("ایمیل فرستنده")} htmlFor="e-from" error={errors.smtpFromEmail?.message}><Input id="e-from" {...ltr} {...register("smtpFromEmail")} /></FormField>
      <FormField label={t("نام فرستنده")} htmlFor="e-name" error={errors.smtpFromName?.message}><Input id="e-name" {...register("smtpFromName")} /></FormField>
      <FormField label={t("اتصال امن (SSL/TLS)")}>
        <Controller control={control} name="smtpUseSsl" render={({ field }) => <Switch checked={field.value} onCheckedChange={field.onChange} aria-label={t("اتصال امن")} />} />
      </FormField>
      <TestEmail />
    </div>
  )
}

function TestEmail() {
  const [to, setTo] = useState("")
  const [busy, setBusy] = useState(false)
  const send = async () => {
    setBusy(true)
    try {
      await api.post("/settings/test-email", { to })
      toast.success(t("ایمیل آزمایشی ارسال شد."))
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <FormField label={t("ارسال ایمیل آزمایشی")} hint={t("اول تنظیمات را ذخیره کنید.")}>
      <div className="flex gap-2">
        <Input dir="ltr" className="text-end" value={to} onChange={(e) => setTo(e.target.value)} placeholder="you@example.com" />
        <Button type="button" variant="outline" disabled={!to || busy} onClick={send}>{busy && <Spinner />} {t("ارسال")}</Button>
      </div>
    </FormField>
  )
}

function BackupFields({ control, errors }: F) {
  return (
    <div className="grid gap-4 sm:grid-cols-3">
      <FormField label={t("پشتیبان روزانه")}>
        <Controller control={control} name="backupEnabled" render={({ field }) => <Switch checked={field.value} onCheckedChange={field.onChange} aria-label={t("پشتیبان روزانه")} />} />
      </FormField>
      <FormField label={t("ساعت اجرا (۰ تا ۲۳)")} htmlFor="bk-hour" error={errors.backupHour?.message}>
        <Controller control={control} name="backupHour" render={({ field }) => <NumberInput id="bk-hour" value={field.value} onChange={field.onChange} />} />
      </FormField>
      <FormField label={t("تعداد نسخه‌های نگهداری‌شده")} htmlFor="bk-keep" error={errors.backupRetentionCount?.message} hint={t("نسخه‌های قدیمی‌تر خودکار حذف می‌شوند.")}>
        <Controller control={control} name="backupRetentionCount" render={({ field }) => <NumberInput id="bk-keep" value={field.value} onChange={field.onChange} />} />
      </FormField>
    </div>
  )
}

function SecurityFields({ control, errors }: F) {
  return (
    <FormField label={t("خروج خودکار بعد از (دقیقه بی‌فعالیتی)")} htmlFor="sec-timeout" error={errors.sessionTimeoutMinutes?.message} className="max-w-xs">
      <Controller control={control} name="sessionTimeoutMinutes" render={({ field }) => <NumberInput id="sec-timeout" value={field.value} onChange={field.onChange} />} />
    </FormField>
  )
}
